using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

/// <summary>Runs the adb.exe (and holds the IsafellieOS.apk) that are built into this program.</summary>
static class Adb
{
    public static string Dir, Exe, Apk;

    /// <summary>Copies the bundled files to %LOCALAPPDATA%\FireSweep\bin, skipping any that are already current.</summary>
    public static void Extract()
    {
        Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FireSweep", "bin");
        Directory.CreateDirectory(Dir);
        foreach (var name in new[] { "adb.exe", "AdbWinApi.dll", "AdbWinUsbApi.dll", "IsafellieOS.apk" })
        {
            string path = Path.Combine(Dir, name);
            byte[] data;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var m = new MemoryStream()) { s.CopyTo(m); data = m.ToArray(); }
            if (File.Exists(path) && SameBytes(File.ReadAllBytes(path), data)) continue;
            // A running adb server keeps adb.exe locked; the copy already there is then the one to use.
            try { File.WriteAllBytes(path, data); } catch (IOException) { }
        }
        Exe = Path.Combine(Dir, "adb.exe");
        Apk = Path.Combine(Dir, "IsafellieOS.apk");
    }

    static bool SameBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        using (var md5 = MD5.Create())
            return Convert.ToBase64String(md5.ComputeHash(a)) == Convert.ToBase64String(md5.ComputeHash(b));
    }

    /// <summary>
    /// Starts the adb server without redirected output. A server started by a redirected call would
    /// inherit its pipes and keep every later call waiting for them to close.
    /// </summary>
    public static void StartServer()
    {
        var psi = new ProcessStartInfo(Exe, "start-server") { UseShellExecute = false, CreateNoWindow = true };
        using (var p = Process.Start(psi)) p.WaitForExit(20000);
    }

    public static string Run(string args, int timeoutMs = 30000)
    {
        var psi = new ProcessStartInfo(Exe, args)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        var lines = new List<string>();
        using (var p = new Process { StartInfo = psi })
        {
            DataReceivedEventHandler add = (s, e) => { if (e.Data != null) lock (lines) lines.Add(e.Data); };
            p.OutputDataReceived += add;
            p.ErrorDataReceived += add;
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { }
                lock (lines) lines.Add("(adb timed out)");
            }
            System.Threading.Thread.Sleep(30);
            lock (lines) return string.Join("\n", lines).TrimEnd();
        }
    }

    /// <summary>Runs a command in the TV's shell. Quote device-side values with single quotes.</summary>
    public static string Shell(string addr, string cmd, int timeoutMs = 30000)
    {
        return Run("-s " + addr + " shell \"" + cmd.Replace("\"", "\\\"") + "\"", timeoutMs);
    }

    /// <summary>Saves the raw output of an adb exec-out command (such as screencap -p) to a file.</summary>
    public static void ExecOutToFile(string addr, string cmd, string file)
    {
        var psi = new ProcessStartInfo(Exe, "-s " + addr + " exec-out " + cmd)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
        };
        using (var p = Process.Start(psi))
        using (var f = File.Create(file))
        {
            var copy = p.StandardOutput.BaseStream.CopyToAsync(f);
            if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } }
            copy.Wait(5000);
        }
    }
}
