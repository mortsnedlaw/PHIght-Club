using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Windows;
using FellowOakDicom;
using PHIghtClub.Core;
using PHIghtClub.Dicom;
using PHIghtClub.Export;
using PHIghtClub.Pixel;
using PHIghtClub.Ocr;
using PHIghtClub.Storage;

namespace PHIghtClub.App;

public partial class MainWindow : Window
{
    private readonly ManifestFactory _manifestFactory = new();
    private readonly IManifestIntegrityService _manifestIntegrity = new ManifestIntegrityService();
    private readonly IDicomVerificationClient _dicomVerificationClient = new DicomVerificationClient();
    private readonly IDicomStoreClient _dicomStoreClient = new DicomStoreClient();
    private DicomStorageScpService? _dicomStorageScpService;
    private DicomImportResult? _lastImportResult;
    private readonly byte[] _vaultSecret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    public MainWindow()
    {
        InitializeComponent();
        Log("PHIght Club v1.0.0 source release shell started.");
        Log("OCR is advisory. No OCR findings does not guarantee that images are free from burned-in PHI.");
    }

    private async void StartListener_Click(object sender, RoutedEventArgs e)
    {
        Log($"Listener start requested: AE={AeTitleTextBox.Text}, Port={PortTextBox.Text}, Bind={BindTextBox.Text}");

        var settings = new DicomReceiveSettings(
            AeTitleTextBox.Text,
            int.TryParse(PortTextBox.Text, out var port) ? port : 11112,
            string.IsNullOrWhiteSpace(BindTextBox.Text) ? "127.0.0.1" : BindTextBox.Text,
            GetStagingFolder());

        try
        {
            _dicomStorageScpService = new DicomStorageScpService();
            await _dicomStorageScpService.StartAsync(settings, CancellationToken.None);
            Log($"DICOM Storage SCP started: AE={settings.AeTitle}, Port={settings.Port}, Bind={settings.BindAddress}, Staging={settings.StagingFolder}");
        }
        catch (Exception ex)
        {
            Log($"Failed to start DICOM Storage SCP: {ex.Message}");
        }
    }

    private async void StopListener_Click(object sender, RoutedEventArgs e)
    {
        if (_dicomStorageScpService is null)
        {
            Log("DICOM Storage SCP is not running.");
            return;
        }

        try
        {
            await _dicomStorageScpService.StopAsync(CancellationToken.None);
            _dicomStorageScpService = null;
            Log("DICOM Storage SCP stopped.");
        }
        catch (Exception ex)
        {
            Log($"Failed to stop DICOM Storage SCP: {ex.Message}");
        }
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = ImportFolderTextBox.Text;
        var stagingFolder = GetStagingFolder();
        var importService = new DicomImportService();
        var result = await importService.ImportFolderAsync(folder, stagingFolder, CancellationToken.None);
        _lastImportResult = result;
        Log($"Folder scan: files={result.FilesScanned}, accepted={result.InstancesAccepted}, quarantine={result.InstancesQuarantined}");
        Log($"Staging: {stagingFolder}");
        foreach (var warning in result.Warnings)
        {
            Log("WARNING: " + warning);
        }
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        var job = BuildJobFromUi();
        var acceptedFolder = Path.Combine(GetStagingFolder(), "accepted");
        if (!Directory.Exists(acceptedFolder))
        {
            Log("Preview blocked because no accepted DICOM objects were found in staging.");
            return;
        }

        var sourceFile = Directory.EnumerateFiles(acceptedFolder, "*.dcm", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (sourceFile is null)
        {
            Log("Preview blocked because no accepted DICOM objects were available.");
            return;
        }

        var previewFolder = Path.Combine(Path.GetTempPath(), "PHIghtClubPreview");
        Directory.CreateDirectory(previewFolder);
        var previewFile = Path.Combine(previewFolder, Path.GetFileName(sourceFile) ?? Guid.NewGuid().ToString("N") + ".dcm");
        File.Copy(sourceFile, previewFile, true);

        DicomFile dicomFile;
        try
        {
            dicomFile = DicomFile.Open(sourceFile, FileReadOption.ReadAll);
        }
        catch (Exception ex)
        {
            Log($"Preview blocked because the DICOM file could not be opened: {ex.Message}");
            return;
        }

        var regions = BuildPreviewMaskRegions(dicomFile);
        if (regions.Count == 0)
        {
            Log("Preview blocked because no preview mask region could be generated.");
            return;
        }

        var scrubber = new PixelPipelineImpl();
        var scrubResult = await scrubber.ApplyAsync(previewFile, regions, job.BurnedInPhi.PixelAction, job.ImageSafety, CancellationToken.None);

        var ocrEngine = new ProductionOcrEngine();
        var ocrStatus = await ocrEngine.InitializeAsync(job.BurnedInPhi.OcrAcceleration, CancellationToken.None);
        var ocrRegions = job.BurnedInPhi.OcrMode == OcrMode.Off || !ocrStatus.Available
            ? Array.Empty<OcrRegion>()
            : await ocrEngine.DetectTextAsync(null!, CancellationToken.None);

        Log($"Preview file: {previewFile}");
        Log($"Pixel scrub preview: {scrubResult.Message} Modified={scrubResult.PixelDataModified} Blocked={scrubResult.Blocked} Lossy={scrubResult.LossyCompressionIntroduced}");
        Log($"OCR preview: backend={ocrStatus.Backend}, available={ocrStatus.Available}, regions detected={ocrRegions.Count}");
    }

    private static IReadOnlyList<MaskRegion> BuildPreviewMaskRegions(DicomFile dicomFile)
    {
        try
        {
            var rows = dicomFile.Dataset.GetSingleValue<int>(DicomTag.Rows);
            var cols = dicomFile.Dataset.GetSingleValue<int>(DicomTag.Columns);
            if (rows <= 0 || cols <= 0)
                return Array.Empty<MaskRegion>();

            var width = Math.Max(1, cols / 4);
            var height = Math.Max(1, rows / 4);
            var x = Math.Max(0, (cols - width) / 2);
            var y = Math.Max(0, (rows - height) / 2);
            return new[] { new MaskRegion(x, y, width, height) };
        }
        catch
        {
            return Array.Empty<MaskRegion>();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (WizardTabs.SelectedIndex > 0)
        {
            WizardTabs.SelectedIndex--;
        }
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var job = BuildJobFromUi();
        var validation = ValidateJob(job);

        foreach (var warning in validation.Warnings)
        {
            Log("WARNING: " + warning);
        }

        foreach (var error in validation.Errors)
        {
            Log("BLOCKED: " + error);
        }

        Log(validation.IsBlocked ? "Validation result: BLOCKED" : "Validation result: PASS with current source-release checks");
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var job = BuildJobFromUi();
        var validation = ValidateJob(job);

        foreach (var warning in validation.Warnings)
        {
            Log("WARNING: " + warning);
        }

        foreach (var error in validation.Errors)
        {
            Log("BLOCKED: " + error);
        }

        if (validation.IsBlocked)
        {
            Log("Export blocked because validation failed.");
            return;
        }

        if (job.Export.TargetType == ExportTargetType.DicomCStore && job.Export.RequireCEchoBeforeExport)
        {
            var destination = new DicomDestination(job.Export.DestinationAeTitle, job.Export.DestinationHost, job.Export.DestinationPort);
            Log($"Performing C-ECHO to {destination.Host}:{destination.Port} AE={destination.AeTitle}");
            var echoSuccess = await _dicomVerificationClient.CEchoAsync(destination, CancellationToken.None);
            Log(echoSuccess ? "C-ECHO succeeded." : "C-ECHO failed.");
            if (!echoSuccess)
            {
                Log("Export blocked because C-ECHO verification failed.");
                return;
            }
        }

        var acceptedFolder = Path.Combine(GetStagingFolder(), "accepted");
        if (!Directory.Exists(acceptedFolder))
        {
            Log("Export blocked because no accepted DICOM objects were found in staging.");
            return;
        }

        // Show progress window
        var progressWindow = new RunProgressWindow();
        progressWindow.Owner = this;
        var allFiles = Directory.EnumerateFiles(acceptedFolder, "*.dcm", SearchOption.TopDirectoryOnly).ToList();
        progressWindow.SetJobId(job.JobId);
        progressWindow.SetTotalCount(allFiles.Count);
        progressWindow.Show();

        var auditLogger = new RunAuditLogger(job.Export.OutputPath, job.JobId);
        var exportedFiles = new List<string>();
        var manifestObjects = new List<ManifestObjectEntry>();
        var outputPath = job.Export.OutputPath;
        Directory.CreateDirectory(outputPath);
        var deidService = new DicomDeIdentificationEngine();
        var exportTemp = Path.Combine(Path.GetTempPath(), "PHIghtClubExport", job.JobId);
        Directory.CreateDirectory(exportTemp);

        try
        {
            foreach (var sourceFile in allFiles)
            {
                var fileName = Path.GetFileName(sourceFile);
                progressWindow.UpdateProgress("De-identifying", fileName);

                try
                {
                    var dicomFile = DicomFile.Open(sourceFile, FileReadOption.ReadAll);
                    var (processed, changeLog) = deidService.ApplyWithTracking(dicomFile, job.DeIdentification, _vaultSecret);

                    var targetFileName = Path.GetFileName(sourceFile) ?? Guid.NewGuid().ToString("N") + ".dcm";
                    var targetFile = Path.Combine(exportTemp, targetFileName);
                    processed.Save(targetFile);
                    exportedFiles.Add(targetFile);

                    manifestObjects.Add(new ManifestObjectEntry
                    {
                        FileName = targetFileName,
                        SopClassUid = processed.Dataset.GetSingleValue<string>(DicomTag.SOPClassUID),
                        OriginalStudyInstanceUid = changeLog.OriginalStudyInstanceUid,
                        OriginalSeriesInstanceUid = changeLog.OriginalSeriesInstanceUid,
                        OriginalSopInstanceUid = changeLog.OriginalSopInstanceUid,
                        RemappedStudyInstanceUid = changeLog.RemappedStudyInstanceUid,
                        RemappedSeriesInstanceUid = changeLog.RemappedSeriesInstanceUid,
                        RemappedSopInstanceUid = changeLog.RemappedSopInstanceUid,
                        TransferSyntaxOriginal = changeLog.OriginalTransferSyntax,
                        TransferSyntaxOutput = changeLog.OutputTransferSyntax,
                        PixelDataModified = changeLog.PixelDataModified,
                        Sha256Hash = ManifestFileHash(targetFile)
                    });

                    // Log to audit trail
                    var auditEntry = new RunAuditLogEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        JobId = job.JobId,
                        Action = "de-identify",
                        Status = "OK",
                        SourcePath = sourceFile,
                        OutputPath = targetFile,
                        PatientIdChanged = changeLog.PatientIdChanged,
                        PatientNameChanged = changeLog.PatientNameChanged,
                        StudyUidChanged = changeLog.StudyUidChanged,
                        SeriesUidChanged = changeLog.SeriesUidChanged,
                        SopUidChanged = changeLog.SopUidChanged,
                        DateOffsetDays = changeLog.DateOffsetDays,
                        PixelDataModified = changeLog.PixelDataModified,
                        TransferSyntaxOriginal = changeLog.OriginalTransferSyntax,
                        TransferSyntaxOutput = changeLog.OutputTransferSyntax
                    };
                    auditLogger.LogEntry(auditEntry);
                    progressWindow.LogStatus($"De-identified: {fileName}", RunProgressStatus.Success);
                    progressWindow.IncrementProcessed();
                    progressWindow.IncrementExported();
                }
                catch (Exception ex)
                {
                    progressWindow.LogStatus($"Failed to process {fileName}: {ex.Message}", RunProgressStatus.Failed);
                    progressWindow.IncrementProcessed();
                    progressWindow.IncrementFailed();

                    var auditEntry = new RunAuditLogEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        JobId = job.JobId,
                        Action = "de-identify",
                        Status = "failed",
                        SourcePath = sourceFile,
                        ErrorMessage = ex.Message
                    };
                    auditLogger.LogEntry(auditEntry);
                }
            }

            if (exportedFiles.Count == 0)
            {
                Log("Export blocked because no accepted DICOM objects were available for export.");
                return;
            }

            progressWindow.UpdateProgress("Packaging", "Preparing export...");

            if (job.Export.TargetType == ExportTargetType.Zip)
            {
                var zipPath = Path.Combine(outputPath, $"{job.JobId}.zip");
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                using var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create);
                foreach (var file in exportedFiles)
                {
                    archive.CreateEntryFromFile(file, Path.GetFileName(file) ?? Path.GetFileName(file)!);
                }

                Log($"Exported {exportedFiles.Count} de-identified DICOM files to ZIP: {zipPath}");
                progressWindow.LogStatus($"Created ZIP with {exportedFiles.Count} files", RunProgressStatus.Success);
            }
            else if (job.Export.TargetType == ExportTargetType.Folder)
            {
                foreach (var file in exportedFiles)
                {
                    var destination = Path.Combine(outputPath, Path.GetFileName(file) ?? Path.GetFileName(file)!);
                    File.Copy(file, destination, overwrite: true);
                }

                Log($"Exported {exportedFiles.Count} de-identified DICOM files to folder: {outputPath}");
                progressWindow.LogStatus($"Exported {exportedFiles.Count} files to folder", RunProgressStatus.Success);
            }
            else if (job.Export.TargetType == ExportTargetType.DicomCStore)
            {
                var destination = new DicomDestination(job.Export.DestinationAeTitle, job.Export.DestinationHost, job.Export.DestinationPort);
                await _dicomStoreClient.SendAsync(destination, exportedFiles, CancellationToken.None);
                Log($"Sent {exportedFiles.Count} de-identified DICOM objects via C-STORE to {destination.Host}:{destination.Port}.");
                progressWindow.LogStatus($"Sent {exportedFiles.Count} objects via C-STORE", RunProgressStatus.Success);
            }

            if (job.Export.CreateJsonManifest)
            {
                var warnings = new List<string>();
                var manifest = _manifestFactory.CreateDryRunManifest(job, warnings, manifestObjects);
                manifest.AuditLogPath = auditLogger.AuditLogPath;
                var devKey = _vaultSecret;
                manifest.ManifestIntegrity = _manifestIntegrity.Sign(manifest, exportedFiles.Select(path => ManifestFileHash(path)).ToArray(), devKey, "enterprise-dev-key");
                var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
                var manifestPath = Path.Combine(outputPath, $"{job.JobId}.manifest.json");
                File.WriteAllText(manifestPath, json);
                Log($"Manifest written: {manifestPath}");
                Log($"Audit log: {auditLogger.AuditLogPath}");
                progressWindow.LogStatus($"Manifest and audit log written", RunProgressStatus.Success);
                Log($"Manifest verification: {_manifestIntegrity.Verify(manifest, exportedFiles.Select(path => ManifestFileHash(path)).ToArray(), devKey)}");
            }

            progressWindow.LogStatus("Export completed successfully", RunProgressStatus.Success);
        }
        catch (Exception ex)
        {
            Log($"Export failed: {ex.Message}");
            progressWindow.LogStatus($"Export failed: {ex.Message}", RunProgressStatus.Failed);
        }
        finally
        {
            try
            {
                if (Directory.Exists(exportTemp))
                {
                    Directory.Delete(exportTemp, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private static string ManifestFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private void DryRun_Click(object sender, RoutedEventArgs e)
    {
        var job = BuildJobFromUi();
        job.Status = JobStatus.DryRun;

        var warnings = new List<string>
        {
            "v1.0.0 source release dry run only. No DICOM files were modified.",
            "OCR is advisory and was not executed in this dry run.",
            "DICOM PS3.15 Annex E mapping is documented as a v1.0 release requirement and must be implemented before real patient-data use."
        };

        var manifest = _manifestFactory.CreateDryRunManifest(job, warnings, Array.Empty<ManifestObjectEntry>());
        var devKey = System.Text.Encoding.UTF8.GetBytes("PHIghtClub-MVP-Development-Key-Replace-Me-32-bytes-minimum");
        manifest.ManifestIntegrity = _manifestIntegrity.Sign(manifest, Array.Empty<string>(), devKey, "mvp-dev-key");

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        Log("Dry-run manifest:");
        Log(json);

        try
        {
            Directory.CreateDirectory(job.Export.OutputPath);
            var path = Path.Combine(job.Export.OutputPath, $"{job.JobId}.manifest.json");
            File.WriteAllText(path, json);
            Log("Manifest written: " + path);
        }
        catch (Exception ex)
        {
            Log("Could not write manifest: " + ex.Message);
        }
    }

    private string GetStagingFolder()
    {
        var folder = string.IsNullOrWhiteSpace(StagingTextBox.Text)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PHIghtClub", "Staging")
            : StagingTextBox.Text;

        Directory.CreateDirectory(folder);
        return folder;
    }

    private ExportJob BuildJobFromUi()
    {
        var targetType = ExportTargetType.Folder;
        if (ZipExportRadio.IsChecked == true)
        {
            targetType = ExportTargetType.Zip;
        }
        else if (DicomCStoreExportRadio.IsChecked == true)
        {
            targetType = ExportTargetType.DicomCStore;
        }

        var imageSafety = ImageSafetyPolicy.StrictDefault();
        if (BalancedSafetyRadio.IsChecked == true)
        {
            imageSafety = new ImageSafetyPolicy { Mode = ImageSafetyMode.Balanced, PreserveOriginalTransferSyntax = true, NeverConvertLosslessToLossy = true, BlockIfSafeReEncodingUnavailable = true, DoNotTouchPixelDataUnlessScrubEnabled = true, ValidateOutputAfterWrite = true };
        }
        else if (CustomSafetyRadio.IsChecked == true)
        {
            imageSafety = new ImageSafetyPolicy { Mode = ImageSafetyMode.Custom, PreserveOriginalTransferSyntax = true, NeverConvertLosslessToLossy = true, BlockIfSafeReEncodingUnavailable = true, DoNotTouchPixelDataUnlessScrubEnabled = true, ValidateOutputAfterWrite = true };
        }

        return new ExportJob
        {
            Input = new InputSettings
            {
                UseDicomStorageScp = UseScpCheckBox.IsChecked == true,
                UseFolderImport = UseFolderCheckBox.IsChecked == true,
                LocalAeTitle = AeTitleTextBox.Text,
                Port = int.TryParse(PortTextBox.Text, out var port) ? port : 11112,
                BindAddress = BindTextBox.Text,
                StagingFolder = StagingTextBox.Text,
                ImportFolder = ImportFolderTextBox.Text
            },
            DeIdentification = new DeIdentificationSettings
            {
                Mode = PseudonymizationRadio.IsChecked == true ? DeIdentificationMode.Pseudonymization : DeIdentificationMode.MetadataAnonymization,
                ProfileName = (ProfileCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "AI Training Strict",
                RemapPatientId = RemapPatientIdCheckBox.IsChecked == true,
                RemapPatientName = RemapPatientNameCheckBox.IsChecked == true,
                RemapStudyInstanceUid = RemapStudyUidCheckBox.IsChecked == true,
                RemapSeriesInstanceUid = RemapSeriesUidCheckBox.IsChecked == true,
                RemapSopInstanceUid = RemapSopUidCheckBox.IsChecked == true,
                RemovePrivateTagsUnlessWhitelisted = RemovePrivateTagsCheckBox.IsChecked == true,
                DateOffset = new DateOffsetPolicy { VaultScoped = DateOffsetVaultScopedCheckBox.IsChecked == true }
            },
            BurnedInPhi = new BurnedInPhiSettings
            {
                OcrMode = OcrAutoMaskRadio.IsChecked == true
                    ? OcrMode.AutoMaskUsingApprovedTemplates
                    : OcrWarnRadio.IsChecked == true
                        ? OcrMode.WarnAndRequireApproval
                        : OcrMode.Off,
                OcrAcceleration = GpuOnlyRadio.IsChecked == true
                    ? OcrAccelerationMode.GpuOnly
                    : CpuOnlyRadio.IsChecked == true
                        ? OcrAccelerationMode.CpuOnly
                        : OcrAccelerationMode.GpuCpuAuto,
                PixelAction = BlackMaskRadio.IsChecked == true
                    ? PixelScrubAction.BlackMask
                    : BlurRadio.IsChecked == true
                        ? PixelScrubAction.Blur
                        : PixelScrubAction.Pixelate
            },
            ImageSafety = imageSafety,
            ImportSummary = new ImportSummary
            {
                FilesScanned = _lastImportResult?.FilesScanned ?? 0,
                InstancesAccepted = _lastImportResult?.InstancesAccepted ?? 0,
                InstancesQuarantined = _lastImportResult?.InstancesQuarantined ?? 0
            },
            Export = new ExportSettings
            {
                TargetType = targetType,
                OutputPath = OutputPathTextBox.Text,
                DestinationAeTitle = DestAeTextBox.Text,
                DestinationHost = DestHostTextBox.Text,
                DestinationPort = int.TryParse(DestPortTextBox.Text, out var destPort) ? destPort : 104,
                RequireCEchoBeforeExport = RequireCEchoBeforeExportCheckBox.IsChecked == true,
                CreateJsonManifest = CreateJsonManifestCheckBox.IsChecked == true,
                CreateCsvSummary = CreateCsvSummaryCheckBox.IsChecked == true
            }
        };
    }

    private static ValidationResult ValidateJob(ExportJob job)
    {
        var result = new ValidationResult();

        if (!job.Input.UseDicomStorageScp && !job.Input.UseFolderImport)
        {
            result.AddError("Select at least one input source.");
        }

        if (job.Input.UseFolderImport && string.IsNullOrWhiteSpace(job.Input.ImportFolder))
        {
            result.AddError("Import folder must be specified when folder import is enabled.");
        }

        if (job.Input.UseDicomStorageScp)
        {
            if (string.IsNullOrWhiteSpace(job.Input.LocalAeTitle))
            {
                result.AddError("Local AE title is required for DICOM SCP.");
            }

            if (job.Input.Port <= 0 || job.Input.Port > 65535)
            {
                result.AddError("Invalid SCP port.");
            }
        }

        if (job.Export.TargetType == ExportTargetType.DicomCStore && string.IsNullOrWhiteSpace(job.Export.DestinationHost))
        {
            result.AddError("Destination host is required for DICOM C-STORE export.");
        }

        if (job.Export.TargetType == ExportTargetType.Zip && string.IsNullOrWhiteSpace(job.Export.OutputPath))
        {
            result.AddError("Output path is required for ZIP export.");
        }

        if (string.IsNullOrWhiteSpace(job.Export.OutputPath))
        {
            result.AddError("Output path is required.");
        }

        if (job.BurnedInPhi.OcrMode != OcrMode.Off)
        {
            result.AddWarning("OCR is advisory. Manual review and templates are still required for high-risk modalities.");
        }

        if (job.DeIdentification.Mode == DeIdentificationMode.Pseudonymization && !job.DeIdentification.DateOffset.VaultScoped)
        {
            result.AddError("Pseudonymization with deterministic date offset requires vault-scoped offset.");
        }

        if (!job.DeIdentification.RemapPatientId || !job.DeIdentification.RemapPatientName)
        {
            result.AddWarning("Disabling patient identifier remapping may leave sensitive identifiers in metadata.");
        }

        return result;
    }

    private void Log(string message)
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }
}
