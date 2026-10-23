using System.Windows.Forms;

namespace SupraAutoPackLe;
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if(args.Length==1 && args[0]=="--startup-smoke")
        {
            Environment.ExitCode=StartupSmoke.Run();
            return;
        }
        ApplicationConfiguration.Initialize();
        if(args.Length==1 && args[0]=="--mode-ui-smoke")
        {
            try { Environment.ExitCode=MainForm.RunModeTransitionSmoke(); }
            catch(Exception ex)
            {
                SaveUiDiagnostic(ex);
                Environment.ExitCode=90;
            }
            return;
        }
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => {
            SaveUiDiagnostic(e.Exception);
            MessageBox.Show("Lỗi giao diện đã được ghi lại trong nhật ký kỹ thuật. "+
                "Vui lòng kiểm tra hoặc xuất nhật ký để phân tích.",
                "SUPRA Auto In Tem", MessageBoxButtons.OK,MessageBoxIcon.Warning);
        };
        Application.Run(new MainForm());
    }
    private static void SaveUiDiagnostic(Exception exception)
    {
        try { new LocalStore().TraceException("APP","UI_EXCEPTION",exception); }
        catch { /* Error reporting must not cause a second UI exception. */ }
    }
}
