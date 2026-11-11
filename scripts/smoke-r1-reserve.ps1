#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Join-Path (Split-Path -Parent $PSScriptRoot) $OutputDirectory
$null=New-Item -ItemType Directory -Force $root;$root=(Resolve-Path $root).Path
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class TidletReserveCheck {
 [StructLayout(LayoutKind.Sequential)]public struct R {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]private struct M {public uint Size;public R Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")]public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr w,uint f);
 [DllImport("user32.dll",EntryPoint="GetMonitorInfoW")]static extern bool Info(IntPtr m,ref M value);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateWindowExW(uint ex,string cls,string name,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr w,int cmd);
 [DllImport("user32.dll")]public static extern bool DestroyWindow(IntPtr w);
 [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr w,int attr,out R r,int size);
 public static R Work(){var m=new M();m.Size=(uint)Marshal.SizeOf(typeof(M));if(!Info(MonitorFromWindow(IntPtr.Zero,1),ref m))throw new Exception("Monitor query failed");return m.Work;}
 public static IntPtr Maximize(){var w=CreateWindowExW(0,"STATIC","Tidlet owned Reserve verification",0x00CF0000,200,200,640,400,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);if(w==IntPtr.Zero)throw new Exception("Owned fixture creation failed");ShowWindow(w,3);return w;}
 public static R Frame(IntPtr w){R r;if(DwmGetWindowAttribute(w,9,out r,16)!=0)throw new Exception("DWM frame query failed");return r;}
}
'@
$priorDpi=[TidletReserveCheck]::SetThreadDpiAwarenessContext([IntPtr](-4))
$before=[TidletReserveCheck]::Work();$owned=[IntPtr]::Zero;$p=$null
try {
 $exe=(Resolve-Path (Join-Path $PSScriptRoot '../src/IsletMonitor/bin/win-x86-probe/IsletMonitor.exe')).Path
 $arguments=@('--r1-probe','--reserve','--exit-after-seconds','10','--settings-file',('"'+$root+'\settings.json"'),'--probe-report',('"'+$root+'\probe.json"'),'--capture',('"'+$root+'\bar.png"'))
 $p=Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
 Start-Sleep -Seconds 2
 $during=[TidletReserveCheck]::Work();$owned=[TidletReserveCheck]::Maximize();Start-Sleep -Milliseconds 300;$frame=[TidletReserveCheck]::Frame($owned)
 [TidletReserveCheck]::DestroyWindow($owned)|Out-Null;$owned=[IntPtr]::Zero
 if(-not $p.WaitForExit(20000)){throw 'Owned bar did not exit naturally; no termination attempted'}
 $p.Refresh();$after=[TidletReserveCheck]::Work()
 $report=Get-Content (Join-Path $root 'probe.json') -Raw|ConvertFrom-Json
 $mode=$report.Observations[0].Placement.ActualMode
 $restored=$before.Left -eq $after.Left -and $before.Top -eq $after.Top -and $before.Right -eq $after.Right -and $before.Bottom -eq $after.Bottom
 $reserved=$during.Top -gt $before.Top
 $maximized=$frame.Top -ge $during.Top
 [ordered]@{Before=$before;During=$during;After=$after;MaximizedFrame=$frame;WorkAreaRestored=$restored;Reserved=$reserved;MaximizedDoesNotOverlap=$maximized;ActualMode=$mode;NaturalExit=($p.ExitCode -eq 0);CrashRecoveryVerified=$false}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $root 'work-area.json')
 Get-Content (Join-Path $root 'work-area.json')
 if(-not $restored -or ($mode -eq 1 -and (-not $reserved -or -not $maximized))){throw 'Reserve/work-area acceptance check failed'}
} finally {
 if($owned -ne [IntPtr]::Zero){[TidletReserveCheck]::DestroyWindow($owned)|Out-Null}
 if($null -ne $p){$null=$p.WaitForExit(15000);$p.Dispose()}
 if($priorDpi -ne [IntPtr]::Zero){[TidletReserveCheck]::SetThreadDpiAwarenessContext($priorDpi)|Out-Null}
}

