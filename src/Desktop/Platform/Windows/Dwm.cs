using System.Runtime.InteropServices;

namespace PaneSpace.Platform.Windows;

public static class Dwm
{
    public const uint Destination = 1, Source = 2, Opacity = 4, Visible = 8, ClientOnly = 16;
    [StructLayout(LayoutKind.Sequential)]
    public struct ThumbnailProperties
    {
        public uint Flags;
        public Win32.RECT DestinationRect, SourceRect;
        public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
    [DllImport("dwmapi.dll")] public static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail, out Win32.SIZE size);
    [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, in ThumbnailProperties properties);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, uint attribute, out Win32.RECT value, uint size);
}
