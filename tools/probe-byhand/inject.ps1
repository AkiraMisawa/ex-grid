# Sends real keystrokes through the OS input queue (keybd_event), as a keyboard does.
# Usage: inject.ps1 "ctrl+1" "alt" "f10" ... ; each chord is pressed then released in reverse order.
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Chords)
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Kb {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
  public static void Down(byte vk, bool ext) { keybd_event(vk, (byte)MapVirtualKey(vk, 0), ext ? 1u : 0u, UIntPtr.Zero); }
  public static void Up(byte vk, bool ext) { keybd_event(vk, (byte)MapVirtualKey(vk, 0), (ext ? 1u : 0u) | 2u, UIntPtr.Zero); }
  public static string Fg() { var s = new System.Text.StringBuilder(512); GetWindowText(GetForegroundWindow(), s, 512); return s.ToString(); }
}
"@
$names = @{ ctrl = 0x11; shift = 0x10; alt = 0x12; f1 = 0x70; f2 = 0x71; f10 = 0x79; space = 0x20; enter = 0x0D; escape = 0x1B;
  plus = 0xBB; minus = 0xBD; kanji = 0x19; grave = 0xC0; tab = 0x09; down = 0x28; up = 0x26; pagedown = 0x22 }
Write-Output "foreground: $([Kb]::Fg())"
foreach ($chord in $Chords) {
  $vks = @()
  foreach ($part in $chord.Split('+')) {
    if ($names.ContainsKey($part)) { $vks += [byte]$names[$part] }
    elseif ($part.Length -eq 1) { $vks += [byte][char]$part.ToUpper() }
    else { throw "unknown key $part" }
  }
  foreach ($vk in $vks) { [Kb]::Down($vk, $vk -in 0x26,0x28,0x22); Start-Sleep -Milliseconds 30 }
  [array]::Reverse($vks)
  foreach ($vk in $vks) { [Kb]::Up($vk, $vk -in 0x26,0x28,0x22); Start-Sleep -Milliseconds 30 }
  Start-Sleep -Milliseconds 300
}
