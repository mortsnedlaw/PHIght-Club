using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace PHIghtClub.Core.Logging;

/// <summary>
/// DI extensions for configuring logging and audit infrastructure.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>
    /// Configure Serilog with file and console sinks, and register audit logger.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="logDirectory">Directory where log files will be written. Defaults to ./logs</param>
    /// <param name="minimumLevel">Minimum log level. Defaults to Information in production, Debug in development.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddPhIghtClubLogging(
        this IServiceCollection services,
        string? logDirectory = null,
        LogEventLevel? minimumLevel = null)
    {
        logDirectory ??= Path.Combine(AppContext.BaseDirectory, "logs");
        minimumLevel ??= LogEventLevel.Information;

        // Ensure logs directory exists
        Directory.CreateDirectory(logDirectory);

        // Configure Serilog
        var logPath = Path.Combine(logDirectory, "phightclub-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel.Value)
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Register Serilog logger instance for DI and audit logger
        services.AddSingleton<Serilog.ILogger>(Log.Logger);
        services.AddSingleton<IAuditLogger>(new SerilogAuditLogger(Log.Logger));

        return services;
    }
}
