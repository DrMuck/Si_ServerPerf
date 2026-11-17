# A full measurement set in one command: N lab passes, optionally the 60-min AI round, then the reports.
#   Baseline (done 2026-11-16):
#     powershell -File run_set.ps1 -Prefix base -OutRoot baseline_1017 -Passes 2 -Round
#   Treatment (Si_ServerPerf with a given runtime.json), compared against the baseline:
#     powershell -File run_set.ps1 -Prefix perf -OutRoot treat_<name> -Passes 2 -Round `
#         -ServerPerfDll C:\Users\schwe\Projects\Si_ServerPerf\bin\Release\netstandard2.1\Si_ServerPerf.dll `
#         -RuntimeJson runtime_all_on.json -CompareTo baseline_1017
# Each pass/round is one run_lab.ps1 call (see there). Reports: <OutRoot>\LAB.md, ROUND.md and *_lab_*.csv,
# *_round_*.csv; with -CompareTo also LAB_vs_<baseline>.md / ROUND_vs_<baseline>.md.
param(
    [Parameter(Mandatory = $true)][string]$Prefix,
    [Parameter(Mandatory = $true)][string]$OutRoot,
    [int]$Passes = 2,
    [switch]$Round,
    [switch]$SkipLab,
    [string]$ServerPerfDll = "",
    [string]$RuntimeJson = "",
    [string]$Scenarios = "",
    [string]$CompareTo = ""
)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
if (-not [IO.Path]::IsPathRooted($OutRoot)) { $OutRoot = Join-Path $here $OutRoot }
if ($CompareTo -and -not [IO.Path]::IsPathRooted($CompareTo)) { $CompareTo = Join-Path $here $CompareTo }
$common = @{ OutRoot = $OutRoot }
if ($ServerPerfDll) { $common.ServerPerfDll = $ServerPerfDll }
if ($RuntimeJson) { $common.RuntimeJson = $RuntimeJson }
if ($Scenarios) { $common.Scenarios = $Scenarios }
$labRuns = @()
if (-not $SkipLab) {
    for ($i = 1; $i -le $Passes; $i++) {
        $lbl = "${Prefix}_lab_p$i"
        & (Join-Path $here "run_lab.ps1") -Mode lab -Label $lbl @common
        $labRuns += (Join-Path $OutRoot $lbl)
    }
}
if ($Round) {
    & (Join-Path $here "run_lab.ps1") -Mode round -Label "${Prefix}_round" @common
}
$py = Join-Path $here "ab_report.py"
if (-not $labRuns) { $labRuns = @(Get-ChildItem $OutRoot -Directory -Filter "${Prefix}_lab_p*" | ForEach-Object { $_.FullName }) }
if ($labRuns) { python $py lab --a @labRuns --md (Join-Path $OutRoot "LAB.md") --csv (Join-Path $OutRoot $Prefix) | Out-Null }
$rd = Join-Path $OutRoot "${Prefix}_round"
if (Test-Path $rd) { python $py round --a $rd --md (Join-Path $OutRoot "ROUND.md") --csv (Join-Path $OutRoot $Prefix) | Out-Null }
if ($CompareTo) {
    $base = Split-Path -Leaf $CompareTo
    $aRuns = @(Get-ChildItem $CompareTo -Directory | Where-Object { $_.Name -like "*_lab_p*" } | ForEach-Object { $_.FullName })
    if ($aRuns -and $labRuns) { python $py lab --a @aRuns --b @labRuns --md (Join-Path $OutRoot "LAB_vs_$base.md") --csv (Join-Path $OutRoot "vs_$base") | Out-Null }
    $aRound = @(Get-ChildItem $CompareTo -Directory | Where-Object { $_.Name -like "*_round" } | Select-Object -First 1)
    if ($aRound -and (Test-Path $rd)) { python $py round --a $aRound[0].FullName --b $rd --md (Join-Path $OutRoot "ROUND_vs_$base.md") --csv (Join-Path $OutRoot "vs_$base") | Out-Null }
}
Write-Output "done: $OutRoot"
