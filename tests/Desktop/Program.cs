using System.Drawing;
using PaneSpace.Platform.Windows;
using PaneSpace.Core.Viewport;
using PaneSpace.Rendering;
using PaneSpace.Application;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;
using PaneSpace.Core.Settings;
using PaneSpace.Core.Sessions;
using PaneSpace.Persistence;
using PaneSpace.UI;

internal static partial class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Win32.SetProcessDpiAwarenessContext(Win32.DPI_PER_MONITOR_V2);
        if (args.Length == 3 && args[0] == "--restore-desktop-icons")
        {
            DesktopIconLease.RecoverAfterExit(int.Parse(args[1]), long.Parse(args[2])); return;
        }
        if (args.Contains("--desktop-lease-owner"))
        {
            _ = new DesktopIconLease();
            Environment.Exit(0); // Deliberately bypass Dispose to exercise companion recovery.
        }
        if (args.Contains("--desktop-lease-smoke")) { CheckDesktopLease(); return; }
        if (args.Contains("--read-desktop") || args.Contains("--desktop-visibility-smoke"))
        {
            using var shell = new DesktopShell();
            var items = shell.ReadItems();
            if (args.Contains("--desktop-visibility-smoke"))
            {
                bool visible = Win32.IsWindowVisible(shell.Window);
                try
                {
                    Win32.ShowWindow(shell.Window, Win32.SW_HIDE);
                    Check(!Win32.IsWindowVisible(shell.Window), "temporarily hide the original Explorer desktop view");
                    var hiddenItems = shell.ReadItems();
                    Console.WriteLine($"Visibility snapshot: before={items.Count}, hidden={hiddenItems.Count}, changed=" +
                        hiddenItems.Count(item => !items.Contains(item)));
                    Check(hiddenItems.OrderBy(item => item.Path).SequenceEqual(items.OrderBy(item => item.Path)),
                        "temporarily hiding icons preserves their identities and original positions");
                }
                finally { Win32.ShowWindow(shell.Window, visible ? 4 : Win32.SW_HIDE); }
                Check(Win32.IsWindowVisible(shell.Window) == visible, "restore Explorer desktop icon visibility after the smoke check");
            }
            Check(shell.Window != IntPtr.Zero, "connect to the actual Explorer desktop view");
            Console.WriteLine($"Desktop contains {items.Count} items; hidden={shell.IconsHidden}.");
            if (items.Count > 0)
            {
                using var image = DesktopShell.LoadIcon(items[0].Path);
                Check(image.Width > 0 && image.Height > 0, "load a desktop item's Shell icon");
            }
            return;
        }
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
            CheckScreenTiling();
            CheckDesktopIcons(args.Contains("--icons-screenshot"));
            CheckPersistence();
            CheckSettings(args.Contains("--settings-screenshot"));
            CheckEdgeCursor();
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
        Set("_mapIconHits", new List<(Rectangle Rect, string Path)>());
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
            Set("_settings", new AppSettings(InfiniteCanvas: true));
            Call("PanTo", -50000f, -75000f);
            Check((float)Get("_panX")! == -50000 && (float)Get("_panY")! == -75000,
                "native infinite mode moves the camera beyond nine screens");
            var logical = (Dictionary<IntPtr, (int X, int Y)>)Get("_logical")!;
            var beforeLogical = logical[source.Handle];
            Call("SetCanvasMode", true);
            Check(logical[source.Handle] == beforeLogical, "re-entering canvas mode preserves parked windows' world coordinates");
            Call("LayerWndProc", layer, (uint)Win32.WM_MOUSEWHEEL, (IntPtr)(-120 << 16), mouse);
            Call("SetCanvasMode", false);
            Check(Math.Abs((float)Get("_panX")!) > 40000, "returning to 100% does not clamp an infinite camera");
            Call("CentreWindow", source.Handle);
            Check(Bounds(source.Handle).IntersectsWith(new Rectangle(0, 0, 640, 480)),
                "focusing a distant parked window brings its native window back into view");
            Set("_settings", new AppSettings());
            Call("PanTo", 100000f, -100000f);
            Check((float)Get("_panX")! == 640 && (float)Get("_panY")! == -480,
                "disabling infinite mode restores the finite camera bounds");
            Call("PanTo", 0f, 0f);
            var edgeArea = new Rectangle(0, 0, 640, 480);
            var edgePoint = new Point(639, 200);
            Call("ProcessEdgePan", edgePoint, edgeArea, 1000L, false);
            Call("ProcessEdgePan", edgePoint, edgeArea, 1300L, false);
            Check((float)Get("_panX")! == 0, "edge panning is disabled by default");
            Set("_settings", new AppSettings(EdgePanning: true));
            var beforeEdge = Bounds(source.Handle);
            Call("ProcessEdgePan", edgePoint, edgeArea, 2000L, false);
            Call("ProcessEdgePan", edgePoint, edgeArea, 2300L, false);
            Check((float)Get("_panX")! == -30 && Bounds(source.Handle).X == beforeEdge.X - 30,
                "edge panning moves native windows without Ctrl or entering canvas mode");
            Check((Point)Get("_edgeCursorDirection")! == new Point(1, 0), "feedback points in the camera's direction");
            Call("ProcessEdgePan", edgePoint, edgeArea, 2316L, true);
            Check((float)Get("_panX")! == -30, "interacting with windows, settings or taskbar pauses edge movement");
            Call("ProcessEdgePan", edgePoint, edgeArea, 2332L, false);
            Check((float)Get("_panX")! == -30, "resuming after an interaction requires a fresh dwell delay");
            Call("PanTo", -640f, 0f);
            Call("ProcessEdgePan", edgePoint, edgeArea, 2632L, false);
            Check((float)Get("_panX")! == -640, "edge movement respects finite canvas boundaries");
            Set("_settings", new AppSettings(InfiniteCanvas: true, EdgePanning: true));
            Call("ProcessEdgePan", edgePoint, edgeArea, 2648L, false);
            Check((float)Get("_panX")! < -640, "infinite mode allows edge movement beyond nine screens");
            Call("PanTo", 0f, 0f); Call("SetCanvasMode", true);
            Call("ZoomAt", new Point(200, 200), -120);
            var physical = Bounds(source.Handle);
            float edgePan = (float)Get("_panX")!;
            Call("ProcessEdgePan", new Point(320, 0), edgeArea, 3000L, false);
            Call("ProcessEdgePan", new Point(320, 0), edgeArea, 3300L, false);
            Check(Bounds(source.Handle) == physical && (float)Get("_panX")! == edgePan && (float)Get("_panY")! > 0,
                "scaled edge movement pans the preview while native windows keep their bounds");
            Call("SetCanvasMode", false); Call("PanTo", 0f, 0f);
            var worldBeforeEdges = logical[source.Handle];
            Call("ProcessEdgePan", edgePoint, edgeArea, 4000L, false);
            Call("ProcessEdgePan", edgePoint, edgeArea, 4300L, false);
            Call("ProcessEdgePan", edgePoint, edgeArea, 4316L, false);
            Call("ProcessEdgePan", edgePoint, edgeArea, 4332L, true);
            Call("ProcessEdgePan", edgePoint, edgeArea, 4348L, false);
            Call("ProcessEdgePan", edgePoint, edgeArea, 4648L, false);
            Check(logical[source.Handle] == worldBeforeEdges, "repeated fractional edge-scroll sessions preserve world positions without rounding drift");
            foreach (var pointer in new[] { new Point(320, 479), new Point(0, 479), new Point(639, 479) })
            {
                Call("PanTo", 0f, 0f); Call("ResetEdgePan");
                Set("_settings", new AppSettings(InfiniteCanvas: true, EdgePanning: true, EdgePanSpeed: 1200));
                var previous = Bounds(source.Handle);
                Call("ProcessEdgePan", pointer, edgeArea, 5000L, false);
                Call("ProcessEdgePan", pointer, edgeArea, 5300L, false);
                float expectedBottomPan = pointer.X == 320 ? -60 : -60 / MathF.Sqrt(2);
                Check(Math.Abs((float)Get("_panY")! - expectedBottomPan) < .002f && Bounds(source.Handle).Y < previous.Y,
                    "bottom and both bottom corners pan actual native windows at the configured speed");
                Check(((Point)Get("_edgeCursorDirection")!).Y == 1, "bottom feedback points down");
                Call("ProcessEdgePan", pointer, edgeArea, 5316L, true);
                Check((Point)Get("_edgeCursorDirection")! == Point.Empty, "paused edge movement clears arrow feedback");
            }
            Check(Call("ApplySettings", new AppSettings(EdgePanSpeed: 0)) is string,
                "invalid speed is rejected before persisting settings or changing windows");
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

    private static void CheckDesktopIcons(bool screenshot)
    {
        var positions = new Dictionary<string, DesktopIconState>(StringComparer.OrdinalIgnoreCase)
        { ["fixture:first"] = new("fixture:first", 6040, 12040) };
        using var icons = new DesktopIconCanvas(positions);
        var camera = new CanvasViewport(640, 480, -6000, -12000, Infinite: true);
        var items = new[] { new ShellDesktopItem("fixture:first", "Saved shortcut", new Point(1, 1)),
            new ShellDesktopItem("fixture:second", "New shortcut", new Point(200, 40)) };
        Bitmap Load(string _) { var image = new Bitmap(32, 32); using var g = Graphics.FromImage(image); g.Clear(Color.Orange); return image; }
        icons.Refresh(items, camera, Load);
        Check(positions["fixture:first"].X == 6040 && positions["fixture:second"].X == 6200,
            "icons restore saved positions and import new icons into the current camera");
        Check(icons.Hit(new Point(60, 60), camera) == "fixture:first", "desktop hit testing follows the world camera");
        var zoomed = camera.ZoomAt(new PointF(60, 60), .5f);
        Check(icons.BeginDrag(new Point(60, 60), zoomed), "begin dragging an isolated canvas icon");
        icons.DragTo(new Point(160, 110), zoomed); icons.EndDrag();
        Check(positions["fixture:first"].X == 6240 && positions["fixture:first"].Y == 12140,
            "scaled icon dragging updates persistent world coordinates without a nine-screen limit");
        string? opened = null; icons.OpenRequested += path => opened = path;
        icons.OpenAt(new Point(160, 110), zoomed);
        Check(opened == "fixture:first", "opening an icon uses its stored Shell identity");
        using (var surface = new DesktopIconSurface(640, 480, icons, () => zoomed))
        {
            surface.Render();
            Check(surface.Frame.GetPixel(0, 0).A == 0 && surface.Frame.GetPixel(165, 115).A > 0,
                "desktop alpha surface preserves clickable icon regions and transparent background");
            if (screenshot)
            {
                using var backdrop = new Form { FormBorderStyle = FormBorderStyle.None, BackColor = Color.DarkSlateBlue,
                    Bounds = new Rectangle(0, 0, 640, 480), StartPosition = FormStartPosition.Manual, TopMost = true };
                backdrop.Show(); surface.Show();
                Win32.SetWindowPos(surface.Handle, Win32.HWND_TOPMOST, 0, 0, 640, 480, Win32.SWP_NOACTIVATE);
                surface.Render();
                var until = Environment.TickCount64 + 300;
                while (Environment.TickCount64 < until) { Application.DoEvents(); Thread.Sleep(10); }
                using var bitmap = new Bitmap(640, 480);
                using (var g = Graphics.FromImage(bitmap)) g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                Check(bitmap.GetPixel(0, 0).ToArgb() == Color.DarkSlateBlue.ToArgb() && bitmap.GetPixel(170, 115).R > 200,
                    "desktop icon pixels display correctly over a transparent native surface");
                string path = Path.Combine(Path.GetTempPath(), "PaneSpace-desktop-icons.png"); bitmap.Save(path);
                Console.WriteLine("Desktop icons screenshot: " + path);
            }
        }
        icons.Refresh(items, camera, Load);
        Check(positions["fixture:first"].X == 6240, "refreshing Explorer metadata retains the user's canvas icon position");
        icons.Refresh(items.Take(1).ToArray(), camera, Load);
        Check(icons.Count == 1 && icons.Hit(new Point(210, 60), camera) == null,
            "removed desktop items disappear from rendering and input");
    }
    private static void CheckPersistence()
    {
        string root = Path.Combine(Path.GetTempPath(), "PaneSpace-persistence-test-" + Guid.NewGuid());
        string settingsPath = Path.Combine(root, "settings.json"), statePath = Path.Combine(root, "state.json");
        try
        {
            var settings = new AppSettings(true, true, true, TileTop: 12, TileRight: 34, TileBottom: 96, TileLeft: 56, EdgePanSpeed: 1250);
            var state = new SessionState { PanX = -90000, DesktopIcons = new() { new("fixture:shortcut", 90500, 70000) } };
            Check(JsonStore.Save(settingsPath, settings) && JsonStore.Load<AppSettings>(settingsPath) == settings,
                "settings survive atomic disk save and reload");
            Check(JsonStore.Save(statePath, state) && JsonStore.Load<SessionState>(statePath)!.DesktopIcons.SequenceEqual(state.DesktopIcons),
                "desktop icon coordinates survive disk save and reload");
            Check(!File.Exists(settingsPath + ".tmp") && !File.Exists(statePath + ".tmp"), "successful atomic saves leave no temporary files");
            File.WriteAllText(settingsPath, "broken json");
            Check(JsonStore.Load<AppSettings>(settingsPath) == null, "corrupt settings allow safe defaults");
        }
        finally { File.Delete(settingsPath); File.Delete(statePath); if (Directory.Exists(root)) Directory.Delete(root); }
    }
    private static void CheckSettings(bool screenshot)
    {
        AppSettings? saved = null;
        using var form = new SettingsForm(new AppSettings(), settings => { saved = settings; return null; }, new Size(640, 480));
        form.Show(); Application.DoEvents();
        Check(!form.InfiniteCanvas.Checked && !form.DesktopIcons.Checked && !form.EdgePanning.Checked,
            "settings preserve all default disabled options");
        Check(form.TileTop.Value == 28 && form.TileRight.Value == 28 && form.TileBottom.Value == 80 && form.TileLeft.Value == 28,
            "settings display the four original default margins");
        Check(form.EdgePanSpeed.Value == 600 && !form.EdgePanSpeed.Enabled, "speed defaults to 600 and is disabled with edge panning");
        form.TileLeft.Value = 400; form.TileRight.Value = 240;
        form.SaveButton.PerformClick();
        Check(saved == null && form.Visible, "settings reject margins that consume all screen width before applying anything");
        form.TileLeft.Value = 56; form.TileRight.Value = 34;
        form.TileTop.Value = 240; form.TileBottom.Value = 240;
        form.SaveButton.PerformClick();
        Check(saved == null && form.Visible, "settings reject margins that consume all screen height");
        form.TileTop.Value = 12; form.TileBottom.Value = 96;
        form.InfiniteCanvas.Checked = true;
        Check(!form.DesktopIcons.Checked && !form.EdgePanning.Checked, "all three settings are independent choices");
        form.DesktopIcons.Checked = true;
        form.EdgePanning.Checked = true;
        Check(form.EdgePanSpeed.Enabled, "enabling edge panning enables its speed control");
        form.EdgePanSpeed.Value = 1250;
        if (screenshot)
        {
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            string path = Path.Combine(Path.GetTempPath(), "PaneSpace-settings.png"); bitmap.Save(path);
            Console.WriteLine("Settings screenshot: " + path);
        }
        form.SaveButton.PerformClick();
        Check(saved == new AppSettings(true, true, true, 12, 34, 96, 56, 1250) && !form.Visible,
            "saving settings applies the choices, margins and scrolling speed");
        using var reopened = new SettingsForm(saved!, _ => null, new Size(640, 480));
        Check(reopened.TileTop.Value == 12 && reopened.TileRight.Value == 34 && reopened.TileBottom.Value == 96 && reopened.TileLeft.Value == 56,
            "reopening settings restores all four custom values");
        Check(reopened.EdgePanSpeed.Value == 1250, "reopening settings restores the saved speed");
    }
    private static void CheckDesktopLease()
    {
        string ticket = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PaneSpace", "desktop-recovery.json");
        if (File.Exists(ticket)) { Console.WriteLine("SKIP live desktop recovery check: an existing lease is present."); return; }
        using var shell = new DesktopShell();
        bool visible = Win32.IsWindowVisible(shell.Window), hidden = shell.IconsHidden;
        var before = shell.ReadItems().OrderBy(item => item.Path).ToArray();
        try
        {
            using (var lease = new DesktopIconLease())
            {
                Check(!Win32.IsWindowVisible(shell.Window), "enabling the icon lease hides the native desktop view");
                Check(lease.Refresh().OrderBy(item => item.Path).SequenceEqual(before), "leased desktop retains the live icon catalog");
            }
            Check(!File.Exists(ticket) && Win32.IsWindowVisible(shell.Window) == visible && shell.IconsHidden == hidden,
                "disabling desktop icons restores original visibility and removes the recovery ticket");
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!)
                { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--desktop-lease-owner");
            using var owner = System.Diagnostics.Process.Start(start)!;
            Check(owner.WaitForExit(10000) && owner.ExitCode == 0, "isolated lease owner exits without normal cleanup");
            var until = Environment.TickCount64 + 10000;
            while (File.Exists(ticket) && Environment.TickCount64 < until) { Application.DoEvents(); Thread.Sleep(30); }
            Check(!File.Exists(ticket) && Win32.IsWindowVisible(shell.Window) == visible && shell.IconsHidden == hidden,
                "companion restores the original desktop after an abrupt owner exit");
            Check(shell.ReadItems().OrderBy(item => item.Path).SequenceEqual(before), "recovery preserves the original desktop icon layout");
        }
        finally
        {
            shell.SetIconsHidden(hidden); Win32.ShowWindow(shell.Window, visible ? 4 : Win32.SW_HIDE);
            DesktopIconLease.RecoverStaleDesktop();
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
}
