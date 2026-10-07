using System.Drawing;

namespace PaneSpace.Core.Layout;

/// <summary>Variable-width masonry: place each item on the lowest available skyline.</summary>
public static class MasonryLayout
{
    private readonly record struct Column(int Left, int Right, int Bottom);

    public static bool TryArrange(IReadOnlyList<Size> sizes, Rectangle bounds, int gap,
        out Point[] positions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gap);
        positions = Array.Empty<Point>();
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        var result = new Point[sizes.Count];
        var skyline = new List<Column> { new(bounds.Left, bounds.Right, bounds.Top) };
        for (int i = 0; i < sizes.Count; i++)
        {
            var size = sizes[i];
            if (size.Width <= 0 || size.Height <= 0 ||
                size.Width > bounds.Width || size.Height > bounds.Height) return false;

            int bestX = 0, bestY = int.MaxValue, bestRight = 0;
            foreach (var column in skyline)
            {
                int x = column.Left;
                if ((long)x + size.Width > bounds.Right) continue;
                int right = (int)Math.Min(bounds.Right, (long)x + size.Width + gap);
                int y = bounds.Top;
                // A wide window spans several columns and must clear all of them.
                foreach (var covered in skyline)
                    if (covered.Left < right && covered.Right > x)
                        y = Math.Max(y, covered.Bottom);

                if ((long)y + size.Height > bounds.Bottom || y >= bestY) continue;
                bestX = x;
                bestY = y;
                bestRight = right;
            }
            if (bestY == int.MaxValue) return false;
            result[i] = new Point(bestX, bestY);

            int bottom = (int)Math.Min(bounds.Bottom, (long)bestY + size.Height + gap);
            var next = new List<Column>();
            foreach (var column in skyline)
            {
                if (column.Right <= bestX || column.Left >= bestRight)
                {
                    Add(column);
                    continue;
                }
                if (column.Left < bestX) Add(new(column.Left, bestX, column.Bottom));
                Add(new(Math.Max(column.Left, bestX), Math.Min(column.Right, bestRight), bottom));
                if (column.Right > bestRight) Add(new(bestRight, column.Right, column.Bottom));
            }
            skyline = next;

            void Add(Column column)
            {
                if (next.Count > 0 && next[^1].Right == column.Left && next[^1].Bottom == column.Bottom)
                    next[^1] = new(next[^1].Left, column.Right, column.Bottom);
                else
                    next.Add(column);
            }
        }

        positions = result;
        return true;
    }
}
