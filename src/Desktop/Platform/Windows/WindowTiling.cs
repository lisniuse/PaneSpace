using System.Drawing;
using System.Runtime.InteropServices;

namespace PaneSpace.Platform.Windows;

public static class WindowTiling
{
    /// <summary>Resize without activation; return the native origin relative to its screen cell.</summary>
    public static bool TryFit(IntPtr hwnd, Size screen, int margin, out Point offset, out bool constrained)
    {
        offset = default; constrained = false;
        if (!Win32.IsWindow(hwnd) || Win32.IsIconic(hwnd)) return false;
        if (Win32.IsZoomed(hwnd))
        {
            var placement = new Win32.WINDOWPLACEMENT { Length = (uint)Marshal.SizeOf<Win32.WINDOWPLACEMENT>() };
            if (!Win32.GetWindowPlacement(hwnd, ref placement)) return false;
            placement.ShowCmd = 4; // SW_SHOWNOACTIVATE: return to normal without stealing focus.
            placement.Flags &= ~2u; // WPF_RESTORETOMAXIMIZED
            if (!Win32.SetWindowPlacement(hwnd, in placement) || Win32.IsZoomed(hwnd)) return false;
        }
        if (!Win32.GetWindowRect(hwnd, out var before)) return false;
        // GetWindowRect includes invisible resize borders. Keep 28px to the visible frame.
        int left = 0, top = 0, right = 0, bottom = 0;
        if (Dwm.DwmGetWindowAttribute(hwnd, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out var frame,
            (uint)Marshal.SizeOf<Win32.RECT>()) == 0 &&
            frame.Right > frame.Left && frame.Bottom > frame.Top &&
            frame.Left >= before.Left && frame.Top >= before.Top &&
            frame.Right <= before.Right && frame.Bottom <= before.Bottom)
        {
            left = frame.Left - before.Left; top = frame.Top - before.Top;
            right = before.Right - frame.Right; bottom = before.Bottom - frame.Bottom;
        }
        int width = screen.Width - 2 * margin, height = screen.Height - 2 * margin;
        if (!Win32.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, width + left + right, height + top + bottom,
            0x0002 /* SWP_NOMOVE */ | Win32.SWP_NOZORDER_ | Win32.SWP_NOACTIVATE_ | Win32.SWP_NOOWNERZORDER) ||
            !Win32.GetWindowRect(hwnd, out var actual)) return false;
        int visibleWidth = actual.Right - actual.Left - left - right;
        int visibleHeight = actual.Bottom - actual.Top - top - bottom;
        offset = new Point((screen.Width - visibleWidth) / 2 - left, (screen.Height - visibleHeight) / 2 - top);
        constrained = visibleWidth != width || visibleHeight != height;
        return true;
    }
}
