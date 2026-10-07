# 架构与目录约定

PaneSpace 分成一个纯逻辑库、一个 Windows 桌面应用和一个轻量回归测试项目。
源码目录使用职责名称 `Core`、`Desktop`，项目文件使用产品名称；目录不再重复套一层
与项目根目录同名的文件夹。

## 项目边界

| 项目 | 职责 | 依赖 |
| --- | --- | --- |
| `src/Core/PaneSpace.Core.csproj` | 布局算法、可序列化的会话模型 | .NET 基础库；无 HWND、WinForms 和文件读写 |
| `src/Desktop/PaneSpace.csproj` | Windows 窗口操作、输入、绘制、托盘和存档 | Core、WinForms、Win32 |
| `tests/Core/PaneSpace.Core.Tests.csproj` | 测试实际编译的布局算法 | Core |

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
| `CanvasController.Input.cs` | Ctrl/Esc 轮询、鼠标消息、命中检测 |
| `CanvasController.Rendering.cs` | 网格、按钮、小地图、共享 DIB 缓冲与呈现 |
| `CanvasController.Session.cs` | 会话组装、窗口身份匹配和恢复 |

这些 partial 文件属于同一个消息循环对象，共享状态仍由 UI 线程驱动，拆分本身不
改变事件时序。`Platform/Windows` 封装平台接口；`Persistence` 负责 JSON 存档；
`Rendering/GrabCursorFactory.cs` 负责光标生成；`Rendering/BrandIcon.cs` 加载嵌入的品牌
图标，供托盘使用。托盘与程序文件图标共用 `assets/branding/icon.ico`。

## 坐标与排列

真实窗口位置满足 `real = logical + pan`。进入画布模式时重新采纳窗口的实际位置；
最小化窗口的停放坐标不用于更新逻辑布局，也不参与平移。

`Core/Layout/MasonryLayout.cs` 独立计算完整的最短列布局。不同宽度的窗口可以跨越
多个水平区段；优先放到最低可用位置，高度相同则从左向右。尺寸和间距保持不变。
计算成功才提交所有坐标，失败不会提交部分排列，也不会把窗口排出可达画布。

## 后续功能的放置

视口坐标、缩放比例等纯计算可以放进 Core；DWM 缩略图或窗口捕获接口放进
`Platform/Windows`；缩放视图的合成放进 `Rendering`。新增渲染方案时应抽出拥有明确
生命周期的渲染组件，避免继续把资源和输入处理堆进主控制器。

当前这次调整建立了目录、项目依赖和文件职责边界，尚未引入缩放渲染或新的并发机制。
