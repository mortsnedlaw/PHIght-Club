namespace PHIghtClub.Dicom;

public sealed class NoopDicomStorageScpService : IDicomStorageScpService
{
    public Task StartAsync(DicomReceiveSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class NoopDicomVerificationClient : IDicomVerificationClient
{
    public Task<bool> CEchoAsync(DicomDestination destination, CancellationToken cancellationToken) => Task.FromResult(false);
}

public sealed class NoopDicomStoreClient : IDicomStoreClient
{
    public Task SendAsync(DicomDestination destination, IReadOnlyList<string> dicomFilePaths, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class NoopDicomImportService : IDicomImportService
{
    public Task<DicomImportResult> ImportFolderAsync(string folder, CancellationToken cancellationToken)
    {
        return Task.FromResult(new DicomImportResult
        {
            FilesScanned = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count() : 0,
            InstancesAccepted = 0,
            InstancesQuarantined = 0,
            Warnings = ["v1.0.0 source release placeholder does not parse DICOM yet. Folder scan only."]
        });
    }
}
