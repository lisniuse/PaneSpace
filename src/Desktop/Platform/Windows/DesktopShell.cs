using System.Runtime.InteropServices;

namespace PaneSpace.Platform.Windows;

public sealed record ShellDesktopItem(string Path, string Name, Point Position);

/// <summary>Reads the actual desktop view, including virtual items and Explorer's icon positions.</summary>
public sealed class DesktopShell : IDisposable
{
    private IntPtr _view;
    public IntPtr Window { get; private set; }
    private const uint NoIcons = 0x1000;
    private static readonly Guid FolderView2 = new("1af3a467-214f-4298-908e-06b03e0b39f9");
    // Slots follow the Windows SDK IShellBrowser/IFolderView2 definitions, including IUnknown.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int OutPointer(IntPtr self, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryService(IntPtr self, in Guid service, in Guid iid, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CountItems(IntPtr self, uint flags, out int count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ItemAt(IntPtr self, int index, out IntPtr pidl);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ItemPosition(IntPtr self, IntPtr pidl, out Win32.POINT point);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetFlags(IntPtr self, out uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetFlags(IntPtr self, uint mask, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ViewMode(IntPtr self, out uint mode, out int size);
    private static T Method<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    [DllImport("shell32.dll")] private static extern int SHGetNameFromIDList(IntPtr pidl, uint nameType, out IntPtr name);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr binding, out IntPtr pidl, uint attributes, out uint result);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(IntPtr pidl, uint attributes, out ShellFileInfo info, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref Win32.POINT point);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    public DesktopShell()
    {
        object? windows = null, desktop = null;
        IntPtr provider = IntPtr.Zero, browser = IntPtr.Zero, shellView = IntPtr.Zero, unknown = IntPtr.Zero;
        try
        {
            windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"))!);
            dynamic collection = windows!;
            object location = 0, root = null!;
            int hwnd = 0;
            desktop = collection.FindWindowSW(ref location, ref root, 8 /*SWC_DESKTOP*/, out hwnd, 1);
            if (desktop == null) throw new InvalidOperationException("Windows 桌面尚未就绪，请稍后重试。");
            unknown = Marshal.GetIUnknownForObject(desktop);
            var providerId = new Guid("6D5140C1-7436-11CE-8034-00AA006009FA");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in providerId, out provider));
            var service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
            var browserId = new Guid("000214E2-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(Method<QueryService>(provider, 3)(provider, in service, in browserId, out browser));
            Marshal.ThrowExceptionForHR(Method<OutPointer>(browser, 15)(browser, out shellView));
            Marshal.ThrowExceptionForHR(Method<OutPointer>(shellView, 3)(shellView, out var viewWindow));
            Window = viewWindow;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(shellView, in FolderView2, out _view));
        }
        finally
        {
            foreach (var pointer in new[] { shellView, browser, provider, unknown })
                if (pointer != IntPtr.Zero) Marshal.Release(pointer);
            if (desktop != null && Marshal.IsComObject(desktop)) Marshal.FinalReleaseComObject(desktop);
            if (windows != null && Marshal.IsComObject(windows)) Marshal.FinalReleaseComObject(windows);
        }
    }
    public bool IconsHidden
    {
        get
        {
            Marshal.ThrowExceptionForHR(Method<GetFlags>(_view, 25)(_view, out var flags));
            return (flags & NoIcons) != 0;
        }
    }
    public void SetIconsHidden(bool hidden) =>
        Marshal.ThrowExceptionForHR(Method<SetFlags>(_view, 24)(_view, NoIcons, hidden ? NoIcons : 0));
    public int IconSize => Method<ViewMode>(_view, 36)(_view, out _, out var size) >= 0 ? Math.Clamp(size, 16, 256) : 48;
    public IReadOnlyList<ShellDesktopItem> ReadItems()
    {
        Marshal.ThrowExceptionForHR(Method<CountItems>(_view, 7)(_view, 2 /*SVGIO_ALLVIEW*/, out var count));
        var items = new List<ShellDesktopItem>(count);
        var origin = new Win32.POINT();
        ClientToScreen(Window, ref origin);
        for (int i = 0; i < count; i++)
        {
            if (Method<ItemAt>(_view, 6)(_view, i, out var pidl) < 0 || pidl == IntPtr.Zero) continue;
            try
            {
                string? path = Name(pidl, 0x80028000 /*DESKTOPABSOLUTEPARSING*/);
                string? name = Name(pidl, 0);
                if (path == null || name == null) continue;
                Marshal.ThrowExceptionForHR(Method<ItemPosition>(_view, 11)(_view, pidl, out var position));
                items.Add(new ShellDesktopItem(path, name, new Point(position.X + origin.X, position.Y + origin.Y)));
            }
            finally { Marshal.FreeCoTaskMem(pidl); }
        }
        return items;
    }
    public static Bitmap LoadIcon(string path)
    {
        IntPtr pidl = IntPtr.Zero;
        try
        {
            if (SHParseDisplayName(path, IntPtr.Zero, out pidl, 0, out _) >= 0 &&
                SHGetFileInfo(pidl, 0, out var info, (uint)Marshal.SizeOf<ShellFileInfo>(), 0x100 | 8 | 0x8000) != IntPtr.Zero)
            {
                try { using var icon = Icon.FromHandle(info.Icon); return icon.ToBitmap(); }
                finally { DestroyIcon(info.Icon); }
            }
        }
        finally { if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl); }
        return SystemIcons.Application.ToBitmap();
    }
    public static void Open(string path)
    {
        object? windows = null, desktop = null, document = null, application = null;
        try
        {
            // Ask the existing Explorer to open the item, retaining the user's normal privilege level.
            windows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"))!);
            dynamic collection = windows!;
            object location = 0, root = null!; int hwnd = 0;
            desktop = collection.FindWindowSW(ref location, ref root, 8, out hwnd, 1);
            if (desktop == null) throw new InvalidOperationException("Windows 桌面尚未就绪。");
            document = ((dynamic)desktop).Document;
            application = ((dynamic)document!).Application;
            ((dynamic)application!).ShellExecute(path.StartsWith("::", StringComparison.Ordinal) ? "shell:" + path : path,
                "", "", "open", 1);
        }
        finally
        {
            foreach (var value in new[] { application, document, desktop, windows })
                if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }
    }
    public void PlaceAboveDesktop(IntPtr surface, int width, int height)
    {
        var desktop = Win32.GetAncestor(Window, Win32.GA_ROOT);
        IntPtr previous = IntPtr.Zero;
        Win32.EnumWindows((hwnd, _) =>
        {
            if (hwnd == desktop) return false;
            if (hwnd != surface) previous = hwnd;
            return true;
        }, IntPtr.Zero);
        Win32.SetWindowPos(surface, previous, 0, 0, width, height, Win32.SWP_NOACTIVATE);
    }
    private static string? Name(IntPtr pidl, uint kind)
    {
        if (SHGetNameFromIDList(pidl, kind, out var text) < 0) return null;
        try { return Marshal.PtrToStringUni(text); }
        finally { Marshal.FreeCoTaskMem(text); }
    }
    public void Dispose()
    {
        if (_view != IntPtr.Zero) Marshal.Release(_view);
        _view = IntPtr.Zero;
    }
}
