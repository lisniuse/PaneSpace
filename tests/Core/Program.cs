using System.Drawing;
using PaneSpace.Core.Layout;
using PaneSpace.Core.Viewport;
using PaneSpace.Core.Settings;
using PaneSpace.Core.Sessions;
using System.Text.Json;

int passed = 0;
Run("Screen tiles center each window with 28px margins and adjacent screen cells", () =>
{
    var screen = new Size(1920, 1080);
    Assert(ScreenTileLayout.TryArrange(9, screen, false, out var tiles), "Nine screen tiles should fit.");
    Assert(tiles.Length == 9 && tiles[0] == new Rectangle(-1892, -1052, 1864, 1024), "First tile geometry.");
    var canvas = new Rectangle(-1920, -1080, 5760, 3240);
    foreach (var tile in tiles) Assert(canvas.Contains(tile), "Every tile must stay in the finite canvas.");
    Assert(tiles[1].X - tiles[0].Right == 56 && tiles[3].Y - tiles[0].Bottom == 56,
        "Adjacent cells each contribute their own 28px margin.");
    var camera = new CanvasViewport(1920, 1080, 1920, 1080);
    Near(camera.ToScreen(new PointF(tiles[0].X + tiles[0].Width / 2f, tiles[0].Y + tiles[0].Height / 2f)),
        new PointF(960, 540));
});
Run("Screen tiles reject finite overflow and extend the infinite grid", () =>
{
    Assert(!ScreenTileLayout.TryArrange(10, new Size(640, 480), false, out var rejected) && rejected.Length == 0,
        "Finite overflow must return no partial layout.");
    Assert(ScreenTileLayout.TryArrange(100, new Size(640, 480), true, out var tiles) &&
        tiles.Length == 100 && tiles[9].Location == new Point(-612, 988), "Infinite tiles continue after the ninth cell.");
    Assert(ScreenTileLayout.TryArrange(0, new Size(640, 480), false, out var empty) && empty.Length == 0, "Empty layout.");
    Assert(!ScreenTileLayout.TryArrange(1, new Size(56, 480), true, out _), "Margins must leave positive content.");
    Assert(!ScreenTileLayout.TryArrange(-1, new Size(640, 480), true, out _), "Negative count.");
    Assert(!ScreenTileLayout.TryArrange(int.MaxValue, new Size(640, 480), true, out _), "Coordinate overflow.");
});
Run("Short windows fill below the shortest column", () =>
{
    var sizes = new[] { new Size(100, 300), new Size(100, 100), new Size(100, 100), new Size(100, 80) };
    var bounds = new Rectangle(-60, -90, 320, 500);
    var points = Arrange(sizes, bounds, 10);
    Assert(points.SequenceEqual(new[] { new Point(-60, -90), new Point(50, -90), new Point(160, -90), new Point(50, 20) }),
        "The fourth window should fill under a short window, without waiting for the tallest window.");
});
Run("Wide windows span columns without resizing", () =>
{
    var sizes = new[] { new Size(100, 300), new Size(100, 100), new Size(100, 100), new Size(210, 80), new Size(100, 50) };
    var points = Arrange(sizes, new Rectangle(0, 0, 320, 500), 10);
    Assert(points[3] == new Point(110, 110), "A wide window should span the two shortest columns.");
    Assert(points[4] == new Point(110, 200), "A short window should continue below the wide window.");
    Assert(sizes[3] == new Size(210, 80), "The input width and height must remain unchanged.");
});
Run("Canvas edges allow an exact fit", () =>
{
    var sizes = new[] { new Size(100, 100), new Size(100, 100) };
    var points = Arrange(sizes, new Rectangle(-100, -100, 100, 210), 10);
    Assert(points[1] == new Point(-100, 10), "Edge margins must not add an extra trailing gap.");
});
Run("Insufficient canvas space rejects the entire layout", () =>
{
    var sizes = new[] { new Size(100, 100), new Size(100, 100) };
    Assert(!MasonryLayout.TryArrange(sizes, new Rectangle(0, 0, 100, 209), 10, out var points), "One-pixel overflow must be rejected.");
    Assert(points.Length == 0, "A rejected layout must not return partial positions.");
});
Run("Oversized and invalid windows are rejected", () =>
{
    foreach (var size in new[] { new Size(101, 40), new Size(40, 101), new Size(0, 40), new Size(40, -1) })
        Assert(!MasonryLayout.TryArrange(new[] { size }, new Rectangle(0, 0, 100, 100), 16, out _), "Invalid window accepted.");
    Assert(!MasonryLayout.TryArrange(Array.Empty<Size>(), Rectangle.Empty, 16, out _), "Empty canvas accepted.");
});
Run("Empty layouts and zero-gap layouts are supported", () =>
{
    var bounds = new Rectangle(0, 0, 200, 100);
    Assert(MasonryLayout.TryArrange(Array.Empty<Size>(), bounds, 0, out var empty) && empty.Length == 0, "An empty layout should succeed.");
    Arrange(new[] { new Size(100, 100), new Size(100, 100) }, bounds, 0);
});
Run("Random mixed-size layouts keep all windows in bounds and separated", () =>
{
    var random = new Random(20261007);
    int accepted = 0, rejected = 0;
    var bounds = new Rectangle(-1892, -1052, 5704, 3184);
    for (int sample = 0; sample < 1000; sample++)
    {
        var sizes = Enumerable.Range(0, random.Next(1, 36))
            .Select(_ => new Size(random.Next(60, 1801), random.Next(40, 1001))).ToArray();
        if (!MasonryLayout.TryArrange(sizes, bounds, 16, out var points))
        {
            rejected++;
            Assert(points.Length == 0, "Rejected random layout returned partial positions.");
            continue;
        }
        accepted++;
        Validate(sizes, points, bounds, 16);
        Assert(MasonryLayout.TryArrange(sizes, bounds, 16, out var repeated) && repeated.SequenceEqual(points),
            "The same windows must produce a deterministic layout.");
    }
    Assert(accepted > 0 && rejected > 0, "Random cases must exercise both successful and insufficient-space layouts.");
    Console.WriteLine($"  1000 cases: {accepted} accepted, {rejected} safely rejected");
});
Run("Zoom keeps the cursor's world point fixed and transforms invert", () =>
{
    var random = new Random(42);
    for (int i = 0; i < 1000; i++)
    {
        var camera = new CanvasViewport(1920, 1080, 0, 0);
        var cursor = new PointF(random.Next(1920), random.Next(1080));
        var world = camera.ToWorld(cursor);
        var zoomed = camera.ZoomAt(cursor, 1.15f);
        Near(zoomed.ToScreen(world), cursor);
        Near(zoomed.ToWorld(zoomed.ToScreen(world)), world);
        Near(zoomed.ZoomAt(cursor, 1).ToScreen(world), cursor);
    }
});
Run("Scaled drags move content by the actual mouse distance", () =>
{
    foreach (float scale in new[] { .5f, 1f, 2f })
    {
        var camera = new CanvasViewport(1920, 1080, 0, 0, scale);
        var before = camera.ToScreen(new PointF(420, 240));
        var after = camera.Drag(72, -48).ToScreen(new PointF(420, 240));
        Near(after, new PointF(before.X + 72, before.Y - 48));
    }
});
Run("Zoom and camera limits keep the 3x3 canvas reachable", () =>
{
    var camera = new CanvasViewport(1920, 1080, 0, 0);
    Assert(camera.Wheel(PointF.Empty, -20000).Scale == CanvasViewport.MinScale, "Minimum zoom limit.");
    Assert(camera.Wheel(PointF.Empty, 20000).Scale == CanvasViewport.MaxScale, "Maximum zoom limit.");
    var overview = camera.ZoomAt(PointF.Empty, .25f).Drag(10000, -10000);
    Assert(overview.PanX == 0 && overview.PanY == 0, "Full overview should stay centered.");
    Assert(overview.VisibleWorld.Contains(new RectangleF(-1920, -1080, 5760, 3240)), "Overview includes the entire canvas.");
    foreach (float scale in new[] { .5f, 1f, 2f })
    {
        var edge = (camera with { Scale = scale }).Drag(10000, -10000).VisibleWorld;
        Assert(edge.Left >= -1920 && edge.Right <= 3840 && edge.Top >= -1080 && edge.Bottom <= 2160,
            "Visible viewport must stay within the canvas when it fits.");
    }
    Assert(camera.Wheel(new PointF(960, 540), 60).Scale > 1 &&
        camera.Wheel(new PointF(960, 540), 60).Scale < 1.15f, "Partial wheel deltas must be supported.");
});
Run("Infinite cameras have no nine-screen boundary at any scale", () =>
{
    foreach (float scale in new[] { .25f, 1f, 2f })
    {
        var camera = new CanvasViewport(1920, 1080, -384000, 216000, scale, Infinite: true);
        Assert(camera.Clamp() == camera, "Infinite camera must not be clamped.");
        var dragged = camera.Drag(19200, -10800);
        Assert(dragged.PanX == camera.PanX + 19200 / scale && dragged.PanY == camera.PanY - 10800 / scale,
            "Far-away drags must preserve their full distance.");
        var native = (dragged with { Scale = 1 }).Clamp();
        Assert(native.PanX == dragged.PanX && native.PanY == dragged.PanY, "Returning to native scale preserves an infinite camera.");
    }
    var edge = new CanvasViewport(1920, 1080, 5000, -6000, Infinite: true);
    var cursor = new PointF(123, 456);
    var point = edge.ToWorld(cursor);
    Near(edge.ZoomAt(cursor, .5f).ToScreen(point), cursor);
});
Run("Infinite overview includes distant content and the current viewport", () =>
{
    var camera = new CanvasViewport(1920, 1080, 100000, -200000, Infinite: true);
    var content = new[] { new RectangleF(-500000, -400000, 300, 200), new RectangleF(300000, 600000, 100, 90) };
    var bounds = CanvasOverview.Bounds(camera, content);
    Assert(bounds.Contains(camera.VisibleWorld) && content.All(bounds.Contains), "Overview must include all world content and the camera.");
    Assert(Math.Abs(bounds.Width / bounds.Height - 1920f / 1080) < .00001f, "Minimap keeps the screen aspect ratio.");
    Assert(CanvasOverview.Bounds(camera with { Infinite = false }, content) == new RectangleF(-1920, -1080, 5760, 3240),
        "Finite mode retains the original nine-screen overview.");
});
Run("Legacy layouts and persisted settings and desktop positions remain compatible", () =>
{
    var legacy = JsonSerializer.Deserialize<SessionState>("{\"PanX\":12,\"PanY\":34,\"Windows\":[]}")!;
    Assert(legacy.DesktopIcons.Count == 0 && legacy.PanX == 12, "Legacy layout defaults to an empty icon list.");
    var state = new SessionState { PanX = -90000, PanY = 50000,
        DesktopIcons = new() { new DesktopIconState("C:\\fixture\\shortcut.lnk", 90500, -49200) } };
    var restored = JsonSerializer.Deserialize<SessionState>(JsonSerializer.Serialize(state))!;
    Assert(restored.PanX == state.PanX && restored.DesktopIcons.SequenceEqual(state.DesktopIcons), "Far-away icon positions survive serialization.");
    foreach (bool infinite in new[] { false, true })
        foreach (bool icons in new[] { false, true })
        {
            var settings = new AppSettings(infinite, icons);
            Assert(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings)) == settings, "Settings are independent and persistent.");
        }
});
Console.WriteLine($"All {passed} core checks passed.");

void Near(PointF actual, PointF expected) => Assert(Math.Abs(actual.X - expected.X) < .002f &&
    Math.Abs(actual.Y - expected.Y) < .002f, $"Expected {expected}, got {actual}.");

Point[] Arrange(Size[] sizes, Rectangle bounds, int gap)
{
    Assert(MasonryLayout.TryArrange(sizes, bounds, gap, out var points), "Expected this layout to fit.");
    Validate(sizes, points, bounds, gap);
    return points;
}
void Validate(Size[] sizes, Point[] points, Rectangle bounds, int gap)
{
    Assert(points.Length == sizes.Length, "Every window must have a position.");
    var rectangles = sizes.Select((size, i) => new Rectangle(points[i], size)).ToArray();
    for (int i = 0; i < rectangles.Length; i++)
    {
        var current = rectangles[i];
        Assert(bounds.Contains(current), "Window extends beyond the reachable canvas.");
        for (int j = 0; j < i; j++)
        {
            var other = rectangles[j];
            Assert(current.Right + gap <= other.Left || other.Right + gap <= current.Left ||
                   current.Bottom + gap <= other.Top || other.Bottom + gap <= current.Top,
                "Windows overlap or do not have the requested gap.");
        }
    }
}
void Run(string name, Action test)
{
    test();
    passed++;
    Console.WriteLine("PASS " + name);
}
void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
