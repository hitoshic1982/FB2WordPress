namespace FB2WordPress;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var singleInstance = new Mutex(true, "Local\\FB2WordPress.SingleInstance", out var first);
        if (!first)
        {
            MessageBox.Show(L.T("single_instance"), "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ApplicationConfiguration.Initialize();
        var settings = SettingsStore.Load();
        L.Configure(settings.InterfaceLanguage);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => CrashReporter.Show(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashReporter.Write(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
        Application.Run(new MainForm(settings));
        GC.KeepAlive(singleInstance);
    }
}

internal static class CrashReporter
{
    internal static void Show(Exception error)
    {
        var path = Write(error);
        MessageBox.Show(L.T("unexpected_error", path, error.Message), "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    internal static string Write(Exception error)
    {
        try
        {
            var folder = PlatformPaths.EnsureReportsDirectory();
            var path = Path.Combine(folder, L.T("error_file", DateTime.Now.ToString("yyyyMMdd-HHmmss"))); File.WriteAllText(path, error.ToString()); return path;
        }
        catch { return L.T("error_log_unavailable"); }
    }
}
