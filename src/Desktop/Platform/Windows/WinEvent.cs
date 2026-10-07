using System.Runtime.InteropServices;

namespace PaneSpace.Platform.Windows;

/// <summary>Out-of-context hooks for window lifetime, activation and restoration.</summary>
public sealed class WinEvent : IDisposable
{
    public delegate void WinEventDelegate(IntPtr hook, uint type, IntPtr hwnd,
        int idObject, int idChild, uint thread, uint time);

    private const uint WINEVENT_OUTOFCONTEXT = 0;

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod,
        WinEventDelegate cb, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr h);

    private readonly WinEventDelegate _proc;
    private IntPtr _h1, _h2, _h3;

    /// <summary>(eventType, hwnd, idObject, idChild)</summary>
    public event Action<uint, IntPtr, int, int>? Raised;

    public WinEvent()
    {
        _proc = (h, type, hwnd, obj, child, t, time) =>
        {
            if (hwnd != IntPtr.Zero) Raised?.Invoke(type, hwnd, obj, child);
        };
        _h1 = SetWinEventHook(Win32.EVENT_OBJECT_CREATE, Win32.EVENT_OBJECT_DESTROY, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
        _h2 = SetWinEventHook(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
        _h3 = SetWinEventHook(Win32.EVENT_SYSTEM_MINIMIZEEND, Win32.EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    public void Dispose()
    {
        if (_h1 != IntPtr.Zero) UnhookWinEvent(_h1);
        if (_h2 != IntPtr.Zero) UnhookWinEvent(_h2);
        if (_h3 != IntPtr.Zero) UnhookWinEvent(_h3);
    }
}
