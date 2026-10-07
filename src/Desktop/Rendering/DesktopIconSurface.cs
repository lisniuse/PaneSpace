using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PaneSpace.Core.Viewport;
using PaneSpace.Platform.Windows;

namespace PaneSpace.Rendering;

/// <summary>Clickable alpha surface positioned above the wallpaper and below application windows.</summary>
public sealed class DesktopIconSurface : Form
{
    private readonly DesktopIconCanvas _icons;
    private readonly Func<CanvasViewport> _camera;
    private readonly Bitmap _frame;
    private readonly IntPtr _screen, _dc, _bitmap, _previous;
    private bool _released;
    public Bitmap Frame => _frame;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x08080080; return p; } // NOACTIVATE | LAYERED | TOOLWINDOW
    }
    public DesktopIconSurface(int width, int height, DesktopIconCanvas icons, Func<CanvasViewport> camera)
    {
        _icons = icons; _camera = camera;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual; Bounds = new Rectangle(0, 0, width, height);
        _screen = Win32.GetDC(IntPtr.Zero); _dc = Win32.CreateCompatibleDC(_screen);
        var info = new Win32.BITMAPINFO { bmiHeader = new Win32.BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(), biWidth = width, biHeight = -height,
            biPlanes = 1, biBitCount = 32,
        } };
        _bitmap = Win32.CreateDIBSection(_screen, ref info, 0, out var pixels, IntPtr.Zero, 0);
        if (_bitmap == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error(); Win32.DeleteDC(_dc); Win32.ReleaseDC(IntPtr.Zero, _screen);
            throw new System.ComponentModel.Win32Exception(error);
        }
        _previous = Win32.SelectObject(_dc, _bitmap);
        _frame = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, pixels);
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) Capture = _icons.BeginDrag(e.Location, _camera()); };
        MouseMove += (_, e) => { if (Capture) _icons.DragTo(e.Location, _camera()); };
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) { _icons.EndDrag(); Capture = false; } };
        MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) _icons.OpenAt(e.Location, _camera()); };
    }
    public void Render()
    {
        using (var g = Graphics.FromImage(_frame)) { g.Clear(Color.Transparent); _icons.Draw(g, _camera()); }
        var size = new Win32.SIZE { cx = Width, cy = Height };
        var destination = new Win32.POINT(); var source = new Win32.POINT();
        var blend = new Win32.BLENDFUNCTION { SourceConstantAlpha = 255, AlphaFormat = 1 };
        Win32.UpdateLayeredWindow(Handle, _screen, ref destination, ref size, _dc, ref source, 0, ref blend, 2);
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21 /*WM_MOUSEACTIVATE*/) { m.Result = (IntPtr)3; return; }
        base.WndProc(ref m);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_released)
        {
            _released = true;
            _frame.Dispose(); Win32.SelectObject(_dc, _previous); Win32.DeleteObject(_bitmap);
            Win32.DeleteDC(_dc); Win32.ReleaseDC(IntPtr.Zero, _screen);
        }
        base.Dispose(disposing);
    }
}
