namespace PaneSpace.Core.Sessions;

public sealed record WinState(string Exe, string Title, int X, int Y, int W, int H);
public sealed record DesktopIconState(string Path, float X, float Y);

public sealed class SessionState
{
    public float PanX { get; set; }
    public float PanY { get; set; }
    public List<WinState> Windows { get; set; } = new();
    public List<DesktopIconState> DesktopIcons { get; set; } = new();
}
