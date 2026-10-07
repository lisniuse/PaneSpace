using PaneSpace.Core.Viewport;
using PaneSpace.Platform.Windows;

namespace PaneSpace.Rendering;

/// <summary>Opaque, nonactivating DWM destination below the layered input surface.</summary>
public sealed class WindowPreview : Form
{
    private readonly Dictionary<IntPtr, IntPtr> _thumbnails = new();
    private readonly List<(IntPtr Hwnd, RectangleF Bounds)> _hits = new();
    private readonly List<RectangleF> _unavailable = new();
    private IntPtr[] _order = Array.Empty<IntPtr>();
    public int LiveCount => _thumbnails.Count;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x08000080; return p; } // NOACTIVATE | TOOLWINDOW
    }
    public WindowPreview(int width, int height)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(0, 0, width, height);
        BackColor = Color.FromArgb(18, 25, 36);
        ShowInTaskbar = false;
        DoubleBuffered = true;
    }
    public void UpdateWindows(IReadOnlyList<(IntPtr Hwnd, RectangleF Bounds)> windows, CanvasViewport viewport)
    {
        var order = windows.Select(w => w.Hwnd).ToArray(); // bottom to top
        if (!_order.SequenceEqual(order))
        {
            ReleaseThumbnails();
            _order = order;
            foreach (var hwnd in order)
                if (Dwm.DwmRegisterThumbnail(Handle, hwnd, out var thumbnail) == 0)
                    _thumbnails[hwnd] = thumbnail;
        }
        _hits.Clear();
        _unavailable.Clear();
        var screen = new RectangleF(0, 0, viewport.Width, viewport.Height);
        foreach (var (hwnd, bounds) in windows)
        {
            var destination = viewport.ToScreen(bounds);
            var clipped = RectangleF.Intersect(destination, screen);
            bool visible = clipped.Width >= 1 && clipped.Height >= 1;
            if (visible) _hits.Add((hwnd, clipped));
            bool rendered = false;
            if (_thumbnails.TryGetValue(hwnd, out var thumbnail) &&
                Dwm.DwmQueryThumbnailSourceSize(thumbnail, out var size) == 0)
            {
                var properties = new Dwm.ThumbnailProperties
                {
                    Flags = Dwm.Destination | Dwm.Source | Dwm.Opacity | Dwm.Visible | Dwm.ClientOnly,
                    Visible = visible, Opacity = 255,
                };
                if (visible)
                {
                    properties.DestinationRect = Rect(clipped);
                    properties.SourceRect = Rect(RectangleF.FromLTRB(
                        (clipped.Left - destination.Left) / destination.Width * size.cx,
                        (clipped.Top - destination.Top) / destination.Height * size.cy,
                        (clipped.Right - destination.Left) / destination.Width * size.cx,
                        (clipped.Bottom - destination.Top) / destination.Height * size.cy));
                }
                rendered = Dwm.DwmUpdateThumbnailProperties(thumbnail, in properties) == 0;
            }
            if (visible && !rendered) _unavailable.Add(clipped);
        }
        Invalidate();
    }
    public void PresentBelow(IntPtr layer)
    {
        if (!Visible) Show();
        Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, 0, 0, Width, Height, Win32.SWP_NOACTIVATE);
        Win32.SetWindowPos(layer, Win32.HWND_TOPMOST, 0, 0, Width, Height, Win32.SWP_NOACTIVATE);
    }
    public IntPtr Hit(Point point)
    {
        for (int i = _hits.Count - 1; i >= 0; i--)
            if (_hits[i].Bounds.Contains(point)) return _hits[i].Hwnd;
        return IntPtr.Zero;
    }
    public void EndPreview()
    {
        Hide();
        ReleaseThumbnails();
        _order = Array.Empty<IntPtr>();
        _hits.Clear();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var fill = new SolidBrush(Color.FromArgb(52, 70, 94));
        foreach (var bounds in _unavailable)
        {
            e.Graphics.FillRectangle(fill, bounds);
            e.Graphics.DrawString("窗口预览不可用 · 点击打开", Font, Brushes.White, bounds);
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) ReleaseThumbnails();
        base.Dispose(disposing);
    }
    private void ReleaseThumbnails()
    {
        foreach (var thumbnail in _thumbnails.Values) Dwm.DwmUnregisterThumbnail(thumbnail);
        _thumbnails.Clear();
    }
    private static Win32.RECT Rect(RectangleF r) => new()
    {
        Left = (int)Math.Round(r.Left), Top = (int)Math.Round(r.Top),
        Right = (int)Math.Round(r.Right), Bottom = (int)Math.Round(r.Bottom),
    };
}
