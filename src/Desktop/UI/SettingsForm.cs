using PaneSpace.Core.Settings;
using PaneSpace.Core.Layout;
using PaneSpace.Core.Viewport;

namespace PaneSpace.UI;

public sealed class SettingsForm : Form
{
    public CheckBox InfiniteCanvas { get; } = new() { Text = "无限画布", AutoSize = true };
    public CheckBox DesktopIcons { get; } = new() { Text = "桌面图标", AutoSize = true };
    public CheckBox EdgePanning { get; } = new() { Text = "屏幕边缘平移", AutoSize = true };
    public NumericUpDown TileTop { get; } = new();
    public NumericUpDown TileRight { get; } = new();
    public NumericUpDown TileBottom { get; } = new();
    public NumericUpDown TileLeft { get; } = new();
    public NumericUpDown EdgePanSpeed { get; } = new() { Minimum = EdgePan.MinSpeed, Maximum = EdgePan.MaxSpeed, Increment = 50, Width = 100 };
    public Button SaveButton { get; } = new() { Text = "保存", AutoSize = true };
    public SettingsForm(AppSettings settings, Func<AppSettings, string?> apply, Size? screenSize = null)
    {
        Text = "PaneSpace 设置";
        Font = new Font("Microsoft YaHei UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(500, 580);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        InfiniteCanvas.Checked = settings.InfiniteCanvas;
        DesktopIcons.Checked = settings.DesktopIcons;
        EdgePanning.Checked = settings.EdgePanning;
        EdgePanSpeed.Value = Math.Clamp(settings.EdgePanSpeed, EdgePan.MinSpeed, EdgePan.MaxSpeed);
        EdgePanSpeed.Enabled = EdgePanning.Checked;
        EdgePanning.CheckedChanged += (_, _) => EdgePanSpeed.Enabled = EdgePanning.Checked;
        var screen = screenSize ?? Screen.PrimaryScreen?.Bounds.Size ?? new Size(1920, 1080);
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(22),
            ColumnCount = 1, RowCount = 12 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(InfiniteCanvas);
        layout.Controls.Add(new Label { Text = "取消 3×3 屏幕边界，可向任意方向持续平移。\n自动排列仍使用 3×3 九屏区域。", AutoSize = true,
            Margin = new Padding(22, 4, 0, 18) });
        layout.Controls.Add(DesktopIcons);
        layout.Controls.Add(new Label { Text = "桌面图标随画布平移和缩放，支持拖动、双击打开。\n位置与设置会保存；关闭或退出后恢复 Windows 桌面。", AutoSize = true,
            Margin = new Padding(22, 4, 0, 12) });
        layout.Controls.Add(EdgePanning);
        layout.Controls.Add(new Label { Text = "无需按 Ctrl，鼠标停在屏幕边缘即可平移并显示方向箭头。\n拖动、打开设置或点击任务栏时暂停；默认关闭。", AutoSize = true,
            Margin = new Padding(22, 4, 0, 12) });
        var speed = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(22, 0, 0, 8) };
        speed.Controls.Add(new Label { Text = "移动速度（px/秒）", AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        speed.Controls.Add(EdgePanSpeed);
        speed.Controls.Add(new Label { Text = "50–3000", AutoSize = true, Margin = new Padding(8, 6, 0, 0) });
        layout.Controls.Add(speed);
        layout.Controls.Add(new Label { Text = "整屏平铺边距（px）", AutoSize = true, Margin = new Padding(3, 8, 3, 4) });
        var margins = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 4, RowCount = 2 };
        for (int i = 0; i < 4; i++) margins.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddMargin("上", TileTop, settings.TileTop, screen.Height - 1, 0);
        AddMargin("右", TileRight, settings.TileRight, screen.Width - 1, 1);
        AddMargin("下", TileBottom, settings.TileBottom, screen.Height - 1, 2);
        AddMargin("左", TileLeft, settings.TileLeft, screen.Width - 1, 3);
        layout.Controls.Add(margins);
        layout.Controls.Add(new Label { Text = "保存后，下次点击“整屏平铺”使用新边距。", AutoSize = true,
            Margin = new Padding(3, 4, 3, 12) });
        var error = new Label { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(420, 0) };
        foreach (var input in new[] { TileTop, TileRight, TileBottom, TileLeft })
            input.ValueChanged += (_, _) => error.Text = "";
        layout.Controls.Add(error);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill, AutoSize = true };
        var cancel = new Button { Text = "取消", AutoSize = true };
        cancel.Click += (_, _) => Close();
        SaveButton.Click += (_, _) =>
        {
            var next = new AppSettings(InfiniteCanvas.Checked, DesktopIcons.Checked, EdgePanning.Checked,
                (int)TileTop.Value, (int)TileRight.Value, (int)TileBottom.Value, (int)TileLeft.Value, (int)EdgePanSpeed.Value);
            if (!ScreenTileLayout.TryGetContent(screen, next.TileLeft, next.TileTop, next.TileRight, next.TileBottom, out _))
            {
                error.Text = $"左右之和须小于 {screen.Width}px，上下之和须小于 {screen.Height}px。";
                return;
            }
            error.Text = apply(next) ?? "";
            if (error.Text.Length == 0) Close();
        };
        buttons.Controls.Add(SaveButton); buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(layout); Controls.Add(scroll);
        AcceptButton = SaveButton; CancelButton = cancel;

        void AddMargin(string label, NumericUpDown input, int value, int maximum, int column)
        {
            input.Maximum = Math.Max(0, maximum); input.Minimum = 0;
            input.Value = Math.Clamp(value, 0, (int)input.Maximum); input.Dock = DockStyle.Top;
            margins.Controls.Add(new Label { Text = label, AutoSize = true }, column, 0);
            margins.Controls.Add(input, column, 1);
        }
    }
}
