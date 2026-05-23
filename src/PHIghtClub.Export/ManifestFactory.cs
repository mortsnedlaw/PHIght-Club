using PHIghtClub.Core;

namespace PHIghtClub.Export;

public sealed class ManifestFactory
{
    public ExportManifest CreateDryRunManifest(ExportJob job, IReadOnlyList<string> warnings)
    {
        return new ExportManifest
        {
            JobId = job.JobId,
            Profile = job.DeIdentification.ProfileName,
            Mode = job.DeIdentification.Mode.ToString(),
            Input = new ManifestInput
            {
                Source = job.Input.UseDicomStorageScp ? "DICOM Storage SCP" : "Folder Import",
                AeTitle = job.Input.LocalAeTitle,
                Port = job.Input.Port
            },
            Output = new ManifestOutput
            {
                Type = job.Export.TargetType.ToString(),
                Path = job.Export.OutputPath
            },
            Counts = new ManifestCounts
            {
                Studies = 0,
                Series = 0,
                Instances = 0,
                BlockedInstances = 0,
                QuarantinedInstances = 0
            },
            DeIdentification = new ManifestDeIdentification
            {
                PatientIdRemapped = job.DeIdentification.RemapPatientId,
                UidsRemapped = job.DeIdentification.RemapStudyInstanceUid && job.DeIdentification.RemapSeriesInstanceUid && job.DeIdentification.RemapSopInstanceUid,
                PrivateTagsRemoved = job.DeIdentification.RemovePrivateTagsUnlessWhitelisted,
                DateHandling = job.DeIdentification.DateOffset.Mode,
                DateOffsetAlgorithm = job.DeIdentification.DateOffset.Algorithm,
                DateOffsetRangeDays = $"{job.DeIdentification.DateOffset.MinDays}..{job.DeIdentification.DateOffset.MaxDays}",
                DateOffsetVaultScoped = job.DeIdentification.DateOffset.VaultScoped
            },
            Ocr = new ManifestOcr
            {
                Mode = job.BurnedInPhi.OcrMode.ToString(),
                AccelerationRequested = job.BurnedInPhi.OcrAcceleration.ToString(),
                BackendUsed = "NotRun",
                Guarantee = job.BurnedInPhi.OcrGuarantee
            },
            ImageSafety = new ManifestImageSafety
            {
                Mode = job.ImageSafety.Mode.ToString(),
                PixelDataModified = false,
                LossyCompressionIntroduced = false,
                TransferSyntaxPreservedWherePossible = true,
                BlockedUnsafeReencoding = false
            },
            Warnings = warnings,
            Status = "DryRun"
        };
    }
}
