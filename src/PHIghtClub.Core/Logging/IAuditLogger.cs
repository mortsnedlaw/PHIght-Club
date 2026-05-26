namespace PHIghtClub.Core.Logging;

/// <summary>
/// Audit logger for compliance and tamper-detection.
/// All pseudonymization and export operations must be logged.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Log pseudonymization of a patient ID.
    /// </summary>
    void LogPseudonymizationCreated(string originalPatientId, string pseudoId, string vaultId, DateTime timestamp);

    /// <summary>
    /// Log deterministic date offset application.
    /// </summary>
    void LogDateOffsetApplied(string pseudoId, int offsetDays, string policyName, DateTime timestamp);

    /// <summary>
    /// Log manifest signing for export.
    /// </summary>
    void LogManifestSigned(string manifestId, string keyId, string signature, int objectCount, DateTime timestamp);

    /// <summary>
    /// Log manifest verification attempt.
    /// </summary>
    void LogManifestVerificationAttempt(string manifestId, bool verified, string? reason, DateTime timestamp);

    /// <summary>
    /// Log export operation decision.
    /// </summary>
    void LogExportDecision(string jobId, bool approved, IReadOnlyList<string> errors, IReadOnlyList<string> warnings, DateTime timestamp);

    /// <summary>
    /// Log validation decision.
    /// </summary>
    void LogValidationDecision(string jobId, bool passed, IReadOnlyList<string> errors, IReadOnlyList<string> warnings, DateTime timestamp);

    /// <summary>
    /// Log export completion.
    /// </summary>
    void LogExportCompleted(string jobId, int instancesExported, string exportTarget, DateTime timestamp);

    /// <summary>
    /// Log security-relevant events (e.g., key usage, vault access).
    /// </summary>
    void LogSecurityEvent(string eventType, string details, DateTime timestamp);
}
