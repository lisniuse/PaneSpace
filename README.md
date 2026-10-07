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
**3 × 3 primary-screen areas**. A minimap helps you find windows and jump back to them.

At 100%, the app moves real windows and keeps their sizes intact. Wheel zoom uses
live window previews from 25% to 200%, anchored at the pointer. Release Ctrl or
click a preview to return to 100% and interact with the original application.

## Features

- **Drag to pan:** move across the canvas without rearranging individual windows.
- **Wheel zoom:** browse live window contents at 25%–200%; the grid and minimap
  viewport follow the camera. Window sizes remain unchanged.
- **Interactive minimap:** click a window block to center and activate that window.
- **Taskbar focus:** restore or activate a window from its taskbar icon or thumbnail
  and the camera follows it, within the canvas bounds.
- **Masonry arrangement:** pack mixed-size windows into the lowest available space,
  allowing wide windows to span columns without resizing them.
- **Layout persistence:** remember the camera and window positions across sessions.
- **Tray controls:** reset the camera or exit from the PaneSpace tray icon.
- **Safe exit:** save the canvas layout, then return its windows to visible screen
  work areas, along with their visible dialogs. Minimized windows stay minimized
  and receive reachable restore positions.

## Build and run

Development requires **Windows, PowerShell 7, and the .NET 10 SDK**. Running the
published app requires the **.NET 10 Windows Desktop Runtime (x64)**.

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
CamCanvas processes, and always writes to **`dist`**. `build.cmd` provides the same
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
| 自动排列(全画布) | Arrange visible, non-minimized windows across the canvas |
| 全部搬回中心屏 | Move the window group back to the center screen |
| 复位 button / Tray → 画布归位 | Reset to 100% and zero camera offset |
| Tray → Exit | Save the canvas layout and bring its windows back onto the screens |

The current toolbar and tray menus use Chinese labels. If an arrangement cannot
fit all eligible windows, PaneSpace keeps the existing layout and shows a notice.
Esc does not reset the camera or move windows; use the reset button or tray menu.

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

## Current limits

- The canvas is bounded to 3 × 3 primary-screen areas. Multi-monitor canvas
  support is not implemented.
- Scaled views are live previews. Application interaction resumes at 100%;
  zoom is temporary and is not saved. Returning to 100% clamps the camera to
  the canvas bounds. Minimized windows remain accessible from the minimap.
- Protected or unavailable window content may appear as a placeholder; click it
  to open the original window.
- Desktop icons, the taskbar, and wallpaper do not move with the windows.
- Maximized windows may behave awkwardly when moved. Some applications suspend
  drawing when their windows are completely off-screen.
- Hidden and minimized windows do not participate in automatic arrangement.
- Exit recovery keeps window sizes. For a window larger than the screen work area,
  its top-left corner and title bar are brought back into view.

Bug reports are welcome through [GitHub Issues](https://github.com/lisniuse/PaneSpace/issues).
Include your display scaling, window state, and steps to reproduce the behavior.
