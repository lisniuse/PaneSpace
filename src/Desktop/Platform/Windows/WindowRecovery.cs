using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PaneSpace.Platform.Windows;

/// <summary>Return canvas windows to physical screens without changing their sizes or show states.</summary>
public static class WindowRecovery
{
    public static void ReturnToScreens(IEnumerable<IntPtr> windows)
    {
        foreach (var hwnd in windows)
        {
            if (!Win32.IsWindow(hwnd)) continue;
            var screen = Screen.FromHandle(hwnd);
            var work = screen.WorkingArea;
            if (work.Width <= 0 || work.Height <= 0) continue;

            if (Win32.IsIconic(hwnd) || Win32.IsZoomed(hwnd))
            {
                var placement = new Win32.WINDOWPLACEMENT
                {
                    Length = (uint)Marshal.SizeOf<Win32.WINDOWPLACEMENT>()
                };
                if (Win32.GetWindowPlacement(hwnd, ref placement))
                {
                    // WINDOWPLACEMENT uses workspace coordinates, not SetWindowPos screen coordinates.
                    int dx = work.Left - screen.Bounds.Left, dy = work.Top - screen.Bounds.Top;
                    var normal = placement.NormalPosition;
                    var position = ClampPosition(new Rectangle(normal.Left + dx, normal.Top + dy,
                        normal.Right - normal.Left, normal.Bottom - normal.Top), work);
                    if (position.X != normal.Left + dx || position.Y != normal.Top + dy)
                    {
                        int width = normal.Right - normal.Left, height = normal.Bottom - normal.Top;
                        placement.NormalPosition = new Win32.RECT
                        {
                            Left = position.X - dx, Top = position.Y - dy,
                            Right = position.X - dx + width, Bottom = position.Y - dy + height
                        };
                        // Retain ShowCmd and RESTORETOMAXIMIZED; never unminimize a user's window.
                        placement.Flags |= Win32.WPF_ASYNCWINDOWPLACEMENT;
                        Win32.SetWindowPlacement(hwnd, in placement);
                    }
                }
            }
            // A minimized window's physical rectangle is the -32000 parking position.
            if (Win32.IsIconic(hwnd) || !Win32.GetWindowRect(hwnd, out var rect)) continue;
            var current = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (current.Width <= 0 || current.Height <= 0) continue;
            Point target;
            if (Win32.IsZoomed(hwnd))
            {
                // Maximized frames normally extend slightly beyond the work area. Leave those alone.
                if (current.Contains(work)) continue;
                target = new Point(work.Left - Math.Max(0, (current.Width - work.Width) / 2),
                    work.Top - Math.Max(0, (current.Height - work.Height) / 2));
            }
            else target = ClampPosition(current, work);

            if (target.X == current.X && target.Y == current.Y) continue;
            Win32.SetWindowPos(hwnd, IntPtr.Zero, target.X, target.Y, 0, 0,
                Win32.SWP_NOSIZE_ | Win32.SWP_NOZORDER_ | Win32.SWP_NOACTIVATE_ |
                Win32.SWP_NOOWNERZORDER | Win32.SWP_ASYNCWINDOWPOS);
        }
    }

    private static Point ClampPosition(Rectangle window, Rectangle work) => new(
        Math.Clamp(window.Left, work.Left, work.Left + Math.Max(0, work.Width - window.Width)),
        Math.Clamp(window.Top, work.Top, work.Top + Math.Max(0, work.Height - window.Height)));
}
