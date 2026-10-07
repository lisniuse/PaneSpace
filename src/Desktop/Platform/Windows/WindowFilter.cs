using System.Text;


namespace PaneSpace.Platform.Windows;

/// <summary>Which top-level windows are "on the canvas" (safe to move).</summary>
public static class WindowFilter
{
    private static readonly string[] BlacklistPrefixes =
    {
        "Shell_", "Windows.UI.", "WorkerW", "Progman", "XamlExplorerHostIslandWindow",
        "TopLevelWindowForOverflow", "NotifyIconOverflowWindow", "SysShadow",
        "tooltips_class", "OleDdeWndClass", "DirectInput", "MSCTF", "Default IME", "IME",
        "Chrome_RenderWidgetHostHWND", "Intermediate D3D Window", "OimeDirectUIWindow",
        "ConsoleWindowClass", // own consoles etc.
    };

    public static bool IsManaged(IntPtr hwnd, uint selfPid)
    {
        if (hwnd == IntPtr.Zero || !Win32.IsWindow(hwnd) || !Win32.IsWindowVisible(hwnd)) return false;
        if (Win32.GetWindow(hwnd, Win32.GW_OWNER) != IntPtr.Zero) return false;
        if ((Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64() & Win32.WS_EX_TOOLWINDOW) != 0) return false;
        Win32.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == selfPid) return false;
        var sb = new StringBuilder(256);
        Win32.GetClassName(hwnd, sb, sb.Capacity);
        string cls = sb.ToString();
        foreach (var p in BlacklistPrefixes)
            if (cls.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
