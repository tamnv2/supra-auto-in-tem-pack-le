using Microsoft.Win32;
using System.Windows.Forms;

namespace SupraAutoPackLe;

/// <summary>
/// Standard-user autostart. Changes only the current user's own Run entry.
/// Does not require elevation, modify policy, or register a service/task.
/// </summary>
internal static class AutoStartManager
{
    private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName="WmsPackingAssistant";

    private static string ExecutablePath =>
        Environment.ProcessPath ?? Application.ExecutablePath;

    public static bool IsEnabled()
    {
        try
        {
            using var key=Registry.CurrentUser.OpenSubKey(RunKey,writable:false);
            var stored=key?.GetValue(ValueName) as string;
            return string.Equals(stored,"\""+ExecutablePath+"\"",StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key=Registry.CurrentUser.CreateSubKey(RunKey,writable:true)
            ?? throw new InvalidOperationException("HKCU_RUN_UNAVAILABLE");
        if(enabled)
        {
            var path=ExecutablePath;
            if(!File.Exists(path))throw new FileNotFoundException("APPLICATION_EXE_NOT_FOUND");
            key.SetValue(ValueName,"\""+path+"\"",RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName,throwOnMissingValue:false);
        }
    }
}
