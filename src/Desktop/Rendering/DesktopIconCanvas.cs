using System.Drawing.Drawing2D;
using PaneSpace.Core.Sessions;
using PaneSpace.Core.Viewport;
using PaneSpace.Platform.Windows;

namespace PaneSpace.Rendering;

public sealed class DesktopIconCanvas : IDisposable
{
    private sealed record Item(string Path, string Name, Bitmap Image);
    private readonly List<Item> _items = new();
    private readonly Dictionary<string, DesktopIconState> _positions;
    private string? _dragPath, _selected;
    private Point _last;
    public event Action? Changed;
    public event Action<string>? OpenRequested;
    public int Count => _items.Count;
    public float IconSize { get; set; } = 48;
    public IEnumerable<(string Path, RectangleF Bounds)> Bounds => _items.Select(item =>
    {
        var position = _positions[item.Path];
        return (item.Path, new RectangleF(position.X, position.Y, Math.Max(100, IconSize + 24), IconSize + 44));
    });
    public DesktopIconCanvas(Dictionary<string, DesktopIconState> positions) => _positions = positions;
    public void Refresh(IReadOnlyList<ShellDesktopItem> items, CanvasViewport camera, Func<string, Bitmap>? loadIcon = null)
    {
        if (_items.Count == items.Count && _items.Select(item => (item.Path, item.Name))
            .SequenceEqual(items.Select(item => (item.Path, item.Name)))) return;
        loadIcon ??= DesktopShell.LoadIcon;
        var remaining = _items.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        var next = new List<Item>();
        foreach (var item in items.DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (!_positions.ContainsKey(item.Path))
                _positions[item.Path] = new DesktopIconState(item.Path, item.Position.X - camera.PanX, item.Position.Y - camera.PanY);
            if (remaining.Remove(item.Path, out var existing)) next.Add(existing with { Name = item.Name });
            else next.Add(new Item(item.Path, item.Name, loadIcon(item.Path)));
        }
        foreach (var item in remaining.Values) { item.Image.Dispose(); _positions.Remove(item.Path); }
        _items.Clear(); _items.AddRange(next);
        if (_dragPath != null && !_positions.ContainsKey(_dragPath)) _dragPath = null;
        Changed?.Invoke();
    }
    public string? Hit(Point screen, CanvasViewport camera)
    {
        foreach (var (path, bounds) in Bounds.Reverse())
            if (camera.ToScreen(bounds).Contains(screen)) return path;
        return null;
    }
    public void Draw(Graphics g, CanvasViewport camera)
    {
        using var format = new StringFormat { Alignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.LineLimit };
        using var hitBrush = new SolidBrush(Color.FromArgb(1, 0, 0, 0));
        using var selection = new SolidBrush(Color.FromArgb(80, 80, 140, 210));
        foreach (var item in _items)
        {
            var position = _positions[item.Path];
            var world = new RectangleF(position.X, position.Y, Math.Max(100, IconSize + 24), IconSize + 44);
            if (!world.IntersectsWith(camera.VisibleWorld)) continue;
            var screen = camera.ToScreen(world);
            g.FillRectangle(item.Path == _selected ? selection : hitBrush, screen);
            var saved = g.Save();
            g.TranslateTransform(screen.X, screen.Y); g.ScaleTransform(camera.Scale, camera.Scale);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(item.Image, (world.Width - IconSize) / 2, 0, IconSize, IconSize);
            var text = new RectangleF(0, IconSize + 4, world.Width, 38);
            var font = SystemFonts.IconTitleFont ?? SystemFonts.DefaultFont;
            g.DrawString(item.Name, font, Brushes.Black,
                new RectangleF(text.X + 1, text.Y + 1, text.Width, text.Height), format);
            g.DrawString(item.Name, font, Brushes.White, text, format);
            g.Restore(saved);
        }
    }
    public bool BeginDrag(Point screen, CanvasViewport camera)
    {
        _dragPath = Hit(screen, camera); _selected = _dragPath; _last = screen;
        Changed?.Invoke();
        return _dragPath != null;
    }
    public void DragTo(Point screen, CanvasViewport camera)
    {
        if (_dragPath == null) return;
        var old = _positions[_dragPath];
        float x = old.X + (screen.X - _last.X) / camera.Scale;
        float y = old.Y + (screen.Y - _last.Y) / camera.Scale;
        if (!camera.Infinite)
        {
            x = Math.Clamp(x, -camera.Width, 2 * camera.Width - Math.Max(100, IconSize + 24));
            y = Math.Clamp(y, -camera.Height, 2 * camera.Height - IconSize - 44);
        }
        _positions[_dragPath] = old with { X = x, Y = y }; _last = screen;
        Changed?.Invoke();
    }
    public void EndDrag() { _dragPath = null; Changed?.Invoke(); }
    public void OpenAt(Point screen, CanvasViewport camera)
    {
        var path = Hit(screen, camera);
        if (path != null) OpenRequested?.Invoke(path);
    }
    public void Dispose()
    {
        foreach (var item in _items) item.Image.Dispose();
        _items.Clear();
    }
}
