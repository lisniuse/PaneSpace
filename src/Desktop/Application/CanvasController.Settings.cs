using PaneSpace.Core.Settings;
using PaneSpace.Core.Layout;
using PaneSpace.Core.Sessions;
using PaneSpace.Persistence;
using PaneSpace.Platform.Windows;
using PaneSpace.Rendering;
using PaneSpace.UI;

namespace PaneSpace.Application;

public sealed partial class CanvasController
{
    private AppSettings? _settings;
    private AppSettings Settings => _settings ??= new AppSettings();
    private SettingsForm? _settingsForm;
    private DesktopIconLease? _desktopLease;
    private DesktopIconCanvas? _desktopIcons;
    private DesktopIconSurface? _iconSurface;
    private Dictionary<string, DesktopIconState>? _iconPositions;
    private Dictionary<string, DesktopIconState> IconPositions => _iconPositions ??= new(StringComparer.OrdinalIgnoreCase);
    private long _iconsRefreshed;
    private long _desktopLaunchUntil;
    private bool _iconDragging;
    private string? _downMapIcon;
    private readonly List<(Rectangle Rect, string Path)> _mapIconHits = new();

    private void ShowSettings()
    {
        SetCanvasMode(false);
        if (_settingsForm is { IsDisposed: false }) { _settingsForm.Activate(); return; }
        _settingsForm = new SettingsForm(Settings, ApplySettings, new Size(_w, _h));
        _settingsForm.Show();
    }
    private string? ApplySettings(AppSettings next)
    {
        if (!ScreenTileLayout.TryGetContent(new Size(_w, _h), next.TileLeft, next.TileTop,
            next.TileRight, next.TileBottom, out _))
            return $"边距须为非负数；左右之和须小于 {_w}px，上下之和须小于 {_h}px。";
        var previous = Settings;
        try
        {
            if (next.DesktopIcons && _desktopIcons == null) EnableDesktopIcons();
            if (!SettingsStore.Save(next))
            {
                if (!previous.DesktopIcons) DisableDesktopIcons();
                return "设置保存失败，请检查本地数据目录的写入权限。";
            }
            _settings = next;
            _edgePan?.Reset(); _edgePanning = false;
            if (!next.DesktopIcons) DisableDesktopIcons();
            if (previous.InfiniteCanvas && !next.InfiniteCanvas) ClampContentToFinite();
            var camera = Viewport.Clamp(); _panX = camera.PanX; _panY = camera.PanY;
            ApplyPan(); SaveSession();
            return null;
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or IOException or
            InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            if (!previous.DesktopIcons) DisableDesktopIcons();
            return "桌面图标暂时无法启用，请确认 Windows 桌面已启动后重试。";
        }
    }
    private void EnableDesktopIcons()
    {
        try
        {
            _desktopLease = new DesktopIconLease();
            _desktopIcons = new DesktopIconCanvas(IconPositions) { IconSize = _desktopLease.Shell.IconSize };
            _desktopIcons.Refresh(_desktopLease.Refresh(), Viewport);
            _iconSurface = new DesktopIconSurface(_w, _h, _desktopIcons, () => Viewport);
            _desktopIcons.Changed += () => { RenderDesktopIcons(); if (_canvasMode) ComposeMapOnly(); ScheduleSave(); };
            _desktopIcons.OpenRequested += OpenDesktopItem;
            RenderDesktopIcons(); ScheduleSave();
        }
        catch { DisableDesktopIcons(); throw; }
    }
    private void DisableDesktopIcons()
    {
        _iconDragging = false;
        _iconSurface?.Dispose(); _iconSurface = null;
        _desktopIcons?.Dispose(); _desktopIcons = null;
        _desktopLease?.Dispose(); _desktopLease = null;
        if (_preview != null) { _preview.BackgroundPainter = null; _preview.Invalidate(); }
    }
    private void RenderDesktopIcons()
    {
        if (_desktopIcons == null || _iconSurface == null || _desktopLease == null) return;
        if (PreviewActive)
        {
            _iconSurface.Hide();
            if (_preview != null)
            {
                _preview.BackgroundPainter = g => _desktopIcons?.Draw(g, Viewport);
                _preview.Invalidate();
            }
        }
        else
        {
            _iconSurface.Render();
            if (!_iconSurface.Visible) _iconSurface.Show();
            _desktopLease.Shell.PlaceAboveDesktop(_iconSurface.Handle, _w, _h);
        }
    }
    private void RefreshDesktopIcons()
    {
        _iconsRefreshed = Environment.TickCount64;
        if (_desktopLease == null || _desktopIcons == null) return;
        try
        {
            _desktopIcons.Refresh(_desktopLease.Refresh(), Viewport);
            RenderDesktopIcons();
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
    }
    private string? DesktopIconHit(Point p)
    {
        if (_desktopIcons == null) return null;
        if (PreviewActive && (_preview?.Hit(p) ?? IntPtr.Zero) != IntPtr.Zero) return null;
        if (!PreviewActive)
            foreach (var hwnd in _logical.Keys)
                if (Win32.IsWindowVisible(hwnd) && !Win32.IsIconic(hwnd) && Win32.GetWindowRect(hwnd, out var rect) &&
                    p.X >= rect.Left && p.X < rect.Right && p.Y >= rect.Top && p.Y < rect.Bottom) return null;
        return _desktopIcons.Hit(p, Viewport);
    }
    private void OpenDesktopItem(string path)
    {
        SetCanvasMode(false);
        _desktopLaunchUntil = Environment.TickCount64 + 1500;
        try { DesktopShell.Open(path); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or
            System.Runtime.InteropServices.COMException)
        {
            _desktopLaunchUntil = 0;
            _tray?.ShowBalloonTip(3500, "无法打开桌面项目", "请确认文件或快捷方式仍然存在。", ToolTipIcon.Warning);
        }
    }
    private void ClampContentToFinite()
    {
        foreach (var hwnd in _logical.Keys.ToArray())
        {
            if (!Win32.GetWindowRect(hwnd, out var r)) continue;
            var logical = _logical[hwnd];
            _logical[hwnd] = (Math.Clamp(logical.X, -_w, Math.Max(-_w, 2 * _w - (r.Right - r.Left))),
                Math.Clamp(logical.Y, -_h, Math.Max(-_h, 2 * _h - (r.Bottom - r.Top))));
        }
        foreach (var (path, position) in IconPositions.ToArray())
            IconPositions[path] = position with { X = Math.Clamp(position.X, -_w, 2 * _w - 100),
                Y = Math.Clamp(position.Y, -_h, 2 * _h - 100) };
    }
}
