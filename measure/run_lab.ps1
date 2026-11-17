# One measurement run on the LAB copy of the Silica dedicated server (C:\tmp\silica_ds_lab, 1.0.17).
# Lays out the mod set + configs, starts the server through the lab's own launch.bat, waits until the run is
# over, stops only the process it started (by PID, verified by path), and archives the run folders + a manifest.
#
#   Lab pass (Si_PerfLab scenarios, server quits by itself when the list is done):
#     powershell -File run_lab.ps1 -Mode lab -Label base_p1
#     powershell -File run_lab.ps1 -Mode lab -Label perf_p1 -ServerPerfDll <path>\Si_ServerPerf.dll [-RuntimeJson <file>]
#   AI round (headless 3-way, Si_RTS_AI preset lab-3way, GreatErg; stopped after the first round ends):
#     powershell -File run_lab.ps1 -Mode round -Label base_round
#   Interleaved switch flips inside one server (needs Si_ServerPerf polling UserData\cfg\Si_ServerPerf\runtime.json):
#     powershell -File run_lab.ps1 -Mode lab -Scenarios scenarios_flip.json -Label flip1 -ServerPerfDll <dll> `
#         -FlipA runtime_off.json -FlipB runtime_on.json -FlipEveryS 20
#
# Output: <OutRoot>\<Label>\ manifest.json, Latest.log, perflab\<run>\, perfprobe\<run>\, flips.csv (flip mode).
# Refuses to start while ANY other Silica.exe runs (live server or client would distort the numbers) unless -AllowOthers.
param(
    [ValidateSet("lab", "round")][string]$Mode = "lab",
    [Parameter(Mandatory = $true)][string]$Label,
    [string]$Scenarios = "",                 # lab mode; default scenarios_ab.json next to this script
    [string]$ServerPerfDll = "",             # empty = baseline (no Si_ServerPerf in Mods)
    [string]$RuntimeJson = "",               # copied to UserData\cfg\Si_ServerPerf\runtime.json before launch
    [string]$FlipA = "", [string]$FlipB = "",# flip mode: alternate these two runtime.json files
    [string[]]$FlipStates = @(),            # flip mode with N states (cycled in order, labelled A, B, C ...); FlipA/FlipB = 2-state shorthand
    [string[]]$FlipLabels = @(),            # optional labels per FlipStates entry (default A, B, C ...)
    [double]$FlipEveryS = 20,
    [string]$ProbeConfig = "",
    [string]$RtsPreset = "",                 # Si_RTS_AI preset (default lab / lab-3way by mode)               # PerfProbe config (default probe_lab.json / probe_round.json by mode)
    [string]$ServerPerfConfig = "",          # copied to UserData\cfg\Si_ServerPerf\Si_ServerPerf.json; empty = delete it (mod writes defaults)
    [double]$TimeoutMin = 0,                 # 0 = 60 (lab) / 75 (round)
    [double]$RoundMaxMin = 61,               # round mode: stop after this many minutes of round time even if it has not ended
    [string]$OutRoot = "",                   # default: <this folder>\runs
    [switch]$AllowOthers
)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$lab = "C:\tmp\silica_ds_lab"
$exe = Join-Path $lab "Silica.exe"
if (-not $OutRoot) { $OutRoot = Join-Path $here "runs" }
if (-not $Scenarios) { $Scenarios = Join-Path $here "scenarios_ab.json" }
if (-not [IO.Path]::IsPathRooted($Scenarios)) { $Scenarios = Join-Path $here $Scenarios }
foreach ($v in "RuntimeJson", "FlipA", "FlipB", "ServerPerfDll", "ServerPerfConfig") {
    $val = Get-Variable $v -ValueOnly
    if ($val -and -not [IO.Path]::IsPathRooted($val)) { Set-Variable $v (Join-Path $here $val) }
}
if ($TimeoutMin -le 0) { $TimeoutMin = $(if ($Mode -eq "lab") { 60 } else { 75 }) }
$out = Join-Path $OutRoot $Label
if (Test-Path $out) { throw "output folder exists: $out (pick a new -Label)" }
if ($FlipA -and $FlipB -and $FlipStates.Count -eq 0) { $FlipStates = @($FlipA, $FlipB) }
$FlipStates = @($FlipStates | ForEach-Object { if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $here $_ } })
foreach ($f in $FlipStates) { if (-not (Test-Path $f)) { throw "flip state file missing: $f" } }
$flip = ($FlipStates.Count -ge 2)
$stateLabels = @("A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L"); $si = 0
$FlipLabels = @($FlipLabels | ForEach-Object { $_ -split "," } | Where-Object { $_ })
if ($FlipLabels.Count) { if ($FlipLabels.Count -ne $FlipStates.Count) { throw "FlipLabels must match FlipStates" }; $stateLabels = $FlipLabels }
if ($flip -and -not $ServerPerfDll) { Write-Output "note: flip mode without Si_ServerPerf = A/A null test (nothing reads runtime.json)" }

function Log($m) { $l = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m"; Write-Output $l; for ($k = 0; $k -lt 5; $k++) { try { [IO.File]::AppendAllText((Join-Path $OutRoot "runs_log.txt"), "[$Label] $l`r`n"); break } catch { Start-Sleep -Milliseconds 200 } } }
function Md5($p) { (Get-FileHash $p -Algorithm MD5).Hash }
function WriteNoBom($path, $text) { [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false))) }

New-Item -ItemType Directory -Force $OutRoot | Out-Null

# ---- 1. nothing else may run
$all = @(Get-Process Silica -ErrorAction SilentlyContinue)
$mine = @($all | Where-Object { $_.Path -eq $exe })
$others = @($all | Where-Object { $_.Path -ne $exe })
if ($mine.Count) { throw "lab server already running (PID $($mine.Id -join ','))" }
if ($others.Count -and -not $AllowOthers) { throw "other Silica process(es) running, not starting: " + (($others | ForEach-Object { "$($_.Id) $($_.Path)" }) -join "; ") }
New-Item -ItemType Directory -Force $out | Out-Null

# ---- 2. mod set: exactly base (+ PerfLab in lab mode) (+ Si_ServerPerf)
$mods = Join-Path $lab "Mods"
$want = @(Get-ChildItem (Join-Path $lab "_modsets\base") -Filter *.dll)
if ($Mode -eq "lab") { $want += Get-ChildItem (Join-Path $lab "_modsets\perflab") -Filter *.dll }
if ($ServerPerfDll) { $want += Get-Item $ServerPerfDll }
Get-ChildItem $mods -Filter *.dll | Where-Object { $want.Name -notcontains $_.Name } | Remove-Item -Force
foreach ($f in $want) { Copy-Item $f.FullName (Join-Path $mods $f.Name) -Force }
if (Test-Path (Join-Path $lab "ModsToUpdate")) { Get-ChildItem (Join-Path $lab "ModsToUpdate") -Filter *.dll | Remove-Item -Force }
$modList = @(Get-ChildItem $mods -Filter *.dll | ForEach-Object { [ordered]@{ dll = $_.Name; size = $_.Length; md5 = (Md5 $_.FullName); mtime = $_.LastWriteTime.ToString("s") } })

# ---- 3. configs
$cfg = Join-Path $lab "UserData\cfg"
$probeSrc = $(if ($ProbeConfig) { if ([IO.Path]::IsPathRooted($ProbeConfig)) { $ProbeConfig } else { Join-Path $here $ProbeConfig } } else { Join-Path $here $(if ($Mode -eq "lab") { "probe_lab.json" } else { "probe_round.json" }) })
$pj = Get-Content $probeSrc -Raw | ConvertFrom-Json
$pj.runTag = "$Label"
WriteNoBom (Join-Path $cfg "PerfProbe\config.json") ($pj | ConvertTo-Json -Depth 10)
$rtsState = Join-Path $cfg "RTSAI\state.json"
$preset = $(if ($RtsPreset) { $RtsPreset } elseif ($Mode -eq "lab") { "lab" } else { "lab-3way" })
WriteNoBom $rtsState ((@{ _readme = "set by Si_ServerPerf\measure\run_lab.ps1"; activeConfig = $preset } | ConvertTo-Json))
if ($Mode -eq "lab") { Copy-Item $Scenarios (Join-Path $cfg "PerfLab\scenarios.json") -Force }
$spCfg = Join-Path $cfg "Si_ServerPerf"
$rt = Join-Path $spCfg "runtime.json"
New-Item -ItemType Directory -Force $spCfg | Out-Null
Remove-Item (Join-Path $spCfg "runtime_state.json") -ErrorAction SilentlyContinue
if (Test-Path $rt) { Remove-Item $rt -Force }       # never inherit a runtime.json from an earlier run
$spJson = Join-Path $spCfg "Si_ServerPerf.json"
if ($ServerPerfConfig) { Copy-Item $ServerPerfConfig $spJson -Force } elseif (Test-Path $spJson) { Remove-Item $spJson -Force }
if ($RuntimeJson -or $flip) {
    Copy-Item $(if ($flip -and -not $RuntimeJson) { $FlipStates[0] } else { $RuntimeJson }) $rt -Force   # flip mode + RuntimeJson: startup file, state A follows after 30 s
}
foreach ($c in @($probeSrc, $Scenarios, (Join-Path $cfg "RTSAI\configs\$preset.json"), $RuntimeJson) + $FlipStates) {
    if ($c -and (Test-Path $c) -and -not ($Mode -eq "round" -and $c -eq $Scenarios)) { Copy-Item $c (Join-Path $out ("cfg_" + (Split-Path -Leaf $c))) -Force }
}
if (Test-Path $rt) { Copy-Item $rt (Join-Path $out "cfg_runtime_at_start.json") -Force }

# ---- 4. launch through the lab's launch.bat
$t0 = Get-Date
$pfRoot = Join-Path $lab "UserData\PerfProbe"; $plRoot = Join-Path $lab "UserData\PerfLab"
# launch.bat = "move ModsToUpdate\*.dll to Mods" (step 2 above empties ModsToUpdate) + "Silica.exe --melonloader.disablestartscreen".
# .bat execution is blocked in the agent environment (cmd: "'launch.bat' is not recognized"), so the script runs the
# bat's exact command line itself; it never goes through the watchdog.
$launchLine = (Get-Content (Join-Path $lab "launch.bat") | Where-Object { $_ -match '^\s*Silica\.exe' } | Select-Object -First 1)
if (-not $launchLine) { throw "launch.bat has no Silica.exe line" }
$launchArgs = ($launchLine -replace '^\s*Silica\.exe\s*', '').Trim()
$cmd = Start-Process -FilePath $exe -ArgumentList $launchArgs -WorkingDirectory $lab -PassThru
$srv = $null
for ($i = 0; $i -lt 60 -and -not $srv; $i++) {
    Start-Sleep -Milliseconds 500
    $srv = Get-Process Silica -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe -and $_.StartTime -ge $t0.AddSeconds(-2) } | Select-Object -First 1
}
if (-not $srv) { throw "lab Silica.exe did not appear within 30 s (cmd PID $($cmd.Id))" }
Log "START mode=$Mode pid=$($srv.Id) mods=$((($modList | ForEach-Object { $_.dll }) -join ','))"

# ---- 5. wait
$deadline = $t0.AddMinutes($TimeoutMin)
$status = "running"; $flipRows = @(); $state = "A"; $nextFlip = $null; $roundSeen = $false
$rsFile = Join-Path $spCfg "runtime_state.json"; $pend = $null; $nflip = 0
if ($flip) {
    $flipRows += "timestamp_iso,state,confirm_lag_ms,items_on"
    if ($RuntimeJson) { $si = -1; $flipRows += "$((Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fff')),S,,"; $nextFlip = (Get-Date).AddSeconds(30) }
    else { $flipRows += "$((Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fff')),$($stateLabels[0]),,"; $nextFlip = (Get-Date).AddSeconds($FlipEveryS) }
}
$lastCheck = Get-Date
while ($true) {
    if ($srv.HasExited) { $status = "exited"; break }
    $now = Get-Date
    if ($now -gt $deadline) { $status = "timeout"; break }
    if ($flip -and $now -ge $nextFlip) {
        $si = ($si + 1) % $FlipStates.Count
        $state = $stateLabels[$si]
        $src = $FlipStates[$si]
        $tmp = "$rt.tmp"; Copy-Item $src $tmp -Force; Move-Item $tmp $rt -Force
        $pend = @{ t = (Get-Date); state = $state }
        $nextFlip = $nextFlip.AddSeconds($FlipEveryS)
    }
    if ($pend) {   # confirm through runtime_state.json (written by Si_ServerPerf after each applied change)
        $lag = ((Get-Date) - $pend.t).TotalMilliseconds; $ok = $false; $non = ""
        if ((Test-Path $rsFile) -and (Get-Item $rsFile).LastWriteTime -gt $pend.t.AddMilliseconds(-50)) {
            try { $js = Get-Content $rsFile -Raw | ConvertFrom-Json; $non = @($js.items.PSObject.Properties | Where-Object { $_.Value.on }).Count; $ok = $true } catch { }
        }
        if ($ok -or $lag -gt 5000) {
            $nflip++
            if ($ok -and $nflip -le 4) { Copy-Item $rsFile (Join-Path $out "runtime_state_flip$nflip.json") -Force }
            $flipRows += "$($pend.t.ToString('yyyy-MM-ddTHH:mm:ss.fff')),$($pend.state),$(if ($ok) { [int]$lag } else { 'none' }),$non"
            $pend = $null
        }
    }
    if ($Mode -eq "round" -and ($now - $lastCheck).TotalSeconds -ge 30) {
        $lastCheck = $now
        $pr = Get-ChildItem $pfRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.CreationTime -ge $t0.AddSeconds(-5) } | Sort-Object CreationTime | Select-Object -Last 1
        $fc = if ($pr) { Join-Path $pr.FullName "frames.csv" } else { $null }
        if ($fc -and (Test-Path $fc)) {
            $last = Import-Csv $fc | Select-Object -Last 1
            if ($last.phase -eq "game") { $roundSeen = $true }
            $tr = 0.0; [double]::TryParse($last.t_round_s, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$tr) | Out-Null
            if ($roundSeen -and ($last.phase -ne "game" -or [int]$last.round_id -gt 1)) { Start-Sleep -Seconds 20; $status = "round_ended"; break }
            if ($roundSeen -and $tr -gt $RoundMaxMin * 60) { $status = "round_max_time"; break }
        }
    }
    Start-Sleep -Milliseconds 250
}
if (-not $srv.HasExited) {
    $p = Get-Process -Id $srv.Id -ErrorAction SilentlyContinue
    if ($p -and $p.Path -eq $exe) { Stop-Process -Id $srv.Id -Force; Log "stopped PID $($srv.Id) ($status)" }
    Start-Sleep -Seconds 3
}
$t1 = Get-Date
if ($flip) { $flipRows | Set-Content -Encoding ascii (Join-Path $out "flips.csv") }
Log "END status=$status after $([math]::Round(($t1 - $t0).TotalMinutes, 1)) min"

# ---- 6. archive
Copy-Item (Join-Path $lab "MelonLoader\Latest.log") (Join-Path $out "Latest.log") -ErrorAction SilentlyContinue
foreach ($f in "Si_ServerPerf.json", "runtime_state.json", "runtime.json") { $src = Join-Path $spCfg $f; if ($ServerPerfDll -and (Test-Path $src)) { Copy-Item $src (Join-Path $out "end_$f") -Force } }
$probeRuns = @(Get-ChildItem $pfRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.CreationTime -ge $t0.AddSeconds(-5) })
$labRuns = @(Get-ChildItem $plRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.CreationTime -ge $t0.AddSeconds(-5) })
foreach ($d in $probeRuns) { Copy-Item $d.FullName (Join-Path $out "perfprobe\$($d.Name)") -Recurse -Force }
foreach ($d in $labRuns) { Copy-Item $d.FullName (Join-Path $out "perflab\$($d.Name)") -Recurse -Force }
$sv = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes((Join-Path $lab "Silica_Data\globalgamemanagers")))
$gv = ([regex]::Matches($sv, '(Beta|Release):[0-9.]+') | ForEach-Object { $_.Value } | Select-Object -Unique) -join ","
$man = [ordered]@{
    label = $Label; mode = $Mode; status = $status; game_version = $gv
    silicacore_md5 = (Md5 (Join-Path $lab "Silica_Data\Managed\SilicaCore.dll"))
    start = $t0.ToString("s"); end = $t1.ToString("s"); pid = $srv.Id; host = $env:COMPUTERNAME
    mods = $modList; serverperf_dll = $ServerPerfDll; runtime_json = $RuntimeJson; serverperf_config = $ServerPerfConfig; flip = $flip; flip_every_s = $FlipEveryS; flip_states = @($FlipStates)
    scenarios = $(if ($Mode -eq "lab") { $Scenarios } else { "" }); rtsai_preset = $preset
    perfprobe_runs = @($probeRuns | ForEach-Object { $_.FullName }); perflab_runs = @($labRuns | ForEach-Object { $_.FullName })
    command = $MyInvocation.Line
}
WriteNoBom (Join-Path $out "manifest.json") ($man | ConvertTo-Json -Depth 6)
Log "archived to $out (perfprobe: $($probeRuns.Name -join ','); perflab: $($labRuns.Name -join ','))"
