# Wave 1 step 5 (after AI round #1): physics sleep candidates, other default-off candidates, Targeting self-check
# re-verification, full lab pass with defaults. Newest release\b<n> per run. Waits for a running lab server first.
param([string[]]$Only = @(), [string]$StopBy = "")   # StopBy "HH:mm": do not START a run after this time
$ErrorActionPreference = "Continue"
$Only = @($Only | ForEach-Object { $_ -split "," } | Where-Object { $_ })
$m = $PSScriptRoot
$rl = Join-Path $m "run_lab.ps1"
$out = Join-Path $m "wave1"
function Newest { Get-ChildItem "C:\Users\schwe\Projects\Si_ServerPerf\release" -Directory | Where-Object { $_.Name -match '^b\d+$' } | Sort-Object { [int]$_.Name.Substring(1) } | Select-Object -Last 1 | ForEach-Object { Join-Path $_.FullName "Si_ServerPerf.dll" } }
function WaitLab { while (Get-Process Silica -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "C:\tmp\silica_ds_lab\Silica.exe" }) { Start-Sleep -Seconds 10 }; Start-Sleep -Seconds 5 }
function Go($label, [hashtable]$p) {
    if ($Only.Count -and $Only -notcontains $label) { return }
    if (Test-Path (Join-Path $out $label)) { Write-Output "skip $label (exists)"; return }
    WaitLab
    if ($StopBy -and (Get-Date) -gt [datetime]::ParseExact($StopBy, "HH:mm", $null)) { Write-Output "skip $label (after $StopBy)"; return }
    $d = Newest
    Write-Output "$(Get-Date -Format HH:mm:ss) run $label with $d"
    try { & $rl -Mode lab -Label $label -OutRoot $out -ServerPerfDll $d @p } catch { Write-Output "run $label failed: $_" }
}
Go "physics_cands" @{ Scenarios = "scenarios_physics.json"; ServerPerfConfig = "cfg_serverperf_physics.json"; FlipStates = @("runtime_phys_def.json", "runtime_phys_avoid.json", "runtime_phys_force.json", "runtime_phys_both.json"); FlipEveryS = 60; TimeoutMin = 35 }
Go "flip_candidates" @{ Scenarios = "scenarios_flip_candidates.json"; ServerPerfConfig = "cfg_serverperf_candidates.json"; FlipA = "runtime_cands_off.json"; FlipB = "runtime_cands_on.json"; FlipEveryS = 20; TimeoutMin = 35 }
Go "targeting_check" @{ Scenarios = "scenarios_targeting_check.json"; ServerPerfConfig = "cfg_serverperf_targeting_check.json"; FlipA = "runtime_all_default.json"; FlipB = "runtime_no_Targeting_dump.json"; FlipEveryS = 20; TimeoutMin = 25 }
Go "perf_lab_p1" @{ TimeoutMin = 50 }
Go "flip_no_Services" @{ Scenarios = "scenarios_flip_Services.json"; FlipA = "runtime_all_default.json"; FlipB = "runtime_no_Services.json"; FlipEveryS = 20; TimeoutMin = 30 }
Go "flip_no_Headless" @{ Scenarios = "scenarios_flip_Headless.json"; FlipA = "runtime_all_default.json"; FlipB = "runtime_no_Headless.json"; FlipEveryS = 20; TimeoutMin = 30 }
