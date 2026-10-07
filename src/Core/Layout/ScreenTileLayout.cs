using System.Drawing;

namespace PaneSpace.Core.Layout;

/// <summary>One window per screen cell, reserving 80 pixels below for the taskbar.</summary>
public static class ScreenTileLayout
{
    public const int Margin = 28;
    public const int BottomMargin = 80;

    public static bool TryArrange(int count, Size screen, bool infinite, out Rectangle[] windows)
    {
        windows = Array.Empty<Rectangle>();
        if (count < 0 || screen.Width <= 2 * Margin || screen.Height <= Margin + BottomMargin ||
            (!infinite && count > 9)) return false;
        long rows = ((long)count + 2) / 3;
        if (2L * screen.Width > int.MaxValue || (rows - 1) * screen.Height > int.MaxValue) return false;
        windows = new Rectangle[count];
        for (int i = 0; i < count; i++)
            windows[i] = new Rectangle((i % 3 - 1) * screen.Width + Margin,
                (i / 3 - 1) * screen.Height + Margin,
                screen.Width - 2 * Margin, screen.Height - Margin - BottomMargin);
        return true;
    }
}
