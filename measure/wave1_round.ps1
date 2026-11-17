# Step 7: one headless 3-way AI round (Si_RTS_AI preset lab-3way, GreatErg, 120 fps cap), arms cycled every
# FlipEveryS seconds inside the same round. Startup runtime = all defaults (so startup-only items such as
# Particles_Disable are ON in every arm), then A = "*" off, B = defaults [, C = third state e.g. MultiCore.*].
#   powershell -File wave1_round.ps1 -Label round_arms_b2
#   powershell -File wave1_round.ps1 -Label round1_arms3 -Dll ...\release\b3\Si_ServerPerf.dll -ServerPerfConfig cfg_serverperf_mc.json `
#       -StateB runtime_defaults_no_mc.json -Third runtime_mc_on.json      (A = "*" off, B = defaults, C = defaults + MultiCore.*)
# Analyse: python ab_report.py round --a wave1\<Label> --md wave1\<Label>\ROUND_ARMS.md --csv wave1\<Label>\arms
param(
    [Parameter(Mandatory = $true)][string]$Label,
    [string]$Dll = "",                       # empty = newest release\b<n>
    [string]$Third = "",
    [string]$StateB = "runtime_all_default.json",
    [string]$ServerPerfConfig = "",
    [string]$RtsPreset = "lab-3way",
    [double]$FlipEveryS = 30,
    [double]$RoundMaxMin = 61
)
$m = $PSScriptRoot
if (-not $Dll) { $Dll = (Get-ChildItem "C:\Users\schwe\Projects\Si_ServerPerf\release" -Directory | Where-Object { $_.Name -match '^b\d+$' } | Sort-Object { [int]$_.Name.Substring(1) } | Select-Object -Last 1 | ForEach-Object { Join-Path $_.FullName "Si_ServerPerf.dll" }) }
while (Get-Process Silica -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "C:\tmp\silica_ds_lab\Silica.exe" }) { Start-Sleep -Seconds 10 }
$states = @("runtime_all_off.json", $StateB)
if ($Third) { $states += $Third }
& (Join-Path $m "run_lab.ps1") -Mode round -Label $Label -OutRoot (Join-Path $m "wave1") `
    -ServerPerfDll $Dll -RuntimeJson "runtime_all_default.json" -FlipStates $states -FlipEveryS $FlipEveryS -RoundMaxMin $RoundMaxMin -ProbeConfig "probe_round_10s.json" -ServerPerfConfig $ServerPerfConfig -RtsPreset $RtsPreset
python (Join-Path $m "ab_report.py") windows --run (Join-Path $m "wave1\$Label") --md (Join-Path $m "wave1\$Label\WINDOWS.md") --csv (Join-Path $m "wave1\$Label\arms") | Out-Null
python (Join-Path $m "ab_report.py") round --settle 3 --a (Join-Path $m "wave1\$Label") --md (Join-Path $m "wave1\$Label\ROUND_ARMS.md") --csv (Join-Path $m "wave1\$Label\arms") | Out-Null
