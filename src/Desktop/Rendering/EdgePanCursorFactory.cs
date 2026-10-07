using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using PaneSpace.Platform.Windows;

namespace PaneSpace.Rendering;

internal static class EdgePanCursorFactory
{
    public static IntPtr Create(Point direction)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var rotation = new Matrix();
        rotation.RotateAt(MathF.Atan2(direction.Y, direction.X) * 180 / MathF.PI + 90, new PointF(16, 16));
        PointF[] tip = { new(16, 3) };
        rotation.TransformPoints(tip);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var path = new GraphicsPath())
        using (var outline = new Pen(Color.FromArgb(240, 25, 35, 45), 2))
        using (var fill = new SolidBrush(Color.FromArgb(255, 120, 218, 255)))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            path.AddPolygon(new PointF[] { new(16, 3), new(27, 14), new(21, 14), new(21, 28),
                new(11, 28), new(11, 14), new(5, 14) });
            path.Transform(rotation);
            graphics.FillPath(fill, path); graphics.DrawPath(outline, path);
        }
        IntPtr icon = bitmap.GetHicon();
        try
        {
            if (!Win32.GetIconInfo(icon, out var info))
                throw new System.ComponentModel.Win32Exception();
            try
            {
                info.fIcon = false;
                // The arrow tip is the hotspot, so the arrow remains visible at physical screen edges.
                info.xHotspot = (uint)Math.Clamp((int)MathF.Round(tip[0].X), 0, 31);
                info.yHotspot = (uint)Math.Clamp((int)MathF.Round(tip[0].Y), 0, 31);
                var cursor = Win32.CreateIconIndirect(ref info);
                if (cursor == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
                return cursor;
            }
            finally { Win32.DeleteObject(info.hbmMask); Win32.DeleteObject(info.hbmColor); }
        }
        finally { Win32.DestroyIcon(icon); }
    }
}
