using System.Diagnostics;
using PaneSpace.Persistence;

namespace PaneSpace.Platform.Windows;

/// <summary>Temporarily suppresses Explorer icons; a companion restores them after an abrupt exit.</summary>
public sealed class DesktopIconLease : IDisposable
{
    private sealed record Recovery(int ProcessId, long StartTicks, bool Hidden, int WatcherProcessId = 0,
        bool ViewVisible = true);
    private static string RecoveryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PaneSpace", "desktop-recovery.json");
    private DesktopShell _shell;
    private readonly bool _originalHidden;
    private readonly bool _originalVisible;
    private Process? _watcher;
    private bool _disposed;
    public DesktopShell Shell => _shell;
    public DesktopIconLease()
    {
        _shell = new DesktopShell(); _originalHidden = _shell.IconsHidden;
        _originalVisible = Win32.IsWindowVisible(_shell.Window);
        try
        {
            using var recoveryLock = AcquireRecoveryLock();
            var parent = Process.GetCurrentProcess();
            var recovery = new Recovery(parent.Id, parent.StartTime.ToUniversalTime().Ticks, _originalHidden,
                ViewVisible: _originalVisible);
            if (!JsonStore.Save(RecoveryPath, recovery)) throw new IOException("无法保存桌面恢复信息。");
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--restore-desktop-icons");
            start.ArgumentList.Add(parent.Id.ToString());
            start.ArgumentList.Add(recovery.StartTicks.ToString());
            _watcher = Process.Start(start) ?? throw new InvalidOperationException("无法启动桌面恢复保护。");
            if (!JsonStore.Save(RecoveryPath, recovery with { WatcherProcessId = _watcher.Id }))
                throw new IOException("无法保存桌面恢复保护信息。");
            // NOICONS empties Explorer's view enumeration. Hide the view window instead,
            // so the live item list and original positions remain available.
            if (_shell.IconsHidden) _shell.SetIconsHidden(false);
            Win32.ShowWindow(_shell.Window, Win32.SW_HIDE);
        }
        catch { Dispose(); throw; }
    }
    public IReadOnlyList<ShellDesktopItem> Refresh()
    {
        if (!Win32.IsWindow(_shell.Window)) { _shell.Dispose(); _shell = new DesktopShell(); }
        if (_shell.IconsHidden) _shell.SetIconsHidden(false);
        if (Win32.IsWindowVisible(_shell.Window)) Win32.ShowWindow(_shell.Window, Win32.SW_HIDE);
        return _shell.ReadItems();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        bool restored = false;
        try
        {
            using var recoveryLock = AcquireRecoveryLock();
            if (!Win32.IsWindow(_shell.Window)) { _shell.Dispose(); _shell = new DesktopShell(); }
            _shell.SetIconsHidden(_originalHidden);
            Win32.ShowWindow(_shell.Window, _originalVisible ? 4 /*SW_SHOWNOACTIVATE*/ : Win32.SW_HIDE);
            restored = true;
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
        finally
        {
            _shell.Dispose();
            if (restored)
            {
                try
                {
                    if (_watcher is { HasExited: false }) { _watcher.Kill(); _watcher.WaitForExit(2000); }
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
                try { File.Delete(RecoveryPath); } catch (IOException) { }
            }
            _watcher?.Dispose();
        }
    }
    public static void RecoverAfterExit(int processId, long startTicks)
    {
        try
        {
            using var parent = Process.GetProcessById(processId);
            if (parent.StartTime.ToUniversalTime().Ticks == startTicks) parent.WaitForExit();
        }
        catch (ArgumentException) { } // Parent may already have exited before the companion starts.
        catch (InvalidOperationException) { }
        RestoreTicket(processId, startTicks);
    }
    public static void RecoverStaleDesktop()
    {
        using var recoveryLock = AcquireRecoveryLock();
        var ticket = JsonStore.Load<Recovery>(RecoveryPath);
        if (ticket == null) return;
        try
        {
            using var owner = Process.GetProcessById(ticket.ProcessId);
            if (owner.StartTime.ToUniversalTime().Ticks == ticket.StartTicks) return;
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        RestoreTicket(ticket.ProcessId, ticket.StartTicks);
    }
    private static void RestoreTicket(int processId, long startTicks)
    {
        using var recoveryLock = AcquireRecoveryLock();
        var ticket = JsonStore.Load<Recovery>(RecoveryPath);
        if (ticket == null || ticket.ProcessId != processId || ticket.StartTicks != startTicks) return;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var shell = new DesktopShell(); shell.SetIconsHidden(ticket.Hidden);
                Win32.ShowWindow(shell.Window, ticket.ViewVisible ? 4 : Win32.SW_HIDE);
                File.Delete(RecoveryPath); return;
            }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or IOException)
            { Thread.Sleep(500); }
        }
    }
    private sealed class RecoveryLock(Mutex mutex) : IDisposable
    {
        public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
    }
    private static IDisposable AcquireRecoveryLock()
    {
        var mutex = new Mutex(false, "Local\\PaneSpace.DesktopRecovery");
        try
        {
            try { mutex.WaitOne(); } catch (AbandonedMutexException) { }
            return new RecoveryLock(mutex);
        }
        catch { mutex.Dispose(); throw; }
    }
}
