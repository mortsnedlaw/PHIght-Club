using Serilog;

namespace PHIghtClub.Core.Logging;

/// <summary>
/// Serilog-based audit logger for production use.
/// Writes to structured logs in files and console.
/// </summary>
public sealed class SerilogAuditLogger : IAuditLogger
{
    private readonly ILogger _logger;

    public SerilogAuditLogger(ILogger? logger = null)
    {
        _logger = logger ?? Log.Logger;
    }

    public void LogPseudonymizationCreated(string originalPatientId, string pseudoId, string vaultId, DateTime timestamp)
    {
        _logger.Information(
            "Pseudonymization created: OriginalPatientId={OriginalPatientId}, PseudoId={PseudoId}, VaultId={VaultId}, Timestamp={Timestamp}",
            originalPatientId, pseudoId, vaultId, timestamp.ToUniversalTime());
    }

    public void LogDateOffsetApplied(string pseudoId, int offsetDays, string policyName, DateTime timestamp)
    {
        _logger.Information(
            "Date offset applied: PseudoId={PseudoId}, OffsetDays={OffsetDays}, PolicyName={PolicyName}, Timestamp={Timestamp}",
            pseudoId, offsetDays, policyName, timestamp.ToUniversalTime());
    }

    public void LogManifestSigned(string manifestId, string keyId, string signature, int objectCount, DateTime timestamp)
    {
        _logger.Information(
            "Manifest signed: ManifestId={ManifestId}, KeyId={KeyId}, ObjectCount={ObjectCount}, Timestamp={Timestamp}",
            manifestId, keyId, objectCount, timestamp.ToUniversalTime());
    }

    public void LogManifestVerificationAttempt(string manifestId, bool verified, string? reason, DateTime timestamp)
    {
        if (verified)
        {
            _logger.Information(
                "Manifest verification succeeded: ManifestId={ManifestId}, Timestamp={Timestamp}",
                manifestId, timestamp.ToUniversalTime());
        }
        else
        {
            _logger.Warning(
                "Manifest verification failed: ManifestId={ManifestId}, Reason={Reason}, Timestamp={Timestamp}",
                manifestId, reason ?? "Unknown", timestamp.ToUniversalTime());
        }
    }

    public void LogExportDecision(string jobId, bool approved, IReadOnlyList<string> errors, IReadOnlyList<string> warnings, DateTime timestamp)
    {
        if (approved)
        {
            _logger.Information(
                "Export approved: JobId={JobId}, Warnings={WarningCount}, Timestamp={Timestamp}",
                jobId, warnings.Count, timestamp.ToUniversalTime());
        }
        else
        {
            _logger.Warning(
                "Export blocked: JobId={JobId}, Errors={ErrorCount}, Warnings={WarningCount}, Timestamp={Timestamp}",
                jobId, errors.Count, warnings.Count, timestamp.ToUniversalTime());
            foreach (var error in errors)
            {
                _logger.Warning("Export error: {Error}", error);
            }
        }
    }

    public void LogValidationDecision(string jobId, bool passed, IReadOnlyList<string> errors, IReadOnlyList<string> warnings, DateTime timestamp)
    {
        if (passed)
        {
            _logger.Information(
                "Validation passed: JobId={JobId}, Warnings={WarningCount}, Timestamp={Timestamp}",
                jobId, warnings.Count, timestamp.ToUniversalTime());
        }
        else
        {
            _logger.Warning(
                "Validation failed: JobId={JobId}, Errors={ErrorCount}, Timestamp={Timestamp}",
                jobId, errors.Count, timestamp.ToUniversalTime());
            foreach (var error in errors)
            {
                _logger.Warning("Validation error: {Error}", error);
            }
        }
    }

    public void LogExportCompleted(string jobId, int instancesExported, string exportTarget, DateTime timestamp)
    {
        _logger.Information(
            "Export completed: JobId={JobId}, InstancesExported={InstancesExported}, Target={ExportTarget}, Timestamp={Timestamp}",
            jobId, instancesExported, exportTarget, timestamp.ToUniversalTime());
    }

    public void LogSecurityEvent(string eventType, string details, DateTime timestamp)
    {
        _logger.Warning(
            "Security event: EventType={EventType}, Details={Details}, Timestamp={Timestamp}",
            eventType, details, timestamp.ToUniversalTime());
    }
}
