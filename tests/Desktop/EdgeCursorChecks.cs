using System.Runtime.InteropServices;
using PaneSpace.Platform.Windows;
using PaneSpace.Rendering;

internal static partial class Program
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X, Y;
        public uint Data, Flags, Time;
        public UIntPtr ExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput { public uint Type; public MouseInput Mouse; }
    [DllImport("user32.dll")]
    private static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo { public uint Size, Flags; public IntPtr Cursor; public Win32.POINT Position; }
    [DllImport("user32.dll")]
    private static extern bool GetCursorInfo(ref CursorInfo info);

    private static void CheckEdgeCursor()
    {
        Win32.GetCursorPos(out var original);
        int interrupted = 0, clicks = 0;
        var work = Screen.PrimaryScreen!.WorkingArea;
        using var fixture = new CursorProbeForm { Bounds = new Rectangle(work.Left + 80, work.Top + 80, 160, 120),
            FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, ShowInTaskbar = false };
        fixture.MouseDown += (_, _) => clicks++;
        using var surface = new EdgePanCursorSurface(() => interrupted++);
        try
        {
            fixture.Show(); Application.DoEvents();
            Win32.SetWindowPos(fixture.Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0,
                Win32.SWP_NOSIZE_ | 0x0002 | Win32.SWP_NOACTIVATE);
            var area = fixture.Bounds;
            var foreground = Win32.GetForegroundWindow();
            var handles = new HashSet<IntPtr>();
            foreach (var direction in new[] { new Point(-1, -1), new Point(0, -1), new Point(1, -1),
                new Point(-1, 0), new Point(1, 0), new Point(-1, 1), new Point(0, 1), new Point(1, 1) })
            {
                int x = direction.X < 0 ? area.Left + 1 : direction.X > 0 ? area.Right - 2 : area.Left + area.Width / 2;
                int y = direction.Y < 0 ? area.Top + 1 : direction.Y > 0 ? area.Bottom - 2 : area.Top + area.Height / 2;
                Win32.SetCursorPos(x, y); Application.DoEvents();
                surface.ShowFeedback(area, direction); Application.DoEvents();
                Check(Win32.WindowFromPoint(new Win32.POINT { X = x, Y = y }) == surface.Handle,
                    "edge feedback owns cursor input even above another native window");
                Check(handles.Add(surface.CursorHandle) && surface.CursorHandle != IntPtr.Zero,
                    "each of the eight directions has a valid native cursor");
                var visibleCursor = new CursorInfo { Size = (uint)Marshal.SizeOf<CursorInfo>() };
                Check(GetCursorInfo(ref visibleCursor) && visibleCursor.Cursor == surface.CursorHandle,
                    "Windows displays the directional cursor above the underlying window");
                Check(Win32.GetIconInfo(surface.CursorHandle, out var info) && !info.fIcon,
                    "direction artwork is a cursor, not an icon");
                try
                {
                    Check(Math.Sign((int)info.xHotspot - 16) == direction.X && Math.Sign((int)info.yHotspot - 16) == direction.Y,
                        "arrow tip and hotspot point toward the intended edge");
                }
                finally { Win32.DeleteObject(info.hbmMask); Win32.DeleteObject(info.hbmColor); }
                Check(Win32.GetForegroundWindow() != surface.Handle &&
                    (foreground == IntPtr.Zero || Win32.GetForegroundWindow() == foreground),
                    "edge cursor never takes foreground focus");
                surface.HideFeedback();
            }
            Win32.SetCursorPos(area.Left + 80, area.Bottom - 2); Application.DoEvents();
            surface.ShowFeedback(area, new Point(0, 1)); Application.DoEvents();
            var inputs = new[] { new NativeInput { Type = 0, Mouse = new MouseInput { Flags = 0x0002 } },
                new NativeInput { Type = 0, Mouse = new MouseInput { Flags = 0x0004 } } };
            Check(SendInput(2, inputs, Marshal.SizeOf<NativeInput>()) == 2, "send a click only into the isolated fixture");
            long until = Environment.TickCount64 + 2000;
            while (clicks == 0 && Environment.TickCount64 < until) { Application.DoEvents(); Thread.Sleep(10); }
            Check(clicks == 1 && interrupted == 1 && !surface.Visible,
                "original click passes through edge feedback exactly once and interrupts scrolling");
            Check(Win32.GetForegroundWindow() != surface.Handle &&
                (foreground == IntPtr.Zero || Win32.GetForegroundWindow() == foreground), "cursor feedback and fixture click keep the original focus");
        }
        finally { surface.HideFeedback(); Win32.SetCursorPos(original.X, original.Y); }
    }

    private sealed class CursorProbeForm : Form
    {
        protected override bool ShowWithoutActivation => true;
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0021) { message.Result = (IntPtr)3; return; }
            base.WndProc(ref message);
        }
    }
}
