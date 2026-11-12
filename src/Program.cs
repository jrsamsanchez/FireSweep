using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("FireSweep")]
[assembly: AssemblyProduct("FireSweep")]
[assembly: AssemblyVersion("1.0.3.0")]

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

    /// <summary>
    /// Each TV's backups, logs and restore script go in Documents\FireSweep\TVs, the same place
    /// wherever the program is run from, so a copy of the exe never loses track of earlier changes.
    /// </summary>
    static string DataRoot()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FireSweep", "TVs");
        Directory.CreateDirectory(root);
        return root;
    }
}
