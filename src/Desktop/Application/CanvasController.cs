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

/// <summary>
/// Native windows are moved at 100%; other scales use live DWM window previews.
/// One layered window renders the grid + button bar + interactive minimap
/// via UpdateLayeredWindow per-pixel alpha — no z-order fights, and alpha=0 areas
/// are naturally click-through so no WS_EX_TRANSPARENT toggling either.
/// On the bare desktop, hold Ctrl: drag to slide all windows, hover/click the
/// minimap to jump the camera, buttons for arrange/gather/reset.
/// </summary>
public sealed partial class CanvasController : IDisposable
{
    private const int MAP_W = 224, MAP_MARGIN = 14, TITLE_H = 26;
    private const int BAR_H = 42, BAR_PAD = 10, BAR_ITEM_PAD = 28;
    private const byte GRID_A = 46, SOLID_A = 235;

    private readonly int _w, _h;
    private readonly uint _selfPid = (uint)Environment.ProcessId;

    private readonly Dictionary<IntPtr, (int X, int Y)> _logical = new();
    private float _panX, _panY;
    private readonly List<IntPtr> _dead = new();

    private IntPtr _layer = IntPtr.Zero;
    private Win32.WndProcDelegate? _layerProc;
    private WinEvent? _events;
    private CanvasWheelHook? _wheelHook;
    private WinForms.NotifyIcon? _tray;
    private Icon? _trayIcon;
    private WinForms.Timer? _pollTimer;
    private WinForms.Timer? _saveTimer;
    private bool _canvasMode, _dragging, _lastCtrl;
    private bool _disposed;
    private bool _lastLeftButton;
    private long _taskbarClickUntil, _focusNotBefore, _focusExpires;
    private IntPtr _pendingTaskbarFocus;
    private Point _lastPos, _downPos;
    private IntPtr _downHwnd;
    private readonly StringBuilder _cls = new(256), _title = new(256);

    // GDI+ draws into a premultiplied bitmap; the memory DC presents the same pixels.
    private IntPtr _screenDc, _memDc, _hbm, _oldBmp;
    private Bitmap? _frame;
    private float _dragAccX, _dragAccY;
    private int _dragTravel;
    private IntPtr _curHand, _curArrow;
    private readonly List<(Rectangle Rect, IntPtr Hwnd)> _mapHits = new();
    private IntPtr _hoverHwnd;
    private int _ulwErrCount;

    private static readonly string[] Buttons = { "自动排列(全画布)", "全部搬回中心屏", "复位" };

    private Cursor? _grabCursor;
    private Cursor GrabCursor => _grabCursor ??= GrabCursorFactory.Create();

    public CanvasController()
    {
        Win32.SetProcessDpiAwarenessContext(Win32.DPI_PER_MONITOR_V2);
        _w = Win32.GetSystemMetrics(0);
        _h = Win32.GetSystemMetrics(1);
        _settings = SettingsStore.Load();

        CreateLayer();
        StartPolling();

        _events = new WinEvent();
        _events.Raised += OnWinEvent;
        SeedWindows();
        RestoreSession();

        _saveTimer = new WinForms.Timer { Interval = 1200 };   // debounce writes
        _saveTimer.Tick += (_, _) => { _saveTimer!.Stop(); SaveSession(); };

        _trayIcon = BrandIcon.Load();
        _tray = new WinForms.NotifyIcon
        {
            Icon = _trayIcon,
            Visible = true,
            Text = "PaneSpace（桌面按 Ctrl：拖动平移、滚轮缩放）",
        };
        var menu = new WinForms.ContextMenuStrip();
        var home = new WinForms.ToolStripMenuItem("画布归位");
        home.Click += (_, _) => ResetPan();
        menu.Items.Add(home);
        var settings = new WinForms.ToolStripMenuItem("设置…");
        settings.Click += (_, _) => ShowSettings();
        menu.Items.Add(settings);
        var quit = new WinForms.ToolStripMenuItem("退出 PaneSpace（收回窗口）");
        quit.Click += (_, _) => WinForms.Application.Exit();
        menu.Items.Add(quit);
        _tray.ContextMenuStrip = menu;
        if (Settings.DesktopIcons)
        {
            try { EnableDesktopIcons(); }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or IOException or
                InvalidOperationException or System.ComponentModel.Win32Exception)
            { _tray.ShowBalloonTip(4000, "桌面图标未启用", "请在设置中重新保存桌面图标选项。", WinForms.ToolTipIcon.Warning); }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SaveSession();
        _saveTimer?.Dispose();
        _pollTimer?.Dispose();
        _events?.Dispose();
        _wheelHook?.Dispose();
        _settingsForm?.Dispose();
        DisableDesktopIcons();
        _preview?.Dispose();
        // Keep the saved canvas layout, but leave the live windows reachable after exit.
        WindowRecovery.ReturnToScreens(_logical.Keys.ToArray());
        if (_layer != IntPtr.Zero) Win32.DestroyWindow(_layer);
        _frame?.Dispose();                           // release wrapper before its native pixels
        if (_memDc != IntPtr.Zero) { Win32.SelectObject(_memDc, _oldBmp); Win32.DeleteObject(_hbm); Win32.DeleteDC(_memDc); Win32.ReleaseDC(IntPtr.Zero, _screenDc); }
        _tray?.Dispose();
        _trayIcon?.Dispose();
    }
}
