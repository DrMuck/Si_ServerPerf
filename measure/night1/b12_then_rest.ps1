$ErrorActionPreference = "Continue"
$m = "C:\Users\schwe\Projects\Si_ServerPerf\measure"; $n1 = "$m\night1"
function P($x) { Add-Content -Path "$n1\chain_progress.log" -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $x" -Encoding utf8 }
$cp = [int](Get-Content "$n1\chain.pid")
try { Wait-Process -Id $cp -Timeout 7200 -ErrorAction Stop } catch { }
if (Get-Process -Id $cp -ErrorAction SilentlyContinue) { P "b12_then_rest: chain still running after 2 h, giving up"; exit 1 }
Start-Sleep 20   # let then_plan2.ps1 see STOP_CHAIN and exit
Remove-Item "$n1\STOP_CHAIN" -ErrorAction SilentlyContinue
foreach ($plan in "chain_plan_b12.json", "chain_plan.json", "chain_plan2.json") {
    if (Test-Path "$n1\STOP_CHAIN") { P "b12_then_rest: STOP_CHAIN present, not starting $plan"; break }
    P "b12_then_rest: starting chain.ps1 -Plan $plan"
    $c = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$n1\chain.ps1", '-Plan', $plan -WorkingDirectory $m -RedirectStandardOutput "$n1\chain_$plan.out" -RedirectStandardError "$n1\chain_$plan.err" -PassThru -WindowStyle Hidden
    $c.Id | Set-Content "$n1\chain.pid"
    $c.WaitForExit()
}
P "b12_then_rest: all plans done"
