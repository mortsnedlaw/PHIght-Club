namespace PHIghtClub.Core;

public sealed class ExportJob
{
    public string JobId { get; init; } = JobIdFactory.NewJobId();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public InputSettings Input { get; init; } = new();
    public DeIdentificationSettings DeIdentification { get; init; } = new();
    public BurnedInPhiSettings BurnedInPhi { get; init; } = new();
    public ImageSafetyPolicy ImageSafety { get; init; } = ImageSafetyPolicy.StrictDefault();
    public ExportSettings Export { get; init; } = new();

    public JobStatus Status { get; set; } = JobStatus.Draft;
}

public static class JobIdFactory
{
    public static string NewJobId() => DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
}

public sealed class InputSettings
{
    public bool UseDicomStorageScp { get; set; } = true;
    public bool UseFolderImport { get; set; }
    public string LocalAeTitle { get; set; } = "PHIGHTCLUB";
    public int Port { get; set; } = 11112;
    public string BindAddress { get; set; } = "0.0.0.0";
    public string StagingFolder { get; set; } = @"D:\PHIghtClub\Staging";
    public string? ImportFolder { get; set; }
}

public sealed class DeIdentificationSettings
{
    public DeIdentificationMode Mode { get; set; } = DeIdentificationMode.Pseudonymization;
    public string ProfileName { get; set; } = "AI Training Strict";
    public bool RemapPatientId { get; set; } = true;
    public bool RemapPatientName { get; set; } = true;
    public bool RemapStudyInstanceUid { get; set; } = true;
    public bool RemapSeriesInstanceUid { get; set; } = true;
    public bool RemapSopInstanceUid { get; set; } = true;
    public bool RemovePrivateTagsUnlessWhitelisted { get; set; } = true;
    public DateOffsetPolicy DateOffset { get; set; } = DateOffsetPolicy.Default();
    public NonPixelObjectPolicy NonPixelObjectPolicy { get; set; } = NonPixelObjectPolicy.ManualReview;
}

public sealed class DateOffsetPolicy
{
    public string Mode { get; set; } = "DeterministicOffset";
    public string Algorithm { get; set; } = "HMAC-SHA256";
    public int MinDays { get; set; } = -365;
    public int MaxDays { get; set; } = 365;
    public bool VaultScoped { get; set; } = true;

    public static DateOffsetPolicy Default() => new();
}

public sealed class BurnedInPhiSettings
{
    public OcrMode OcrMode { get; set; } = OcrMode.WarnAndRequireApproval;
    public OcrAccelerationMode OcrAcceleration { get; set; } = OcrAccelerationMode.GpuCpuAuto;
    public PixelScrubAction PixelAction { get; set; } = PixelScrubAction.Pixelate;
    public string OcrGuarantee { get; set; } = "AdvisoryOnly";
}

public sealed class ImageSafetyPolicy
{
    public ImageSafetyMode Mode { get; set; } = ImageSafetyMode.Strict;
    public bool PreserveOriginalTransferSyntax { get; set; } = true;
    public bool NeverConvertLosslessToLossy { get; set; } = true;
    public bool BlockIfSafeReEncodingUnavailable { get; set; } = true;
    public bool DoNotTouchPixelDataUnlessScrubEnabled { get; set; } = true;
    public bool ValidateOutputAfterWrite { get; set; } = true;

    public static ImageSafetyPolicy StrictDefault() => new();
}

public sealed class ExportSettings
{
    public ExportTargetType TargetType { get; set; } = ExportTargetType.Folder;
    public string OutputPath { get; set; } = @"D:\PHIghtClub\Export";
    public string DestinationAeTitle { get; set; } = "AITRAINING";
    public string DestinationHost { get; set; } = "127.0.0.1";
    public int DestinationPort { get; set; } = 104;
    public bool RequireCEchoBeforeExport { get; set; } = true;
    public bool CreateJsonManifest { get; set; } = true;
    public bool CreateCsvSummary { get; set; } = true;
}
