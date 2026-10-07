# PaneSpace brand assets

The mark combines window panes and a viewfinder: applications arranged in a
navigable canvas. It is drawn as SVG, with no linked images or fonts in the icon.

| File | Use |
| --- | --- |
| `icon.svg` | Master vector mark |
| `logo.svg` | Wordmark for a light background |
| `logo-dark.svg` | Wordmark for a dark background |
| `icon.png` | 256 px preview |
| `icon.ico` | Windows executable and tray; 16–256 px frames |

The wordmarks use Inter, converted to vector paths. They embed the vector mark
directly, so GitHub can render them without external font or image requests.
The dark square gives the tray icon a consistent silhouette on light and dark taskbars.

| Color | Hex |
| --- | --- |
| Canvas | `#0b1220` |
| Primary blue | `#2563eb` |
| Sky blue | `#38bdf8` |
| Light pane | `#e0f2fe` |

Edit `icon.svg`, then run `npm ci --prefix tools/Branding` and
`npm run build --prefix tools/Branding`. Commit the generated SVG, PNG, and ICO
alongside the source. Application builds use the committed ICO and embed it as a
resource; no SVG renderer is shipped with the app. The optional generator uses
Inter from `@fontsource/inter` (SIL Open Font License); the font is not bundled
with the Windows application.
