using System.Drawing;
using PaneSpace.Platform.Windows;
using PaneSpace.Core.Viewport;
using PaneSpace.Rendering;
using PaneSpace.Application;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
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
            CheckPreview(args.Contains("--preview-screenshot"));
            CheckZoomInput();
            Console.WriteLine("All native desktop checks passed; only test windows were moved.");
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

    private static void CheckPreview(bool screenshot)
    {
        using var source = new Form
        {
            FormBorderStyle = FormBorderStyle.None, BackColor = Color.DarkOrange,
            StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(30, 30, 360, 260),
            ShowInTaskbar = false,
        };
        source.Controls.Add(new Label { Text = "Live window preview", AutoSize = true,
            Location = new Point(16, 16), ForeColor = Color.White, Font = new Font("Segoe UI", 18) });
        source.Show();
        Application.DoEvents();
        var original = Bounds(source.Handle);
        using var preview = new WindowPreview(640, 480);
        var camera = new CanvasViewport(640, 480, 0, 0, .5f);
        var items = new[] { (source.Handle, new RectangleF(160, 100, 360, 260)) };
        preview.UpdateWindows(items, camera);
        preview.PresentBelow(preview.Handle);
        Check(preview.LiveCount == 1, "register live DWM preview for an isolated source");
        Check(preview.Hit(new Point(250, 180)) == source.Handle && preview.Hit(new Point(20, 20)) == IntPtr.Zero,
            "scaled preview hit testing matches the displayed destination");
        preview.UpdateWindows(items, camera.Drag(-600, 0));
        Check(preview.Hit(new Point(330, 180)) == IntPtr.Zero, "hit regions follow the scaled camera");
        preview.UpdateWindows(items, camera);
        Check(Bounds(source.Handle) == original, "preview zoom and pan do not move or resize the native source");
        if (screenshot)
        {
            var until = Environment.TickCount64 + 500;
            while (Environment.TickCount64 < until) { Application.DoEvents(); Thread.Sleep(10); }
            using var bitmap = new Bitmap(640, 480);
            using (var g = Graphics.FromImage(bitmap)) g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
            string path = Path.Combine(Path.GetTempPath(), "PaneSpace-zoom-preview.png");
            bitmap.Save(path);
            var pixel = bitmap.GetPixel(330, 240);
            Check(pixel.R > 200 && pixel.G > 70 && pixel.G < 180 && pixel.B < 50,
                "DWM displays the scaled source pixels on screen");
            Console.WriteLine("Preview screenshot: " + path);
        }
        preview.EndPreview();
        Check(preview.LiveCount == 0 && !preview.Visible && preview.Hit(new Point(250, 180)) == IntPtr.Zero,
            "ending preview releases thumbnail resources and clears input regions");
    }

    private static Rectangle Bounds(IntPtr hwnd)
    {
        if (!Win32.GetWindowRect(hwnd, out var rect)) throw new Exception("Cannot read window bounds.");
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static void CheckZoomInput()
    {
        using var source = new Form { Bounds = new Rectangle(60, 60, 320, 220), ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual };
        source.Show();
        Application.DoEvents();
        var original = Bounds(source.Handle);
        // Bypass startup enumeration and persistence: this controller owns only our fixture.
        var controller = (CanvasController)RuntimeHelpers.GetUninitializedObject(typeof(CanvasController));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(CanvasController).GetField(name, flags)!.SetValue(controller, value);
        object? Get(string name) => typeof(CanvasController).GetField(name, flags)!.GetValue(controller);
        object? Call(string name, params object[] arguments) => typeof(CanvasController).GetMethod(name, flags)!.Invoke(controller, arguments);
        Set("_w", 640); Set("_h", 480); Set("_zoom", 1f);
        Set("_logical", new Dictionary<IntPtr, (int X, int Y)> { [source.Handle] = (original.X, original.Y) });
        Set("_dead", new List<IntPtr>()); Set("_mapHits", new List<(Rectangle Rect, IntPtr Hwnd)>());
        Set("_title", new StringBuilder(256)); Set("_cls", new StringBuilder(256));
        try
        {
            Call("CreateLayer");
            Call("SetCanvasMode", true);
            var layer = (IntPtr)Get("_layer")!;
            var mouse = (IntPtr)((200 << 16) | 200);
            Call("LayerWndProc", layer, (uint)Win32.WM_MOUSEWHEEL, (IntPtr)(-120 << 16), mouse);
            Check((float)Get("_zoom")! < 1 && ((WindowPreview)Get("_preview")!).Visible,
                "wheel input enters a scaled live preview");
            var hook = (CanvasWheelHook)Get("_wheelHook")!;
            var callback = (Win32.HookProc)typeof(CanvasWheelHook).GetField("_callback", flags)!.GetValue(hook)!;
            var data = Marshal.AllocHGlobal(32);
            try
            {
                for (int i = 0; i < 32; i += 4) Marshal.WriteInt32(data, i, 0);
                Marshal.WriteInt32(data, 0, 200); Marshal.WriteInt32(data, 4, 200);
                Marshal.WriteInt32(data, 8, -120 << 16);
                float before = (float)Get("_zoom")!;
                Check(callback(0, (IntPtr)Win32.WM_MOUSEWHEEL, data) == (IntPtr)1,
                    "wheel hook consumes canvas scrolling without forwarding to the desktop");
                Application.DoEvents();
                Check((float)Get("_zoom")! < before, "wheel hook queues zoom through the canvas message loop");
            }
            finally { Marshal.FreeHGlobal(data); }
            Check(Bounds(source.Handle) == original, "wheel input preserves physical source bounds");
            Call("PanBy", 60f, 30f);
            Check(Bounds(source.Handle) == original, "dragging the scaled camera leaves native windows stationary");
            var frame = (Bitmap)Get("_frame")!;
            Check(frame.GetPixel(100, 100).A > 0, "scaled input surface retains alpha for mouse hit testing");
            Call("SetCanvasMode", false);
            Check((float)Get("_zoom")! == 1 && !((WindowPreview)Get("_preview")!).Visible,
                "leaving canvas mode returns to native scale and hides the preview");
            var expected = new Point(original.X + (int)Math.Round((float)Get("_panX")!),
                original.Y + (int)Math.Round((float)Get("_panY")!));
            Check(Bounds(source.Handle).Location == expected && Bounds(source.Handle).Size == original.Size,
                "native windows follow the final camera offset without resizing");
            Check(frame.GetPixel(100, 100).A == 0, "leaving canvas mode restores click-through alpha");
            Check(Get("_wheelHook") == null, "leaving canvas mode removes the wheel hook");
            Call("SetCanvasMode", true);
            Call("LayerWndProc", layer, (uint)Win32.WM_MOUSEWHEEL, (IntPtr)(120 << 16), mouse);
            Call("ResetPan");
            Check((float)Get("_zoom")! == 1 && (float)Get("_panX")! == 0 && (float)Get("_panY")! == 0,
                "reset clears both zoom and camera offset");
            Call("LayerWndProc", layer, (uint)Win32.WM_MOUSEWHEEL, (IntPtr)(-120 << 16), mouse);
            var camera = new CanvasViewport(640, 480, (float)Get("_panX")!, (float)Get("_panY")!, (float)Get("_zoom")!);
            var center = camera.ToScreen(new PointF(original.X + original.Width / 2f, original.Y + original.Height / 2f));
            var click = (IntPtr)(((int)center.Y << 16) | (int)center.X);
            Call("LayerWndProc", layer, (uint)Win32.WM_LBUTTONDOWN, IntPtr.Zero, click);
            Call("LayerWndProc", layer, (uint)Win32.WM_LBUTTONUP, IntPtr.Zero, click);
            Check((float)Get("_zoom")! == 1 && !(bool)Get("_canvasMode")! && Bounds(source.Handle).Size == original.Size,
                "clicking a scaled preview opens its native window without resizing");
        }
        finally
        {
            // Do not call normal Dispose: it saves the user's layout. Release test resources directly.
            (Get("_preview") as WindowPreview)?.Dispose();
            (Get("_wheelHook") as CanvasWheelHook)?.Dispose();
            if (Get("_layer") is IntPtr layer && layer != IntPtr.Zero) Win32.DestroyWindow(layer);
            (Get("_frame") as Bitmap)?.Dispose();
            var dc = (IntPtr)Get("_memDc")!;
            if (dc != IntPtr.Zero)
            {
                Win32.SelectObject(dc, (IntPtr)Get("_oldBmp")!);
                Win32.DeleteObject((IntPtr)Get("_hbm")!); Win32.DeleteDC(dc);
                Win32.ReleaseDC(IntPtr.Zero, (IntPtr)Get("_screenDc")!);
            }
        }
    }

    private static void Move(IntPtr hwnd, Point position) => Win32.SetWindowPos(hwnd, IntPtr.Zero,
        position.X, position.Y, 0, 0, Win32.SWP_NOSIZE_ | Win32.SWP_NOZORDER_ | Win32.SWP_NOACTIVATE_);

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
