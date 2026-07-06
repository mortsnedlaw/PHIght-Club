using System.Text.Json.Serialization;

namespace PHIghtClub.Core;

public sealed class ExportManifest
{
    public string Application { get; init; } = "PHIght Club";
    public string Version { get; init; } = "0.1.0";
    public required string JobId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Profile { get; init; } = "AI Training Strict";
    public string Mode { get; init; } = "Pseudonymization";
    public ManifestInput Input { get; init; } = new();
    public ManifestOutput Output { get; init; } = new();
    public ManifestCounts Counts { get; init; } = new();
    public ManifestDeIdentification DeIdentification { get; init; } = new();
    public ManifestOcr Ocr { get; init; } = new();
    public ManifestImageSafety ImageSafety { get; init; } = new();
    public IReadOnlyList<ManifestObjectEntry> Objects { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string Status { get; init; } = "DryRun";
    public ManifestIntegrity? ManifestIntegrity { get; set; }
    public string? AuditLogPath { get; set; }
}

public sealed class ManifestObjectEntry
{
    public string FileName { get; init; } = string.Empty;
    public string SopClassUid { get; init; } = string.Empty;
    public string OriginalStudyInstanceUid { get; init; } = string.Empty;
    public string OriginalSeriesInstanceUid { get; init; } = string.Empty;
    public string OriginalSopInstanceUid { get; init; } = string.Empty;
    public string RemappedStudyInstanceUid { get; init; } = string.Empty;
    public string RemappedSeriesInstanceUid { get; init; } = string.Empty;
    public string RemappedSopInstanceUid { get; init; } = string.Empty;
    public string TransferSyntaxOriginal { get; init; } = string.Empty;
    public string TransferSyntaxOutput { get; init; } = string.Empty;
    public bool PixelDataModified { get; init; }
    public string Sha256Hash { get; init; } = string.Empty;
}

public sealed class ManifestInput
{
    public string Source { get; init; } = "DICOM Storage SCP";
    public string AeTitle { get; init; } = "PHIGHTCLUB";
    public int Port { get; init; } = 11112;
}

public sealed class ManifestOutput
{
    public string Type { get; init; } = "Folder";
    public string Path { get; init; } = @"D:\PHIghtClub\Export";
}

public sealed class ManifestCounts
{
    public int Studies { get; init; }
    public int Series { get; init; }
    public int Instances { get; init; }
    public int BlockedInstances { get; init; }
    public int QuarantinedInstances { get; init; }
}

public sealed class ManifestDeIdentification
{
    public bool PatientIdRemapped { get; init; } = true;
    public bool UidsRemapped { get; init; } = true;
    public bool PrivateTagsRemoved { get; init; } = true;
    public string DateHandling { get; init; } = "DeterministicOffset";
    public string DateOffsetAlgorithm { get; init; } = "HMAC-SHA256";
    public string DateOffsetRangeDays { get; init; } = "-365..365";
    public bool DateOffsetVaultScoped { get; init; } = true;
    public string DicomStandardReference { get; init; } = "DICOM PS3.15 Annex E";
}

public sealed class ManifestOcr
{
    public string Mode { get; init; } = "WarnAndRequireApproval";
    public string AccelerationRequested { get; init; } = "GpuCpuAuto";
    public string BackendUsed { get; init; } = "NotRun";
    public bool CpuFallbackUsed { get; init; }
    public int RegionsDetected { get; init; }
    public int RegionsApproved { get; init; }
    public string Guarantee { get; init; } = "AdvisoryOnly";
}

public sealed class ManifestImageSafety
{
    public string Mode { get; init; } = "Strict";
    public bool PixelDataModified { get; init; }
    public bool LossyCompressionIntroduced { get; init; }
    public bool TransferSyntaxPreservedWherePossible { get; init; } = true;
    public bool BlockedUnsafeReencoding { get; init; }
}

public sealed class ManifestIntegrity
{
    public string Canonicalization { get; init; } = "json-canonical-v1";
    public required string ManifestSha256 { get; init; }
    public required string ObjectListSha256 { get; init; }
    public string HmacAlgorithm { get; init; } = "HMAC-SHA256";
    public required string HmacKeyId { get; init; }
    public required string Signature { get; init; }
}
