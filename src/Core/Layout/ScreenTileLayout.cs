using System.Drawing;

namespace PaneSpace.Core.Layout;

/// <summary>One window per screen cell, with independent configurable edge insets.</summary>
public static class ScreenTileLayout
{
    public const int Margin = 28;
    public const int BottomMargin = 80;

    public static bool TryGetContent(Size screen, int left, int top, int right, int bottom, out Rectangle content)
    {
        content = Rectangle.Empty;
        if (left < 0 || top < 0 || right < 0 || bottom < 0 ||
            (long)left + right >= screen.Width || (long)top + bottom >= screen.Height) return false;
        content = new Rectangle(left, top, screen.Width - left - right, screen.Height - top - bottom);
        return true;
    }

    public static bool TryArrange(int count, Size screen, bool infinite, out Rectangle[] windows,
        int left = Margin, int top = Margin, int right = Margin, int bottom = BottomMargin)
    {
        windows = Array.Empty<Rectangle>();
        if (count < 0 || !TryGetContent(screen, left, top, right, bottom, out var content) ||
            (!infinite && count > 9)) return false;
        long rows = ((long)count + 2) / 3;
        if (2L * screen.Width > int.MaxValue || (rows - 1) * screen.Height > int.MaxValue) return false;
        windows = new Rectangle[count];
        for (int i = 0; i < count; i++)
            windows[i] = new Rectangle((i % 3 - 1) * screen.Width + content.X,
                (i / 3 - 1) * screen.Height + content.Y, content.Width, content.Height);
        return true;
    }
}
