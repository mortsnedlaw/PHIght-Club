using System.Windows;
using PHIghtClub.Core;
using Serilog;

namespace PHIghtClub.App;

public partial class App : Application
{
    public App()
    {
        // Initialize services and logging before UI starts
        try
        {
            ServiceLocator.Initialize();
            var logger = ServiceLocator.GetService<Serilog.ILogger>();
            logger.Information("PHIght Club v1.0.0 application started");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize application: {ex.Message}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }
}
