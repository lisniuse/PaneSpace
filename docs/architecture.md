# 架构与目录约定

PaneSpace 分成一个纯逻辑库、一个 Windows 桌面应用和一个轻量回归测试项目。
源码目录使用职责名称 `Core`、`Desktop`，项目文件使用产品名称；目录不再重复套一层
与项目根目录同名的文件夹。

## 项目边界

| 项目 | 职责 | 依赖 |
| --- | --- | --- |
| `src/Core/PaneSpace.Core.csproj` | 布局算法、视口变换、可序列化的会话模型 | .NET 基础库；无 HWND、WinForms 和文件读写 |
| `src/Desktop/PaneSpace.csproj` | Windows 窗口操作、输入、绘制、托盘和存档 | Core、WinForms、Win32 |
| `tests/Core/PaneSpace.Core.Tests.csproj` | 测试实际编译的布局算法 | Core |
| `tests/Desktop/PaneSpace.Desktop.Tests.csproj` | 用独立原生窗口验证退出收回行为 | Desktop、Win32 |

Core 使用 `System.Drawing` 的 `Size`、`Point`、`Rectangle` 值类型，不依赖 GDI+ 绘图。
测试不再通过链接源码重复编译算法，避免应用与测试引用不同实现。

## 桌面应用

`Program.cs` 创建 WinForms 消息循环，`Application/CanvasController.cs` 持有画布的
共享状态、启动托盘和释放资源。同一个控制器按职责分成 partial 文件：

| 文件 | 内容 |
| --- | --- |
| `CanvasController.Windows.cs` | 窗口登记、事件接收、逻辑坐标重同步 |
| `CanvasController.Actions.cs` | 平移、瀑布流、归中、小地图聚焦 |
| `CanvasController.Taskbar.cs` | 任务栏点击识别与恢复后的延迟定位 |
| `CanvasController.Input.cs` | Ctrl 轮询、鼠标消息、命中检测 |
| `CanvasController.Zoom.cs` | 缩放镜头、实时预览协调及回到原生视图 |
| `CanvasController.Settings.cs` | 设置窗口、独立选项、桌面图标生命周期和刷新 |
| `CanvasController.Rendering.cs` | 网格、按钮、小地图、共享 DIB 缓冲与呈现 |
| `CanvasController.Session.cs` | 会话组装、窗口身份匹配和恢复 |

这些 partial 文件属于同一个消息循环对象，共享状态仍由 UI 线程驱动，拆分本身不
改变事件时序。`Platform/Windows` 封装平台接口；`Persistence` 负责 JSON 存档；
`Rendering/GrabCursorFactory.cs` 负责光标生成；`Rendering/BrandIcon.cs` 加载嵌入的品牌
图标，供托盘使用。托盘与程序文件图标共用 `assets/branding/icon.ico`。

正常退出时先保存画布状态，再通过 `Platform/Windows/WindowRecovery.cs` 将受管理的
窗口及其可见的所属对话框收回最近显示器的工作区。不会把收回后的屏幕坐标写入画布存档；下次启动仍恢复
原有画布布局。最小化窗口通过 `WINDOWPLACEMENT` 修正还原坐标，保留最小化及
还原到最大化的标志。普通窗口保持尺寸、层级和焦点；超大窗口确保左上角可见。
退出处理可重复调用；消息循环结束后的 `ApplicationContext.Dispose` 也执行相同收尾。

## 坐标与排列

真实窗口位置满足 `real = logical + pan`。进入画布模式时重新采纳窗口的实际位置；
最小化窗口的停放坐标不用于更新逻辑布局，也不参与平移。

`Core/Layout/MasonryLayout.cs` 独立计算完整的最短列布局。不同宽度的窗口可以跨越
多个水平区段；优先放到最低可用位置，高度相同则从左向右。尺寸和间距保持不变。
计算成功才提交所有坐标，失败不会提交部分排列，也不会把窗口排出可达画布。

`Core/Layout/ScreenTileLayout.cs` 计算独立“整屏平铺”的三列屏幕格子，每格上、左、右
各留 28px，底部为任务栏留 80px；有限模式最多九格，无限模式增加行。
先检查完整布局容量再变更镜头和窗口。
`Platform/Windows/WindowTiling.cs` 在不激活窗口的前提下还原最大化、调整真实尺寸，
用 DWM 可见边框补偿不可见原生边框。应用限制尺寸时读回实际结果，在可用区域内居中。
控制器将首格置于镜头中心，保留隐藏、最小化窗口及桌面图标的原有世界坐标。
控制器记录本次平铺的实际尺寸和格内偏移，后续窗口定位复用偏移以保留任务栏空间；
尺寸变化、成功瀑布流排列、整体归中或窗口销毁会清理相应记录。

## 缩放视图

`Core/Viewport/CanvasViewport.cs` 计算缩放、鼠标锚点、逆变换、拖动和视口边界。
镜头偏移使用世界单位，屏幕坐标为 `(world + pan - screenCenter) * scale + screenCenter`。
100% 与既有 `real = logical + pan` 一致；有限模式视口小于画布时限制在画布内，大于画布时居中。
无限模式跳过边界限制，`CanvasOverview` 动态计算小地图范围；自动排列仍使用固定九屏矩形。
极远窗口停放在安全原生坐标，逻辑位置保持不变，进入画布时跳过停放窗口的位置重同步。

`Rendering/WindowPreview.cs` 持有不激活的普通顶层窗口及 DWM 缩略图；平台声明在
`Platform/Windows/Dwm.cs`。预览宿主在分层输入窗口下方，按真实窗口层级由下至上登记。
超出视口的预览同步裁剪源与目标矩形，保持内容比例。窗口列表每 100ms 检查一次，
内容由 DWM 实时更新；拖动及滚轮立即刷新镜头，不调整原窗口尺寸。

缩放时原窗口不移动；松开 Ctrl 或点击预览，按当前视口中心恢复 100% 并应用真实窗口
位置，然后隐藏预览、释放缩略图。无限模式恢复 100% 保留镜头位置。比例不写入会话，既有 JSON 格式保持兼容。
测试只向控制器注入独立窗口，不执行真实窗口枚举或读写用户存档。

## 设置与桌面图标

`UI/SettingsForm.cs` 显示两个独立勾选项，保存后由控制器应用；重复打开复用同一窗口。
`SettingsStore` 使用 `JsonStore` 原子保存设置，`StateStore` 使用相同实现保存窗口、镜头和图标。
旧会话缺少 `DesktopIcons` 时默认为空；关闭图标选项不丢弃已保存位置。

`DesktopShell` 通过 `IShellWindows → IShellBrowser → IFolderView2` 读取实际桌面项和位置，
包括文件、快捷方式及虚拟 Shell 项目；通过 Explorer 的 ShellExecute 打开项目。
不修改 Explorer 图标坐标和实际文件。接口槽位对应 Windows SDK 声明，不向跨进程 ListView 写坐标。
参考 [微软桌面图标接口示例](https://devblogs.microsoft.com/oldnewthing/20130318-00/?p=4933/)。

`DesktopIconCanvas` 管理按 Shell 路径识别的图标、世界坐标、命中、拖动及绘制。
`DesktopIconSurface` 使用共享 PArgb DIB，在壁纸上方、普通窗口下方呈现可交互图标。
预览缩放时图标改由 `WindowPreview` 背景绘制，DWM 窗口缩略图保持在图标上方。

`DesktopIconLease` 隐藏原生桌面视图窗口，保留可枚举项目；直接设置 `FWF_NOICONS` 会清空
视图项目列表，因此不能用它实现接管。保存原先显示状态并启动恢复伴随进程；退出或禁用时恢复，
异常退出由伴随进程处理。恢复票据包含进程 ID、启动时间和恢复进程 ID，启动时也处理遗留票据。
程序使用同一用户会话内的单实例互斥量，恢复伴随进程不启动画布或读取会话布局。
