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
    // ---- activation gating: Ctrl arms the canvas only on the bare desktop ------------

    private bool ForegroundIsDesktop()
    {
        IntPtr fg = Win32.GetForegroundWindow();
        if (fg == IntPtr.Zero) return true;
        _cls.Clear();
        Win32.GetClassName(fg, _cls, 256);
        return _cls.ToString() is "Progman" or "WorkerW";
    }

    private void StartPolling()
    {
        _pollTimer = new WinForms.Timer { Interval = 16 };
        _pollTimer.Tick += (_, _) =>
        {
            PollTaskbarClick();
            bool ctrl = (Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) != 0;
            if (ctrl && !_lastCtrl)
            {
                if (ForegroundIsDesktop()) SetCanvasMode(true);
            }
            else if (!ctrl && _lastCtrl && !_dragging && !_iconDragging)
                SetCanvasMode(false);
            _lastCtrl = ctrl;
            if ((_dragAccX != 0 || _dragAccY != 0) && _dragging)
            {
                float dx = _dragAccX, dy = _dragAccY;
                _dragAccX = _dragAccY = 0;
                PanBy(dx, dy);
            }
            ProcessTaskbarFocus(Win32.GetForegroundWindow(), Environment.TickCount64);
            if (PreviewActive && Environment.TickCount64 - _previewRefreshed >= 100) RefreshPreview();
            if (_desktopIcons != null && Environment.TickCount64 - _iconsRefreshed >= 2000) RefreshDesktopIcons();
        };
        _pollTimer.Start();
    }

    private void SetCanvasMode(bool on)
    {
        if (on == _canvasMode) return;
        if (!on) ReturnToNative();
        _canvasMode = on;
        if (on)
        {
            ResyncLogical();
            _wheelHook = new CanvasWheelHook(_layer);
        }
        else
        {
            _wheelHook?.Dispose(); _wheelHook = null;
            _hoverHwnd = IntPtr.Zero; _dragging = false; _iconDragging = false;
            _desktopIcons?.EndDrag(); _dragAccX = _dragAccY = 0; ScheduleSave();
        }
        ComposeFull();
    }

    // ---- input on the single surface ----------------------------------------------------

    private IntPtr LayerWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32.WM_SETCURSOR:
            {
                Win32.GetCursorPos(out var gp);
                IntPtr c = IntPtr.Zero;
                if (_canvasMode)
                {
                    var p = new Point(gp.X, gp.Y);
                    c = (MapRect.Contains(p) && MapHit(p) != IntPtr.Zero) || InBar(p) ||
                        (PreviewActive && (_preview?.Hit(p) ?? IntPtr.Zero) != IntPtr.Zero) || DesktopIconHit(p) != null
                        ? _curHand : GrabCursor.Handle;
                }
                Win32.SetCursor(c != IntPtr.Zero ? c : _curArrow);
                return IntPtr.Zero;
            }
            case Win32.WM_LBUTTONDOWN:
            {
                var p = ScreenPt(lParam);
                _downPos = p;
                _dragTravel = 0;
                _downHwnd = IntPtr.Zero;
                _downMapIcon = null;
                if (_canvasMode)
                {
                    for (int i = 0; i < Buttons.Length; i++)
                        if (BarRect(i).Contains(p)) { _downHwnd = IntPtr.Zero; return IntPtr.Zero; }
                    _downMapIcon = MapIconHit(p);
                    if (_downMapIcon != null) return IntPtr.Zero;
                    _downHwnd = MapHit(p);
                    MapDbg($"DOWN p={p.X},{p.Y} down={_downHwnd.ToInt64():X} hits={_mapHits.Count} map={MapRect}");
                    if (_downHwnd != IntPtr.Zero) return IntPtr.Zero;   // map click, not drag
                    if (PreviewActive) _downHwnd = _preview?.Hit(p) ?? IntPtr.Zero;
                    if (DesktopIconHit(p) != null && _desktopIcons!.BeginDrag(p, Viewport))
                    {
                        _iconDragging = true; Win32.SetCapture(hWnd); return IntPtr.Zero;
                    }
                }
                _dragging = true;
                _lastPos = p;
                Win32.SetCapture(hWnd);
                return IntPtr.Zero;
            }
            case Win32.WM_MOUSEMOVE:
            {
                var p = ScreenPt(lParam);
                if (_iconDragging) { _desktopIcons?.DragTo(p, Viewport); return IntPtr.Zero; }
                if (_dragging)
                {
                    _dragTravel += Math.Abs(p.X - _lastPos.X) + Math.Abs(p.Y - _lastPos.Y);
                    _dragAccX += p.X - _lastPos.X;
                    _dragAccY += p.Y - _lastPos.Y;
                    _lastPos = p;
                    return IntPtr.Zero;   // actual move happens in the 16ms flush
                }
                if (_canvasMode)
                {
                    var hit = MapHit(p);
                    if (hit != _hoverHwnd) { _hoverHwnd = hit; ComposeMapOnly(); }
                }
                return IntPtr.Zero;
            }
            case Win32.WM_LBUTTONUP:
            {
                var p = ScreenPt(lParam);
                if (_iconDragging)
                {
                    _desktopIcons?.DragTo(p, Viewport); _desktopIcons?.EndDrag();
                    _iconDragging = false; Win32.ReleaseCapture(); ScheduleSave();
                    if ((Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) == 0) SetCanvasMode(false);
                    return IntPtr.Zero;
                }
                if (_dragging)
                {
                    _dragging = false;
                    Win32.ReleaseCapture();
                    // flush any residual drag delta so the NEXT drag starts clean
                    if (_dragAccX != 0 || _dragAccY != 0)
                    {
                        float dx = _dragAccX, dy = _dragAccY;
                        _dragAccX = _dragAccY = 0;
                        PanBy(dx, dy);
                    }
                    if (PreviewActive && _downHwnd != IntPtr.Zero && _dragTravel <= 8 &&
                        Math.Abs(p.X - _downPos.X) <= 5 && Math.Abs(p.Y - _downPos.Y) <= 5)
                        FocusWindowOnMap(_downHwnd);
                    _downHwnd = IntPtr.Zero;
                    // Ctrl may have been released while the mouse was still down
                    if ((Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) == 0) SetCanvasMode(false);
                    return IntPtr.Zero;
                }
                if (_canvasMode)
                {
                    for (int i = 0; i < Buttons.Length; i++)
                        if (BarRect(i).Contains(p) && _downPos == p || BarRect(i).Contains(p))
                        {
                            if (i == 0) AutoArrange(); else if (i == 1) GatherToCentreScreen(); else ResetPan();
                            return IntPtr.Zero;
                        }
                    // be forgiving with tiny map blocks: pressing one and releasing a
                    // few px away inside the map still counts as a click on it
                    MapDbg($"UP p={p.X},{p.Y} down={_downHwnd.ToInt64():X} inMap={MapRect.Contains(p)} drag={_dragging}");
                    if (_downHwnd != IntPtr.Zero && MapRect.Contains(p)) FocusWindowOnMap(_downHwnd);
                    if (_downMapIcon != null && MapRect.Contains(p) && IconPositions.TryGetValue(_downMapIcon, out var icon))
                        PanTo(_w / 2f - icon.X - 50, _h / 2f - icon.Y - 46);
                    _downHwnd = IntPtr.Zero;
                    _downMapIcon = null;
                }
                return IntPtr.Zero;
            }
            case Win32.WM_MOUSEWHEEL:
            {
                short d = unchecked((short)((long)wParam >> 16));
                if (_canvasMode) ZoomAt(ScreenPt(lParam), d);
                return IntPtr.Zero;
            }
            case Win32.WM_LBUTTONDBLCLK:
            {
                var p = ScreenPt(lParam);
                if (_canvasMode && !InBar(p) && !MapRect.Contains(p) && DesktopIconHit(p) != null)
                {
                    _iconDragging = false; Win32.ReleaseCapture();
                    _desktopIcons?.OpenAt(p, Viewport);
                    return IntPtr.Zero;
                }
                if (_canvasMode) return LayerWndProc(hWnd, Win32.WM_LBUTTONDOWN, wParam, lParam);
                break;
            }
        }
        return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private bool InBar(Point p) =>
        p.Y >= _h - BAR_H - 24 && p.Y <= _h - 24 &&
        p.X >= (_w - BarTotalWidth) / 2 && p.X <= (_w + BarTotalWidth) / 2;

    private IntPtr MapHit(Point screenPt)
    {
        var mr = MapRect;
        if (!mr.Contains(screenPt)) return IntPtr.Zero;
        // exact hit first
        for (int i = _mapHits.Count - 1; i >= 0; i--)
            if (_mapHits[i].Rect.Contains(screenPt)) return _mapHits[i].Hwnd;
        // proximity fallback (44px): blocks are tiny; clicking *at* one should work
        IntPtr best = IntPtr.Zero; int bestD = 44 * 44;
        for (int i = _mapHits.Count - 1; i >= 0; i--)
        {
            var rc = _mapHits[i].Rect;
            int cx = Math.Clamp(screenPt.X, rc.X, rc.Right);
            int cy = Math.Clamp(screenPt.Y, rc.Y, rc.Bottom);
            int dx = screenPt.X - cx, dy = screenPt.Y - cy;
            int d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = _mapHits[i].Hwnd; }
        }
        return best;
    }

    private string? MapIconHit(Point p)
    {
        if (!MapRect.Contains(p)) return null;
        if (_mapHits.Any(hit => hit.Rect.Contains(p))) return null;
        for (int i = _mapIconHits.Count - 1; i >= 0; i--)
            if (_mapIconHits[i].Rect.Contains(p)) return _mapIconHits[i].Path;
        return null;
    }

    private static Point ScreenPt(IntPtr lParam)
    {
        long v = lParam.ToInt64();
        return new Point(unchecked((short)(v & 0xFFFF)), unchecked((short)((v >> 16) & 0xFFFF)));
    }
}
