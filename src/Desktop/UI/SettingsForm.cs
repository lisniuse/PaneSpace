using PaneSpace.Core.Settings;

namespace PaneSpace.UI;

public sealed class SettingsForm : Form
{
    public CheckBox InfiniteCanvas { get; } = new() { Text = "无限画布", AutoSize = true };
    public CheckBox DesktopIcons { get; } = new() { Text = "桌面图标", AutoSize = true };
    public CheckBox EdgePanning { get; } = new() { Text = "屏幕边缘平移", AutoSize = true };
    public Button SaveButton { get; } = new() { Text = "保存", AutoSize = true };
    public SettingsForm(AppSettings settings, Func<AppSettings, string?> apply)
    {
        Text = "PaneSpace 设置";
        Font = new Font("Microsoft YaHei UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(500, 440);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        InfiniteCanvas.Checked = settings.InfiniteCanvas;
        DesktopIcons.Checked = settings.DesktopIcons;
        EdgePanning.Checked = settings.EdgePanning;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22),
            ColumnCount = 1, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(InfiniteCanvas);
        layout.Controls.Add(new Label { Text = "取消 3×3 屏幕边界，可向任意方向持续平移。\n自动排列仍使用 3×3 九屏区域。", AutoSize = true,
            Margin = new Padding(22, 4, 0, 18) });
        layout.Controls.Add(DesktopIcons);
        layout.Controls.Add(new Label { Text = "桌面图标随画布平移和缩放，支持拖动、双击打开。\n位置与设置会保存；关闭或退出后恢复 Windows 桌面。", AutoSize = true,
            Margin = new Padding(22, 4, 0, 12) });
        layout.Controls.Add(EdgePanning);
        layout.Controls.Add(new Label { Text = "无需按 Ctrl，鼠标停在屏幕工作区边缘即可平移。\n拖动、打开设置或操作任务栏时暂停；默认关闭。", AutoSize = true,
            Margin = new Padding(22, 4, 0, 12) });
        var error = new Label { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(420, 0) };
        layout.Controls.Add(error);
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill, AutoSize = true };
        var cancel = new Button { Text = "取消", AutoSize = true };
        cancel.Click += (_, _) => Close();
        SaveButton.Click += (_, _) =>
        {
            error.Text = apply(new AppSettings(InfiniteCanvas.Checked, DesktopIcons.Checked, EdgePanning.Checked)) ?? "";
            if (error.Text.Length == 0) Close();
        };
        buttons.Controls.Add(SaveButton); buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
        AcceptButton = SaveButton; CancelButton = cancel;
    }
}
