using PaneSpace.Core.Viewport;
using PaneSpace.Platform.Windows;
using PaneSpace.Rendering;

namespace PaneSpace.Application;

public sealed partial class CanvasController
{
    private EdgePan? _edgePan;
    private bool _edgePanning;
    private EdgePanCursorSurface? _edgeCursor;
    private Point _edgeCursorDirection;
    private long _edgeInputUntil;

    private void ResetEdgePan()
    {
        _edgePan?.Reset(); _edgePanning = false; _edgeCursorDirection = Point.Empty;
        _edgeCursor?.HideFeedback();
    }

    private void PollEdgePan(long now)
    {
        if (!Settings.EdgePanning)
        {
            ResetEdgePan(); _edgeCursor?.Dispose(); _edgeCursor = null;
            return;
        }
        if (!Win32.GetCursorPos(out var cursor)) { ResetEdgePan(); return; }
        // Use physical bounds: the taskbar occupies the bottom of the working area.
        var area = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, _w, _h);
        bool paused = _dragging || _iconDragging || _settingsForm is { Visible: true } ||
            _tray?.ContextMenuStrip?.Visible == true || _pendingTaskbarFocus != IntPtr.Zero ||
            now < _taskbarClickUntil || now < _desktopLaunchUntil || now < _edgeInputUntil ||
            (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0 ||
            (Win32.GetAsyncKeyState(0x02 /* VK_RBUTTON */) & 0x8000) != 0 ||
            (Win32.GetAsyncKeyState(0x04 /* VK_MBUTTON */) & 0x8000) != 0 ||
            (Win32.GetAsyncKeyState(0x05 /* VK_XBUTTON1 */) & 0x8000) != 0 ||
            (Win32.GetAsyncKeyState(0x06 /* VK_XBUTTON2 */) & 0x8000) != 0;
        var gui = new Win32.GUITHREADINFO { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.GUITHREADINFO>() };
        if (Win32.GetGUIThreadInfo(0, ref gui) && (gui.Flags & 0x1Eu) != 0)
            paused = true; // Move/size loops and native popup/system menus.
        ProcessEdgePan(new Point(cursor.X, cursor.Y), area, now, paused);
        if (_edgeCursorDirection == Point.Empty) { _edgeCursor?.HideFeedback(); return; }
        _edgeCursor ??= new EdgePanCursorSurface(() =>
        {
            _edgeInputUntil = Environment.TickCount64 + EdgePan.DwellMilliseconds;
            ResetEdgePan();
        });
        _edgeCursor.ShowFeedback(area, _edgeCursorDirection);
    }

    private void ProcessEdgePan(Point cursor, Rectangle area, long now, bool paused)
    {
        _edgeCursorDirection = Point.Empty;
        var delta = (_edgePan ??= new()).Step(cursor, area, now, Settings.EdgePanning && !paused, Settings.EdgePanSpeed);
        if (delta == PointF.Empty) { _edgePanning = false; return; }
        if (!_edgePanning) ResyncLogical(); // Adopt manual window moves before an edge-scroll session.
        _edgePanning = true;
        var next = Viewport.Drag(delta.X, delta.Y);
        if (next.PanX == _panX && next.PanY == _panY) return;
        _edgeCursorDirection = new Point(-_edgePan.Direction.X, -_edgePan.Direction.Y);
        _panX = next.PanX; _panY = next.PanY;
        ApplyPan();
    }
}
