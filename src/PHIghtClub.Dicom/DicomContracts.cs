using PHIghtClub.Core;

namespace PHIghtClub.Dicom;

public sealed record DicomReceiveSettings(
    string AeTitle,
    int Port,
    string BindAddress,
    string StagingFolder);

public sealed record DicomDestination(
    string AeTitle,
    string Host,
    int Port);

public sealed record DicomAssociationInfo(
    string CallingAeTitle,
    string CalledAeTitle,
    string RemoteIp,
    DateTimeOffset Timestamp);

public sealed record DicomRejectedObject(
    DateTimeOffset Timestamp,
    string? CallingAeTitle,
    string? RemoteIp,
    string Reason,
    string? SopClassUid,
    string? TransferSyntaxUid,
    string QuarantinePath);

public interface IDicomStorageScpService
{
    Task StartAsync(DicomReceiveSettings settings, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public interface IDicomVerificationClient
{
    Task<bool> CEchoAsync(DicomDestination destination, CancellationToken cancellationToken);
}

public interface IDicomStoreClient
{
    Task SendAsync(DicomDestination destination, IReadOnlyList<string> dicomFilePaths, CancellationToken cancellationToken);
}

public interface IDicomImportService
{
    Task<DicomImportResult> ImportFolderAsync(string folder, string stagingFolder, CancellationToken cancellationToken);
}

public sealed class DicomImportResult
{
    public int FilesScanned { get; init; }
    public int InstancesAccepted { get; init; }
    public int InstancesQuarantined { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
