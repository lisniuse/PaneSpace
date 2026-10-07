using PaneSpace.Application;

namespace PaneSpace;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
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
