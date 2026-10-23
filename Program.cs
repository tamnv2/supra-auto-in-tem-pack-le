using System.Diagnostics;
using System.Text.Json;
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
        try
        {
            var root=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),"SupraAutoPackLe","logs");
            Directory.CreateDirectory(root);
            // Only exception type and managed method names. No stack file paths,
            // messages, user inputs, business data, passwords or auth tokens.
            var methods=new StackTrace(exception,false).GetFrames()?
                .Take(8).Select(frame=>{
                    var method=frame.GetMethod();
                    return (method?.DeclaringType?.Name??"Unknown")+"."+
                        (method?.Name??"Unknown");
                }).ToArray() ?? [];
            var record=JsonSerializer.Serialize(new {
                at=DateTimeOffset.Now.ToString("O"), kind="UI_EXCEPTION",
                type=exception.GetType().Name, methods
            });
            File.AppendAllText(Path.Combine(root,DateTime.Now.ToString("yyyy-MM-dd")+".jsonl"),
                record+Environment.NewLine);
        }
        catch { /* Never let diagnostic logging cause another UI exception. */ }
    }
}
