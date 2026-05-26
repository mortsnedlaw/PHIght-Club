using Microsoft.Extensions.DependencyInjection;
using PHIghtClub.Core.Logging;
using PHIghtClub.Dicom;
using PHIghtClub.Export;
using PHIghtClub.Storage;
using Serilog;

namespace PHIghtClub.App;

/// <summary>
/// Service locator for dependency injection in WPF (which lacks built-in DI).
/// </summary>
public static class ServiceLocator
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initialize the service provider with all required services.
    /// Must be called before using GetService.
    /// </summary>
    public static void Initialize()
    {
        var services = new ServiceCollection();

        // Configure logging
        services.AddPhIghtClubLogging();

        // Register core services
        services.AddSingleton<IManifestIntegrityService, ManifestIntegrityService>();
        services.AddSingleton<IDicomImportService, NoopDicomImportService>();
        services.AddSingleton<ManifestFactory>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// Get a service instance from the service provider.
    /// </summary>
    public static T GetService<T>() where T : notnull
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
        }

        return _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Get the underlying service provider.
    /// </summary>
    public static IServiceProvider Provider
    {
        get
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
            }
            return _serviceProvider;
        }
    }
}
