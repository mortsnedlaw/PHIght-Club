namespace PHIghtClub.Core;

public enum DeIdentificationMode
{
    MetadataAnonymization,
    Pseudonymization
}

public enum OcrMode
{
    Off,
    WarnAndRequireApproval,
    AutoMaskUsingApprovedTemplates
}

public enum OcrAccelerationMode
{
    GpuOnly,
    GpuCpuAuto,
    CpuOnly
}

public enum OcrBackend
{
    Disabled,
    DirectML,
    Cuda,
    Cpu
}

public enum ImageSafetyMode
{
    Strict,
    Balanced,
    Custom
}

public enum ExportTargetType
{
    Folder,
    Zip,
    DicomCStore
}

public enum PixelScrubAction
{
    BlackMask,
    Pixelate,
    Blur
}

public enum NonPixelObjectPolicy
{
    AllowWithMetadataScrub,
    ManualReview,
    Block,
    Quarantine
}

public enum JobStatus
{
    Draft,
    DryRun,
    Running,
    Completed,
    CompletedWithWarnings,
    Blocked,
    Failed
}
