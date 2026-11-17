# rig 2 driver (2026-11-17 ~04:50). Inserts the b18/b19 plans after qcgod1 and before b15_eveh / b15_snap_check4:
#  1. waits until chain_plan3's chain logs "BEGIN qcgod1", then creates STOP_CHAIN (that chain ends after qcgod1,
#     night_driver4 then exits without starting anything else);
#  2. waits for night_driver4 (PID 26724) to exit, removes STOP_CHAIN;
#  3. runs, in order, chain_plan_b18a / b18b / b20a / b20b / b19a / b19b / b19c / rest .json (all on b20). b18b..b19c are written by the rig agent
#     after the preceding check (night1\b18_cfg\mk_arms.py); each is waited for up to 25 min, a missing one is skipped.
#  STOP_CHAIN created later by the rig = stop after the current run, nothing further starts.
$ErrorActionPreference = "Continue"
$m = "C:\Users\schwe\Projects\Si_ServerPerf\measure"; $n1 = "$m\night1"; $log = "$n1\chain_progress.log"
function P($x) { Add-Content -Path $log -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') night_driver5: $x" -Encoding utf8 }
$old = 26724
P "started pid=$PID, waiting for the qcgod1 start line (old driver $old)"
while ($true) {
    if (-not (Get-Process -Id $old -ErrorAction SilentlyContinue)) { P "old driver gone before BEGIN qcgod1 was seen"; break }
    $t = Get-Content $log -Raw
    if ($t -match "(?m)^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d BEGIN qcgod1") {
        New-Item -ItemType File -Path "$n1\STOP_CHAIN" -Force | Out-Null
        P "qcgod1 start line seen -> STOP_CHAIN created (old chain ends after qcgod1)"; break
    }
    if ($t -match "(?m)^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d SKIP qcgod1") { New-Item -ItemType File -Path "$n1\STOP_CHAIN" -Force | Out-Null; P "qcgod1 skip line seen -> STOP_CHAIN created"; break }
    Start-Sleep 15
}
try { Wait-Process -Id $old -Timeout 7200 -ErrorAction Stop } catch { }
if (Get-Process -Id $old -ErrorAction SilentlyContinue) { P "old driver still alive after 2 h, giving up"; exit 1 }
Start-Sleep 5
Remove-Item "$n1\STOP_CHAIN" -ErrorAction SilentlyContinue
P "old chain ended, STOP_CHAIN removed"
foreach ($pn in "chain_plan_b18a.json", "chain_plan_b18b.json", "chain_plan_b20a.json", "chain_plan_b20b.json", "chain_plan_b19a.json", "chain_plan_b19b.json", "chain_plan_b19c.json", "chain_plan_rest.json") {
    $deadline = (Get-Date).AddMinutes(25)
    while (-not (Test-Path "$n1\$pn") -and (Get-Date) -lt $deadline) {
        if (Test-Path "$n1\STOP_CHAIN") { break }
        Start-Sleep 15
    }
    if (Test-Path "$n1\STOP_CHAIN") { P "STOP_CHAIN present, not starting $pn - driver ends"; exit 0 }
    if (-not (Test-Path "$n1\$pn")) { P "$pn not written within 25 min - skipped"; continue }
    P "starting chain.ps1 -Plan $pn"
    $c = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$n1\chain.ps1", '-Plan', $pn -WorkingDirectory $m -RedirectStandardOutput "$n1\chain_$pn.out" -RedirectStandardError "$n1\chain_$pn.err" -PassThru -WindowStyle Hidden
    $c.Id | Set-Content "$n1\chain.pid"
    $c.WaitForExit()
}
P "all plans done"


