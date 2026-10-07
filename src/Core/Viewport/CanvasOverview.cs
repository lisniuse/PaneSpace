using System.Drawing;

namespace PaneSpace.Core.Viewport;

public static class CanvasOverview
{
    /// <summary>Fits the current camera and all content, with a stable display aspect ratio.</summary>
    public static RectangleF Bounds(CanvasViewport camera, IEnumerable<RectangleF> content)
    {
        if (!camera.Infinite) return new RectangleF(-camera.Width, -camera.Height, 3 * camera.Width, 3 * camera.Height);
        var bounds = camera.VisibleWorld;
        foreach (var item in content) bounds = RectangleF.Union(bounds, item);
        bounds.Inflate(Math.Max(camera.Width * .1f, 48), Math.Max(camera.Height * .1f, 48));
        float width = Math.Max(bounds.Width, bounds.Height * camera.Width / camera.Height);
        float height = width * camera.Height / camera.Width;
        return new RectangleF(bounds.X - (width - bounds.Width) / 2, bounds.Y - (height - bounds.Height) / 2, width, height);
    }
}
