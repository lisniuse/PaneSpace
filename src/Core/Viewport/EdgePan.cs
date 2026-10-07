using System.Drawing;

namespace PaneSpace.Core.Viewport;

/// <summary>Timed edge scrolling in screen pixels; the viewport handles zoom and bounds.</summary>
public sealed class EdgePan
{
    public const int EdgeWidth = 12, DwellMilliseconds = 250;
    public const int PixelsPerSecond = 600, MinSpeed = 50, MaxSpeed = 3000;
    public Point Direction => _direction;
    private Point _direction;
    private long _enteredAt, _lastTick;

    public void Reset() { _direction = Point.Empty; _enteredAt = _lastTick = 0; }

    public PointF Step(Point cursor, Rectangle area, long now, bool allowed, int speed = PixelsPerSecond)
    {
        if (!allowed || !area.Contains(cursor) || area.Width <= 2 * EdgeWidth || area.Height <= 2 * EdgeWidth)
        { Reset(); return PointF.Empty; }
        var direction = new Point(cursor.X < area.Left + EdgeWidth ? 1 : cursor.X >= area.Right - EdgeWidth ? -1 : 0,
            cursor.Y < area.Top + EdgeWidth ? 1 : cursor.Y >= area.Bottom - EdgeWidth ? -1 : 0);
        if (direction == Point.Empty) { Reset(); return PointF.Empty; }
        if (direction != _direction || now < _lastTick)
        {
            _direction = direction; _enteredAt = _lastTick = now;
            return PointF.Empty;
        }
        long elapsed = Math.Clamp(now - _lastTick, 0, 50);
        _lastTick = now;
        if (now - _enteredAt < DwellMilliseconds) return PointF.Empty;
        float distance = Math.Clamp(speed, MinSpeed, MaxSpeed) * elapsed / 1000f;
        if (direction.X != 0 && direction.Y != 0) distance /= MathF.Sqrt(2);
        return new PointF(direction.X * distance, direction.Y * distance);
    }
}
