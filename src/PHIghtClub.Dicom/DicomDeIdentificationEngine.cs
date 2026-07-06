using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using FellowOakDicom;
using PHIghtClub.Core;
using PHIghtClub.DeIdentification;

namespace PHIghtClub.Dicom;

public sealed class DicomDeIdentificationEngine
{
    private readonly IDateOffsetService _dateOffsetService;
    private readonly IPseudonymizationService _pseudonymizationService;
    private readonly DicomExportPolicyEngine _policyEngine;

    public DicomDeIdentificationEngine()
        : this(new DeterministicDateOffsetService(), new HmacPseudonymizationService(), new DicomExportPolicyEngine())
    {
    }

    internal DicomDeIdentificationEngine(IDateOffsetService dateOffsetService, IPseudonymizationService pseudonymizationService, DicomExportPolicyEngine policyEngine)
    {
        _dateOffsetService = dateOffsetService;
        _pseudonymizationService = pseudonymizationService;
        _policyEngine = policyEngine;
    }

    public (DicomFile ProcessedFile, DeIdentificationChangeLog ChangeLog) ApplyWithTracking(DicomFile file, DeIdentificationSettings settings, byte[] vaultSecret)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        if (vaultSecret is null || vaultSecret.Length < 32)
        {
            throw new ArgumentException("Vault secret must be at least 32 bytes.", nameof(vaultSecret));
        }

        var policyResult = _policyEngine.Evaluate(file, settings);
        if (!policyResult.Passed)
        {
            throw new InvalidDataException($"DICOM object rejected by export policy: {policyResult.Reason}");
        }

        DicomValidator.Validate(file);

        var output = file.Clone();
        var dataset = output.Dataset;
        var changeLog = new DeIdentificationChangeLog
        {
            OriginalTransferSyntax = file.FileMetaInfo?.TransferSyntax?.UID?.UID ?? "Unknown"
        };

        var stablePseudoSubjectId = GetStablePseudoSubjectId(dataset);

        TrackOriginalUids(dataset, changeLog);
        ApplyIdentifyingMetadataReplacements(dataset, settings, vaultSecret, changeLog);

        if (settings.RemovePrivateTagsUnlessWhitelisted)
        {
            RemovePrivateTagsRecursively(dataset, DicomDeIdentificationProfile.CreateFromSettings(settings).PrivateTagWhitelist);
        }

        ProcessDatasetRecursively(dataset, settings, vaultSecret, changeLog, stablePseudoSubjectId);

        if (output.FileMetaInfo is not null && dataset.TryGetString(DicomTag.SOPInstanceUID, out var sopInstanceUid))
        {
            output.FileMetaInfo.MediaStorageSOPInstanceUID = new DicomUID(sopInstanceUid, "Pseudo SOP Instance UID", DicomUidType.SOPInstance);
        }

        changeLog.OutputTransferSyntax = output.FileMetaInfo?.TransferSyntax?.UID?.UID ?? "Unknown";
        TrackRemappedUids(dataset, changeLog);

        return (output, changeLog);
    }

    private static void TrackOriginalUids(DicomDataset dataset, DeIdentificationChangeLog changeLog)
    {
        if (dataset.TryGetString(DicomTag.StudyInstanceUID, out var studyUid))
        {
            changeLog.OriginalStudyInstanceUid = studyUid;
        }

        if (dataset.TryGetString(DicomTag.SeriesInstanceUID, out var seriesUid))
        {
            changeLog.OriginalSeriesInstanceUid = seriesUid;
        }

        if (dataset.TryGetString(DicomTag.SOPInstanceUID, out var sopUid))
        {
            changeLog.OriginalSopInstanceUid = sopUid;
        }
    }

    private static void TrackRemappedUids(DicomDataset dataset, DeIdentificationChangeLog changeLog)
    {
        if (dataset.TryGetString(DicomTag.StudyInstanceUID, out var studyUid))
        {
            changeLog.RemappedStudyInstanceUid = studyUid;
        }

        if (dataset.TryGetString(DicomTag.SeriesInstanceUID, out var seriesUid))
        {
            changeLog.RemappedSeriesInstanceUid = seriesUid;
        }

        if (dataset.TryGetString(DicomTag.SOPInstanceUID, out var sopUid))
        {
            changeLog.RemappedSopInstanceUid = sopUid;
        }
    }

    private static string GetStablePseudoSubjectId(DicomDataset dataset)
    {
        if (dataset.TryGetString(DicomTag.PatientID, out var patientId) && !string.IsNullOrWhiteSpace(patientId))
        {
            return patientId.Trim();
        }

        if (dataset.TryGetString(DicomTag.SOPInstanceUID, out var sopUid) && !string.IsNullOrWhiteSpace(sopUid))
        {
            return sopUid.Trim();
        }

        return Guid.NewGuid().ToString("N");
    }

    private static void ApplyIdentifyingMetadataReplacements(DicomDataset dataset, DeIdentificationSettings settings, byte[] vaultSecret, DeIdentificationChangeLog changeLog)
    {
        if (settings.RemapPatientId && dataset.TryGetString(DicomTag.PatientID, out var patientId))
        {
            if (settings.Mode == DeIdentificationMode.Pseudonymization)
            {
                dataset.AddOrUpdate(DicomTag.PatientID, _StaticPseudoPatientId(patientId, vaultSecret));
            }
            else
            {
                dataset.AddOrUpdate(DicomTag.PatientID, "ANONYMIZED");
            }

            changeLog.PatientIdChanged = true;
        }

        if (settings.RemapPatientName && dataset.TryGetString(DicomTag.PatientName, out var patientName))
        {
            if (settings.Mode == DeIdentificationMode.Pseudonymization)
            {
                dataset.AddOrUpdate(DicomTag.PatientName, _StaticPseudoPatientId(patientName, vaultSecret));
            }
            else
            {
                dataset.AddOrUpdate(DicomTag.PatientName, "ANONYMIZED");
            }

            changeLog.PatientNameChanged = true;
        }

        if (settings.RemapStudyInstanceUid && dataset.TryGetString(DicomTag.StudyInstanceUID, out var studyUid))
        {
            dataset.AddOrUpdate(DicomTag.StudyInstanceUID, CreateDeterministicUid(studyUid, vaultSecret));
            changeLog.StudyUidChanged = true;
        }

        if (settings.RemapSeriesInstanceUid && dataset.TryGetString(DicomTag.SeriesInstanceUID, out var seriesUid))
        {
            dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, CreateDeterministicUid(seriesUid, vaultSecret));
            changeLog.SeriesUidChanged = true;
        }

        if (settings.RemapSopInstanceUid && dataset.TryGetString(DicomTag.SOPInstanceUID, out var sopUid))
        {
            var remapped = CreateDeterministicUid(sopUid, vaultSecret);
            dataset.AddOrUpdate(DicomTag.SOPInstanceUID, remapped);
            changeLog.SopUidChanged = true;
        }
    }

    private static void ProcessDatasetRecursively(DicomDataset dataset, DeIdentificationSettings settings, byte[] vaultSecret, DeIdentificationChangeLog changeLog, string stablePseudoSubjectId)
    {
        var items = dataset.ToArray();
        var offsetDays = settings.DateOffset.VaultScoped
            ? new DeterministicDateOffsetService().GetOffsetDays(stablePseudoSubjectId, vaultSecret, settings.DateOffset)
            : 0;

        foreach (var item in items)
        {
            if (item.ValueRepresentation == DicomVR.SQ && item is DicomSequence sequence)
            {
                foreach (var nested in sequence.Items)
                {
                    ProcessDatasetRecursively(nested, settings, vaultSecret, changeLog, stablePseudoSubjectId);
                }

                continue;
            }

            if (item.Tag == DicomTag.PixelData && settings.Mode == DeIdentificationMode.MetadataAnonymization)
            {
                continue;
            }

            if (item.ValueRepresentation == DicomVR.UI && ShouldRemapUid(item.Tag))
            {
                if (dataset.TryGetValues(item.Tag, out string[] parallelUids))
                {
                    var remapped = parallelUids.Select(uid => string.IsNullOrWhiteSpace(uid) ? uid : CreateDeterministicUid(uid, vaultSecret)).ToArray();
                    dataset.AddOrUpdate(item.Tag, remapped);
                }

                continue;
            }

            if (offsetDays != 0 && (item.ValueRepresentation == DicomVR.DA || item.ValueRepresentation == DicomVR.DT || item.ValueRepresentation == DicomVR.TM))
            {
                if (dataset.TryGetValues(item.Tag, out string[] values))
                {
                    var updated = values.Select(value => OffsetDicomValue(value, item.ValueRepresentation, offsetDays)).ToArray();
                    dataset.AddOrUpdate(item.Tag, updated);
                    changeLog.DateOffsetDays = offsetDays;
                }
            }
        }
    }

    private static string OffsetDicomValue(string value, DicomVR vr, int offsetDays)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (vr == DicomVR.DA)
        {
            if (DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date.AddDays(offsetDays).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }

            return value;
        }

        if (vr == DicomVR.DT && value.Length >= 8)
        {
            var prefix = value[..8];
            if (DateOnly.TryParseExact(prefix, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                var remainder = value.Length > 8 ? value[8..] : string.Empty;
                return date.AddDays(offsetDays).ToString("yyyyMMdd", CultureInfo.InvariantCulture) + remainder;
            }

            return value;
        }

        // Preserve TM metadata-only as time-only values have no date context.
        return value;
    }

    private static bool ShouldRemapUid(DicomTag tag)
    {
        return tag != DicomTag.SOPClassUID
            && tag != DicomTag.TransferSyntaxUID
            && tag != DicomTag.MediaStorageSOPClassUID
            && tag != DicomTag.MediaStorageSOPInstanceUID
            && tag != DicomTag.ImplementationClassUID
            && tag != DicomTag.ImplementationVersionName
            && tag != DicomTag.SourceApplicationEntityTitle
            && tag != DicomTag.InstanceCreatorUID;
    }

    private static void RemovePrivateTagsRecursively(DicomDataset dataset, IReadOnlySet<DicomTag> whitelist)
    {
        var privateItems = dataset.Where(item => item.Tag.IsPrivate && !whitelist.Contains(item.Tag)).ToArray();
        foreach (var privateItem in privateItems)
        {
            dataset.Remove(privateItem.Tag);
        }

        foreach (var sequenceItem in dataset.Where(item => item.ValueRepresentation == DicomVR.SQ).OfType<DicomSequence>())
        {
            foreach (var nested in sequenceItem.Items)
            {
                RemovePrivateTagsRecursively(nested, whitelist);
            }
        }
    }

    private static string CreateDeterministicUid(string input, byte[] vaultSecret)
    {
        var normalized = string.IsNullOrWhiteSpace(input) ? Guid.NewGuid().ToString("N") : input.Trim();
        using var hmac = new HMACSHA256(vaultSecret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        var numeric = new BigInteger(hash, isUnsigned: true, isBigEndian: true);
        var uid = "2.25." + BigInteger.Abs(numeric).ToString(CultureInfo.InvariantCulture);
        return uid.Length <= 64 ? uid : uid[..64];
    }

    private static string _StaticPseudoPatientId(string value, byte[] vaultSecret)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "PSEUDO-UNKNOWN";
        }

        using var hmac = new HMACSHA256(vaultSecret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value.Trim()));
        return "PSEUDO-" + Convert.ToHexString(hash)[..16];
    }
}

public sealed class DicomExportPolicyEngine
{
    private static readonly HashSet<string> BlockedNonImageSopClasses = new(StringComparer.Ordinal)
    {
        DicomUID.BasicTextSRStorage.UID,
        DicomUID.EnhancedSRStorage.UID,
        DicomUID.ComprehensiveSRStorage.UID,
        DicomUID.KeyObjectSelectionDocumentStorage.UID,
        DicomUID.EncapsulatedPDFStorage.UID,
        DicomUID.GrayscaleSoftcopyPresentationStateStorage.UID,
        DicomUID.ColorSoftcopyPresentationStateStorage.UID,
        DicomUID.PseudoColorSoftcopyPresentationStateStorage.UID,
        DicomUID.BlendingSoftcopyPresentationStateStorage.UID,
        DicomUID.MRSpectroscopyStorage.UID // image classes are still allowed if pixel data is present
    };

    public DicomExportPolicyResult Evaluate(DicomFile file, DeIdentificationSettings settings)
    {
        if (file is null)
        {
            return DicomExportPolicyResult.Block("DICOM object is null.");
        }

        try
        {
            DicomValidator.Validate(file);
        }
        catch (Exception ex)
        {
            return DicomExportPolicyResult.Block(ex.Message);
        }

        var sopClassUid = file.FileMetaInfo?.MediaStorageSOPClassUID?.UID ?? file.Dataset.GetSingleValueOrDefault(DicomTag.SOPClassUID, string.Empty);
        if (string.IsNullOrWhiteSpace(sopClassUid))
        {
            return DicomExportPolicyResult.Block("Missing SOP Class UID.");
        }

        if (BlockedNonImageSopClasses.Contains(sopClassUid))
        {
            return DicomExportPolicyResult.Block($"Blocked non-image or text-bearing SOP class: {sopClassUid}");
        }

        try
        {
            var uid = DicomUID.Parse(sopClassUid);
            if (uid.StorageCategory != DicomStorageCategory.Image)
            {
                if (settings.NonPixelObjectPolicy == NonPixelObjectPolicy.AllowWithMetadataScrub)
                {
                    return DicomExportPolicyResult.Allow();
                }

                return DicomExportPolicyResult.Block($"Non-image DICOM object without explicit policy: {sopClassUid}");
            }
        }
        catch
        {
            return DicomExportPolicyResult.Block($"Invalid SOP Class UID: {sopClassUid}");
        }

        if (!file.Dataset.Contains(DicomTag.PixelData))
        {
            if (settings.NonPixelObjectPolicy == NonPixelObjectPolicy.AllowWithMetadataScrub)
            {
                return DicomExportPolicyResult.Allow();
            }

            return DicomExportPolicyResult.Block($"DICOM object is missing PixelData and is blocked by current safety policy: {sopClassUid}");
        }

        return DicomExportPolicyResult.Allow();
    }
}

public sealed class DicomExportPolicyResult
{
    private DicomExportPolicyResult(bool passed, string reason)
    {
        Passed = passed;
        Reason = reason;
    }

    public bool Passed { get; }
    public string Reason { get; }

    public static DicomExportPolicyResult Allow() => new(true, string.Empty);
    public static DicomExportPolicyResult Block(string reason) => new(false, reason);
}

public sealed class DicomDeIdentificationProfile
{
    public string Name { get; init; } = string.Empty;
    public bool RemovePrivateTagsUnlessWhitelisted { get; init; }
    public IReadOnlySet<DicomTag> PrivateTagWhitelist { get; init; } = new HashSet<DicomTag>();

    public static DicomDeIdentificationProfile CreateFromSettings(DeIdentificationSettings settings)
    {
        return new DicomDeIdentificationProfile
        {
            Name = settings.ProfileName,
            RemovePrivateTagsUnlessWhitelisted = settings.RemovePrivateTagsUnlessWhitelisted,
            PrivateTagWhitelist = settings.RemovePrivateTagsUnlessWhitelisted
                ? new HashSet<DicomTag>
                {
                    // Keep private tags needed for pixel data/encoding or vendor-specific identifiers when explicitly allowed
                    DicomTag.PrivateInformationCreatorUID,
                    DicomTag.PrivateInformation
                }
                : new HashSet<DicomTag>()
        };
    }
}
