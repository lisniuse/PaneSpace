using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using PaneSpace.Core.Layout;
using PaneSpace.Core.Viewport;
using PaneSpace.Core.Sessions;
using PaneSpace.Persistence;
using PaneSpace.Platform.Windows;
using PaneSpace.Rendering;
using WinForms = System.Windows.Forms;

namespace PaneSpace.Application;

public sealed partial class CanvasController
{
    // ---- geometry --------------------------------------------------------------------

    private int MapH => (int)(MAP_W / (3f * _w) * 3 * _h) + 2 + TITLE_H;
    private Rectangle MapRect => new(_w - MAP_W - MAP_MARGIN, _h - MapH - MAP_MARGIN, MAP_W, MapH);
    private int BarItemWidth(int i) => BAR_ITEM_PAD + Buttons[i].Length * 15;
    private int BarTotalWidth
    {
        get { int t = 0; for (int i = 0; i < Buttons.Length; i++) t += BarItemWidth(i); return t + (Buttons.Length - 1) * BAR_PAD; }
    }
    private Rectangle BarRect(int i)
    {
        int x = (_w - BarTotalWidth) / 2;
        int y = _h - BAR_H - 24;
        // On smaller screens keep the new button and reset clear of the minimap.
        if (x + BarTotalWidth > MapRect.Left) y = MapRect.Top - BAR_H - BAR_PAD;
        for (int k = 0; k < i; k++) x += BarItemWidth(k) + BAR_PAD;
        return new Rectangle(x, y, BarItemWidth(i), BAR_H);
    }

    // ---- composition + UpdateLayeredWindow ---------------------------------------------

    private void CreateLayer()
    {
        var instance = Win32.GetModuleHandle(null);
        _layerProc = LayerWndProc;
        var wc = new Win32.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
            style = 8, // CS_DBLCLKS, for desktop icon activation
            lpfnWndProc = _layerProc,
            hInstance = instance,
            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32649), // hand (fallback)
            lpszClassName = "PaneSpaceLayer",
        };
        Win32.RegisterClassEx(ref wc);
        long ex = Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW;
        _layer = Win32.CreateWindowEx(ex, "PaneSpaceLayer", "", Win32.WS_POPUP,
            0, 0, _w, _h, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        CreateDrawingSurface();
        _curHand = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32649);
        _curArrow = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512);
        ComposeFull();                               // fully transparent start
        // window stays visible permanently; invisibility = all-zero alpha frame
        Win32.ShowWindow(_layer, 5 /*SW_SHOW*/);
        Win32.SetWindowPos(_layer, Win32.HWND_TOPMOST, 0, 0, _w, _h, Win32.SWP_NOACTIVATE);
    }

    private void CreateDrawingSurface()
    {
        _screenDc = Win32.GetDC(IntPtr.Zero);
        _memDc = Win32.CreateCompatibleDC(_screenDc);
        var info = new Win32.BITMAPINFO
        {
            bmiHeader = new Win32.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                biWidth = _w,
                biHeight = -_h,                      // top-down, matching mouse coordinates
                biPlanes = 1,
                biBitCount = 32,
            },
        };
        _hbm = Win32.CreateDIBSection(_screenDc, ref info, 0, out var pixels, IntPtr.Zero, 0);
        if (_hbm == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        _oldBmp = Win32.SelectObject(_memDc, _hbm);
        // FromHdc can use opaque GDI fills that zero alpha, turning a hovered map
        // block into a click-through hole. FromImage preserves alpha even at 255.
        _frame = new Bitmap(_w, _h, checked(_w * 4), PixelFormat.Format32bppPArgb, pixels);
    }

    private void ComposeFull()
    {
        using (var g = Graphics.FromImage(_frame!))
        {
            g.ResetClip();
            g.Clear(Color.Transparent);              // alpha 0 = click-through
            if (_canvasMode)
            {
                DrawGrid(g);
                DrawButtons(g);
                DrawMap(g);
            }
        }
        PushRect(new Rectangle(0, 0, _w, _h));
    }

    /// <summary>Repaint only the minimap during dragging, then present the full frame.</summary>
    private void ComposeMapOnly()
    {
        if (PreviewActive) { ComposeFull(); return; }
        var mr = MapRect;
        using (var g = Graphics.FromImage(_frame!))
        {
            g.SetClip(mr);
            g.Clear(Color.Transparent);
            DrawMap(g);
            g.ResetClip();
        }
        PushRect(mr);
    }

    private void PushRect(Rectangle r)
    {
        // ULW semantics: psize = NEW WINDOW SIZE, pptDst = NEW WINDOW POS.
        // There is no partial-region push; always re-present the full frame at (0,0).
        // (The remaining optimisations — persistent DC, no per-frame HBITMAP copy,
        //  60Hz drag coalescing, cheap map-only redraw — are what actually save time.)
        _ = r;
        var size = new Win32.SIZE { cx = _w, cy = _h };
        var dstPos = new Win32.POINT { X = 0, Y = 0 };
        var srcPos = new Win32.POINT { X = 0, Y = 0 };
        var blend = new Win32.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
        bool ok = Win32.UpdateLayeredWindow(_layer, _screenDc, ref dstPos, ref size,
            _memDc, ref srcPos, 0, ref blend, 2 /*ULW_PANE*/);
        if (!ok && _ulwErrCount++ < 3)
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "camcanvas-map.log"),
                $"ulw FAIL err={Marshal.GetLastWin32Error()}{Environment.NewLine}");
    }

    private void DrawButtons(Graphics g)
    {
        using var bg = new SolidBrush(Color.FromArgb(SOLID_A, 46, 92, 150));
        using var fg = new SolidBrush(Color.FromArgb(245, 255, 255, 255));
        using var bp = new Pen(Color.FromArgb(SOLID_A, 150, 180, 210), 1.2f);
        using var f = new Font("Microsoft YaHei UI", 10.5f);
        for (int i = 0; i < Buttons.Length; i++)
        {
            var r = BarRect(i);
            g.FillRectangle(bg, r);
            g.DrawRectangle(bp, r);
            var ts = g.MeasureString(Buttons[i], f);
            g.DrawString(Buttons[i], f, fg, r.X + (r.Width - ts.Width) / 2, r.Y + (r.Height - ts.Height) / 2);
        }
    }

    private void DrawGrid(Graphics g)
    {
        g.Clear(Color.FromArgb(GRID_A, 20, 96, 180));
        using var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 1);
        float step = 48 * _zoom;
        var origin = Viewport.ToScreen(PointF.Empty);
        float startX = (origin.X % step + step) % step, startY = (origin.Y % step + step) % step;
        for (float x = startX; x < _w; x += step) g.DrawLine(pen, x, 0, x, _h);
        for (float y = startY; y < _h; y += step) g.DrawLine(pen, 0, y, _w, y);
        if (PreviewActive)
        {
            using var f = new Font("Microsoft YaHei UI", 10);
            g.DrawString($"{_zoom:P0} · 滚轮缩放 · 拖动平移 · 点击窗口打开 · 松开 Ctrl 回到 100%",
                f, Brushes.White, 18, 18);
        }
    }

    private void DrawMap(Graphics g)
    {
        var mr = MapRect;
        var windows = new List<(IntPtr Hwnd, RectangleF World)>();
        foreach (var (hwnd, logical) in _logical)
        {
            if (!Win32.IsWindow(hwnd) || !Win32.GetWindowRect(hwnd, out var r)) continue;
            windows.Add((hwnd, new RectangleF(logical.X, logical.Y,
                Math.Max(r.Right - r.Left, 40), Math.Max(r.Bottom - r.Top, 20))));
        }
        var icons = _desktopIcons?.Bounds.ToArray() ?? Array.Empty<(string Path, RectangleF Bounds)>();
        var overview = CanvasOverview.Bounds(Viewport, windows.Select(window => window.World).Concat(icons.Select(icon => icon.Bounds)));
        float s = MAP_W / overview.Width;
        Rectangle MapBounds(RectangleF world) => new(
            mr.X + (int)((world.X - overview.X) * s), mr.Y + TITLE_H + (int)((world.Y - overview.Y) * s),
            Math.Max((int)(world.Width * s), 3), Math.Max((int)(world.Height * s), 2));
        _mapHits.Clear();
        _mapIconHits.Clear();
        using var bg = new SolidBrush(Color.FromArgb(SOLID_A, 16, 20, 26));
        g.FillRectangle(bg, mr);
        using var winBrush = new SolidBrush(Color.FromArgb(SOLID_A, 96, 158, 218));
        using var hlBrush = new SolidBrush(Color.FromArgb(255, 250, 205, 120));
        using var vpPen = new Pen(Color.White, 2f);
        using var border = new Pen(Color.FromArgb(230, 190, 200, 215), 1f);
        using var iconBrush = new SolidBrush(Color.FromArgb(SOLID_A, 175, 140, 225));
        var mapClip = g.Save();
        g.SetClip(new Rectangle(mr.X, mr.Y + TITLE_H, mr.Width, mr.Height - TITLE_H));
        foreach (var (path, bounds) in icons)
        {
            var rc = MapBounds(bounds);
            g.FillRectangle(iconBrush, rc); _mapIconHits.Add((rc, path));
        }

        foreach (var (hwnd, world) in windows)
        {
            var rc = MapBounds(world);
            g.FillRectangle(hwnd == _hoverHwnd ? hlBrush : winBrush, rc);
            _mapHits.Add((rc, hwnd));
        }
        var viewport = Viewport.VisibleWorld;
        g.DrawRectangle(vpPen,
            mr.X + (viewport.X - overview.X) * s, mr.Y + TITLE_H + (viewport.Y - overview.Y) * s,
            viewport.Width * s, viewport.Height * s);
        g.Restore(mapClip);
        g.DrawRectangle(border, mr.X, mr.Y, mr.Width - 1, mr.Height - 1);

        string ttl = $"{(Settings.InfiniteCanvas ? "∞ · " : "")}{_zoom:P0} · 滚轮缩放";
        if (_hoverHwnd != IntPtr.Zero && Win32.IsWindow(_hoverHwnd))
        {
            _title.Clear();
            Win32.GetWindowTextW(_hoverHwnd, _title, 200);
            ttl = _title.ToString();
            if (ttl.Length == 0) ttl = "(无标题)";
            if (ttl.Length > 24) ttl = ttl[..24] + "…";
        }
        using var tb = new SolidBrush(Color.FromArgb(240, 235, 240, 248));
        using var tf = new Font("Microsoft YaHei UI", 8.5f);
        using var titleBg = new SolidBrush(Color.FromArgb(235, 30, 40, 54));
        g.FillRectangle(titleBg, mr.X, mr.Y, mr.Width, TITLE_H);
        g.DrawString(ttl, tf, tb, mr.X + 6, mr.Y + 5);
    }
}
