# Waits for chain 1 to stop (STOP_CHAIN), runs the b11 FixedWakeTrace diagnostic, writes FIXEDWAKE.txt,
# removes STOP_CHAIN and restarts chain.ps1 with chain_plan.json (finished runs are skipped: their folders exist).
$ErrorActionPreference = "Continue"
$m = "C:\Users\schwe\Projects\Si_ServerPerf\measure"; $n1 = "$m\night1"
function P($x) { Add-Content -Path "$n1\chain_progress.log" -Value "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $x" -Encoding utf8 }
$chainPid = [int](Get-Content "$n1\chain.pid")
try { Wait-Process -Id $chainPid -Timeout 5400 -ErrorAction Stop } catch { }
if (Get-Process -Id $chainPid -ErrorAction SilentlyContinue) { P "fixedwake: chain 1 still running after 90 min, giving up"; exit 1 }
$sil = @(Get-Process Silica -ErrorAction SilentlyContinue)
if ($sil.Count) { P "fixedwake: FOREIGN Silica process(es) $(($sil | ForEach-Object { $_.Id }) -join ','), not starting"; Add-Content "$n1\STATE.md" "`r`n- fixedwake_then_chain: foreign Silica process(es) $(($sil | ForEach-Object { "$($_.Id) $($_.Path)" }) -join '; ') -> nothing started, chain NOT restarted."; exit 2 }
P "BEGIN fixedwake_b11 (b11 diagnostic)"
Set-Location $m
& "$m\run_lab.ps1" -Mode lab -Label fixedwake_b11 -OutRoot $n1 -Scenarios "$n1\fixedwake_cfg\scenarios_fixedwake.json" `
    -ServerPerfDll "C:\Users\schwe\Projects\Si_ServerPerf\release\b11\Si_ServerPerf.dll" -ServerPerfConfig "$n1\fixedwake_cfg\cfg_serverperf_fixedwake_b11.json" -TimeoutMin 15 *> "$n1\fixedwake_b11.driver.out"
$t0 = (Get-Date).AddMinutes(-20)
foreach ($p in @(Get-Process Silica -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "C:\tmp\silica_ds_lab\Silica.exe" -and $_.StartTime -ge $t0 })) { Stop-Process -Id $p.Id -Force; P "fixedwake: killed leftover lab server $($p.Id)" }
$log = "$n1\fixedwake_b11\Latest.log"
$o = New-Object System.Collections.Generic.List[string]
$o.Add("FIXEDWAKE b11 diagnostic (night1\fixedwake_b11), $(Get-Date -Format s). Scenario phys-idle-vehicles 300 LightStriker tight grid, arm F + IdleSleepDiag + FixedWakeTrace from startup, no flips. Not for timing.")
$o.Add("== startup status lines (FixedWakeTrace, UpdateTiers, Physics items)")
Select-String -Path $log -Pattern 'Physics\.FixedWakeTrace|UpdateTiers\.|Physics\.IdleForceSleep|Physics\.AvoidanceSkipOntoResting|Physics\.AvoidanceNoIdleDetour|Physics\.IdleSleepDiag|NOT APPLIED|FAILED' | Select-Object -First 30 | ForEach-Object { $o.Add($_.Line) }
$o.Add("== [Physics] fixedwake lines")
Select-String -Path $log -Pattern '\[Physics\] fixedwake' | ForEach-Object { $o.Add($_.Line) }
$o.Add("== last idlesleep line")
Select-String -Path $log -Pattern '\[Physics\] idlesleep' | Select-Object -Last 1 | ForEach-Object { $o.Add($_.Line) }
$o.Add("== last census line")
Select-String -Path $log -Pattern '\[Physics\] census' | Select-Object -Last 1 | ForEach-Object { $o.Add($_.Line) }
$o.Add("== exceptions / errors")
Select-String -Path $log -Pattern 'Exception|\[ERROR\]|\[Diag\].*[1-9]\d* logged' | Where-Object { $_.Line -notmatch ' 0 logged exceptions' } | Select-Object -First 30 | ForEach-Object { $o.Add($_.Line) }
$o.Add("== Si_PerfLab scenario lines")
Select-String -Path $log -Pattern '\[LAB\] scenario phys' | ForEach-Object { $o.Add($_.Line) }
[IO.File]::WriteAllLines("$n1\fixedwake_b11\FIXEDWAKE.txt", $o)
P "DONE fixedwake_b11 ($($o.Count) lines in FIXEDWAKE.txt)"
Remove-Item "$n1\STOP_CHAIN" -ErrorAction SilentlyContinue
$c = Start-Process powershell -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "$n1\chain.ps1" -WorkingDirectory $m -RedirectStandardOutput "$n1\chain.out" -RedirectStandardError "$n1\chain.err" -PassThru -WindowStyle Hidden
$c.Id | Set-Content "$n1\chain.pid"
P "chain restarted pid=$($c.Id) (remaining runs of chain_plan.json)"
