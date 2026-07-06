using PHIghtClub.Core;

namespace PHIghtClub.Export;

public sealed class ManifestFactory
{
    public ExportManifest CreateDryRunManifest(ExportJob job, IReadOnlyList<string> warnings, IReadOnlyList<ManifestObjectEntry> objects)
    {
        var source = job.Input.UseDicomStorageScp && job.Input.UseFolderImport
            ? "DICOM Storage SCP + Folder Import"
            : job.Input.UseDicomStorageScp
                ? "DICOM Storage SCP"
                : "Folder Import";

        return new ExportManifest
        {
            JobId = job.JobId,
            Profile = job.DeIdentification.ProfileName,
            Mode = job.DeIdentification.Mode.ToString(),
            Input = new ManifestInput
            {
                Source = source,
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
                Instances = job.ImportSummary.InstancesAccepted,
                BlockedInstances = 0,
                QuarantinedInstances = job.ImportSummary.InstancesQuarantined
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
            Objects = objects,
            Warnings = warnings,
            Status = "DryRun"
        };
    }
}
