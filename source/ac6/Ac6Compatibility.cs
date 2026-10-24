using System;
using System.Collections.Generic;
using System.IO;

namespace SenseBridge.Ac6 {
// Compatibility and protection checks must pass before any connection attempt.
// This class does not load a DLL, change another process, or disable protection.
public static class Ac6Compatibility {
 public const string ExecutableSha256 = "e003771e5548bca91882c1d7dfe2df3de97cee05c2c989d0c0e34b036ea610d6";
 public const string SteamAppId = "1888160";
 public static bool ProtectedName(string name) {
  if(string.IsNullOrWhiteSpace(name)) return false;
  string n=Path.GetFileNameWithoutExtension(name);
  return n.IndexOf("easyanticheat",StringComparison.OrdinalIgnoreCase)>=0
   || string.Equals(n,"start_protected_game",StringComparison.OrdinalIgnoreCase);
 }
 public static string RejectReason(string executable,string sha256,bool offlineConfirmed,
     bool inspectionComplete,IEnumerable<string> processes,IEnumerable<string> modules) {
  if(!inspectionComplete||processes==null||modules==null) return "inspection-unavailable";
  foreach(string n in processes) if(ProtectedName(n)) return "protected-process-active";
  foreach(string n in modules) if(ProtectedName(n)) return "protected-module-loaded";
  if(!offlineConfirmed) return "offline-session-not-confirmed";
  if(!string.Equals(Path.GetFileName(executable??""),"armoredcore6.exe",StringComparison.OrdinalIgnoreCase))
   return "unexpected-executable";
  if(!string.Equals(sha256,ExecutableSha256,StringComparison.OrdinalIgnoreCase)) return "unsupported-game-version";
  return null;
 }
}
}
