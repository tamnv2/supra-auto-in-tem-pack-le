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
                    "Chạy ngầm", "Lịch sử đồng bộ", "Tài khoản Google",
                    "Thoát ứng dụng", "TỐC ĐỘ TẢI LÊN", "TỐC ĐỘ TẢI XUỐNG",
                    "GOOGLE DRIVE", "TRÊN LAPTOP"
                };
                foreach (var label in required)
                    if (!all.Any(c => c.Text.Contains(label, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("CONTROL_MISSING: " + label);

                if (all.OfType<RoundedButton>().Count() < 6)
                    throw new InvalidOperationException("BUTTON_COUNT_INVALID");
                if (all.OfType<ToggleSwitch>().Count() != 1)
                    throw new InvalidOperationException("STARTUP_TOGGLE_MISSING");
                var history = all.OfType<ListView>().SingleOrDefault();
                if (history is null || history.Columns.Count != 4)
                    throw new InvalidOperationException("SCROLLABLE_HISTORY_MISSING");
                if (Enum.GetValues<SyncState>().Length < 10)
                    throw new InvalidOperationException("SYNC_STATUS_SET_INCOMPLETE");
                var progress = new List<TransferProgress>();
                var meter = new TransferMeter(progress.Add, TransferDirection.Upload, "sample.txt", 2048);
                meter.Add(1024);
                meter.Add(1024);
                meter.Finish();
                if (progress.Last().BytesTransferred != 2048 ||
                    !progress.Last().Completed || TransferFormat.Speed(1024).Length == 0)
                    throw new InvalidOperationException("TRANSFER_METER_INCORRECT");

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
        if (args.Any(a => string.Equals(a, "--sync-smoke", StringComparison.OrdinalIgnoreCase)))
            return SyncSmoke.RunAsync().GetAwaiter().GetResult();

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
