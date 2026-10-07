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
    // ---- persistence -----------------------------------------------------------

    private void ScheduleSave()
    {
        if (_saveTimer == null) return;
        _saveTimer.Stop();
        _saveTimer.Start();          // debounced: fires 1.2s after the last change
    }

    private void SaveSession()
    {
        var st = new SessionState { PanX = _panX, PanY = _panY, DesktopIcons = IconPositions.Values.ToList() };
        foreach (var (hwnd, l) in _logical)
        {
            if (!Win32.IsWindow(hwnd)) continue;
            Win32.GetWindowRect(hwnd, out var r);
            st.Windows.Add(new WinState(ExeOf(hwnd), TitleOf(hwnd), l.X, l.Y,
                r.Right - r.Left, r.Bottom - r.Top));
        }
        StateStore.Save(st);
    }

    /// <summary>Re-apply the saved layout: match today's windows against saved
    /// identities (exe+title, then nearest position), assign logical coords,
    /// then restore the camera and move everything into place.</summary>
    private void RestoreSession()
    {
        var st = StateStore.Load();
        if (st == null) return;
        foreach (var icon in st.DesktopIcons)
            if (!string.IsNullOrWhiteSpace(icon.Path) && float.IsFinite(icon.X) && float.IsFinite(icon.Y)) IconPositions[icon.Path] = icon;

        var consumed = new bool[st.Windows.Count];
        // pass 1: exact identity (exe + title)
        var pending = _logical.Keys.ToArray();
        foreach (var hwnd in pending)
            TryMatch(hwnd, st, consumed, exactOnly: true);
        // pass 2: same exe, nearest physical position (title changed / tabs)
        foreach (var hwnd in _logical.Keys.ToArray())
            if (!_matched.Contains(hwnd)) TryMatch(hwnd, st, consumed, exactOnly: false);

        var camera = (Viewport with { PanX = st.PanX, PanY = st.PanY }).Clamp();
        _panX = camera.PanX; _panY = camera.PanY;
        ApplyPan();
    }
    private readonly HashSet<IntPtr> _matched = new();

    private void TryMatch(IntPtr hwnd, SessionState st, bool[] consumed, bool exactOnly)
    {
        var cur = RealPos(hwnd);
        string exe = ExeOf(hwnd), title = TitleOf(hwnd);
        int best = -1, bestD = int.MaxValue;
        for (int i = 0; i < st.Windows.Count; i++)
        {
            if (consumed[i]) continue;
            var w = st.Windows[i];
            bool idOk = string.Equals(w.Exe, exe, StringComparison.OrdinalIgnoreCase) &&
                        (exactOnly ? string.Equals(w.Title, title, StringComparison.Ordinal) : true);
            if (!idOk) continue;
            int sx = w.X + (int)st.PanX, sy = w.Y + (int)st.PanY;   // saved physical pos
            int d = Math.Abs(sx - cur.X) + Math.Abs(sy - cur.Y);
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best >= 0 && (exactOnly || bestD < 400))
        {
            consumed[best] = true;
            _matched.Add(hwnd);
            _logical[hwnd] = (st.Windows[best].X, st.Windows[best].Y);
        }
    }

    private static string ExeOf(IntPtr hwnd)
    {
        Win32.GetWindowThreadProcessId(hwnd, out uint pid);
        try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
        catch { return ""; }
    }

    private string TitleOf(IntPtr hwnd)
    {
        _title.Clear();
        Win32.GetWindowTextW(hwnd, _title, 200);
        return _title.ToString();
    }
}
