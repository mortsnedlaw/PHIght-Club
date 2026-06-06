using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using FellowOakDicom.Imaging.Codec;
using FellowOakDicom.Memory;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using FellowOakDicom.Network.Tls;
using Microsoft.Extensions.Logging;
using PHIghtClub.Core;
using PHIghtClub.DeIdentification;

namespace PHIghtClub.Dicom;

public sealed class DicomImportService : IDicomImportService
{
    public async Task<DicomImportResult> ImportFolderAsync(string folder, string stagingFolder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return new DicomImportResult
            {
                FilesScanned = 0,
                InstancesAccepted = 0,
                InstancesQuarantined = 0,
                Warnings = new[] { "Import folder not found. Please select an existing folder." }
            };
        }

        var acceptedDirectory = Path.Combine(stagingFolder, "accepted");
        var quarantineDirectory = Path.Combine(stagingFolder, "quarantine");
        Directory.CreateDirectory(acceptedDirectory);
        Directory.CreateDirectory(quarantineDirectory);
        var audit = new AuditLogger(stagingFolder);

        var scanned = 0;
        var accepted = 0;
        var quarantined = 0;
        var warnings = new List<string>();

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                warnings.Add("Folder scan was cancelled.");
                break;
            }

            scanned++;

            try
            {
                var dicomFile = await DicomFile.OpenAsync(file, FileReadOption.ReadAll, 4096).ConfigureAwait(false);
                DicomValidator.Validate(dicomFile);

                var destinationFile = Path.Combine(acceptedDirectory, GetSafeDestinationFileName(Path.GetFileName(file)));
                dicomFile.Save(destinationFile);
                accepted++;
                audit.LogEvent("Import", "Accepted object", $"Source={file}; Destination={destinationFile}");
            }
            catch (Exception ex)
            {
                quarantined++;
                var quarantinePath = Path.Combine(quarantineDirectory, GetSafeDestinationFileName(Path.GetFileName(file)));
                try
                {
                    File.Copy(file, quarantinePath, overwrite: true);
                    audit.LogEvent("ImportQuarantine", "Quarantined invalid object", $"Source={file}; Destination={quarantinePath}; Reason={ex.Message}");
                }
                catch
                {
                    audit.LogEvent("ImportQuarantine", "Failed to quarantine invalid object", $"Source={file}; Reason={ex.Message}");
                }
                warnings.Add($"Quarantined invalid file: {Path.GetFileName(file)}");
            }
        }

        if (accepted == 0)
        {
            warnings.Add("No valid DICOM objects were accepted. Please review the quarantine folder for malformed or unsupported files.");
        }

        return new DicomImportResult
        {
            FilesScanned = scanned,
            InstancesAccepted = accepted,
            InstancesQuarantined = quarantined,
            Warnings = warnings
        };
    }

    private static string GetSafeDestinationFileName(string originalFileName)
    {
        var name = Path.GetFileNameWithoutExtension(originalFileName);
        name = string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "dicom";
        }

        return name + "-" + Guid.NewGuid().ToString("N") + ".dcm";
    }
}

public sealed class DicomStorageScpService : IDicomStorageScpService, IDisposable
{
    private IDicomServer? _server;
    private StorageScpServerState? _serviceState;
    private ILoggerFactory? _loggerFactory;

    public Task StartAsync(DicomReceiveSettings settings, CancellationToken cancellationToken)
    {
        if (_server is not null)
        {
            throw new InvalidOperationException("DICOM Storage SCP is already running.");
        }

        _serviceState = new StorageScpServerState(settings);
        StorageScpServer.ServiceState = _serviceState;

        _loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None));
        var logger = _loggerFactory.CreateLogger<StorageScpServer>();

        _server = DicomServerFactory.Create<StorageScpServer>(settings.BindAddress, settings.Port, null, Encoding.UTF8, logger, null, options => { });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_server is null)
        {
            return Task.CompletedTask;
        }

        _server.Dispose();
        _server = null;
        StorageScpServer.ServiceState = null;

        _loggerFactory?.Dispose();
        _loggerFactory = null;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _server?.Dispose();
        _server = null;
        StorageScpServer.ServiceState = null;
        _loggerFactory?.Dispose();
        _loggerFactory = null;
    }
}

public sealed class DicomVerificationClient : IDicomVerificationClient
{
    public async Task<bool> CEchoAsync(DicomDestination destination, CancellationToken cancellationToken)
    {
        var request = new DicomCEchoRequest();
        var success = false;

        request.OnResponseReceived += (req, response) =>
        {
            if (response.Status == DicomStatus.Success)
            {
                success = true;
            }
        };

        var client = DicomClientFactory.Create(destination.Host, destination.Port, false, "PHIGHTCLUB", destination.AeTitle);
        await client.AddRequestAsync(request).ConfigureAwait(false);
        await client.SendAsync(cancellationToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation).ConfigureAwait(false);

        return success;
    }
}

public sealed class DicomStoreClient : IDicomStoreClient
{
    public async Task SendAsync(DicomDestination destination, IReadOnlyList<string> dicomFilePaths, CancellationToken cancellationToken)
    {
        if (dicomFilePaths is null || dicomFilePaths.Count == 0)
        {
            throw new ArgumentException("No DICOM files provided for C-STORE.", nameof(dicomFilePaths));
        }

        var client = DicomClientFactory.Create(destination.Host, destination.Port, false, "PHIGHTCLUB", destination.AeTitle);

        foreach (var filePath in dicomFilePaths)
        {
            var dicomFile = DicomFile.Open(filePath, FileReadOption.ReadAll);
            var request = new DicomCStoreRequest(dicomFile);
            await client.AddRequestAsync(request).ConfigureAwait(false);
        }

        await client.SendAsync(cancellationToken, DicomClientCancellationMode.ImmediatelyReleaseAssociation).ConfigureAwait(false);
    }
}

public sealed class DicomMetadataDeIdentificationService
{
    private readonly IDateOffsetService _dateOffsetService;
    private readonly IPseudonymizationService _pseudonymizationService;

    public DicomMetadataDeIdentificationService()
        : this(new DeterministicDateOffsetService(), new HmacPseudonymizationService())
    {
    }

    public DicomMetadataDeIdentificationService(IDateOffsetService dateOffsetService, IPseudonymizationService pseudonymizationService)
    {
        _dateOffsetService = dateOffsetService;
        _pseudonymizationService = pseudonymizationService;
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

        var changeLog = new DeIdentificationChangeLog
        {
            OriginalTransferSyntax = file.FileMetaInfo?.TransferSyntax?.UID?.UID ?? "Unknown"
        };

        var output = file.Clone();
        var dataset = output.Dataset;

        if (settings.Mode == DeIdentificationMode.Pseudonymization)
        {
            if (settings.RemapPatientId && dataset.TryGetString(DicomTag.PatientID, out var patientId))
            {
                dataset.AddOrUpdate(DicomTag.PatientID, _pseudonymizationService.CreatePseudoPatientId(patientId, vaultSecret));
                changeLog.PatientIdChanged = true;
            }

            if (settings.RemapPatientName && dataset.TryGetString(DicomTag.PatientName, out var patientName))
            {
                dataset.AddOrUpdate(DicomTag.PatientName, _pseudonymizationService.CreatePseudoPatientId(patientName, vaultSecret));
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
                var remappedSop = CreateDeterministicUid(sopUid, vaultSecret);
                dataset.AddOrUpdate(DicomTag.SOPInstanceUID, remappedSop);
                if (output.FileMetaInfo is not null)
                {
                    output.FileMetaInfo.MediaStorageSOPInstanceUID = new DicomUID(remappedSop, "Pseudo SOP Instance UID", DicomUidType.SOPInstance);
                }
                changeLog.SopUidChanged = true;
            }

            if (settings.DateOffset.VaultScoped)
            {
                var offsetDays = ApplyDateOffsetAndTrack(dataset, settings.DateOffset, vaultSecret);
                changeLog.DateOffsetDays = offsetDays;
            }
        }
        else
        {
            if (settings.RemapPatientId)
            {
                dataset.AddOrUpdate(DicomTag.PatientID, "ANONYMIZED");
                changeLog.PatientIdChanged = true;
            }

            if (settings.RemapPatientName)
            {
                dataset.AddOrUpdate(DicomTag.PatientName, "ANONYMIZED");
                changeLog.PatientNameChanged = true;
            }

            if (settings.RemapStudyInstanceUid && dataset.Contains(DicomTag.StudyInstanceUID))
            {
                dataset.AddOrUpdate(DicomTag.StudyInstanceUID, CreateDeterministicUid(dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID), vaultSecret));
                changeLog.StudyUidChanged = true;
            }

            if (settings.RemapSeriesInstanceUid && dataset.Contains(DicomTag.SeriesInstanceUID))
            {
                dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, CreateDeterministicUid(dataset.GetSingleValue<string>(DicomTag.SeriesInstanceUID), vaultSecret));
                changeLog.SeriesUidChanged = true;
            }

            if (settings.RemapSopInstanceUid && dataset.Contains(DicomTag.SOPInstanceUID))
            {
                var remappedSop = CreateDeterministicUid(dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID), vaultSecret);
                dataset.AddOrUpdate(DicomTag.SOPInstanceUID, remappedSop);

                if (output.FileMetaInfo is not null)
                {
                    output.FileMetaInfo.MediaStorageSOPInstanceUID = new DicomUID(remappedSop, "Pseudo SOP Instance UID", DicomUidType.SOPInstance);
                }
                changeLog.SopUidChanged = true;
            }
        }

        if (settings.RemovePrivateTagsUnlessWhitelisted)
        {
            RemovePrivateTags(dataset);
        }

        changeLog.OutputTransferSyntax = output.FileMetaInfo?.TransferSyntax?.UID?.UID ?? "Unknown";

        return (output, changeLog);
    }

    private static int ApplyDateOffsetAndTrack(DicomDataset dataset, DateOffsetPolicy policy, byte[] vaultSecret)
    {
        if (!dataset.TryGetString(DicomTag.PatientID, out var patientId))
        {
            patientId = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, Guid.NewGuid().ToString());
        }

        var offsetDays = new DeterministicDateOffsetService().GetOffsetDays(patientId, vaultSecret, policy);
        var dateTags = new[]
        {
            DicomTag.StudyDate,
            DicomTag.SeriesDate,
            DicomTag.AcquisitionDate,
            DicomTag.ContentDate,
            DicomTag.InstanceCreationDate,
            DicomTag.PatientBirthDate
        };

        foreach (var tag in dateTags)
        {
            if (dataset.TryGetString(tag, out var text) && DateOnly.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                dataset.AddOrUpdate(tag, date.AddDays(offsetDays).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            }
        }

        return offsetDays;
    }

    private static void ApplyDateOffset(DicomDataset dataset, DateOffsetPolicy policy, byte[] vaultSecret)
    {
        if (!dataset.TryGetString(DicomTag.PatientID, out var patientId))
        {
            patientId = dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, Guid.NewGuid().ToString());
        }

        var offsetDays = new DeterministicDateOffsetService().GetOffsetDays(patientId, vaultSecret, policy);
        var dateTags = new[]
        {
            DicomTag.StudyDate,
            DicomTag.SeriesDate,
            DicomTag.AcquisitionDate,
            DicomTag.ContentDate,
            DicomTag.InstanceCreationDate,
            DicomTag.PatientBirthDate
        };

        foreach (var tag in dateTags)
        {
            if (dataset.TryGetString(tag, out var text) && DateOnly.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                dataset.AddOrUpdate(tag, date.AddDays(offsetDays).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            }
        }
    }

    private static void RemovePrivateTags(DicomDataset dataset)
    {
        var privateItems = dataset.Where(item => item.Tag.IsPrivate).ToArray();
        foreach (var item in privateItems)
        {
            dataset.Remove(item.Tag);
        }
    }

    private static string CreateDeterministicUid(string input, byte[] vaultSecret)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            input = Guid.NewGuid().ToString();
        }

        using var hmac = new HMACSHA256(vaultSecret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input.Trim()));
        var buffer = new byte[hash.Length + 1];
        Array.Copy(hash, 0, buffer, 1, hash.Length);
        var value = BigInteger.Abs(new BigInteger(buffer));
        var uid = "2.25." + value.ToString(CultureInfo.InvariantCulture);

        return uid.Length <= 64 ? uid : uid.Substring(0, 64);
    }
}

internal sealed class StorageScpServerState
{
    public DicomReceiveSettings Settings { get; }
    public AuditLogger AuditLogger { get; }

    public StorageScpServerState(DicomReceiveSettings settings)
    {
        Settings = settings;
        AuditLogger = new AuditLogger(settings.StagingFolder);
    }
}

internal sealed class StorageScpServer : DicomService, IDicomServiceProvider, IDicomCStoreProvider, IDicomCEchoProvider
{
    internal static StorageScpServerState? ServiceState;

    public StorageScpServer(INetworkStream stream, Encoding fallbackEncoding, ILogger log)
        : base(stream, fallbackEncoding, log, new DicomServiceDependencies(
            LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.None)),
            new DesktopNetworkManager(),
            new DefaultTranscoderManager(),
            new ArrayPoolMemoryProvider(),
            null))
    {
    }

    public Task OnReceiveAssociationRequestAsync(DicomAssociation association)
    {
        foreach (var pc in association.PresentationContexts)
        {
            if (pc.AbstractSyntax.StorageCategory != DicomStorageCategory.None || pc.AbstractSyntax == DicomUID.Verification)
            {
                pc.AcceptTransferSyntaxes(pc.GetTransferSyntaxes().ToArray());
                pc.SetResult(DicomPresentationContextResult.Accept, pc.GetTransferSyntaxes().FirstOrDefault() ?? DicomTransferSyntax.ImplicitVRLittleEndian);
            }
            else
            {
                pc.SetResult(DicomPresentationContextResult.RejectAbstractSyntaxNotSupported);
            }
        }

        ServiceState?.AuditLogger.LogEvent("Association", "Association accepted", $"CallingAE={association.CallingAE}; CalledAE={association.CalledAE}");
        return Task.CompletedTask;
    }

    public Task OnReceiveAssociationReleaseRequestAsync()
    {
        return Task.CompletedTask;
    }

    public void OnReceiveAbort(DicomAbortSource source, DicomAbortReason reason)
    {
        _ = OnReceiveAbortAsync(source, reason);
    }

    public Task OnReceiveAbortAsync(DicomAbortSource source, DicomAbortReason reason)
    {
        ServiceState?.AuditLogger.LogEvent("Abort", "Association aborted", $"Source={source}; Reason={reason}");
        return Task.CompletedTask;
    }

    public void OnConnectionClosed(Exception exception)
    {
        if (exception is not null)
        {
            ServiceState?.AuditLogger.LogEvent("ConnectionClosed", "Connection closed with error", exception.Message);
        }
        else
        {
            ServiceState?.AuditLogger.LogEvent("ConnectionClosed", "Connection closed", null);
        }
    }

    public Task<DicomCStoreResponse> OnCStoreRequestAsync(DicomCStoreRequest request)
    {
        if (ServiceState is null)
        {
            return Task.FromResult(new DicomCStoreResponse(request, DicomStatus.ProcessingFailure));
        }

        try
        {
            var dicomFile = request.File;
            DicomValidator.Validate(dicomFile);

            var destinationFolder = Path.Combine(ServiceState.Settings.StagingFolder, "accepted");
            Directory.CreateDirectory(destinationFolder);
            var destinationFile = Path.Combine(destinationFolder, GetSafeFileName(dicomFile, request.SOPInstanceUID.UID));
            dicomFile.Save(destinationFile);
            ServiceState.AuditLogger.LogEvent("CStore", "Received DICOM object", $"SourceAE={Association.CallingAE}; Remote={Association.RemoteHost}:{Association.RemotePort}; Destination={destinationFile}");
            return Task.FromResult(new DicomCStoreResponse(request, DicomStatus.Success));
        }
        catch (Exception ex)
        {
            var quarantineFolder = Path.Combine(ServiceState.Settings.StagingFolder, "quarantine");
            Directory.CreateDirectory(quarantineFolder);
            var quarantinePath = Path.Combine(quarantineFolder, Guid.NewGuid().ToString("N") + ".dcm");
            try
            {
                request.File?.Save(quarantinePath);
            }
            catch
            {
                // Ignore save failure during quarantine fallback.
            }

            ServiceState.AuditLogger.LogEvent("CStoreQuarantine", "Quarantined invalid inbound object", $"SourceAE={Association.CallingAE}; Remote={Association.RemoteHost}:{Association.RemotePort}; Path={quarantinePath}; Reason={ex.Message}");
            return Task.FromResult(new DicomCStoreResponse(request, DicomStatus.ProcessingFailure));
        }
    }

    public Task OnCStoreRequestExceptionAsync(string tempFileName, Exception e)
    {
        ServiceState?.AuditLogger.LogEvent("CStoreError", "C-STORE exception", $"TempFile={tempFileName}; Error={e.Message}");
        return Task.CompletedTask;
    }

    public Task<DicomCEchoResponse> OnCEchoRequestAsync(DicomCEchoRequest request)
    {
        ServiceState?.AuditLogger.LogEvent("CEcho", "C-ECHO request received", $"SourceAE={Association.CallingAE}; Remote={Association.RemoteHost}:{Association.RemotePort}");
        return Task.FromResult(new DicomCEchoResponse(request, DicomStatus.Success));
    }

    private static string GetSafeFileName(DicomFile file, string sopInstanceUid)
    {
        var baseName = file.Dataset.TryGetString(DicomTag.SOPInstanceUID, out var uid) ? uid : sopInstanceUid;
        baseName = string.Concat(baseName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = Guid.NewGuid().ToString("N");
        }

        return baseName + ".dcm";
    }
}

internal static class DicomValidator
{
    public static void Validate(DicomFile file)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        if (file.FileMetaInfo is null)
        {
            throw new InvalidDataException("Missing DICOM file meta information.");
        }

        if (file.FileMetaInfo.MediaStorageSOPClassUID is null || file.FileMetaInfo.MediaStorageSOPInstanceUID is null)
        {
            throw new InvalidDataException("Missing SOP Class UID or SOP Instance UID in file meta information.");
        }

        if (file.FileMetaInfo.TransferSyntax is null)
        {
            throw new InvalidDataException("Missing Transfer Syntax UID.");
        }

        if (!file.Dataset.Contains(DicomTag.StudyInstanceUID) || !file.Dataset.Contains(DicomTag.SeriesInstanceUID) || !file.Dataset.Contains(DicomTag.SOPInstanceUID))
        {
            throw new InvalidDataException("Missing mandatory Study/Series/SOP Instance UID(s).");
        }
    }
}

internal sealed class AuditLogger
{
    private readonly string _logFile;

    public AuditLogger(string stagingFolder)
    {
        var directory = Path.Combine(stagingFolder, "logs");
        Directory.CreateDirectory(directory);
        _logFile = Path.Combine(directory, "audit.log");
    }

    public void LogEvent(string category, string action, string? details)
    {
        try
        {
            var line = $"{DateTimeOffset.UtcNow:O}\t{category}\t{action}\t{details}{Environment.NewLine}";
            File.AppendAllText(_logFile, line);
        }
        catch
        {
            // Swallow logging failures to avoid breaking DICOM ingestion.
        }
    }
}
