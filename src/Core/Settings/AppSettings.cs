namespace PaneSpace.Core.Settings;

public sealed record AppSettings(bool InfiniteCanvas = false, bool DesktopIcons = false, bool EdgePanning = false,
    int TileTop = 28, int TileRight = 28, int TileBottom = 80, int TileLeft = 28);
