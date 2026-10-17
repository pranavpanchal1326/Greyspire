# Runs the automated play-through in a Unity window and prints anything that went wrong.
#   -Batch : headless compile/import check only (no screenshots)
param([switch]$Batch)
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.4f1\Editor\Unity.exe'
$root = Split-Path $PSScriptRoot -Parent
$log = Join-Path $root 'Logs\smoke.log'
Remove-Item $log -ErrorAction SilentlyContinue
# A run that quits while playing leaves scene backups, and Unity then blocks on a recovery dialog.
Remove-Item (Join-Path $root 'Temp\__Backupscenes') -Recurse -Force -ErrorAction SilentlyContinue

if ($Batch) {
    $p = Start-Process -FilePath $unity -ArgumentList '-batchmode', '-quit', '-projectPath', "`"$root`"", '-logFile', "`"$log`"" -PassThru
    if (-not $p.WaitForExit(420000)) { $p.Kill(); 'TIMEOUT - killed' } else { "exit: $($p.ExitCode)" }
} else {
    Remove-Item (Join-Path $root 'Logs\smoke\*.png') -ErrorAction SilentlyContinue
    Add-Type @"
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices;
public class SmokeWin {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
  // One pixel there and back: enough to stop the display going to sleep mid-run. With the
  // display off the game view stops drawing, no screenshots are saved and the timings drift.
  public static void StayAwake() { mouse_event(1, 1, 0, 0, UIntPtr.Zero); mouse_event(1, -1, 0, 0, UIntPtr.Zero); }
  // Titles of any native dialogs the process is showing (these block the editor).
  public static List<string> Dialogs(uint pid) {
    var r = new List<string>();
    EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) { var c = new StringBuilder(100); GetClassName(h, c, 100);
        if (c.ToString() == "#32770") { var s = new StringBuilder(300); GetWindowText(h, s, 300); r.Add(s.ToString()); } }
      return true; }, IntPtr.Zero);
    return r;
  }
}
"@
    # The play-through starts from a wiped save and writes its own. The player's save (the
    # editor keeps PlayerPrefs in the registry) is set aside first and put back afterwards.
    $regKey = 'HKCU\Software\Unity\UnityEditor\DefaultCompany\Greyspire'
    $saveBackup = Join-Path $root 'Logs\save-backup.reg'
    Remove-Item $saveBackup -ErrorAction SilentlyContinue
    cmd /c "reg export `"$regKey`" `"$saveBackup`" /y >nul 2>&1"
    $p = Start-Process -FilePath $unity -ArgumentList '-projectPath', "`"$root`"", '-executeMethod', 'SmokeTest.Run', '-logFile', "`"$log`"" -PassThru
    $stuck = 0
    for ($i = 0; $i -lt 190 -and -not $p.HasExited; $i++) {
        Start-Sleep -Seconds 4
        [SmokeWin]::StayAwake()
        $d = [SmokeWin]::Dialogs([uint32]$p.Id)
        if ($d.Count -gt 0 -and ($d -notmatch 'Importing|Compiling|Hold on|Loading|Unity')) { $stuck++ } else { $stuck = 0 }
        if ($stuck -ge 6) { "BLOCKED by dialog: $($d -join '; ')"; break }
    }
    if ($p.HasExited) { "exit: $($p.ExitCode)" } else { $p.Kill(); $p.WaitForExit(15000) | Out-Null; 'killed' }
    if (Test-Path $saveBackup) {
        Start-Sleep -Seconds 2
        cmd /c "reg delete `"$regKey`" /f >nul 2>&1"
        cmd /c "reg import `"$saveBackup`" >nul 2>&1"
        'save restored'
    }
}
Select-String -Path $log -Pattern 'SMOKE (state|DONE|FAILED|wilds|fps|awakening|crowd|floor|stairs|rescue|boon|status|town|rested|no such)|error CS|Shader error|Shader warning|NullReference|Exception:|Model not found|MissingComponent|Unable to find a font' |
    Where-Object { $_.Line -notmatch 'Packages/|SearchDatabase' } | Select-Object -First 60 | ForEach-Object { $_.Line }
Get-ChildItem (Join-Path $root 'Logs\smoke') -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
