# PaneSpace 开发约定

本文件适用于整个仓库。修改前先阅读相关源码及 `docs/`，以实际实现为准。
与用户沟通默认使用中文；`README.md` 默认英语，`README.zh-CN.md` 为中文版。

## 终端与工具

- 使用 **PowerShell 7（`pwsh`）**，不要使用 Windows PowerShell 5.1 或 Bash 语法。
- 工具命令使用 `rtk` 前缀。PowerShell cmdlet 放进 `rtk pwsh -NoProfile -Command '...'`。
- RTK 改写参数或省略必要输出时，使用 `rtk proxy` 保留原始命令行为。
- 搜索优先使用 `rg` / `rg --files`。从仓库根目录执行命令，不硬编码个人工作目录。
- 删除或移动目录前检查解析后的绝对路径；使用 `-LiteralPath`，保持文件操作在同一种 shell 内完成。

```powershell
rtk git status -sb
rtk rg -n 'ReturnToScreens' src tests
rtk pwsh -NoProfile -Command 'Get-Content -LiteralPath scripts/test.ps1'
```

## 项目与目录

项目名称为 **PaneSpace（窗域）**，解决方案为 `PaneSpace.slnx`。
技术栈为 .NET 10、WinForms、Win32；SDK 策略在 `global.json` 中。

| 目录 | 职责 |
| --- | --- |
| `src/Core` | 纯布局算法、会话数据模型；不引用 WinForms、HWND 或平台 API |
| `src/Desktop/Application` | 生命周期、窗口登记、输入、画布操作、渲染协调、会话恢复 |
| `src/Desktop/Platform/Windows` | Win32 声明、窗口筛选、事件监听、退出收回 |
| `src/Desktop/Persistence` | 布局文件读写 |
| `src/Desktop/Rendering` | 图标、光标等绘制资源 |
| `tests/Core` | 引用真实 Core 项目的布局回归检查 |
| `tests/Desktop` | 使用独立原生窗口验证桌面行为 |
| `assets/branding` | SVG 源文件、生成的 Logo、PNG、ICO |
| `tools/Branding` | 可选的品牌资源生成工具 |
| `scripts` | 构建、测试、发布入口 |
| `docs` | 架构、开发与渲染说明 |
| `dist` | 唯一的本地发布目录，不提交 |

依赖方向为 `Desktop → Core`、`Core.Tests → Core`、`Desktop.Tests → Desktop`。
`CanvasController` 的 partial 文件共享同一对象，状态由 UI 消息循环驱动；新增功能按职责放置，
避免把平台调用和纯算法重新集中到一个大文件中。

## 构建、测试与发布

```powershell
rtk pwsh -NoProfile -File scripts/build.ps1
rtk pwsh -NoProfile -File scripts/test.ps1
rtk pwsh -NoProfile -File scripts/publish.ps1
```

- 测试是可执行检查程序，使用 `scripts/test.ps1`，不是 `dotnet test`。
- 代码修改运行相关检查；影响窗口操作或退出流程时，包含 `tests/Desktop` 的原生检查。
- 文档修改检查内容、链接和 `git diff --check` 即可。
- 自动回归使用独立测试窗口，不移动用户现有的应用窗口。
- 发布固定到 **`dist`**，不要创建 `dist-*` 目录。入口是 `dist/PaneSpace.exe`。
- 发布为 `win-x64`、依赖运行时的多文件应用；不要擅自启用 `PublishSingleFile`。
- 用户已允许结束占用发布文件的 PaneSpace / 旧名 CamCanvas 进程；不要扩大到无关进程。
- 普通退出走托盘菜单并收回窗口；强制结束进程不会执行退出收尾。

## 窗口管理约束

- 当前画布为主屏大小的 **3 × 3**；Ctrl 画布模式下滚轮以鼠标为锚点缩放 **25%–200%**。
- 100% 移动真实窗口；其他比例通过 DWM 实时预览，禁止通过调整原窗口尺寸模拟缩放。
  松开 Ctrl 或点击预览回到 100%，保持镜头中心并限制在画布边界内；Esc 同时复位比例和偏移。
- 预览期间原窗口坐标固定，新窗口登记使用进入预览时的原生镜头偏移。
  DWM 缩略图需要显式释放；预览宿主位于分层输入界面下方，不抢焦点。
- 普通窗口的物理坐标满足 `real = logical + pan`；最小化停放坐标不用于更新逻辑位置。
- 平移和排列保持窗口尺寸；最小化窗口不参与平移，隐藏和最小化窗口不参与排列。
- 自动排列使用支持跨列的最短列瀑布流；只提交完整布局，放不下时保留原布局并提示。
- 任务栏跟随需要真实的任务栏点击及前台/还原事件；不要让任意前台变化都移动镜头。
- 正常退出先保存原画布布局，再收回窗口及其可见所属对话框。
- 退出收回使用显示器工作区，保留最小化、最大化状态；超大窗口至少保证左上角及标题栏可见。
- 收回后的屏幕坐标不要覆盖画布存档。退出处理须可重复调用，资源按生命周期释放。
- `WINDOWPLACEMENT` 通常使用工作区坐标，工具窗口使用屏幕坐标；不要直接混用 `SetWindowPos` 坐标。
- 分层绘制使用共享 DIB 和 `Graphics.FromImage`，保留预乘 Alpha。
  不要改回 `Graphics.FromHdc` 绘制不透明色块，以免小地图产生点击穿透。

布局文件为 `%LOCALAPPDATA%/PaneSpace/state.json`，首次启动兼容旧 CamCanvas 存档。
保持既有 JSON 字段兼容；需要迁移时明确处理已有布局。

## 品牌与文档

- 图标源文件是 `assets/branding/icon.svg`，程序和托盘共用生成的 `icon.ico`。
- Logo 文字已转为 SVG 路径，保持无需外部字体和图片的渲染方式。
- 修改品牌源文件后重新生成并提交相应 SVG、PNG、ICO；普通 .NET 构建无需 Node.js。

```powershell
rtk proxy npm ci --prefix tools/Branding
rtk proxy npm run build --prefix tools/Branding
```

功能与使用说明同时更新两份 README；涉及架构或开发流程时更新相应 `docs/`。
文档只描述已经实现的行为，不把规划中的多显示器画布等写成现有功能。

## Git

- 只提交与任务有关的源码、文档和品牌资源，遵守 `.gitignore`。
- 不提交 `dist`、`bin`、`obj`、`node_modules`、IDE 本地状态、日志或本地环境配置。
- 提交前检查差异与工作区，保留用户已有修改。
- **每完成一个任务，在必要检查通过后自动提交并推送到 GitHub，无需再次询问用户。**
  提交仅包含本任务的修改；没有文件变更时不创建空提交。推送失败时明确报告原因，不宣称已同步。
- 提交信息使用 **Conventional Commits（CC）**：`<type>(<scope>): <description>`，scope 可省略。
  标题默认英语，简洁描述本次结果；常用 type 为 `feat`、`fix`、`docs`、`refactor`、`perf`、
  `test`、`build`、`ci`、`chore`、`revert`。不兼容变更使用 `!` 或 `BREAKING CHANGE:` 正文说明。
- 普通任务使用常规推送，不改写已发布历史。用户明确要求修改历史提交时，可以重写；
  先备份、核对远端分支，再使用带明确预期远端提交的 `--force-with-lease`，避免覆盖他人更新。

```text
feat(branding): add SVG icon and logo
fix(windows): recover off-screen windows on exit
docs(agents): document automatic task commits
```
