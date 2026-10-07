using PaneSpace.Core.Viewport;
using PaneSpace.Platform.Windows;

namespace PaneSpace.Application;

public sealed partial class CanvasController
{
    private EdgePan? _edgePan;
    private bool _edgePanning;

    private void PollEdgePan(long now)
    {
        if (!Settings.EdgePanning) { _edgePan?.Reset(); _edgePanning = false; return; }
        if (!Win32.GetCursorPos(out var cursor)) { _edgePan?.Reset(); _edgePanning = false; return; }
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, _w, _h);
        bool paused = _dragging || _iconDragging || _settingsForm is { Visible: true } ||
            _tray?.ContextMenuStrip?.Visible == true || _pendingTaskbarFocus != IntPtr.Zero ||
            now < _taskbarClickUntil || now < _desktopLaunchUntil ||
            (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0 ||
            (Win32.GetAsyncKeyState(0x02 /* VK_RBUTTON */) & 0x8000) != 0 ||
            (Win32.GetAsyncKeyState(0x04 /* VK_MBUTTON */) & 0x8000) != 0 || CursorIsOnTaskbar();
        var gui = new Win32.GUITHREADINFO { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.GUITHREADINFO>() };
        if (Win32.GetGUIThreadInfo(0, ref gui) && (gui.Flags & 0x1Eu) != 0)
            paused = true; // Move/size loops and native popup/system menus.
        ProcessEdgePan(new Point(cursor.X, cursor.Y), area, now, paused);
    }

    private void ProcessEdgePan(Point cursor, Rectangle area, long now, bool paused)
    {
        var delta = (_edgePan ??= new()).Step(cursor, area, now, Settings.EdgePanning && !paused);
        if (delta == PointF.Empty) { _edgePanning = false; return; }
        if (!_edgePanning) ResyncLogical(); // Adopt manual window moves before an edge-scroll session.
        _edgePanning = true;
        var next = Viewport.Drag(delta.X, delta.Y);
        if (next.PanX == _panX && next.PanY == _panY) return;
        _panX = next.PanX; _panY = next.PanY;
        ApplyPan();
    }
}
