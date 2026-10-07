<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/branding/logo-dark.svg">
    <img src="assets/branding/logo.svg" alt="PaneSpace — a canvas for your windows" width="420">
  </picture>
</p>

<p align="center"><strong>English</strong> · <a href="README.zh-CN.md">简体中文</a></p>
<p align="center">Windows desktop · .NET 10 · Experimental</p>

# PaneSpace

PaneSpace (窗域) gives your application windows a navigable desktop canvas. Hold
**Ctrl while the desktop is foreground**, then drag to explore a workspace spanning
**3 × 3 primary-screen areas** by default. Enable **Infinite canvas** in the tray
settings to remove that camera boundary. A minimap helps you find windows and jump back to them.

At 100%, panning moves real windows and keeps their sizes intact. Wheel zoom uses
live window previews from 25% to 200%, anchored at the pointer. Release Ctrl or
click a preview to return to 100% and interact with the original application.

## Features

- **Drag to pan:** move across the canvas without rearranging individual windows.
- **Optional edge panning:** move the pointer to a working-area edge and pause briefly
  to scroll the camera without Ctrl. Disabled by default and configurable in settings.
- **Optional infinite canvas:** keep panning in any direction, with a minimap
  that fits the camera and all content. Automatic arrangement still uses nine screens.
- **Optional desktop icons:** include desktop files, shortcuts, folders and Shell
  items in the canvas; drag to reposition or double-click to open them.
- **Wheel zoom:** browse live window contents at 25%–200%; the grid and minimap
  viewport follow the camera. Window sizes remain unchanged.
- **Interactive minimap:** click a window block to center and activate that window.
- **Taskbar focus:** restore or activate a window from its taskbar icon or thumbnail
  and the camera follows it, within the canvas bounds.
- **Masonry arrangement:** pack mixed-size windows into the lowest available space,
  allowing wide windows to span columns without resizing them.
- **Screen tiling:** resize each window to one primary-screen cell, reserving 28px
  at the top and sides and 80px at the bottom for the taskbar,
  then arrange the cells in three columns. Infinite mode adds rows beyond nine windows.
- **Layout persistence:** remember the camera, window positions, icon positions,
  and settings across sessions.
- **Tray controls:** open settings, reset the camera or exit from the PaneSpace tray icon.
- **Safe exit:** save the canvas layout, then return its windows to visible screen
  work areas, along with their visible dialogs. Minimized windows stay minimized
  and receive reachable restore positions.

## Build and run

Development requires **Windows, PowerShell 7, and the .NET 10 SDK**. The production
app is a **self-contained Windows x64 single EXE**, including the .NET runtime.
Copy `dist/PaneSpace.exe` to run it; no separate runtime installation is needed.

```powershell
git clone https://github.com/lisniuse/PaneSpace.git
cd PaneSpace
pwsh -NoProfile -File scripts/publish.ps1
.\dist\PaneSpace.exe
```

Accept the administrator prompt on startup. The application requests elevation to
manage elevated application windows. It runs in the tray rather than opening a
main window.

Publishing builds and checks the solution, stops running PaneSpace / legacy
CamCanvas processes, and always writes one **`dist/PaneSpace.exe`**. `build.cmd` provides the same
publishing entry point. Build output is excluded from this repository.

## Controls

| Action | Result |
| --- | --- |
| Hold Ctrl with the desktop foreground | Show the grid, toolbar, and minimap |
| Drag with the left mouse button | Pan the canvas |
| Mouse wheel in canvas mode | Zoom at the pointer, from 25% to 200% |
| Click a scaled window preview | Return to 100%, center and activate the window |
| Release Ctrl after zooming | Return to 100% around the current view center |
| Click a minimap window block | Center and activate that window |
| Activate a window from the taskbar | Follow the restored or activated window |
| 自动排列(全画布) | Arrange visible, non-minimized windows inside the 3 × 3 area, in either mode |
| 整屏平铺 | Fit each window in a screen cell: 28px top/side margins, 80px bottom reservation |
| Drag a canvas desktop icon | Reposition that icon and save its world position |
| Double-click a canvas desktop icon | Open the file, folder or shortcut |
| Click a purple minimap icon block | Center the camera on that desktop icon |
| Tray → 设置… | Configure infinite canvas, desktop icons and edge panning |
| Pointer near a working-area edge, with edge panning enabled | Scroll the camera after a short dwell, without Ctrl |
| 全部搬回中心屏 | Move the window group back to the center screen |
| 复位 button / Tray → 画布归位 | Reset to 100% and zero camera offset |
| Tray → Exit | Save the canvas layout and bring its windows back onto the screens |

The current toolbar and tray menus use Chinese labels. If an arrangement cannot
fit all eligible windows, PaneSpace keeps the existing layout and shows a notice.
Esc does not reset the camera or move windows; use the reset button or tray menu.

**整屏平铺 — Screen tiling** returns to 100% and centers the first cell in the
viewport. Each cell reserves 28px at the top and sides and 80px below for the
taskbar. Adjacent windows have a 56px horizontal gap and a 108px vertical gap.
Finite mode supports up to nine windows and preserves the entire layout
if that limit is exceeded; infinite mode continues in additional rows. Maximized
windows are restored before resizing; hidden and minimized windows are excluded.
Applications with size constraints keep their accepted size, centered in the cell's available area,
and a tray notice reports constrained or unsuccessful adjustments. Desktop icon
positions are unchanged. The masonry button retains its original behavior.
Following a tiled window from the minimap or taskbar keeps this reservation;
manually resizing it returns to ordinary window centering.

## Settings

Right-click the tray icon, choose **设置…**, and save any combination of independent options:

- **无限画布 — Infinite canvas:** remove the nine-screen camera boundary. Turning
  it off brings content outside the finite canvas back inside its bounds.
  Automatic arrangement always retains the original 3 × 3 area.
- **桌面图标 — Desktop icons:** replace the native desktop icon view with canvas
  icons while PaneSpace runs. Icons follow pan and zoom, keep their saved positions,
  and support dragging and double-click opening. Added or removed items are refreshed.
- **屏幕边缘平移 — Edge panning:** hold the pointer within 12px of a primary-screen
  working-area edge for 250ms to scroll in that direction, including diagonally.
  The bottom edge sits above the taskbar. Scrolling pauses while dragging, pressing
  mouse buttons, using menus or the taskbar, and while settings are open. It works
  in native and zoomed views and respects the finite canvas boundary.

All options start disabled; older settings leave edge panning disabled. Turning desktop icons off or exiting restores the
original Windows desktop visibility and layout. A recovery companion also restores
the native view after an abrupt app exit. Desktop files are not moved or renamed.

## Development

```powershell
pwsh -NoProfile -File scripts/build.ps1
pwsh -NoProfile -File scripts/test.ps1
```

The tests reference the actual Core library and cover shortest-column placement,
wide windows, bounds, insufficient space, and 1,000 deterministic random layouts.
The test runner is an executable; use `scripts/test.ps1` rather than `dotnet test`.
Native desktop checks also create isolated test windows to verify exit recovery,
including minimized, maximized, partially off-screen, and oversized windows.
Zoom checks cover cursor anchoring, scale limits, drag distances, live thumbnails,
mouse message handling, native-size preservation, and input-surface alpha.
Additional checks cover unbounded cameras, dynamic overview bounds, desktop icon
input and rendering, settings controls, atomic persistence, and legacy layout data.
Screen-tiling checks cover margins, finite overflow, additional infinite rows,
maximized windows, application size constraints and toolbar input during zoom.
Edge-panning checks cover directions, dwell timing, pauses, diagonal speed, zoom
and finite/infinite bounds. CI also publishes and boots the real production EXE.

To verify a published single EXE without moving your application windows:

```powershell
pwsh -NoProfile -File scripts/verify-package.ps1
```

Accept the Windows administrator prompt. The check verifies bundled UI resources,
Core logic, DWM previews and the recovery companion, then exits.

```text
PaneSpace/
├─ PaneSpace.slnx
├─ Directory.Build.props
├─ global.json
├─ assets/branding/       SVG sources, PNG preview, Windows ICO
├─ scripts/               Build, test, publish
├─ docs/                  Architecture, development, rendering notes
├─ src/
│  ├─ Core/               Layout, viewport transforms, and session models
│  └─ Desktop/            WinForms app, Win32 integration, rendering, persistence
├─ tests/                 Core layout checks and native desktop recovery checks
├─ tools/Branding/        Optional SVG → ICO generation tool
└─ dist/                  Local publish output (ignored)
```

Dependencies flow from `Desktop → Core` and `Core.Tests → Core`. More detail is
available in the [architecture notes](docs/architecture.md),
[development guide](docs/development.md), and [rendering notes](docs/rendering.md)
(currently in Chinese).

## Brand assets

The [icon](assets/branding/icon.svg) and [wordmark](assets/branding/logo.svg) are SVG.
The README selects a matching wordmark for light and dark themes. The Windows
application and tray share a multi-resolution ICO generated from the same SVG.

To regenerate the assets (optional; normal .NET builds do not require Node.js):

```powershell
npm ci --prefix tools/Branding
npm run build --prefix tools/Branding
```

See the [brand asset guide](assets/branding/README.md) for colors and variants.

## Saved layouts

Layouts are saved to `%LOCALAPPDATA%/PaneSpace/state.json`. If that file does not
exist, PaneSpace reads the old `%LOCALAPPDATA%/CamCanvas/state.json` on startup;
subsequent saves use the new location. The old file is preserved.
Desktop icon positions use Shell paths as identities and are stored in the same
layout file. Options are stored separately in `%LOCALAPPDATA%/PaneSpace/settings.json`.

## Current limits

- The viewport uses the primary screen. Multi-monitor canvas support is not implemented.
- Infinite mode removes the camera boundary; automatic arrangement still targets
  nine screens and keeps the existing layout if all windows cannot fit.
- Scaled views are live previews. Application interaction resumes at 100%;
  zoom is temporary and is not saved. In finite mode, returning to 100% clamps the
  camera to the canvas bounds. Minimized windows remain accessible from the minimap.
- Protected or unavailable window content may appear as a placeholder; click it
  to open the original window.
- The taskbar and wallpaper remain fixed. Desktop icons participate when their setting is enabled.
- Canvas icons support dragging and double-click opening; use File Explorer for other file operations.
- Maximized windows may behave awkwardly when moved. Some applications suspend
  drawing when their windows are completely off-screen.
- Hidden and minimized windows do not participate in automatic arrangement.
- Exit recovery keeps window sizes. For a window larger than the screen work area,
  its top-left corner and title bar are brought back into view.

Bug reports are welcome through [GitHub Issues](https://github.com/lisniuse/PaneSpace/issues).
Include your display scaling, window state, and steps to reproduce the behavior.
