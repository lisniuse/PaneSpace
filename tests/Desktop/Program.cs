using System.Drawing;
using PaneSpace.Platform.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Win32.SetProcessDpiAwarenessContext(Win32.DPI_PER_MONITOR_V2);
        var fixtures = new List<IntPtr>();
        try
        {
            var normal = MakeWindow();
            var work = Screen.FromHandle(normal).WorkingArea;
            var expectedSize = Bounds(normal).Size;
            foreach (var position in new[]
            {
                new Point(work.Left - 8000, work.Top - 8000),
                new Point(work.Right + 8000, work.Bottom + 8000),
                new Point(work.Left - 120, work.Top - 90)
            })
            {
                Move(normal, position);
                WindowRecovery.ReturnToScreens(new[] { normal, IntPtr.Zero });
                Check(work.Contains(Bounds(normal)), "off-screen and partially visible windows return to the work area");
                Check(Bounds(normal).Size == expectedSize, "recovery preserves window size");
            }
            var visible = new Point(work.Left + 25, work.Top + 25);
            Move(normal, visible);
            var foreground = Win32.GetForegroundWindow();
            WindowRecovery.ReturnToScreens(new[] { normal });
            Check(Bounds(normal).Location == visible, "visible windows retain their positions");
            Check(Win32.GetForegroundWindow() == foreground, "normal recovery does not steal focus");

            var minimized = MakeWindow();
            Move(minimized, new Point(work.Left - 8000, work.Top - 8000));
            Win32.ShowWindow(minimized, 6);
            Check(Win32.IsIconic(minimized), "minimized fixture starts minimized");
            WindowRecovery.ReturnToScreens(new[] { minimized });
            Check(Win32.IsIconic(minimized), "recovery preserves minimization");
            Win32.ShowWindow(minimized, 4);
            Check(work.Contains(Bounds(minimized)), "a minimized window restores inside the screen after recovery");

            var maximized = MakeWindow();
            Win32.ShowWindow(maximized, 3);
            Check(Win32.IsZoomed(maximized), "maximized fixture starts maximized");
            Move(maximized, new Point(work.Right + 8000, work.Bottom + 8000));
            WindowRecovery.ReturnToScreens(new[] { maximized });
            Check(Win32.IsZoomed(maximized), "recovery preserves maximization");
            Check(Bounds(maximized).Contains(work), "off-screen maximized windows cover the visible work area again");
            Win32.ShowWindow(maximized, 4);
            Check(work.Contains(Bounds(maximized)), "a maximized window's normal restore position is reachable");

            var minimizedMaximized = MakeWindow();
            Move(minimizedMaximized, new Point(work.Left - 8000, work.Top - 8000));
            Win32.ShowWindow(minimizedMaximized, 3);
            Move(minimizedMaximized, new Point(work.Right + 8000, work.Bottom + 8000));
            Win32.ShowWindow(minimizedMaximized, 6);
            WindowRecovery.ReturnToScreens(new[] { minimizedMaximized });
            Check(Win32.IsIconic(minimizedMaximized), "a minimized maximized window stays minimized");
            Win32.ShowWindow(minimizedMaximized, 9);
            Check(Win32.IsZoomed(minimizedMaximized), "restore-to-maximized state survives recovery");
            Check(Bounds(minimizedMaximized).Contains(work), "a minimized maximized window restores onto the screen");

            var oversized = MakeWindow();
            Win32.SetWindowPos(oversized, IntPtr.Zero, work.Right + 8000, work.Bottom + 8000,
                work.Width + 200, work.Height + 100, Win32.SWP_NOZORDER_ | Win32.SWP_NOACTIVATE_);
            var oversizedSize = Bounds(oversized).Size;
            WindowRecovery.ReturnToScreens(new[] { oversized });
            Check(Bounds(oversized).Location == work.Location, "oversized windows regain a visible title bar");
            Check(Bounds(oversized).Size == oversizedSize, "oversized windows are not resized");

            var owner = MakeWindow();
            var dialog = MakeWindow(owner);
            var nestedDialog = MakeWindow(dialog);
            var unrelated = MakeWindow();
            Move(owner, new Point(work.Left - 8000, work.Top - 8000));
            Move(dialog, new Point(work.Left - 7800, work.Top - 7800));
            Move(nestedDialog, new Point(work.Left - 7600, work.Top - 7600));
            Move(unrelated, new Point(work.Left - 7400, work.Top - 7400));
            var unrelatedBefore = Bounds(unrelated);
            WindowRecovery.ReturnToScreens(new[] { owner });
            Check(work.Contains(Bounds(dialog)) && work.Contains(Bounds(nestedDialog)),
                "owned and nested dialogs return with their canvas owner");
            Check(Bounds(unrelated) == unrelatedBefore, "unrelated windows are left alone");

            var closed = MakeWindow();
            Win32.DestroyWindow(closed);
            WindowRecovery.ReturnToScreens(new[] { closed, IntPtr.Zero });
            Console.WriteLine("PASS destroyed handles are ignored");
            Console.WriteLine("All native window recovery checks passed; only test windows were moved.");
        }
        finally
        {
            foreach (var hwnd in fixtures)
                if (Win32.IsWindow(hwnd)) Win32.DestroyWindow(hwnd);
        }

        IntPtr MakeWindow(IntPtr owner = default)
        {
            var hwnd = Win32.CreateWindowEx(0, "STATIC", "PaneSpace recovery test fixture", unchecked((int)0x90cf0000),
                -8000, -8000, 320, 220, owner, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Check(hwnd != IntPtr.Zero, "create isolated window fixture");
            fixtures.Add(hwnd);
            return hwnd;
        }
    }

    private static Rectangle Bounds(IntPtr hwnd)
    {
        if (!Win32.GetWindowRect(hwnd, out var rect)) throw new Exception("Cannot read window bounds.");
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static void Move(IntPtr hwnd, Point position) => Win32.SetWindowPos(hwnd, IntPtr.Zero,
        position.X, position.Y, 0, 0, Win32.SWP_NOSIZE_ | Win32.SWP_NOZORDER_ | Win32.SWP_NOACTIVATE_);

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
