using System.Runtime.InteropServices;

namespace PaneSpace.Platform.Windows;

/// <summary>Routes wheel input to the canvas even when inactive-window scrolling is disabled.</summary>
public sealed class CanvasWheelHook : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseData
    {
        public Win32.POINT Point;
        public uint Data, Flags, Time;
        public UIntPtr ExtraInfo;
    }
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    private readonly Win32.HookProc _callback;
    private IntPtr _hook;
    public CanvasWheelHook(IntPtr layer)
    {
        _callback = (code, message, data) =>
        {
            if (code >= 0 && message == (IntPtr)Win32.WM_MOUSEWHEEL)
            {
                var mouse = Marshal.PtrToStructure<MouseData>(data);
                if (Win32.WindowFromPoint(mouse.Point) == layer)
                {
                    var position = (IntPtr)((mouse.Point.Y << 16) | (mouse.Point.X & 0xffff));
                    // Queue rendering outside the low-level hook so it returns promptly.
                    if (PostMessage(layer, Win32.WM_MOUSEWHEEL, (IntPtr)(long)mouse.Data, position))
                        return (IntPtr)1;
                }
            }
            return Win32.CallNextHookEx(_hook, code, message, data);
        };
        _hook = Win32.SetWindowsHookEx(14 /*WH_MOUSE_LL*/, _callback, Win32.GetModuleHandle(null), 0);
    }
    public void Dispose()
    {
        if (_hook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }
}
