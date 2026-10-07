using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using PaneSpace.Application;
using PaneSpace.Core.Settings;
using PaneSpace.Platform.Windows;
using PaneSpace.Rendering;

internal static partial class Program
{
    private static void CheckScreenTiling()
    {
        var fixtures = new List<IntPtr>();
        var controller = (CanvasController)RuntimeHelpers.GetUninitializedObject(typeof(CanvasController));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(CanvasController).GetField(name, flags)!.SetValue(controller, value);
        object? Get(string name) => typeof(CanvasController).GetField(name, flags)!.GetValue(controller);
        object? Call(string name, params object[] arguments) => typeof(CanvasController).GetMethod(name, flags)!.Invoke(controller, arguments);
        var logical = new Dictionary<IntPtr, (int X, int Y)>();
        Set("_w", 640); Set("_h", 480); Set("_zoom", 1f);
        Set("_logical", logical); Set("_dead", new List<IntPtr>());
        Set("_mapHits", new List<(Rectangle Rect, IntPtr Hwnd)>());
        Set("_mapIconHits", new List<(Rectangle Rect, string Path)>());
        Set("_title", new StringBuilder(256)); Set("_cls", new StringBuilder(256));
        try
        {
            for (int i = 0; i < 10; i++) AddFixture(i == 1 ? unchecked((int)0x90cf0000) : unchecked((int)0x90000000));
            Win32.ShowWindow(fixtures[1], 3);
            var minimized = AddFixture(unchecked((int)0x90cf0000));
            Win32.ShowWindow(minimized, 6);
            var hidden = AddFixture(unchecked((int)0x90000000));
            Win32.ShowWindow(hidden, Win32.SW_HIDE);
            var owned = AddFixture(unchecked((int)0x90000000), fixtures[0]);
            var excluded = new[] { minimized, hidden, owned }.ToDictionary(hwnd => hwnd, Bounds);
            Call("CreateLayer"); Call("SetCanvasMode", true);
            Call("PanTo", 65f, -40f);
            Call("ZoomAt", new Point(200, 200), -120);
            var before = fixtures.ToDictionary(hwnd => hwnd, Bounds);
            var beforeLogical = logical.ToArray();
            var beforePan = ((float)Get("_panX")!, (float)Get("_panY")!);
            float beforeZoom = (float)Get("_zoom")!;
            ClickButton(1);
            Check(fixtures.All(hwnd => Bounds(hwnd) == before[hwnd]) && logical.SequenceEqual(beforeLogical) &&
                ((float)Get("_panX")!, (float)Get("_panY")!) == beforePan && (float)Get("_zoom")! == beforeZoom &&
                Win32.IsZoomed(fixtures[1]), "finite screen-tile overflow preserves sizes, positions, camera, preview and maximization");

            logical.Remove(fixtures[9]);
            var foreground = Win32.GetForegroundWindow();
            ClickButton(1);
            Check((float)Get("_zoom")! == 1 && !((WindowPreview)Get("_preview")!).Visible,
                "screen-tile button exits zoom preview before resizing native windows");
            Check(Bounds(fixtures[0]) == new Rectangle(28, 28, 584, 372), "first borderless tile leaves 28px above and 80px below for the taskbar");
            Check(Bounds(fixtures[2]) == new Rectangle(1308, 28, 584, 372) &&
                Bounds(fixtures[3]) == new Rectangle(28, 508, 584, 372), "screen tiles use adjacent 640x480 cells without overlap");
            Check(!Win32.IsZoomed(fixtures[1]), "maximized tiles restore to a normal resizable window");
            Check(Win32.GetForegroundWindow() == foreground, "screen tiling does not steal the foreground window");
            Check(excluded.All(pair => Bounds(pair.Key).Size == pair.Value.Size &&
                logical[pair.Key] == beforeLogical.First(item => item.Key == pair.Key).Value) && Win32.IsIconic(minimized),
                "hidden, minimized and owned windows retain their sizes, world positions and state");
            if (Dwm.DwmGetWindowAttribute(fixtures[1], 9, out var frame, 16) == 0)
                Check(new Rectangle(frame.Left, frame.Top, frame.Right - frame.Left, frame.Bottom - frame.Top) ==
                    new Rectangle(668, 28, 584, 372), "restored bordered window keeps visible 28px top and 80px bottom margins");
            for (int i = 0; i < 4; i++)
            {
                var button = (Rectangle)Call("BarRect", i)!;
                var map = (Rectangle)typeof(CanvasController).GetProperty("MapRect", flags)!.GetValue(controller)!;
                Check(new Rectangle(0, 0, 640, 480).Contains(button) && !button.IntersectsWith(map),
                    "all toolbar buttons remain visible and clear of the minimap on a small screen");
            }
            Set("_settings", new AppSettings(InfiniteCanvas: true));
            logical[fixtures[9]] = (0, 0);
            ClickButton(1);
            Check(Bounds(fixtures[9]) == new Rectangle(28, 1468, 584, 372),
                "infinite screen tiling continues into a fourth row");
            Call("CentreWindow", fixtures[9]);
            Check(Bounds(fixtures[9]) == new Rectangle(28, 28, 584, 372),
                "following a tiled window preserves the taskbar reservation rather than centering on the full screen");

            using var constrained = new Form { StartPosition = FormStartPosition.Manual, ShowInTaskbar = true,
                FormBorderStyle = FormBorderStyle.None, Bounds = new Rectangle(30, 30, 200, 180),
                MaximumSize = new Size(200, 180) };
            constrained.Show(); Application.DoEvents();
            logical.Clear(); logical[constrained.Handle] = (30, 30);
            ClickButton(1);
            Check(Bounds(constrained.Handle) == new Rectangle(220, 124, 200, 180),
                "application size constraints are honored and its accepted size is centered in the cell");
            Call("PanTo", -900f, -700f); Call("CentreWindow", constrained.Handle);
            Check(Bounds(constrained.Handle) == new Rectangle(220, 124, 200, 180),
                "following a constrained tile centers it in the area remaining above the reserved taskbar space");
            ClickButton(0);
            Check(Bounds(constrained.Handle) == new Rectangle(28, 28, 200, 180),
                "original masonry button still uses nine screens and preserves window size in infinite mode");
            ClickButton(2);
            Check((float)Get("_panX")! == 0 && (float)Get("_panY")! == 0 &&
                Bounds(constrained.Handle) == new Rectangle(220, 150, 200, 180),
                "gather button retains its action after the new toolbar button is inserted");
            Call("PanTo", 33f, 44f); ClickButton(3);
            Check((float)Get("_panX")! == 0 && (float)Get("_panY")! == 0,
                "reset button retains its action after the new toolbar button is inserted");
            ClickButton(1);
            Win32.SetWindowPos(constrained.Handle, IntPtr.Zero, 0, 0, 160, 140,
                0x0002 | Win32.SWP_NOZORDER_ | Win32.SWP_NOACTIVATE_);
            Call("CentreWindow", constrained.Handle);
            Check(Bounds(constrained.Handle) == new Rectangle(240, 170, 160, 140),
                "manually resized tiles return to ordinary window centering");
        }
        finally
        {
            // Bypass Dispose, which would overwrite the user's session with fixture data.
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
            foreach (var hwnd in fixtures) if (Win32.IsWindow(hwnd)) Win32.DestroyWindow(hwnd);
        }

        IntPtr AddFixture(int style, IntPtr owner = default)
        {
            var hwnd = Win32.CreateWindowEx(0, "STATIC", "PaneSpace screen tile fixture", style,
                -8000, -8000, 320, 220, owner, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Check(hwnd != IntPtr.Zero, "create isolated screen-tile fixture");
            fixtures.Add(hwnd); logical[hwnd] = (-8000, -8000); return hwnd;
        }
        void ClickButton(int index)
        {
            var rect = (Rectangle)Call("BarRect", index)!;
            var point = (IntPtr)(((rect.Y + rect.Height / 2) << 16) | (rect.X + rect.Width / 2));
            var layer = (IntPtr)Get("_layer")!;
            Call("LayerWndProc", layer, (uint)Win32.WM_LBUTTONDOWN, IntPtr.Zero, point);
            Call("LayerWndProc", layer, (uint)Win32.WM_LBUTTONUP, IntPtr.Zero, point);
        }
    }
}
