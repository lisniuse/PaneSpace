<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/branding/logo-dark.svg">
    <img src="assets/branding/logo.svg" alt="PaneSpace 窗域 — 应用窗口的桌面画布" width="420">
  </picture>
</p>

<p align="center"><a href="README.md">English</a> · <strong>简体中文</strong></p>
<p align="center">Windows 桌面 · .NET 10 · 实验阶段</p>

# PaneSpace · 窗域

把应用窗口放进一张可浏览的桌面画布。**桌面前台按住 Ctrl**，即可拖动网格，
浏览覆盖 **3 × 3 个主屏区域**的工作空间。通过小地图可以找到窗口并跳转过去。

程序直接移动真实窗口，保留原有尺寸和应用交互。当前鼠标滚轮用于纵向平移，
尚未实现缩放浏览。

## 功能

- **拖拽平移**：在画布中浏览窗口，无需逐个搬动。
- **交互小地图**：点击窗口色块，镜头居中并激活对应窗口。
- **任务栏跟随**：点击任务栏图标或缩略图还原、激活窗口后，镜头自动跟随，受画布边界限制。
- **瀑布流排列**：将不同尺寸的窗口放到最低可用位置，支持宽窗口跨列，保持窗口尺寸。
- **布局存档**：记住镜头偏移及窗口位置，供下次启动恢复。
- **托盘控制**：通过 PaneSpace 托盘图标复位或退出。
- **退出收回窗口**：保存画布布局后，将画布窗口搬回可见工作区；最小化窗口保持最小化，
  修正其还原位置，避免后续还原到屏幕外；可见的所属对话框也会一起收回。

## 构建与运行

开发需要 **Windows、PowerShell 7 和 .NET 10 SDK**。运行发布程序需要
**.NET 10 Windows Desktop Runtime（x64）**。

```powershell
git clone https://github.com/lisniuse/PaneSpace.git
cd PaneSpace
pwsh -NoProfile -File scripts/publish.ps1
.\dist\PaneSpace.exe
```

启动时接受管理员权限提示，以便管理提权运行的应用窗口。程序启动后驻留托盘，
不打开独立主窗口。

发布脚本先构建和运行检查，再结束运行中的 PaneSpace / 旧名 CamCanvas 进程，
统一发布到 **`dist`**。`build.cmd` 提供同样的发布入口。仓库不包含构建产物。

## 操作

| 操作 | 效果 |
| --- | --- |
| 桌面前台按住 Ctrl | 显示网格、按钮栏和交互小地图 |
| 左键拖动网格 | 平移画布 |
| 画布模式下滚轮 | 纵向平移 |
| 点击小地图中的窗口色块 | 镜头居中并激活该窗口 |
| 从任务栏唤起窗口 | 跟随还原或激活的窗口 |
| 自动排列(全画布) | 排列可见、未最小化的窗口 |
| 全部搬回中心屏 | 保持相对布局，整体移回中心屏 |
| Esc / 复位 | 将镜头偏移归零 |
| 托盘退出 | 保存画布布局，将窗口收回屏幕后退出 |

当前工具栏和托盘菜单使用中文。排列放不下所有符合条件的窗口时，会保留原布局并提示。

## 开发

```powershell
pwsh -NoProfile -File scripts/build.ps1
pwsh -NoProfile -File scripts/test.ps1
```

检查直接引用实际的 Core 库，覆盖最短列、宽窗口跨列、边界、空间不足及
1000 组固定种子的随机布局。测试是可执行程序，请使用 `scripts/test.ps1`，而非 `dotnet test`。
原生桌面检查使用独立测试窗口，验证普通、最小化、最大化、部分出屏和超大窗口的退出收回行为。

```text
PaneSpace/
├─ PaneSpace.slnx
├─ Directory.Build.props
├─ global.json
├─ assets/branding/       SVG 源文件、PNG 预览、Windows ICO
├─ scripts/               构建、测试、发布
├─ docs/                  架构、开发、渲染说明
├─ src/
│  ├─ Core/               布局算法及会话模型
│  └─ Desktop/            WinForms、Win32、绘制、存档
├─ tests/                 Core 布局检查和原生桌面收回窗口检查
├─ tools/Branding/        可选的 SVG → ICO 生成工具
└─ dist/                  本地发布产物（不提交）
```

项目依赖为 `Desktop → Core`、`Core.Tests → Core`。详情见
[架构说明](docs/architecture.md)、[开发指南](docs/development.md)及[渲染说明](docs/rendering.md)。

## 品牌资源

[图标](assets/branding/icon.svg)和 [Logo](assets/branding/logo.svg)均提供 SVG。
README 根据浅色、深色主题选择相应 Logo。程序文件与托盘共用由 SVG 生成的多尺寸 ICO。

重新生成资源（可选；普通 .NET 构建不需要 Node.js）：

```powershell
npm ci --prefix tools/Branding
npm run build --prefix tools/Branding
```

颜色和资源版本见[品牌资源说明](assets/branding/README.md)。

## 布局数据

布局写入 `%LOCALAPPDATA%/PaneSpace/state.json`。新位置没有存档时，会读取旧的
`%LOCALAPPDATA%/CamCanvas/state.json`；后续保存使用新位置，原存档保留。

## 当前限制

- 画布限定为 3 × 3 个主屏区域，尚未实现缩放浏览和多显示器画布。
- 桌面图标、任务栏和壁纸不随窗口平移。
- 最大化窗口平移时可能表现不自然；完全移出屏幕的应用可能暂停绘制。
- 隐藏、最小化窗口不参与自动排列。
- 退出收回时保持窗口尺寸；窗口超过屏幕工作区大小时，将左上角及标题栏搬回可见范围。

欢迎在 [GitHub Issues](https://github.com/lisniuse/PaneSpace/issues) 反馈问题，
请附上显示缩放比例、窗口状态和复现步骤。
