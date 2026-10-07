using System.Drawing;
using PaneSpace.Core.Viewport;
using PaneSpace.Platform.Windows;
using PaneSpace.Rendering;

namespace PaneSpace.Application;

public sealed partial class CanvasController
{
    private float _zoom = 1, _nativePanX, _nativePanY;
    private WindowPreview? _preview;
    private long _previewRefreshed;
    private CanvasViewport Viewport => new(_w, _h, _panX, _panY, _zoom);
    private bool PreviewActive => _canvasMode && _zoom != 1;

    private void ZoomAt(Point mouse, int delta)
    {
        if (delta == 0) return;
        var next = Viewport.Wheel(mouse, delta);
        // Snap close to 100% so reversing the wheel returns to native rendering.
        if (Math.Abs(next.Scale - 1) < .005f) next = Viewport.ZoomAt(mouse, 1);
        if (next.Scale == _zoom) return;
        if (!PreviewActive)
        {
            ResyncLogical();
            _nativePanX = _panX; _nativePanY = _panY;
        }
        _zoom = next.Scale; _panX = next.PanX; _panY = next.PanY;
        if (PreviewActive) RefreshPreview();
        else { ApplyPan(); _preview?.EndPreview(); }
        ComposeFull();
    }
    private void ReturnToNative()
    {
        if (_zoom == 1) return;
        var native = (Viewport with { Scale = 1 }).Clamp();
        _zoom = 1; _panX = native.PanX; _panY = native.PanY;
        ApplyPan();
        _preview?.EndPreview();
    }
    private void RefreshPreview()
    {
        _preview ??= new WindowPreview(_w, _h);
        var windows = new List<(IntPtr Hwnd, RectangleF Bounds)>();
        Win32.EnumWindows((hwnd, _) =>
        {
            if (_logical.TryGetValue(hwnd, out var logical) && Win32.IsWindowVisible(hwnd) &&
                !Win32.IsIconic(hwnd) && Win32.GetWindowRect(hwnd, out var rect) &&
                rect.Right > rect.Left && rect.Bottom > rect.Top)
                windows.Add((hwnd, new RectangleF(logical.X, logical.Y, rect.Right - rect.Left, rect.Bottom - rect.Top)));
            return true;
        }, IntPtr.Zero);
        windows.Reverse();
        _preview.UpdateWindows(windows, Viewport);
        _preview.PresentBelow(_layer);
        _previewRefreshed = Environment.TickCount64;
    }
}
