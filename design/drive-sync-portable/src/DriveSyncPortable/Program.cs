namespace DriveSyncPortable;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(a => string.Equals(a, "--ui-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using var app = new MainForm();
                app.CreateControl();
                if (!app.Text.Contains("DriveSync Portable", StringComparison.Ordinal))
                    throw new InvalidOperationException("APP_TITLE_MISSING");
                if (app.Controls.Count < 2)
                    throw new InvalidOperationException("MAIN_LAYOUT_MISSING");

                var all = EnumerateControls(app).ToArray();
                string[] required = {
                    "Đăng nhập Google", "Chọn thư mục", "Tự khởi động cùng Windows",
                    "Chạy ngầm", "Hoạt động gần đây", "Tài khoản Google",
                    "GOOGLE DRIVE", "TRÊN LAPTOP"
                };
                foreach (var label in required)
                    if (!all.Any(c => c.Text.Contains(label, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("CONTROL_MISSING: " + label);

                if (all.OfType<RoundedButton>().Count() < 6)
                    throw new InvalidOperationException("BUTTON_COUNT_INVALID");
                if (all.OfType<ToggleSwitch>().Count() != 1)
                    throw new InvalidOperationException("STARTUP_TOGGLE_MISSING");

                Console.WriteLine("UI_SMOKE_PASS");
                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return 2;
            }
        }

        using var singleInstance = new Mutex(true,
            "Local\\DriveSyncPortable-" + Environment.UserName, out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("DriveSync Portable đang chạy. Hãy mở từ biểu tượng khay hệ thống.",
                "DriveSync Portable", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
        Application.Run(new MainForm());
        return 0;
    }

    private static IEnumerable<Control> EnumerateControls(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var nested in EnumerateControls(child))
                yield return nested;
        }
    }
}
