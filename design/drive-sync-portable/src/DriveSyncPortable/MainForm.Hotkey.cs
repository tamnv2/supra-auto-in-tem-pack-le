using System.Runtime.InteropServices;

namespace DriveSyncPortable;

// Ctrl+Alt+D restores this portable single-instance window from the tray.
public sealed partial class MainForm
{
    private const int HotkeyId = 0x4453;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private bool _hotkeyRegistered;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _hotkeyRegistered = RegisterHotKey(Handle, HotkeyId,
            ModControl | ModAlt, (uint)Keys.D);
        // If the combination is owned by another app, the tray still works.
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_hotkeyRegistered)
        {
            UnregisterHotKey(Handle, HotkeyId);
            _hotkeyRegistered = false;
        }
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam == (IntPtr)HotkeyId)
        {
            RestoreFromTray();
            return;
        }
        base.WndProc(ref m);
    }
}
