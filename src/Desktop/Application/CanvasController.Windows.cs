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
    // ---- window bookkeeping --------------------------------------------------------

    private void SeedWindows()
    {
        Win32.EnumWindows((hwnd, _) =>
        {
            if (WindowFilter.IsManaged(hwnd, _selfPid) && !Win32.IsIconic(hwnd))
                _logical[hwnd] = RealPos(hwnd);
            return true;
        }, IntPtr.Zero);
    }

    private (int X, int Y) RealPos(IntPtr hwnd)
    {
        Win32.GetWindowRect(hwnd, out var r);
        return (r.Left, r.Top);
    }

    private void OnWinEvent(uint type, IntPtr hwnd, int idObject, int idChild)
    {
        if (idObject != 0 || idChild != 0) return;
        switch (type)
        {
            case Win32.EVENT_OBJECT_CREATE:
            case Win32.EVENT_SYSTEM_FOREGROUND:
            case Win32.EVENT_SYSTEM_MINIMIZEEND:
                TrackWindow(hwnd);
                if (type != Win32.EVENT_OBJECT_CREATE && WindowFilter.IsManaged(hwnd, _selfPid) &&
                    TaskbarWasClicked())
                    QueueTaskbarFocus(hwnd, Environment.TickCount64);
                break;
            case Win32.EVENT_OBJECT_DESTROY:
                if (Win32.IsWindow(hwnd)) break;      // fake-destroy broadcast
                _logical.Remove(hwnd);
                if (_pendingTaskbarFocus == hwnd) _pendingTaskbarFocus = IntPtr.Zero;
                break;
        }
    }

    private void TrackWindow(IntPtr hwnd)
    {
        // GetWindowRect returns the minimized parking position (often -32000).
        // Keep existing canvas coordinates until the window is restored.
        if (_logical.ContainsKey(hwnd) || Win32.IsIconic(hwnd) || !WindowFilter.IsManaged(hwnd, _selfPid)) return;
        var p = RealPos(hwnd);
        _logical[hwnd] = (p.X - (int)_panX, p.Y - (int)_panY);
    }

    private void ResyncLogical()
    {
        foreach (var hwnd in _logical.Keys.ToArray())
        {
            if (!Win32.IsWindow(hwnd) || Win32.IsIconic(hwnd)) continue;
            var p = RealPos(hwnd);
            _logical[hwnd] = (p.X - (int)_panX, p.Y - (int)_panY);
        }
    }
}
