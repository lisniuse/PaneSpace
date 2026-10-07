using System.Drawing;
using System.Drawing.Imaging;
using PaneSpace.Platform.Windows;
using WinForms = System.Windows.Forms;

namespace PaneSpace.Rendering;

internal static class GrabCursorFactory
{
    // ---- ✋ grab cursor ---------------------------------------------------------------

    public static Cursor Create()
    {
        const int SZ = 32;
        using var bmp = new Bitmap(SZ, SZ, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = new System.Drawing.Drawing2D.GraphicsPath();
            var family = new FontFamily("Segoe UI Emoji");
            path.AddString("\U0001F91E", family, (int)FontStyle.Regular, 26f,
                new PointF(SZ / 2f, SZ / 2f), null);
            var bounds = path.GetBounds();
            using var move = new System.Drawing.Drawing2D.Matrix();
            move.Translate(SZ / 2f - bounds.X - bounds.Width / 2f, SZ / 2f - bounds.Y - bounds.Height / 2f);
            path.Transform(move);
            using var pen = new Pen(Color.Black, 2.6f);
            using var fill = new SolidBrush(Color.FromArgb(250, 255, 224, 178));
            g.DrawPath(pen, path);
            g.FillPath(fill, path);
        }
        // GetHicon puts the hotspot at the icon CENTRE — the drawn fingertip would sit
        // ~16px above the real pointer. Rebuild as a cursor with hotspot at the fingertip.
        using (var tmp = System.Drawing.Icon.FromHandle(bmp.GetHicon()))
        {
            Win32.GetIconInfo(tmp.Handle, out var ii);
            ii.fIcon = false;
            ii.xHotspot = 14;                 // glyph fingertip inside the 32px canvas
            ii.yHotspot = 3;
            IntPtr hCur = Win32.CreateIconIndirect(ref ii);
            Win32.DeleteObject(ii.hbmMask);
            Win32.DeleteObject(ii.hbmColor);
            if (hCur != IntPtr.Zero) return new Cursor(hCur);
            return new Cursor(WinForms.Cursors.Hand.Handle);
        }
    }
}
