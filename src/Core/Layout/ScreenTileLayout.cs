using System.Drawing;

namespace PaneSpace.Core.Layout;

/// <summary>One window per screen cell, with a 28-pixel inset on each side.</summary>
public static class ScreenTileLayout
{
    public const int Margin = 28;

    public static bool TryArrange(int count, Size screen, bool infinite, out Rectangle[] windows)
    {
        windows = Array.Empty<Rectangle>();
        if (count < 0 || screen.Width <= 2 * Margin || screen.Height <= 2 * Margin ||
            (!infinite && count > 9)) return false;
        long rows = ((long)count + 2) / 3;
        if (2L * screen.Width > int.MaxValue || (rows - 1) * screen.Height > int.MaxValue) return false;
        windows = new Rectangle[count];
        for (int i = 0; i < count; i++)
            windows[i] = new Rectangle((i % 3 - 1) * screen.Width + Margin,
                (i / 3 - 1) * screen.Height + Margin,
                screen.Width - 2 * Margin, screen.Height - 2 * Margin);
        return true;
    }
}
