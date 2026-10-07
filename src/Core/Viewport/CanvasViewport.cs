using System.Drawing;

namespace PaneSpace.Core.Viewport;

/// <summary>World-unit camera offset; scale one matches native window coordinates.</summary>
public readonly record struct CanvasViewport(float Width, float Height, float PanX, float PanY, float Scale = 1,
    bool Infinite = false)
{
    public const float MinScale = .25f, MaxScale = 2f;
    public PointF ToScreen(PointF world) => new(
        (world.X + PanX - Width / 2) * Scale + Width / 2,
        (world.Y + PanY - Height / 2) * Scale + Height / 2);
    public PointF ToWorld(PointF screen) => new(
        (screen.X - Width / 2) / Scale + Width / 2 - PanX,
        (screen.Y - Height / 2) / Scale + Height / 2 - PanY);
    public RectangleF VisibleWorld => new(ToWorld(PointF.Empty), new SizeF(Width / Scale, Height / Scale));
    public RectangleF ToScreen(RectangleF world) => new(ToScreen(world.Location),
        new SizeF(world.Width * Scale, world.Height * Scale));
    public CanvasViewport Clamp()
    {
        if (Infinite) return this;
        float limitX = Width * Math.Max(0, (3 - 1 / Scale) / 2);
        float limitY = Height * Math.Max(0, (3 - 1 / Scale) / 2);
        return this with { PanX = Math.Clamp(PanX, -limitX, limitX), PanY = Math.Clamp(PanY, -limitY, limitY) };
    }
    public CanvasViewport Drag(float dx, float dy) => (this with
        { PanX = PanX + dx / Scale, PanY = PanY + dy / Scale }).Clamp();
    public CanvasViewport ZoomAt(PointF screen, float scale)
    {
        scale = Math.Clamp(scale, MinScale, MaxScale);
        var world = ToWorld(screen);
        return (this with { Scale = scale,
            PanX = (screen.X - Width / 2) / scale + Width / 2 - world.X,
            PanY = (screen.Y - Height / 2) / scale + Height / 2 - world.Y }).Clamp();
    }
    public CanvasViewport Wheel(PointF screen, int delta) =>
        ZoomAt(screen, Scale * MathF.Pow(1.15f, delta / 120f));
}
