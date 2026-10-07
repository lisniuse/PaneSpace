# 分层窗口渲染

网格、按钮和小地图由同一个顶层分层窗口绘制。网格采用较低 Alpha，按钮和小地图
采用较高 Alpha；没有画布模式时整个帧清为透明，让鼠标正常穿透。

## ULW + GDI+ 的 Alpha 陷阱

2026-10-07，由 richie 定位修复。

`Graphics.FromHdc(dc)` 上 Alpha=255 的不透明填充会走 GDI 快速路径，把像素 Alpha
写成 0。分层窗口的命中测试按 Alpha 值处理：这会把刚画出的高亮色块变成点击穿透洞。
症状是悬停高亮的色块点不中，周围空白反而能通过就近命中触发跳转。

正确路径是：

1. 使用 `CreateDIBSection` 分配 top-down 的 32bpp 像素缓冲。
2. 使用 `new Bitmap(w, h, stride, Format32bppPArgb, pixels)` 包装同一段内存。
3. 只通过 `Graphics.FromImage` 绘制，保留真实 Alpha。
4. 将同一张 DIB 选入内存 DC，使用 `UpdateLayeredWindow` 呈现。

PArgb 满足分层窗口的预乘 Alpha 要求，绘制与呈现共享像素，不需要每帧复制 HBITMAP。
释放时先释放托管 Bitmap 包装，再释放它依赖的原生位图和 DC。

## 刷新与输入

拖拽期间合并鼠标移动量，每 16ms 更新一次窗口位置。小地图只重绘自身区域，但
`UpdateLayeredWindow` 仍提交完整帧；它不是局部区域提交接口。

小地图绘制与命中共用矩形列表，当前保留 44 像素的就近选择范围。这解释了点击
色块附近空白也会选中窗口；修改容差时应同步验证重叠色块、边缘和鼠标光标反馈。
