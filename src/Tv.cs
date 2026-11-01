using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// What the tool changed on one TV, kept in that TV's folder so every change can be undone, by
/// this program or by the restore.ps1 written beside it. The file names match the earlier
/// hand-run scripts, so their logs carry over as they are.
/// </summary>
class Journal
{
    readonly string dir;
    public List<string> Disabled, AppOps;
    public List<string[]> Settings;          // namespace, key, value before the tool changed it
    public string AccessibilityBefore;       // null when the tool hasn't touched accessibility
    public bool InstalledLauncher;

    const string DisabledFile = "disabled-by-isafellieos.txt", AppOpsFile = "appops-changed-by-isafellieos.txt",
        SettingsFile = "settings-changed-by-isafellieos.txt", AccessFile = "accessibility-before.txt",
        LauncherFile = "isafellieos-installed.txt";

    public Journal(string dir)
    {
        this.dir = dir;
        Disabled = Lines(DisabledFile).Distinct().ToList();
        AppOps = Lines(AppOpsFile).Distinct().ToList();
        Settings = new List<string[]>();
        foreach (var l in Lines(SettingsFile))
        {
            var parts = l.Split(new[] { '|' }, 3);
            if (parts.Length == 3 && Setting(parts[0], parts[1]) == null) Settings.Add(parts);
        }
        string acc = Path.Combine(dir, AccessFile);
        AccessibilityBefore = File.Exists(acc) ? File.ReadAllText(acc).Trim() : null;
        InstalledLauncher = File.Exists(Path.Combine(dir, LauncherFile));
    }

    IEnumerable<string> Lines(string name)
    {
        string f = Path.Combine(dir, name);
        return File.Exists(f) ? File.ReadAllLines(f).Select(l => l.Trim()).Where(l => l.Length > 0) : new string[0];
    }

    public string[] Setting(string ns, string key)
    {
        return Settings.FirstOrDefault(s => s[0] == ns && s[1] == key);
    }

    public bool Empty
    {
        get { return Disabled.Count == 0 && AppOps.Count == 0 && Settings.Count == 0 && AccessibilityBefore == null && !InstalledLauncher; }
    }

    public void Save()
    {
        Write(DisabledFile, Disabled);
        Write(AppOpsFile, AppOps);
        Write(SettingsFile, Settings.Select(s => string.Join("|", s)));
        Write(AccessFile, AccessibilityBefore == null ? null : new[] { AccessibilityBefore });
        Write(LauncherFile, InstalledLauncher ? new[] { "IsafellieOS was installed by FireSweep" } : null);
    }

    void Write(string name, IEnumerable<string> lines)
    {
        string f = Path.Combine(dir, name);
        var list = lines == null ? new List<string>() : lines.ToList();
        if (list.Count == 0) { if (File.Exists(f)) File.Delete(f); return; }
        File.WriteAllLines(f, list);
    }

    /// <summary>After a full restore, moves the logs aside so the TV reads as untouched.</summary>
    public void Archive()
    {
        string to = Path.Combine(dir, "restored-" + DateTime.Now.ToString("yyyy-MM-dd_HHmm"));
        Directory.CreateDirectory(to);
        foreach (var n in new[] { DisabledFile, AppOpsFile, SettingsFile, AccessFile, LauncherFile })
        {
            string f = Path.Combine(dir, n);
            if (File.Exists(f)) File.Move(f, Path.Combine(to, n));
        }
        Disabled.Clear(); AppOps.Clear(); Settings.Clear(); AccessibilityBefore = null; InstalledLauncher = false;
    }
}

/// <summary>A connected TV: what's on it now, and the changes the tool can make to it.</summary>
class Tv
{
    public string Addr, Serial, Model, Maker, FireOs, Folder;
    public Journal J;
    public HashSet<string> All = new HashSet<string>(), Disabled = new HashSet<string>();
    public Dictionary<string, string> Secure = new Dictionary<string, string>();
    public string Accessibility = "null";
    public int LauncherVersion;
    public Action<string> Log;

    public Tv(string addr, Action<string> log) { Addr = addr; Log = log; }

    public string Sh(string cmd) { return Adb.Shell(Addr, cmd); }

    static HashSet<string> Packages(string listing)
    {
        return new HashSet<string>(listing.Split('\n').Select(l => l.Trim())
            .Where(l => l.StartsWith("package:")).Select(l => l.Substring(8)));
    }

    /// <summary>Reads the TV's identity and opens (or creates) its folder under dataRoot.</summary>
    public void Open(string dataRoot)
    {
        var props = Sh("getprop ro.serialno; getprop ro.product.model; getprop ro.product.manufacturer; getprop ro.build.version.name")
            .Split('\n').Select(l => l.Trim()).ToArray();
        if (props.Length < 4 || props[0].Contains("unauthorized") || props[0].StartsWith("error"))
            throw new InvalidOperationException(string.Join(" ", props));
        Serial = props[0]; Model = props[1]; Maker = props[2]; FireOs = props[3];
        Folder = Path.Combine(dataRoot, Safe(Model + "_" + Serial));
        Directory.CreateDirectory(Folder);
        File.WriteAllLines(Path.Combine(Folder, "tv.txt"), new[]
        {
            "Model: " + Maker + " " + Model, "Serial: " + Serial, "System: " + FireOs,
            "Last address: " + Addr, "Last opened: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        });
        J = new Journal(Folder);
    }

    static string Safe(string s) { return Regex.Replace(s, @"[^A-Za-z0-9_.-]", "_"); }

    public void Refresh()
    {
        All = Packages(Sh("pm list packages"));
        Disabled = Packages(Sh("pm list packages -d"));
        var keys = Catalog.PrivacySettings.Select(k => k.Key).Concat(new[] { "enabled_accessibility_services", "accessibility_enabled" }).ToList();
        var vals = Sh(string.Join("; ", keys.Select(k => "settings get secure '" + k + "'"))).Split('\n');
        for (int i = 0; i < keys.Count; i++) Secure[keys[i]] = i < vals.Length ? vals[i].Trim() : "null";
        Accessibility = Secure["enabled_accessibility_services"];
        var m = Regex.Match(Sh("dumpsys package " + Catalog.LauncherPackage + " | grep versionCode"), @"versionCode=(\d+)");
        LauncherVersion = All.Contains(Catalog.LauncherPackage) && m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    public bool HasBackup
    {
        get { return Directory.GetDirectories(Folder, "backup-*").Length > 0; }
    }

    public string[] Present(Item it) { return it.Packages.Where(p => All.Contains(p)).ToArray(); }

    /// <summary>True when the change is in effect, false when not, null when the TV doesn't have it.</summary>
    public bool? IsOn(Item it)
    {
        switch (it.Kind)
        {
            case Kind.Settings:
                return Catalog.PrivacySettings.All(s => Secure[s.Key] == s.Value);
            case Kind.Launcher:
                return LauncherVersion > 0 && Accessibility.Contains(Catalog.LauncherService);
            default:
                var present = Present(it);
                if (present.Length == 0) return null;
                return present.All(p => Disabled.Contains(p) || J.AppOps.Contains(p));
        }
    }

    // ---- making and undoing changes ----

    public void Apply(Item it, bool on)
    {
        Log((on ? "Turning on: " : "Undoing: ") + it.Label);
        switch (it.Kind)
        {
            case Kind.Settings: if (on) SettingsOff(); else SettingsBack(); break;
            case Kind.Launcher: if (on) InstallLauncher(); else RemoveLauncher(); break;
            default: if (on) Disable(Present(it)); else Enable(Present(it)); break;
        }
        J.Save();
    }

    void Disable(IEnumerable<string> pkgs)
    {
        foreach (var p in pkgs)
        {
            if (Disabled.Contains(p) || J.AppOps.Contains(p)) continue;
            string r = Sh("pm disable-user --user 0 " + p);
            if (r.Contains("new state: disabled"))
            {
                if (!J.Disabled.Contains(p)) J.Disabled.Add(p);
                Log("  off  " + p);
            }
            else
            {
                // Fire OS refuses to disable a few protected packages; stop them running in the background instead.
                Sh("cmd appops set " + p + " RUN_IN_BACKGROUND ignore");
                J.AppOps.Add(p);
                Log("  " + p + " is protected by Fire OS, so it's blocked from running in the background instead");
            }
        }
    }

    void Enable(IEnumerable<string> pkgs)
    {
        foreach (var p in pkgs)
        {
            if (J.AppOps.Remove(p)) { Sh("cmd appops set " + p + " RUN_IN_BACKGROUND default"); Log("  background allowed again  " + p); }
            if (Disabled.Contains(p)) { Log("  on   " + p + "  " + Sh("pm enable " + p)); J.Disabled.Remove(p); }
        }
    }

    void PutSetting(string key, string value)
    {
        if (value == "null") Sh("settings delete secure '" + key + "'");
        else Sh("settings put secure '" + key + "' '" + value + "'");
    }

    /// <summary>Sets a secure setting, recording its earlier value the first time.</summary>
    void ChangeSetting(string key, string value)
    {
        string old = Secure.ContainsKey(key) ? Secure[key] : Sh("settings get secure '" + key + "'").Trim();
        if (old == value) return;
        if (J.Setting("secure", key) == null) J.Settings.Add(new[] { "secure", key, old });
        PutSetting(key, value);
        Log("  " + key + ": " + old + " -> " + value);
    }

    /// <summary>Puts a secure setting back to the value recorded before the tool changed it.</summary>
    void RevertSetting(string key)
    {
        var s = J.Setting("secure", key);
        if (s == null) { Log("  " + key + " was already set that way before; left as it is"); return; }
        PutSetting(key, s[2]);
        J.Settings.Remove(s);
        Log("  " + key + " back to " + s[2]);
    }

    void SettingsOff() { foreach (var s in Catalog.PrivacySettings) ChangeSetting(s.Key, s.Value); }

    void SettingsBack() { foreach (var s in Catalog.PrivacySettings) RevertSetting(s.Key); }

    List<string> OtherAccessibilityServices()
    {
        if (Accessibility == "null" || Accessibility == "") return new List<string>();
        return Accessibility.Split(':').Where(s => s.Length > 0 && s != Catalog.LauncherService).ToList();
    }

    void SetAccessibilityServices(List<string> services)
    {
        if (services.Count == 0) Sh("settings delete secure enabled_accessibility_services");
        else Sh("settings put secure enabled_accessibility_services '" + string.Join(":", services) + "'");
    }

    void InstallLauncher()
    {
        bool wasInstalled = LauncherVersion > 0;
        if (LauncherVersion < Catalog.LauncherVersion)
        {
            Log("  installing IsafellieOS (about 10 MB)...");
            string r = Adb.Run("-s " + Addr + " install -r \"" + Adb.Apk + "\"", 300000);
            if (!r.Contains("Success")) throw new InvalidOperationException("IsafellieOS didn't install: " + r);
            if (!wasInstalled) J.InstalledLauncher = true;
            Log("  installed");
        }
        // The Home-button redirect is an accessibility service. Switching it off and on again makes
        // Fire OS bind the newly installed copy.
        var others = OtherAccessibilityServices();
        if (J.AccessibilityBefore == null && !Accessibility.Contains(Catalog.LauncherService)) J.AccessibilityBefore = Accessibility;
        SetAccessibilityServices(others);
        SetAccessibilityServices(others.Concat(new[] { Catalog.LauncherService }).ToList());
        ChangeSetting("accessibility_enabled", "1");
        Log("  Home button now opens IsafellieOS");
    }

    void RemoveLauncher()
    {
        var others = OtherAccessibilityServices();
        SetAccessibilityServices(others);
        if (others.Count == 0 && J.Setting("secure", "accessibility_enabled") != null) RevertSetting("accessibility_enabled");
        if (LauncherVersion > 0) Log("  " + Sh("pm uninstall " + Catalog.LauncherPackage) + " (IsafellieOS removed)");
        J.AccessibilityBefore = null;
        J.InstalledLauncher = false;
    }

    /// <summary>Reads the dock from IsafellieOS 1.1 or later, or null if it can't be read.</summary>
    public List<string> GetDock()
    {
        if (LauncherVersion < 2) return null;
        var m = Regex.Match(Sh("am broadcast -n tv.isafellie.os/.DockReceiver -a tv.isafellie.os.DOCK"), "data=\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value.Split(',').Where(p => p.Length > 0).ToList() : null;
    }

    public void SetDock(List<string> pkgs)
    {
        string r = Sh("am broadcast -n tv.isafellie.os/.DockReceiver -a tv.isafellie.os.DOCK --es dock '" + string.Join(",", pkgs) + "'");
        Log(r.Contains("data=") ? "Dock set: " + string.Join(", ", pkgs.Select(p => Catalog.AppName(p))) : "The dock didn't update: " + r);
    }

    public void ShowLauncher() { Sh("am start -n tv.isafellie.os/.HomeActivity"); }

    /// <summary>Launchable apps on the TV, for the dock picker.</summary>
    public List<string> LaunchableApps()
    {
        var r = Sh("cmd package query-activities --brief -a android.intent.action.MAIN -c android.intent.category.LEANBACK_LAUNCHER");
        return r.Split('\n').Select(l => l.Trim()).Where(l => l.Contains("/") && !l.Contains(" "))
            .Select(l => l.Substring(0, l.IndexOf('/'))).Distinct()
            .Where(p => !Catalog.HiddenFromDock(p)).ToList();
    }

    /// <summary>Undoes everything in the journal, in the same order as restore.ps1.</summary>
    public void RestoreAll()
    {
        Log("Restoring everything on " + Model + "...");
        if (Accessibility.Contains(Catalog.LauncherService) || J.AccessibilityBefore != null || LauncherVersion > 0)
        {
            string before = J.AccessibilityBefore;
            if (before == null) SetAccessibilityServices(OtherAccessibilityServices());
            else if (before == "null" || before == "") Sh("settings delete secure enabled_accessibility_services");
            else Sh("settings put secure enabled_accessibility_services '" + before + "'");
            if (LauncherVersion > 0) Log("  " + Sh("pm uninstall " + Catalog.LauncherPackage) + " (IsafellieOS removed)");
        }
        foreach (var p in J.Disabled) Log("  on   " + p + "  " + Sh("pm enable " + p));
        foreach (var p in J.AppOps) { Sh("cmd appops set " + p + " RUN_IN_BACKGROUND default"); Log("  background allowed again  " + p); }
        foreach (var s in J.Settings)
        {
            if (s[2] == "null") Sh("settings delete " + s[0] + " '" + s[1] + "'");
            else Sh("settings put " + s[0] + " '" + s[1] + "' '" + s[2] + "'");
            Log("  " + s[1] + " back to " + s[2]);
        }
        J.Archive();
        Log("Done. Restart the TV (Settings > My Fire TV > Restart) to finish.");
    }

    // ---- backup and the restore script ----

    public string Backup()
    {
        string dir = Path.Combine(Folder, "backup-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
        Directory.CreateDirectory(dir);
        Log("Backing up the TV's current state to " + dir);
        var files = new[]
        {
            new[] { "getprop.txt", "getprop" },
            new[] { "packages-all.txt", "pm list packages -f" },
            new[] { "packages-enabled.txt", "pm list packages -e" },
            new[] { "packages-disabled.txt", "pm list packages -d" },
            new[] { "packages-system.txt", "pm list packages -s" },
            new[] { "packages-thirdparty.txt", "pm list packages -3" },
            new[] { "settings-global.txt", "settings list global" },
            new[] { "settings-secure.txt", "settings list secure" },
            new[] { "settings-system.txt", "settings list system" },
            new[] { "home-activity.txt", "cmd package resolve-activity -a android.intent.action.MAIN -c android.intent.category.HOME" },
            new[] { "home-candidates.txt", "cmd package query-activities -a android.intent.action.MAIN -c android.intent.category.HOME" },
            new[] { "user-restrictions.txt", "dumpsys user" },
            new[] { "appops-background.txt", "cmd appops query-op RUN_IN_BACKGROUND ignore" },
        };
        foreach (var f in files) File.WriteAllText(Path.Combine(dir, f[0]), Sh(f[1]) + "\n");
        // A screenshot is taken off-screen: nothing shows on the TV.
        Adb.ExecOutToFile(Addr, "screencap -p", Path.Combine(dir, "screen.png"));
        Log("  backup saved");
        return dir;
    }

    public void WriteRestoreScript()
    {
        File.WriteAllText(Path.Combine(Folder, "restore.ps1"), RestoreScript.Replace("__DEVICE__", Addr));
        File.WriteAllText(Path.Combine(Folder, "Restore this TV.cmd"),
            "@echo off\r\npowershell -NoProfile -ExecutionPolicy Bypass -File \"%~dp0restore.ps1\" %*\r\npause\r\n");
    }

    const string RestoreScript = @"# Puts this Fire TV back the way it was before FireSweep changed it.
# Double-click ""Restore this TV.cmd"" next to this file. If the TV's address has changed, run:
#   powershell -ExecutionPolicy Bypass -File restore.ps1 -Device <new address>:5555
param([string]$Device = ""__DEVICE__"")

$adb = ""$env:LOCALAPPDATA\FireSweep\bin\adb.exe""
if (-not (Test-Path $adb)) { $adb = (Get-Command adb -ErrorAction SilentlyContinue).Source }
if (-not $adb) { $adb = ""$env:USERPROFILE\platform-tools\adb.exe"" }
$here = $PSScriptRoot
& $adb connect $Device | Out-Null
function sh { & $adb -s $Device shell @args }
if ((sh echo ok) -ne 'ok') { ""Can't reach the TV at $Device. Check it's on and ADB debugging is on.""; exit 1 }

# 1. Remove the IsafellieOS home screen and its Home-button redirect.
$acc = Join-Path $here 'accessibility-before.txt'
if (Test-Path $acc) {
  $old = (Get-Content $acc -Raw).Trim()
  if ($old -eq 'null' -or $old -eq '') { sh settings delete secure enabled_accessibility_services | Out-Null }
  else { sh settings put secure enabled_accessibility_services ""'$old'"" | Out-Null }
}
if ((sh pm list packages tv.isafellie.os) -match 'tv.isafellie.os') { sh pm uninstall tv.isafellie.os | Out-Null; 'removed IsafellieOS' }

# 2. Re-enable every package that was turned off.
$log = Join-Path $here 'disabled-by-isafellieos.txt'
if (Test-Path $log) { Get-Content $log | Where-Object { $_ } | Sort-Object -Unique | ForEach-Object { ""on    $_  $(sh pm enable $_)"" } }

# 3. Let protected packages run in the background again.
$ops = Join-Path $here 'appops-changed-by-isafellieos.txt'
if (Test-Path $ops) { Get-Content $ops | Where-Object { $_ } | Sort-Object -Unique | ForEach-Object { sh cmd appops set $_ RUN_IN_BACKGROUND default; ""background allowed again  $_"" } }

# 4. Put settings back to their original values (first recorded value wins).
$set = Join-Path $here 'settings-changed-by-isafellieos.txt'
if (Test-Path $set) {
  $seen = @{}
  foreach ($line in Get-Content $set) {
    $ns, $key, $val = $line -split '\|', 3
    if (-not $key -or $seen.ContainsKey(""$ns|$key"")) { continue }; $seen[""$ns|$key""] = 1
    if ($val -eq 'null') { sh settings delete $ns ""'$key'"" | Out-Null } else { sh settings put $ns ""'$key'"" ""'$val'"" }
    ""setting $key = $val""
  }
}

# Move the logs aside so FireSweep sees this TV as untouched.
$done = Join-Path $here ('restored-' + (Get-Date -Format 'yyyy-MM-dd_HHmm'))
New-Item -ItemType Directory -Force $done | Out-Null
'disabled-by-isafellieos.txt', 'appops-changed-by-isafellieos.txt', 'settings-changed-by-isafellieos.txt',
  'accessibility-before.txt', 'isafellieos-installed.txt' | ForEach-Object {
  $f = Join-Path $here $_; if (Test-Path $f) { Move-Item $f $done }
}
'Done. Restart the TV (Settings > My Fire TV > Restart) to finish.'
";
}
