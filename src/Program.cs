using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("FireSweep")]
[assembly: AssemblyProduct("FireSweep")]
[assembly: AssemblyVersion("1.0.0.0")]

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main()
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            Adb.Extract();
            Adb.StartServer();
        }
        catch (Exception ex)
        {
            MessageBox.Show("FireSweep couldn't start adb: " + ex.Message, "FireSweep", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Application.Run(new MainForm(DataRoot()));
    }

    /// <summary>Each TV's backups, logs and restore script go in a "TVs" folder next to the program.</summary>
    static string DataRoot()
    {
        string root = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "TVs");
        try
        {
            Directory.CreateDirectory(root);
            string probe = Path.Combine(root, ".write-test");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return root;
        }
        catch
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FireSweep");
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
