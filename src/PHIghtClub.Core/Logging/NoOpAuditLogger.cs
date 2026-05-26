namespace PHIghtClub.Core.Logging;

/// <summary>
/// No-op audit logger for scenarios where logging is not available.
/// Used as fallback to avoid null reference exceptions.
/// </summary>
public sealed class NoOpAuditLogger : IAuditLogger
{
    public void LogPseudonymizationCreated(string originalPatientId, string pseudoId, string vaultId, DateTime timestamp)
    {
        // No operation
    }

    public void LogDateOffsetApplied(string pseudoId, int offsetDays, string policyName, DateTime timestamp)
    {
        // No operation
    }

    public void LogManifestSigned(string manifestId, string keyId, string signature, int objectCount, DateTime timestamp)
    {
        // No operation
    }

    public void LogManifestVerificationAttempt(string manifestId, bool verified, string? reason, DateTime timestamp)
    {
        // No operation
    }

    public void LogExportDecision(string jobId, bool approved, IReadOnlyList<string> errors, IReadOnlyList<string> warnings, DateTime timestamp)
    {
        // No operation
    }

    public void LogValidationDecision(string jobId, bool passed, IReadOnlyList<string> errors, IReadOnlyList<string> warnings, DateTime timestamp)
    {
        // No operation
    }

    public void LogExportCompleted(string jobId, int instancesExported, string exportTarget, DateTime timestamp)
    {
        // No operation
    }

    public void LogSecurityEvent(string eventType, string details, DateTime timestamp)
    {
        // No operation
    }
}
