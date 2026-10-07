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
    private HashSet<IntPtr>? _parked;
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
                    (TaskbarWasClicked() || Environment.TickCount64 < _desktopLaunchUntil))
                    QueueTaskbarFocus(hwnd, Environment.TickCount64);
                break;
            case Win32.EVENT_OBJECT_DESTROY:
                if (Win32.IsWindow(hwnd)) break;      // fake-destroy broadcast
                _logical.Remove(hwnd);
                _parked?.Remove(hwnd);
                _tiledWindows?.Remove(hwnd);
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
        _logical[hwnd] = (p.X - (int)(PreviewActive ? _nativePanX : _panX),
            p.Y - (int)(PreviewActive ? _nativePanY : _panY));
    }

    private void ResyncLogical()
    {
        if (PreviewActive) return; // native windows stay stationary while the preview camera moves
        foreach (var hwnd in _logical.Keys.ToArray())
        {
            if (!Win32.IsWindow(hwnd) || Win32.IsIconic(hwnd) || _parked?.Contains(hwnd) == true) continue;
            var p = RealPos(hwnd);
            var logical = _logical[hwnd];
            // Native positions are rounded. Repeated timed edge-scroll sessions must not
            // adopt that rounding as a manual move and gradually drift the world layout.
            if (p.X == Math.Round((double)logical.X + _panX) && p.Y == Math.Round((double)logical.Y + _panY)) continue;
            _logical[hwnd] = (p.X - (int)_panX, p.Y - (int)_panY);
        }
    }
}
