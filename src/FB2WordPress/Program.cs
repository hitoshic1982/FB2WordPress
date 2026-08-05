namespace FB2WordPress;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var singleInstance = new Mutex(true, "Local\\FB2WordPress.SingleInstance", out var first);
        if (!first)
        {
            MessageBox.Show("FB2WordPress 已經在執行，請回到原本的視窗。", "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => CrashReporter.Show(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashReporter.Write(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
        Application.Run(new MainForm());
        GC.KeepAlive(singleInstance);
    }
}

internal static class CrashReporter
{
    internal static void Show(Exception error)
    {
        var path = Write(error);
        MessageBox.Show($"程式遇到非預期問題，已保存錯誤報告：\n{path}\n\n{error.Message}", "FB2WordPress", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    internal static string Write(Exception error)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FB2WordPress Reports");
            Directory.CreateDirectory(folder); var path = Path.Combine(folder, $"錯誤-{DateTime.Now:yyyyMMdd-HHmmss}.txt"); File.WriteAllText(path, error.ToString()); return path;
        }
        catch { return "無法寫入錯誤報告"; }
    }
}
