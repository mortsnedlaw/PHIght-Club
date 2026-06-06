using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using FellowOakDicom.Network;
using FellowOakDicom.Network.Client;
using PHIghtClub.Core;
using PHIghtClub.Dicom;
using PHIghtClub.Export;
using PHIghtClub.Storage;
using Xunit;

namespace PHIghtClub.Tests;

public class DicomIntegrationTests
{
    [Fact]
    public async Task FolderImport_AcceptsValidDicomAndQuarantinesInvalid()
    {
        var root = Path.Combine(Path.GetTempPath(), "PHIghtClubTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var importFolder = Path.Combine(root, "import");
        var stagingFolder = Path.Combine(root, "staging");
        Directory.CreateDirectory(importFolder);

        var validFile = Path.Combine(importFolder, "valid.dcm");
        CreateSyntheticDicomFile(validFile, includePixelData: false);
        File.WriteAllText(Path.Combine(importFolder, "invalid.txt"), "not a dicom file");

        var importer = new DicomImportService();
        var result = await importer.ImportFolderAsync(importFolder, stagingFolder, CancellationToken.None);

        Assert.Equal(2, result.FilesScanned);
        Assert.Equal(1, result.InstancesAccepted);
        Assert.Equal(1, result.InstancesQuarantined);
        Assert.Contains(result.Warnings, w => w.Contains("Quarantined invalid file"));
        Assert.True(Directory.Exists(Path.Combine(stagingFolder, "accepted")));
        Assert.True(Directory.Exists(Path.Combine(stagingFolder, "quarantine")));
    }

    [Fact]
    public async Task ScpService_ReceivesSyntheticDicomViaCStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "PHIghtClubScpTests", Guid.NewGuid().ToString("N"));
        var stagingFolder = Path.Combine(root, "staging");
        Directory.CreateDirectory(root);
        var settings = new DicomReceiveSettings("PHIGHTCLUB", 11113, "127.0.0.1", stagingFolder);

        var scp = new DicomStorageScpService();
        await scp.StartAsync(settings, CancellationToken.None);

        var filePath = Path.Combine(root, "received.dcm");
        CreateSyntheticDicomFile(filePath, includePixelData: false);

        var client = DicomClientFactory.Create(settings.BindAddress, settings.Port, false, "TESTSCU", settings.AeTitle);
        var request = new DicomCStoreRequest(DicomFile.Open(filePath, FileReadOption.ReadAll));
        await client.AddRequestAsync(request);
        await client.SendAsync(CancellationToken.None, DicomClientCancellationMode.ImmediatelyReleaseAssociation);

        await scp.StopAsync(CancellationToken.None);

        var accepted = Directory.GetFiles(Path.Combine(stagingFolder, "accepted"));
        Assert.Single(accepted);
    }

    [Fact]
    public void MetadataDeIdentification_PreservesPixelDataAndTransferSyntax()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "PHIghtClubDeid", Guid.NewGuid().ToString("N") + ".dcm");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var original = CreateSyntheticDicomFile(filePath, includePixelData: true);

        var service = new DicomMetadataDeIdentificationService();
        var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var settings = new DeIdentificationSettings
        {
            Mode = DeIdentificationMode.Pseudonymization,
            RemapPatientId = true,
            RemapPatientName = true,
            RemapStudyInstanceUid = true,
            RemapSeriesInstanceUid = true,
            RemapSopInstanceUid = true,
            RemovePrivateTagsUnlessWhitelisted = true,
            DateOffset = DateOffsetPolicy.Default()
        };

        var result = service.ApplyWithTracking(original, settings, secret);

        Assert.Equal(original.FileMetaInfo.TransferSyntax.UID.UID, result.ProcessedFile.FileMetaInfo.TransferSyntax.UID.UID);
        Assert.True(original.Dataset.GetValues<byte>(DicomTag.PixelData).SequenceEqual(result.ProcessedFile.Dataset.GetValues<byte>(DicomTag.PixelData)));
        Assert.NotEqual(original.Dataset.GetSingleValue<string>(DicomTag.PatientID), result.ProcessedFile.Dataset.GetSingleValue<string>(DicomTag.PatientID));
        Assert.NotEqual(original.Dataset.GetSingleValue<string>(DicomTag.PatientName), result.ProcessedFile.Dataset.GetSingleValue<string>(DicomTag.PatientName));
    }

    [Fact]
    public void ManifestIntegrity_VerifiesSignedManifestSuccessfully()
    {
        var job = new ExportJob();
        job.DeIdentification.ProfileName = "AI Training Strict";
        job.DeIdentification.DateOffset = DateOffsetPolicy.Default();

        var manifest = new ManifestFactory().CreateDryRunManifest(job, new[] { "Validation test." });
        var integrityService = new ManifestIntegrityService();
        var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var objectHashes = new[] { "ABC123", "DEF456" };

        manifest.ManifestIntegrity = integrityService.Sign(manifest, objectHashes, secret, "test-key");

        Assert.True(integrityService.Verify(manifest, objectHashes, secret));
    }

    private static DicomFile CreateSyntheticDicomFile(string filePath, bool includePixelData)
    {
        var studyInstanceUid = DicomUID.Generate();
        var seriesInstanceUid = DicomUID.Generate();
        var sopInstanceUid = DicomUID.Generate();
        var dataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian)
        {
            { DicomTag.PatientName, "DOE^JOHN" },
            { DicomTag.PatientID, "PATIENT01" },
            { DicomTag.StudyInstanceUID, studyInstanceUid.UID },
            { DicomTag.SeriesInstanceUID, seriesInstanceUid.UID },
            { DicomTag.SOPInstanceUID, sopInstanceUid.UID },
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage.UID },
            { DicomTag.StudyDate, DateTime.UtcNow.ToString("yyyyMMdd") }
        };

        if (includePixelData)
        {
            dataset.Add(DicomTag.Rows, (ushort)1);
            dataset.Add(DicomTag.Columns, (ushort)1);
            dataset.Add(DicomTag.BitsAllocated, (ushort)8);
            dataset.Add(DicomTag.SamplesPerPixel, (ushort)1);
            dataset.Add(DicomTag.PhotometricInterpretation, "MONOCHROME2");
            dataset.Add(DicomTag.PixelData, new byte[] { 0x7F });
        }

        var file = new DicomFile(dataset);
        file.FileMetaInfo.MediaStorageSOPClassUID = DicomUID.SecondaryCaptureImageStorage;
        file.FileMetaInfo.MediaStorageSOPInstanceUID = sopInstanceUid;
        file.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
        file.FileMetaInfo.ImplementationClassUID = DicomUID.Generate();
        file.Save(filePath);
        return DicomFile.Open(filePath, FileReadOption.ReadAll);
    }
}
