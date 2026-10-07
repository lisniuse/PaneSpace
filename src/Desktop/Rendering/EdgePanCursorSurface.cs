using System.Runtime.InteropServices;
using PaneSpace.Core.Viewport;
using PaneSpace.Platform.Windows;

namespace PaneSpace.Rendering;

/// <summary>Owns the edge cursor without changing the user's system cursor scheme.</summary>
public sealed class EdgePanCursorSurface : Form
{
    private readonly Dictionary<Point, IntPtr> _cursors = new();
    private readonly Win32.HookProc _mouseCallback;
    private IntPtr _hook, _cursor;
    public IntPtr CursorHandle => _cursor;
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var parameters = base.CreateParams; parameters.ExStyle |= 0x08000000 /* WS_EX_NOACTIVATE */ | 0x80 /* TOOLWINDOW */; return parameters; }
    }

    public EdgePanCursorSurface(Action interrupted)
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual; BackColor = Color.Black;
        // Alpha 1 is visually transparent but lets this window own WM_SETCURSOR on an active edge.
        Opacity = 1 / 255d;
        _mouseCallback = (code, message, data) =>
        {
            if (code >= 0 && Visible)
            {
                if (message != (IntPtr)Win32.WM_MOUSEMOVE)
                {
                    // Hide BEFORE Windows routes the original button/wheel event to the window below.
                    // Nothing is swallowed or synthesized, preserving taskbar clicks and double clicks.
                    HideFeedback(); interrupted();
                }
                else
                {
                    var pointer = Marshal.PtrToStructure<Win32.POINT>(data);
                    if (!Bounds.Contains(pointer.X, pointer.Y)) HideFeedback();
                }
            }
            return Win32.CallNextHookEx(_hook, code, message, data);
        };
        _hook = Win32.SetWindowsHookEx(14 /* WH_MOUSE_LL */, _mouseCallback, Win32.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) { Dispose(); throw new System.ComponentModel.Win32Exception(); }
    }

    public void ShowFeedback(Rectangle screen, Point direction)
    {
        if (direction == Point.Empty) { HideFeedback(); return; }
        if (!_cursors.TryGetValue(direction, out _cursor))
            _cursors[direction] = _cursor = EdgePanCursorFactory.Create(direction);
        int width = direction.X == 0 ? screen.Width : EdgePan.EdgeWidth;
        int height = direction.Y == 0 ? screen.Height : EdgePan.EdgeWidth;
        Bounds = new Rectangle(direction.X > 0 ? screen.Right - width : screen.Left,
            direction.Y > 0 ? screen.Bottom - height : screen.Top, width, height);
        if (!Visible) Show();
        Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOSIZE_ | 0x0002 /* NOMOVE */ | Win32.SWP_NOACTIVATE);
        Win32.SetCursor(_cursor);
    }

    public void HideFeedback()
    {
        if (!Visible) return;
        Hide(); Win32.SetCursor(Cursors.Default.Handle);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == Win32.WM_SETCURSOR && _cursor != IntPtr.Zero)
        { Win32.SetCursor(_cursor); message.Result = (IntPtr)1; return; }
        if (message.Msg == 0x0021 /* WM_MOUSEACTIVATE */)
        { message.Result = (IntPtr)3 /* MA_NOACTIVATE */; return; }
        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            HideFeedback();
            if (_hook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            foreach (var cursor in _cursors.Values) Win32.DestroyCursor(cursor);
            _cursors.Clear(); _cursor = IntPtr.Zero;
        }
        base.Dispose(disposing);
    }
}
