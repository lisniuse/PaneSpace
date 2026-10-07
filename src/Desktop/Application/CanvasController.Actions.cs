using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using PaneSpace.Core.Layout;
using PaneSpace.Core.Sessions;
using PaneSpace.Persistence;
using PaneSpace.Platform.Windows;
using PaneSpace.Rendering;
using WinForms = System.Windows.Forms;

namespace PaneSpace.Application;

public sealed partial class CanvasController
{
    // ---- pan / actions ---------------------------------------------------------------

    private void ApplyPan()
    {
        if (PreviewActive)
        {
            var camera = Viewport.Clamp();
            _panX = camera.PanX; _panY = camera.PanY;
            RefreshPreview();
            ComposeFull();
            ScheduleSave();
            return;
        }
        IntPtr info = Win32.BeginDeferWindowPos(Math.Max(_logical.Count, 1));
        foreach (var (hwnd, logical) in _logical)
        {
            if (!Win32.IsWindow(hwnd)) { _dead.Add(hwnd); continue; }
            if (Win32.IsIconic(hwnd)) continue;
            double x = (double)logical.X + _panX, y = (double)logical.Y + _panY;
            if (Math.Abs(x) > 24000 || Math.Abs(y) > 24000)
            {
                // Keep distant windows in safe native coordinates, preserving their world position.
                (_parked ??= new()).Add(hwnd); x = Math.Min(_w + 8192, 24000); y = -8192;
            }
            else _parked?.Remove(hwnd);
            info = Win32.DeferWindowPos(info, hwnd, IntPtr.Zero,
                (int)Math.Round(x), (int)Math.Round(y), 0, 0,
                Win32.SWP_NOSIZE_ | Win32.SWP_NOZORDER_ | Win32.SWP_NOACTIVATE_ | Win32.SWP_NOOWNERZORDER);
        }
        Win32.EndDeferWindowPos(info);
        foreach (var d in _dead) { _logical.Remove(d); _parked?.Remove(d); }
        _dead.Clear();
        RenderDesktopIcons();
        if (_canvasMode) ComposeFull();
        ScheduleSave();
    }

    private void PanBy(float dx, float dy)
    {
        var camera = Viewport.Drag(dx, dy);
        _panX = camera.PanX; _panY = camera.PanY;
        ApplyPan();
    }

    private void PanTo(float panX, float panY)
    {
        var camera = (Viewport with { PanX = panX, PanY = panY }).Clamp();
        _panX = camera.PanX; _panY = camera.PanY;
        ApplyPan();
    }

    private void ResetPan()
    {
        ReturnToNative();
        _panX = _panY = 0;
        ApplyPan();
        if (_canvasMode) ComposeFull();
    }

    /// <summary>Shortest-column masonry across the 3x3 canvas, preserving window sizes.</summary>
    private void AutoArrange()
    {
        const int GAP = 16, M = 28;
        var items = new List<(IntPtr Hwnd, Size Size)>();
        foreach (var hwnd in _logical.Keys.ToArray())
        {
            if (!WindowFilter.IsManaged(hwnd, _selfPid) || Win32.IsIconic(hwnd)) continue;
            if (!Win32.GetWindowRect(hwnd, out var r)) continue;
            int width = r.Right - r.Left, height = r.Bottom - r.Top;
            if (width > 0 && height > 0) items.Add((hwnd, new Size(width, height)));
        }
        if (items.Count == 0) return;
        var bounds = new Rectangle(-_w + M, -_h + M, 3 * _w - 2 * M, 3 * _h - 2 * M);
        if (!MasonryLayout.TryArrange(items.Select(item => item.Size).ToArray(), bounds, GAP, out var positions))
        {
            _tray?.ShowBalloonTip(4500, "自动排列未完成",
                "本次排列无法在 3×3 画布中放下所有窗口，已保留原布局。请缩小部分窗口或最小化暂不需要的窗口。",
                WinForms.ToolTipIcon.Warning);
            return;
        }
        // Commit only a complete layout; never leave windows below the reachable canvas.
        for (int i = 0; i < items.Count; i++)
            _logical[items[i].Hwnd] = (positions[i].X, positions[i].Y);
        _panX = _w; _panY = _h;                     // look at canvas top-left
        ApplyPan();
    }

    private void TileToScreen()
    {
        var windows = _logical.Keys.Where(hwnd => WindowFilter.IsManaged(hwnd, _selfPid) &&
            !Win32.IsIconic(hwnd)).ToArray();
        if (windows.Length == 0) return;
        if (!ScreenTileLayout.TryArrange(windows.Length, new Size(_w, _h), Settings.InfiniteCanvas, out var tiles))
        {
            _tray?.ShowBalloonTip(4500, "整屏平铺未完成",
                "有限画布最多平铺 9 个窗口，已保留原布局。请开启无限画布，或最小化暂不需要的窗口。",
                WinForms.ToolTipIcon.Warning);
            return;
        }
        // Check capacity before changing either the camera, window sizes or window states.
        ReturnToNative();
        int constrained = 0, failed = 0;
        for (int i = 0; i < windows.Length; i++)
        {
            if (!WindowTiling.TryFit(windows[i], new Size(_w, _h), ScreenTileLayout.Margin,
                out var offset, out bool limited)) { failed++; continue; }
            _logical[windows[i]] = (tiles[i].X - ScreenTileLayout.Margin + offset.X,
                tiles[i].Y - ScreenTileLayout.Margin + offset.Y);
            if (limited) constrained++;
        }
        _panX = _w; _panY = _h; // Center the first screen cell in the viewport.
        ApplyPan();
        if (constrained > 0 || failed > 0)
            _tray?.ShowBalloonTip(4500, "整屏平铺",
                $"{constrained} 个窗口有尺寸限制，已按实际尺寸居中；{failed} 个窗口未能调整。",
                WinForms.ToolTipIcon.Info);
    }

    private void GatherToCentreScreen()
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        bool any = false;
        foreach (var (hwnd, l) in _logical)
        {
            if (!Win32.IsWindow(hwnd)) continue;
            Win32.GetWindowRect(hwnd, out var r);
            minX = Math.Min(minX, l.X); minY = Math.Min(minY, l.Y);
            maxX = Math.Max(maxX, l.X + r.Right - r.Left);
            maxY = Math.Max(maxY, l.Y + r.Bottom - r.Top);
            any = true;
        }
        if (!any) return;
        int dx = (_w - (maxX - minX)) / 2 - minX;
        int dy = (_h - (maxY - minY)) / 2 - minY;
        foreach (var k in _logical.Keys.ToArray())
        {
            var l = _logical[k];
            _logical[k] = (l.X + dx, l.Y + dy);
        }
        _panX = 0; _panY = 0;
        ApplyPan();
    }

    private static void MapDbg(string msg) => System.IO.File.AppendAllText(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "camcanvas-map.log"), msg + System.Environment.NewLine);

    private void FocusWindowOnMap(IntPtr hwnd)
    {
        MapDbg($"FOCUS hwnd={hwnd.ToInt64():X} tracked={_logical.ContainsKey(hwnd)} alive={Win32.IsWindow(hwnd)}");
        if (!_logical.ContainsKey(hwnd) || !Win32.IsWindow(hwnd)) return;
        if (PreviewActive) SetCanvasMode(false);
        if (Win32.IsIconic(hwnd)) Win32.ShowWindow(hwnd, 9);           // SW_RESTORE
        CentreWindow(hwnd);
        // and bring the window to the front, so clicking an already-centred window
        // still gives visible feedback (like Alt+Tab)
        Win32.BringWindowToTop(hwnd);
        uint fgThread = Win32.GetWindowThreadProcessId(Win32.GetForegroundWindow(), out _);
        uint myThread = Win32.GetWindowThreadProcessId(hwnd, out _);
        if (fgThread != 0 && myThread != 0 && fgThread != myThread)
        {
            Win32.AttachThreadInput(fgThread, myThread, true);
            Win32.SetForegroundWindow(hwnd);
            Win32.AttachThreadInput(fgThread, myThread, false);
        }
        else Win32.SetForegroundWindow(hwnd);
    }

    private bool CentreWindow(IntPtr hwnd)
    {
        if (!_logical.TryGetValue(hwnd, out var l) || Win32.IsIconic(hwnd) ||
            !Win32.GetWindowRect(hwnd, out var r) || r.Right <= r.Left || r.Bottom <= r.Top) return false;
        float cx = l.X + (r.Right - r.Left) / 2f, cy = l.Y + (r.Bottom - r.Top) / 2f;
        PanTo(_w / 2f - cx, _h / 2f - cy);
        return true;
    }
}
