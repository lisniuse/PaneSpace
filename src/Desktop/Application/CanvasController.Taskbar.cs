using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using PaneSpace.Core.Layout;
using PaneSpace.Core.Sessions;
using PaneSpace.Persistence;
using PaneSpace.Platform.Windows;
using PaneSpace.Rendering;
using WinForms = System.Windows.Forms;

namespace PaneSpace.Application;

public sealed partial class CanvasController
{
    // ---- taskbar activation ----------------------------------------------------------

    private bool CursorIsOnTaskbar()
    {
        if (!Win32.GetCursorPos(out var p)) return false;
        var root = Win32.GetAncestor(Win32.WindowFromPoint(p), Win32.GA_ROOT);
        _cls.Clear();
        Win32.GetClassName(root, _cls, _cls.Capacity);
        return _cls.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "TaskListThumbnailWnd";
    }

    private void PollTaskbarClick()
    {
        bool down = (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;
        if (down)
        {
            if (CursorIsOnTaskbar()) _taskbarClickUntil = Environment.TickCount64 + 1500;
            else if (!_lastLeftButton) _taskbarClickUntil = 0;
        }
        _lastLeftButton = down;
    }

    private bool TaskbarWasClicked()
    {
        // The foreground notification can arrive before the next polling tick.
        if ((Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0)
            return CursorIsOnTaskbar();
        return Environment.TickCount64 < _taskbarClickUntil;
    }

    private void QueueTaskbarFocus(IntPtr hwnd, long now)
    {
        if (_canvasMode || _dragging) return;
        _pendingTaskbarFocus = hwnd;
        _focusNotBefore = now + 120;
        _focusExpires = now + 1500;
        _taskbarClickUntil = 0;                       // consume this taskbar interaction
        _desktopLaunchUntil = 0;                     // or this explicit desktop icon launch
    }

    private void ProcessTaskbarFocus(IntPtr foreground, long now)
    {
        var hwnd = _pendingTaskbarFocus;
        if (hwnd == IntPtr.Zero) return;
        if (_canvasMode || _dragging || now > _focusExpires || !Win32.IsWindow(hwnd))
        {
            _pendingTaskbarFocus = IntPtr.Zero;
            return;
        }
        // MINIMIZEEND is sent before restoration finishes. Defer until the restored
        // window is foreground, non-minimized, and has its real dimensions again.
        if (now < _focusNotBefore || foreground != hwnd || Win32.IsIconic(hwnd)) return;
        if (!WindowFilter.IsManaged(hwnd, _selfPid))
        {
            _pendingTaskbarFocus = IntPtr.Zero;
            return;
        }
        TrackWindow(hwnd);
        ResyncLogical();                             // adopt manual moves and the restored position
        if (CentreWindow(hwnd)) _pendingTaskbarFocus = IntPtr.Zero;
    }
}
