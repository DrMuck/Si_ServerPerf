$n1 = "C:\Users\schwe\Projects\Si_ServerPerf\measure\night1"
$cp = [int](Get-Content "$n1\chain.pid")
try { Wait-Process -Id $cp -Timeout 18000 -ErrorAction Stop } catch { }
if (Get-Process -Id $cp -ErrorAction SilentlyContinue) { exit 1 }
if (Test-Path "$n1\STOP_CHAIN") { Add-Content "$n1\chain_progress.log" "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') plan2 not started (STOP_CHAIN present)"; exit 0 }
$c = Start-Process powershell -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File',"$n1\chain.ps1",'-Plan','chain_plan2.json' -WorkingDirectory "C:\Users\schwe\Projects\Si_ServerPerf\measure" -RedirectStandardOutput "$n1\chain2.out" -RedirectStandardError "$n1\chain2.err" -PassThru -WindowStyle Hidden
$c.Id | Set-Content "$n1\chain.pid"
Add-Content "$n1\chain_progress.log" "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') plan2 chain started pid=$($c.Id)"
