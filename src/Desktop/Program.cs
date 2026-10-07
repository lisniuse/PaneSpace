using PaneSpace.Application;
using PaneSpace.Platform.Windows;

namespace PaneSpace;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 3 && args[0] == "--restore-desktop-icons" &&
            int.TryParse(args[1], out int processId) && long.TryParse(args[2], out long startTicks))
        {
            DesktopIconLease.RecoverAfterExit(processId, startTicks); return;
        }
        using var instance = new Mutex(true, "Local\\PaneSpace.Desktop", out bool first);
        if (!first) return;
        DesktopIconLease.RecoverStaleDesktop();
        using var ctx = new CamContext();
        System.Windows.Forms.Application.Run(ctx);
    }

    /// <summary>Headless app: message loop drives the camera; the tray is the only UI.</summary>
    private sealed class CamContext : System.Windows.Forms.ApplicationContext
    {
        private readonly CanvasController _cam;
        public CamContext() => _cam = new CanvasController();

        protected override void ExitThreadCore()
        {
            _cam.Dispose();   // windows go back home before we die
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing) _cam.Dispose();
            }
            finally { base.Dispose(disposing); }
        }
    }
}
