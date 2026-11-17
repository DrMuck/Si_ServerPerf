# night1 unattended driver chain (phases 2-5 + optional repeats). Started detached; survives the rig agent.
# Each run: foreign-process check -> 06:30 cut-off check -> run_lab.ps1 as child with a hard wall timeout ->
# make sure the lab server it started is gone -> reports -> one line in chain_progress.log. A failed run does not stop
# the chain; a foreign Silica process does (and is written to STATE.md).
param([string]$Plan = "chain_plan.json", [string]$CutOff = "09:30", [string]$EndBy = "10:00")
$ErrorActionPreference = "Continue"
$here = Split-Path -Parent $PSScriptRoot           # ...\measure
$n1 = $PSScriptRoot                                 # ...\measure\night1
$labExe = "C:\tmp\silica_ds_lab\Silica.exe"
$prog = Join-Path $n1 "chain_progress.log"
$state = Join-Path $n1 "STATE.md"
$py = "python"
function P($m) { $l = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $m"; Add-Content -Path $prog -Value $l -Encoding utf8 }
function StateNote($m) { Add-Content -Path $state -Value "`r`n- CHAIN $(Get-Date -Format 'HH:mm:ss'): $m" -Encoding utf8 }

$cutTime = [datetime]::ParseExact((Get-Date -Format "yyyy-MM-dd") + " " + $CutOff, "yyyy-MM-dd HH:mm", $null)
$endTime = [datetime]::ParseExact((Get-Date -Format "yyyy-MM-dd") + " " + $EndBy, "yyyy-MM-dd HH:mm", $null)
if ((Get-Date).Hour -ge 12) { $cutTime = $cutTime.AddDays(1); $endTime = $endTime.AddDays(1) }   # started in the evening -> tomorrow morning
$planObj = Get-Content (Join-Path $n1 $Plan) -Raw | ConvertFrom-Json
P "CHAIN START pid=$PID plan=$Plan runs=$(@($planObj.runs).Count) cutoff=$($cutTime.ToString('s'))"

foreach ($r in @($planObj.runs)) {
    $label = $r.label
    if ((Get-Date) -ge $cutTime) { P "SKIP $label (cut-off $CutOff reached)"; continue }
    if ($r.latest_start -and (Get-Date) -ge [datetime]::Parse($r.latest_start)) { P "SKIP $label (latest_start $($r.latest_start))"; continue }
    if (Test-Path (Join-Path $n1 "STOP_CHAIN")) { P "STOP_CHAIN file present -> chain ends before $label"; break }
    $sil = @(Get-Process Silica -ErrorAction SilentlyContinue)
    if ($sil.Count) {
        $desc = ($sil | ForEach-Object { "$($_.Id) $($_.Path) start $($_.StartTime)" }) -join "; "
        P "FOREIGN Silica process(es) before $label -> chain stops: $desc"
        StateNote "chain stopped before $label because Silica process(es) were running that the chain did not start: $desc. Not touched."
        break
    }
    $left = ($endTime - (Get-Date)).TotalMinutes - 4
    if ($left -lt [double]$r.expect_min) { P "SKIP $label (expected $($r.expect_min) min does not fit before $EndBy)"; continue }
    if ([double]$r.timeout_min -gt $left) { $r.timeout_min = [math]::Floor($left - 8) }
    $out = Join-Path $n1 $label
    if (Test-Path $out) { P "SKIP $label (output folder exists)"; continue }
    # -Command (not -File) so that "-FlipStates a,b,c" arrives as a string array; paths contain no spaces or quotes
    $cl = "& '" + (Join-Path $here "run_lab.ps1") + "' -Mode lab -Label '$label' -OutRoot '$n1' -ServerPerfDll '$($r.dll)' -Scenarios '$($r.scenarios)' -TimeoutMin $($r.timeout_min)"
    if ($r.config) { $cl += " -ServerPerfConfig '$($r.config)'" }
    if ($r.runtime) { $cl += " -RuntimeJson '$($r.runtime)'" }
    if ($r.probe) { $cl += " -ProbeConfig '$($r.probe)'" }
    if ($r.flips) { $cl += " -FlipStates " + ((@($r.flips) | ForEach-Object { "'$_'" }) -join ",") + " -FlipEveryS $($r.flip_every_s)" }
    if ($r.flip_labels) { $cl += " -FlipLabels " + ((@($r.flip_labels) | ForEach-Object { "'$_'" }) -join ",") }
    Set-Content -Path (Join-Path $n1 "$label.cmdline.txt") -Value $cl
    $rargs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $cl)
    $t0 = Get-Date
    P "BEGIN $label (timeout $($r.timeout_min) min, expected ~$($r.expect_min) min)"
    $drv = Start-Process powershell -ArgumentList $rargs -WorkingDirectory $here -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $n1 "$label.driver.out") -RedirectStandardError (Join-Path $n1 "$label.driver.err")
    $drv.Id | Set-Content (Join-Path $n1 "current_run.pid")
    "$label driver=$($drv.Id) start=$($t0.ToString('s')) timeout_min=$($r.timeout_min)" | Set-Content (Join-Path $n1 "current_run.txt")
    $hard = ([double]$r.timeout_min + 8) * 60000
    if (-not $drv.WaitForExit([int]$hard)) {
        P "HARD TIMEOUT ${label}: killing driver $($drv.Id)"
        Stop-Process -Id $drv.Id -Force -ErrorAction SilentlyContinue
    }
    # the lab server the driver started (started after t0, lab path) must be gone
    Start-Sleep 3
    foreach ($p in @(Get-Process Silica -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $labExe -and $_.StartTime -ge $t0 })) {
        P "killing leftover lab server PID $($p.Id) of $label"; Stop-Process -Id $p.Id -Force
    }
    Start-Sleep 5
    $status = "?"; $ok = "?"
    $man = Join-Path $out "manifest.json"
    if (Test-Path $man) { $status = (Get-Content $man -Raw | ConvertFrom-Json).status }
    $failed = @(Get-ChildItem (Join-Path $out "perflab") -Recurse -Filter FAILED -ErrorAction SilentlyContinue).Count
    $sc = Get-ChildItem (Join-Path $out "perflab") -Recurse -Filter scenarios.csv -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($sc) { $rows = @(Import-Csv $sc.FullName); $ok = "$(@($rows | Where-Object { $_.status -eq 'ok' }).Count)/$($rows.Count) rows ok" }
    # reports (failures are logged, never fatal)
    $ri = 0
    foreach ($rep in @($r.reports)) {
        if (-not $rep) { continue }
        $ri++
        $cmdl = $rep.Replace("{run}", $out)
        $parts = @($cmdl -split " " | Where-Object { $_ })
        $of = Join-Path $out ("report_$ri" + "_" + ((Split-Path -Leaf $parts[0]) -replace '\.py$', '') + ".txt")
        try { Set-Location $here; & $py @parts *> $of } catch { P "report failed for $label : $cmdl : $_" }
    }
    $mins = [math]::Round(((Get-Date) - $t0).TotalMinutes, 1)
    P "DONE $label status=$status $ok FAILED_files=$failed after $mins min"
}
Remove-Item (Join-Path $n1 "current_run.pid"), (Join-Path $n1 "current_run.txt") -ErrorAction SilentlyContinue
P "CHAIN END"
