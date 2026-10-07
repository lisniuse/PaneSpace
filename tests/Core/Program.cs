using System.Drawing;
using PaneSpace.Core.Layout;

int passed = 0;
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
Console.WriteLine($"All {passed} layout checks passed.");

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
