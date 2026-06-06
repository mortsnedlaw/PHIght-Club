using System.IO;
using System.Linq;

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
    public Task<DicomImportResult> ImportFolderAsync(string folder, string stagingFolder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return Task.FromResult(new DicomImportResult
            {
                FilesScanned = 0,
                InstancesAccepted = 0,
                InstancesQuarantined = 0,
                Warnings = new[] { "Import folder not found. Please select an existing folder." }
            });
        }

        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories);
        var scanned = 0;
        var accepted = 0;
        var quarantined = 0;
        var warnings = new List<string>();

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                warnings.Add("Folder scan was cancelled.");
                break;
            }

            scanned++;

            try
            {
                if (IsDicomFile(file))
                {
                    accepted++;
                }
                else
                {
                    quarantined++;
                    warnings.Add($"Quarantined non-DICOM file: {Path.GetFileName(file)}");
                }
            }
            catch (Exception ex)
            {
                quarantined++;
                warnings.Add($"Quarantined unreadable file: {Path.GetFileName(file)} ({ex.Message})");
            }
        }

        if (accepted == 0)
        {
            warnings.Add("No valid DICOM objects were accepted. This source release validates only the DICOM prefix and does not perform full DICOM parsing.");
        }

        return Task.FromResult(new DicomImportResult
        {
            FilesScanned = scanned,
            InstancesAccepted = accepted,
            InstancesQuarantined = quarantined,
            Warnings = warnings
        });
    }

    private static bool IsDicomFile(string path)
    {
        using var stream = File.OpenRead(path);

        if (stream.Length < 132)
        {
            return false;
        }

        stream.Seek(128, SeekOrigin.Begin);
        var buffer = new byte[4];
        return stream.Read(buffer, 0, 4) == 4
            && buffer[0] == (byte)'D'
            && buffer[1] == (byte)'I'
            && buffer[2] == (byte)'C'
            && buffer[3] == (byte)'M';
    }
}
