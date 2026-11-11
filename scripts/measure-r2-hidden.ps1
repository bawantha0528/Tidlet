#requires -Version 5.1
# Owns a bounded fullscreen test window; no display settings or process termination.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory,[Parameter(Mandatory)][string]$Executable,[ValidateRange(5,300)][int]$DurationSeconds=300,[ValidateSet('None','CPU','RAM')][string]$ApplicationPage='None',[switch]$InteractiveFixture)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Measurement.psm1') -Force
$root=Join-Path (Split-Path -Parent $PSScriptRoot) $OutputDirectory
$null=New-Item -ItemType Directory -Force $root;$root=(Resolve-Path $root).Path
if((Get-ChildItem -LiteralPath $root -Force|Measure-Object).Count){throw 'Use a fresh evidence directory.'}
$exe=(Resolve-Path -LiteralPath $Executable).Path
if(@(Get-Process IsletMonitor -ErrorAction SilentlyContinue).Count){throw 'Exit other Tidlet instances before this resource trial.'}
Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
public sealed class TidletFullscreenFixture : IDisposable {
 [StructLayout(LayoutKind.Sequential)] struct R { public int L,T,Ri,B; }
 [StructLayout(LayoutKind.Sequential)] struct M { public uint Size;public R Monitor,Work;public uint Flags; }
 [StructLayout(LayoutKind.Sequential)] struct MSG {public IntPtr W;public uint Message;public UIntPtr WP;public IntPtr LP;public uint Time;public int X,Y;public uint Private;}
 [DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr h,uint f);
 [DllImport("user32.dll",EntryPoint="GetMonitorInfoW")]static extern bool Info(IntPtr h,ref M m);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateWindowExW(uint ex,string cls,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern bool DestroyWindow(IntPtr h);
 [DllImport("user32.dll")]static extern bool PeekMessageW(out MSG m,IntPtr h,uint min,uint max,uint remove);
 [DllImport("user32.dll")]static extern bool TranslateMessage(ref MSG m);
 [DllImport("user32.dll")]static extern IntPtr DispatchMessageW(ref MSG m);
 readonly ManualResetEvent stop=new ManualResetEvent(false),ready=new ManualResetEvent(false);
 readonly Thread thread;readonly bool interactive;IntPtr owned;int observing;public Exception Failure;public long Checks,ForegroundFailures;
 public TidletFullscreenFixture(bool interactive){this.interactive=interactive;thread=new Thread(Run);thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!ready.WaitOne(5000))throw new Exception("Fixture startup timed out");if(Failure!=null)throw Failure;}
 public bool AwaitForeground(){var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<60){if(owned!=IntPtr.Zero&&GetForegroundWindow()==owned){Interlocked.Exchange(ref observing,1);return true;}Thread.Sleep(50);}return false;}
 void Run(){IntPtr window=IntPtr.Zero,prior=IntPtr.Zero;try{
  prior=SetThreadDpiAwarenessContext(new IntPtr(-4));var m=new M();m.Size=(uint)Marshal.SizeOf(typeof(M));if(!Info(MonitorFromWindow(IntPtr.Zero,1),ref m))throw new Exception("Monitor query failed");var r=m.Monitor;
  window=CreateWindowExW(0x00000088,"STATIC","Tidlet fullscreen measurement - exits automatically",0x90000000,r.L,r.T,r.Ri-r.L,r.B-r.T,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
  if(window==IntPtr.Zero)throw new Exception("Fixture creation failed");owned=window;
  if(!interactive&&(!SetForegroundWindow(window)||GetForegroundWindow()!=window))throw new Exception("Windows did not grant fixture foreground; no fullscreen claim");
  if(!interactive)Interlocked.Exchange(ref observing,1);
  ready.Set();var clock=Stopwatch.StartNew();var next=0.0;
  while(clock.Elapsed.TotalSeconds<420&&!stop.WaitOne(20)){MSG message;while(PeekMessageW(out message,IntPtr.Zero,0,0,1)){TranslateMessage(ref message);DispatchMessageW(ref message);}if(clock.Elapsed.TotalSeconds>=next){if(Interlocked.CompareExchange(ref observing,0,0)==1){Interlocked.Increment(ref Checks);if(GetForegroundWindow()!=window)Interlocked.Increment(ref ForegroundFailures);}next+=.5;}}
 }catch(Exception ex){Failure=ex;}finally{ready.Set();if(window!=IntPtr.Zero)DestroyWindow(window);if(prior!=IntPtr.Zero)SetThreadDpiAwarenessContext(prior);}}
 public void Dispose(){stop.Set();if(!thread.Join(5000))throw new Exception("Fixture cleanup timed out");stop.Dispose();ready.Dispose();}
}
'@
$fixture=$null;$p=$null
try {
 $hashes=foreach($name in @('IsletMonitor.exe','IsletMonitor.dll','Islet.Shell.dll','Islet.Windows.dll','Islet.Core.dll')){[ordered]@{File=$name;SHA256=(Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $name) -Algorithm SHA256).Hash}}
 $hashes|ConvertTo-Json|Set-Content (Join-Path $root 'binaries.json')
 if($ApplicationPage -eq 'None'){$fixture=[TidletFullscreenFixture]::new([bool]$InteractiveFixture);if($InteractiveFixture -and -not $fixture.AwaitForeground()){throw 'Click the owned fullscreen fixture before retrying; foreground was not verified.'}}
 $exitSeconds=$DurationSeconds+$(if($InteractiveFixture){120}else{45})
 $arguments=@('--r1-probe','--exit-after-seconds',"$exitSeconds",'--settings-file',('"'+$root+'\settings.json"'),'--probe-report',('"'+$root+'\probe.json"'))
 if($ApplicationPage -ne 'None'){$arguments+=@('--dashboard-page',$ApplicationPage)}
 $p=Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
 if($ApplicationPage -ne 'None'){Start-Sleep -Seconds 8;$fixture=[TidletFullscreenFixture]::new([bool]$InteractiveFixture);if($InteractiveFixture -and -not $fixture.AwaitForeground()){throw 'Click the owned fullscreen fixture before retrying; foreground was not verified.'}}
 $short=@();if($DurationSeconds -lt 300){$short=@('-AllowShortRun')}
 & powershell -NoProfile -File (Join-Path $PSScriptRoot 'measure.ps1') -ProcessId $p.Id -Scenario Hidden -VerifyHiddenWindows -DurationSeconds $DurationSeconds -WarmupSeconds 30 -OutputDirectory $root @short
 $result=$LASTEXITCODE
 $tail=[System.Collections.Generic.List[object]]::new();$deadline=[DateTime]::UtcNow.AddSeconds($(if($InteractiveFixture){100}else{25}))
 while(-not $p.HasExited -and [DateTime]::UtcNow -lt $deadline){$p.Refresh();if(-not $p.HasExited){$tail.Add([pscustomobject]@{Utc=[DateTime]::UtcNow.ToString('o');WorkingSetBytes=$p.WorkingSet64;LifetimePeakBytes=$p.PeakWorkingSet64})};Start-Sleep -Milliseconds 250}
 $tail|Export-Csv -NoTypeInformation (Join-Path $root 'post-observation.csv')
 if(-not $p.HasExited){throw 'Bounded app did not exit naturally; no termination attempted.'}
 $p.WaitForExit()
 $p.Refresh();$d=Get-Content (Join-Path $root 'probe.json') -Raw|ConvertFrom-Json
 $summary=Get-Content (Join-Path $root 'summary.json') -Raw|ConvertFrom-Json
 $wholePeak=[long]$summary.LifetimePeakWorkingSetBytes;$currentMax=[long]$summary.WorkingSetMaxBytes
 if($tail.Count){$wholePeak=[Math]::Max($wholePeak,[long]($tail.LifetimePeakBytes|Measure-Object -Maximum).Maximum);$currentMax=[Math]::Max($currentMax,[long]($tail.WorkingSetBytes|Measure-Object -Maximum).Maximum)}
 $policy=Test-SessionMemory -CurrentMaximumBytes $currentMax -LifetimePeakBytes $wholePeak -ApplicationSession ($ApplicationPage -ne 'None') -ApplicationOpen $false
 $policy|ConvertTo-Json|Set-Content (Join-Path $root 'memory-policy.json')
 [ordered]@{InteractiveFixture=[bool]$InteractiveFixture;ApplicationPage=$ApplicationPage;ExitAfterSeconds=$exitSeconds;ForegroundAcquiredBeforeObservation=$true;CompleteTailAccounted=$true;WorkingSetMaximumBytes=$currentMax;LifetimePeakBytes=$wholePeak}|ConvertTo-Json|Set-Content (Join-Path $root 'fixture-context.json')
 $unchanged=$true;foreach($hash in $hashes){if((Get-FileHash -LiteralPath (Join-Path (Split-Path $exe) $hash.File) -Algorithm SHA256).Hash -ne $hash.SHA256){$unchanged=$false}}
 $verified=$p.ExitCode -eq 0 -and $d.EverHidden -and $d.HiddenMetricReads -eq 0 -and $d.ChartWork -eq 0 -and $d.ProcessWork -eq 0 -and $d.TimersStopped -and $d.AllTrayRemoved -and $d.PointerSize -eq 4 -and $summary.HiddenBehaviorVerified -and $null -eq $fixture.Failure -and $fixture.ForegroundFailures -eq 0 -and $unchanged -and $policy.Accepted
 if($ApplicationPage -ne 'None'){$verified=$verified -and $d.DashboardWorkersDrained -and $d.BaselineDashboardReads -gt 0 -and -not $d.BaselineDashboardOpen -and $d.DashboardReads -eq $d.BaselineDashboardReads -and $d.DashboardFrames -eq $d.BaselineDashboardFrames}
 [ordered]@{Scenario='Hidden-window CPU with owned fullscreen stimulus';LifecycleVerified=$verified;HiddenWindowsVerified=$summary.HiddenBehaviorVerified;ForegroundChecks=$fixture.Checks;ForegroundFailures=$fixture.ForegroundFailures;StableFixtureForeground=($fixture.ForegroundFailures -eq 0);BinaryHashesUnchanged=$unchanged;LifetimePeakWorkingSetBytes=$wholePeak;MemoryPolicy=$policy;NumericalExit=$result;PhysicalGameVerified=$false;InitialOrOutsideObservationMetricReads=$d.MetricReads;FixtureCost='External test harness, not a Tidlet helper; observer costs separate';OverallAccepted=($verified -and $result -eq 0 -and $DurationSeconds -ge 300)}|ConvertTo-Json|Set-Content (Join-Path $root 'scenario-check.json')
 if(-not $verified){throw 'Hidden/fullscreen scenario proof failed.'}
}finally{if($null -ne $fixture){$fixture.Dispose()};if($null -ne $p){$p.Dispose()}}
exit $result
