using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using PaneSpace.Core.Settings;
using PaneSpace.Core.Viewport;
using PaneSpace.Platform.Windows;
using PaneSpace.Rendering;
using PaneSpace.UI;

namespace PaneSpace.Application;

/// <summary>Exercise the published executable without touching the user's windows or saved layout.</summary>
internal static class PackageCheck
{
    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "An empty assembly location explicitly verifies that this check runs from the bundle.")]
    internal static void Run(string reportPath)
    {
        try
        {
            using var icon = BrandIcon.Load();
            using var settings = new SettingsForm(new AppSettings(), _ => null);
            _ = settings.Handle;
            if (settings.EdgePanning.Checked || settings.TileTop.Value != 28 || settings.TileRight.Value != 28 ||
                settings.TileBottom.Value != 80 || settings.TileLeft.Value != 28 || icon.Width <= 0)
                throw new InvalidOperationException("Bundled UI resources failed.");
            var edges = new EdgePan();
            var area = new Rectangle(0, 0, 640, 480);
            edges.Step(new Point(639, 200), area, 1000, true);
            if (edges.Step(new Point(639, 200), area, 1300, true).X >= 0)
                throw new InvalidOperationException("Bundled Core library failed.");
            using var source = new ProbeWindow { Bounds = new Rectangle(-8000, -8000, 200, 120), ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual };
            source.Show(); System.Windows.Forms.Application.DoEvents();
            using var preview = new WindowPreview(640, 480);
            preview.UpdateWindows(new[] { (source.Handle, new RectangleF(0, 0, 200, 120)) }, new CanvasViewport(640, 480, 0, 0, .5f));
            if (preview.LiveCount != 1) throw new InvalidOperationException("Bundled DWM preview failed.");
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            // An impossible owner identity checks the actual companion entry point without restoring any live lease.
            start.ArgumentList.Add("--restore-desktop-icons"); start.ArgumentList.Add(int.MaxValue.ToString()); start.ArgumentList.Add("0");
            using var child = Process.Start(start) ?? throw new InvalidOperationException("Bundled companion failed to start.");
            if (!child.WaitForExit(15000)) { child.Kill(); throw new InvalidOperationException("Bundled companion startup timed out."); }
            if (child.ExitCode != 0) throw new InvalidOperationException($"Bundled companion exited with error 0x{child.ExitCode:X8}.");
            using var identity = WindowsIdentity.GetCurrent();
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Success = true,
                SingleFile = typeof(PackageCheck).Assembly.Location.Length == 0,
                Elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator),
                Runtime = RuntimeInformation.FrameworkDescription, Icon = true, Settings = true,
                Core = true, Preview = true, Companion = true }));
        }
        catch (Exception e)
        {
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Success = false, Error = e.ToString() }));
            Environment.ExitCode = 1;
        }
    }

    private sealed class ProbeWindow : Form
    {
        protected override bool ShowWithoutActivation => true;
    }
}
