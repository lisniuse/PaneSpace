using System.Drawing;

namespace PaneSpace.Rendering;

internal static class BrandIcon
{
    public static Icon Load()
    {
        using var stream = typeof(BrandIcon).Assembly.GetManifestResourceStream("PaneSpace.Branding.Icon.ico")
            ?? throw new InvalidOperationException("The PaneSpace icon resource is missing.");
        // Clone so the icon remains valid after its resource stream is closed.
        using var icon = new Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
        return (Icon)icon.Clone();
    }
}
