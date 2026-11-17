$ErrorActionPreference = "Continue"
$m = "C:\Users\schwe\Projects\Si_ServerPerf\measure"; $n1 = "$m\night1"
function P($x) { Add-Content -Path "$n1\chain_progress.log" -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $x" -Encoding utf8 }
function RunPlan($plan) {
    if (Test-Path "$n1\STOP_CHAIN") { P "night_driver2: STOP_CHAIN present, not starting $plan"; return $false }
    P "night_driver2: starting chain.ps1 -Plan $plan"
    $c = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$n1\chain.ps1", '-Plan', $plan -WorkingDirectory $m -RedirectStandardOutput "$n1\chain_$plan.out" -RedirectStandardError "$n1\chain_$plan.err" -PassThru -WindowStyle Hidden
    $c.Id | Set-Content "$n1\chain.pid"; $c.WaitForExit(); return $true
}
try { Wait-Process -Id 18896 -Timeout 7200 -ErrorAction Stop } catch { }
$cp = [int](Get-Content "$n1\chain.pid"); try { Wait-Process -Id $cp -Timeout 3600 -ErrorAction Stop } catch { }
if (Get-Process -Id 18896 -ErrorAction SilentlyContinue) { P "night_driver2: night_driver still alive, giving up"; exit 1 }
Start-Sleep 5
Remove-Item "$n1\STOP_CHAIN" -ErrorAction SilentlyContinue
foreach ($p in "chain_plan_check3.json", "chain_plan.json", "chain_plan2.json") { if (-not (RunPlan $p)) { exit 0 } }
P "night_driver2: all plans done"
