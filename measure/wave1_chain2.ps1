param([string[]]$Only = @(), [string]$Dll = "")   # empty = newest release\b<n> at each run start
# waits for any running lab server first
while (Get-Process Silica -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq 'C:\tmp\silica_ds_lab\Silica.exe' }) { Start-Sleep -Seconds 10 }
Start-Sleep -Seconds 15
# Wave 1, steps 4b-6: module leave-one-out flips, correctness pass, default-off candidates. Each step is one run_lab.ps1 call.
$ErrorActionPreference = "Continue"
$Only = @($Only | ForEach-Object { $_ -split "," } | Where-Object { $_ })
$m = $PSScriptRoot
$dll = $Dll
$rl = Join-Path $m "run_lab.ps1"
$out = Join-Path $m "wave1"
function Go($label, [string[]]$extra) {
    if ($Only.Count -and $Only -notcontains $label) { return }
    if (Test-Path (Join-Path $out $label)) { Write-Output "skip $label (exists)"; return }
    $d = $dll; if (-not $d) { $d = (Get-ChildItem "C:\Users\schwe\Projects\Si_ServerPerf\release" -Directory | Where-Object { $_.Name -match '^b\d+$' } | Sort-Object { [int]$_.Name.Substring(1) } | Select-Object -Last 1 | ForEach-Object { Join-Path $_.FullName "Si_ServerPerf.dll" }) }
    Write-Output "$(Get-Date -Format HH:mm:ss) run $label with $d"
    $a = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $rl, "-Mode", "lab", "-Label", $label, "-OutRoot", $out, "-ServerPerfDll", $d) + $extra
    & powershell @a
}
# headline (b2 priority 2): all defaults vs "*" off on the stationary flip set, then the full 21-scenario pass with the mod on
function Headline { Go "flip_all" @("-Scenarios", "scenarios_flip.json", "-FlipA", "runtime_all_default.json", "-FlipB", "runtime_all_off.json", "-FlipEveryS", "20", "-TimeoutMin", "45"); Go "perf_lab_p1" @() }
# A = all defaults, B = defaults without one module (leave-one-out: B - A = what the module saves, sign reversed)
Go "flip_no_UpdateTiers" @("-Scenarios", "scenarios_flip_UpdateTiers.json", "-FlipA", "runtime_all_default.json", "-FlipB", "runtime_no_UpdateTiers.json", "-FlipEveryS", "20", "-TimeoutMin", "35")
Headline
foreach ($mod in "Services", "Headless", "Physics", "Targeting") {
    Go "flip_no_$mod" @("-Scenarios", "scenarios_flip_$mod.json", "-FlipA", "runtime_all_default.json", "-FlipB", "runtime_no_$mod.json", "-FlipEveryS", "20", "-TimeoutMin", "30")
}
# correctness / diagnostics (self-checks + WakeProbe; not for timing)
Go "correctness" @("-Scenarios", "scenarios_correctness.json", "-ServerPerfConfig", "cfg_serverperf_selfcheck_wakeprobe.json", "-FlipA", "runtime_all_default_dump.json", "-FlipB", "runtime_all_default_dump2.json", "-FlipEveryS", "60", "-TimeoutMin", "30")
# default-off candidates: A = defaults (candidates patched but off), B = candidates on
Go "flip_candidates" @("-Scenarios", "scenarios_flip_candidates.json", "-ServerPerfConfig", "cfg_serverperf_candidates.json", "-FlipA", "runtime_cands_off.json", "-FlipB", "runtime_cands_on.json", "-FlipEveryS", "20", "-TimeoutMin", "35")
