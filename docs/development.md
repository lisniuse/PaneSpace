# 开发指南

## 环境

- Windows，PowerShell 7，.NET 10 SDK。
- `global.json` 以 SDK 10.0.300 为基线，允许同一主版本的更新 feature band。
- 发布物是 `win-x64`、依赖运行时的普通多文件应用，需要 .NET 10 Windows Desktop Runtime。
- 程序 manifest 请求管理员权限，并启用 PerMonitorV2 DPI。

`Directory.Build.props` 统一开启 nullable、隐式 using 和警告视为错误。
`.editorconfig` 定义格式；`.gitignore` 排除 bin、obj、dist、IDE 状态和诊断文件。
仓库只提交源码、文档和品牌资源，不包含本地发布目录。

## 常用命令

```powershell
# 构建整个解决方案
pwsh -NoProfile -File scripts/build.ps1

# Debug 构建
pwsh -NoProfile -File scripts/build.ps1 -Configuration Debug

# 构建并执行 Core 回归检查
pwsh -NoProfile -File scripts/test.ps1

# 已经构建过相同配置时，仅运行检查
pwsh -NoProfile -File scripts/test.ps1 -NoBuild

# 检查通过后，停止运行中的旧程序并发布
pwsh -NoProfile -File scripts/publish.ps1

# 仅在本次版本已经验证过时跳过检查
pwsh -NoProfile -File scripts/publish.ps1 -SkipTests
```

脚本按自身位置解析项目根目录，从其他工作目录调用也会使用本项目的 SDK 策略和
固定 `dist` 输出。`build.cmd` 调用 PowerShell 发布脚本，支持相同参数并传递退出码。
发布会停止 PaneSpace 和旧名 CamCanvas 进程；新程序需要手动启动。

不启用 `PublishSingleFile`：本项目此前实测单文件 apphost 在提权启动时会卡住。
发布成功后清理 `dist` 中旧产品名的程序文件，保留唯一入口 `PaneSpace.exe`。

## 图标与 Logo

`assets/branding/icon.svg` 是图标源文件；`tools/Branding/build.mjs` 从该 SVG 生成
浅色/深色 Logo、PNG 预览和多尺寸 ICO。使用 `npm ci --prefix tools/Branding` 安装
锁定的生成工具，再运行 `npm run build --prefix tools/Branding`。日常编译无需 Node.js。
生成后的 ICO 同时用于程序文件图标和嵌入式托盘资源，修改后应一起提交生成文件。

## 测试

当前测试项目是无第三方测试框架的可执行回归检查，入口为 `scripts/test.ps1`，
不是 `dotnet test`。断言失败返回非零退出码。
覆盖矮窗口下方补位、宽窗口跨列、尺寸保持、间距、精确边界、空间不足时整体拒绝，
以及 1000 组固定种子的随机布局。测试直接引用 `PaneSpace.Core`。

修改 Win32 输入、绘制或生命周期时，还应验证真实桌面行为，包括 Ctrl 门控、
小地图点击、任务栏唤起、最小化恢复以及退出归位。

## 状态与诊断

- 存档：`%LOCALAPPDATA%/PaneSpace/state.json`，首次启动兼容旧 CamCanvas 存档。
- 当前小地图诊断日志：`%TEMP%/camcanvas-map.log`，名称暂时保留供已有诊断流程使用。
- 旧存档不会删除；新保存使用 PaneSpace 路径，JSON 字段保持兼容。
- 强制结束进程不会执行退出归位；日常退出仍建议使用托盘菜单。
