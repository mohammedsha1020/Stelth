using System.Windows;

namespace Stealth.PC
{
    /// <summary>
    /// Main application entry point
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Check for silent mode
            var silentMode = e.Args.Contains("--silent") || e.Args.Contains("-s");

            // Create and show main window
            var mainWindow = new MainWindow(silentMode);
            
            if (!silentMode)
            {
                mainWindow.Show();
            }

            // Set as main window
            MainWindow = mainWindow;
        }
    }
}
