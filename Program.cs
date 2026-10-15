using System.Windows.Forms;
namespace SupraAutoPackLe;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, _) => MessageBox.Show("Lỗi giao diện. Xuất nhật ký kỹ thuật để phân tích.", "SUPRA Auto In Tem");
        Application.Run(new MainForm());
    }
}
