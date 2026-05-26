using System.IO;
using System.Text.Json;
using System.Windows;
using PHIghtClub.Core;
using PHIghtClub.Core.Logging;
using PHIghtClub.Dicom;
using PHIghtClub.Export;
using PHIghtClub.Storage;
using Serilog;

namespace PHIghtClub.App;

public partial class MainWindow : Window
{
    private ManifestFactory? _manifestFactory;
    private IManifestIntegrityService? _manifestIntegrity;
    private IDicomImportService? _dicomImport;
    private ILogger? _logger;
    private IAuditLogger? _auditLogger;

    public MainWindow()
    {
        InitializeComponent();
        
        try
        {
            // Get services from DI container
            _manifestFactory = ServiceLocator.GetService<ManifestFactory>();
            _manifestIntegrity = ServiceLocator.GetService<IManifestIntegrityService>();
            _dicomImport = ServiceLocator.GetService<IDicomImportService>();
            _logger = ServiceLocator.GetService<ILogger>();
            _auditLogger = ServiceLocator.GetService<IAuditLogger>();

            _logger!.Information("PHIght Club v1.0.0 source release shell started");
            _logger.Warning("OCR is advisory. No OCR findings does not guarantee that images are free from burned-in PHI");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize UI services: {ex.Message}",
                "Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            LogTextBox.AppendText($"FATAL: {ex.Message}\n");
        }
    }

    private void StartListener_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var port = int.TryParse(PortTextBox.Text, out var p) ? p : 11112;
            LogUi($"Listener start requested: AE={AeTitleTextBox.Text}, Port={port}, Bind={BindTextBox.Text}");
            _logger.Information("DICOM SCP listener start requested: AE={AETitle}, Port={Port}, Bind={BindAddress}", 
                AeTitleTextBox.Text, port, BindTextBox.Text);
            LogUi("v1.0.0 source release placeholder: real Storage SCP implementation is pending fo-dicom integration.");
        }
        catch (Exception ex)
        {
            HandleUiError("Start Listener", ex);
        }
    }

    private void StopListener_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogUi("Listener stop requested.");
            _logger.Information("DICOM SCP listener stop requested");
        }
        catch (Exception ex)
        {
            HandleUiError("Stop Listener", ex);
        }
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = ImportFolderTextBox.Text;
            if (string.IsNullOrWhiteSpace(folder))
            {
                MessageBox.Show("Please specify an import folder.", "Input Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(folder))
            {
                MessageBox.Show($"Folder does not exist: {folder}", "Folder Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LogUi($"Scanning folder: {folder}");
            var result = await _dicomImport.ImportFolderAsync(folder, CancellationToken.None);
            LogUi($"Folder scan: files={result.FilesScanned}, accepted={result.InstancesAccepted}, quarantine={result.InstancesQuarantined}");
            
            foreach (var warning in result.Warnings)
            {
                LogUi($"WARNING: {warning}");
                _logger.Warning("Import warning: {Warning}", warning);
            }

            _logger.Information("Folder scan completed: FilesScanned={FilesScanned}, Accepted={Accepted}, Quarantined={Quarantined}",
                result.FilesScanned, result.InstancesAccepted, result.InstancesQuarantined);
        }
        catch (Exception ex)
        {
            HandleUiError("Scan Folder", ex);
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogUi("Preview/mask requested. v1.0.0 source release placeholder: pixel preview is pending pixel pipeline integration.");
            _logger.Information("Pixel preview requested (placeholder in source release)");
        }
        catch (Exception ex)
        {
            HandleUiError("Preview", ex);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (WizardTabs.SelectedIndex > 0)
            {
                WizardTabs.SelectedIndex--;
            }
        }
        catch (Exception ex)
        {
            HandleUiError("Back Navigation", ex);
        }
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var job = BuildJobFromUi();
            var validation = ValidateJob(job);

            foreach (var warning in validation.Warnings)
            {
                LogUi($"WARNING: {warning}");
                _logger.Warning("Validation warning: {Warning}", warning);
            }

            foreach (var error in validation.Errors)
            {
                LogUi($"BLOCKED: {error}");
                _logger.Warning("Validation error: {Error}", error);
            }

            var message = validation.IsBlocked ? "Validation result: BLOCKED" : "Validation result: PASS with current source-release checks";
            LogUi(message);

            _auditLogger.LogValidationDecision(job.JobId, !validation.IsBlocked, validation.Errors, validation.Warnings, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            HandleUiError("Validate", ex);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LogUi("Export requested. Real export is intentionally blocked in this source release until DICOM and pixel pipelines are validated.");
            LogUi("Use Dry run to generate a signed manifest preview.");
            _logger.Information("Real export blocked in source release (use Dry Run for manifest preview)");
        }
        catch (Exception ex)
        {
            HandleUiError("Export", ex);
        }
    }

    private void DryRun_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var job = BuildJobFromUi();
            job.Status = JobStatus.DryRun;

            var warnings = new List<string>
            {
                "v1.0.0 source release dry run only. No DICOM files were modified.",
                "OCR is advisory and was not executed in this dry run.",
                "DICOM PS3.15 Annex E mapping is documented as a v1.0 release requirement and must be implemented before real patient-data use."
            };

            var manifest = _manifestFactory.CreateDryRunManifest(job, warnings);
            var devKey = System.Text.Encoding.UTF8.GetBytes("PHIghtClub-MVP-Development-Key-Replace-Me-32-bytes-minimum");
            manifest.ManifestIntegrity = _manifestIntegrity.Sign(manifest, [], devKey, "mvp-dev-key");

            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            LogUi("Dry-run manifest:");
            LogUi(json);

            // Validate output path before attempting write
            var outputPath = job.Export.OutputPath;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                MessageBox.Show("Output path is not specified.", "Missing Output Path",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(outputPath);
            }
            catch (UnauthorizedAccessException)
            {
                const string errorMsg = "Permission denied: cannot create or access the output directory.";
                LogUi($"ERROR: {errorMsg}");
                MessageBox.Show(errorMsg, "Permission Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.Error("Failed to create output directory: Access denied. Path={OutputPath}", outputPath);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                const string errorMsg = "Output path is invalid or unreachable.";
                LogUi($"ERROR: {errorMsg}");
                MessageBox.Show(errorMsg, "Path Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.Error("Failed to create output directory: Path not found. Path={OutputPath}", outputPath);
                return;
            }

            try
            {
                var path = Path.Combine(outputPath, $"{job.JobId}.manifest.json");
                File.WriteAllText(path, json);
                LogUi($"✓ Manifest written: {path}");
                _logger.Information("Manifest written successfully: {ManifestPath}", path);
                _auditLogger.LogExportCompleted(job.JobId, 0, outputPath, DateTime.UtcNow);
            }
            catch (UnauthorizedAccessException)
            {
                const string errorMsg = "Permission denied: cannot write to the output directory.";
                LogUi($"ERROR: {errorMsg}");
                MessageBox.Show(errorMsg, "Permission Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.Error("Failed to write manifest: Access denied. Path={OutputPath}", outputPath);
            }
            catch (IOException ioEx)
            {
                var errorMsg = $"I/O error writing manifest: {ioEx.Message}";
                LogUi($"ERROR: {errorMsg}");
                MessageBox.Show(errorMsg, "I/O Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                _logger.Error(ioEx, "I/O error writing manifest to {OutputPath}", outputPath);
            }
        }
        catch (Exception ex)
        {
            HandleUiError("Dry Run", ex);
        }
    }

    private ExportJob BuildJobFromUi()
    {
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
                ProfileName = (ProfileCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "AI Training Strict"
            },
            BurnedInPhi = new BurnedInPhiSettings
            {
                OcrMode = OcrWarnRadio.IsChecked == true ? OcrMode.WarnAndRequireApproval : OcrMode.Off,
                OcrAcceleration = GpuOnlyRadio.IsChecked == true
                    ? OcrAccelerationMode.GpuOnly
                    : CpuOnlyRadio.IsChecked == true
                        ? OcrAccelerationMode.CpuOnly
                        : OcrAccelerationMode.GpuCpuAuto,
                PixelAction = PixelateRadio.IsChecked == true ? PixelScrubAction.Pixelate : PixelScrubAction.BlackMask
            },
            ImageSafety = ImageSafetyPolicy.StrictDefault(),
            Export = new ExportSettings
            {
                TargetType = ExportTargetType.Folder,
                OutputPath = OutputPathTextBox.Text,
                DestinationAeTitle = DestAeTextBox.Text,
                DestinationHost = DestHostTextBox.Text,
                DestinationPort = int.TryParse(DestPortTextBox.Text, out var destPort) ? destPort : 104
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

        if (job.Input.Port <= 0 || job.Input.Port > 65535)
        {
            result.AddError("Invalid SCP port.");
        }

        if (job.BurnedInPhi.OcrMode != OcrMode.Off)
        {
            result.AddWarning("OCR is advisory. Manual review and templates are still required for high-risk modalities.");
        }

        if (job.DeIdentification.Mode == DeIdentificationMode.Pseudonymization && !job.DeIdentification.DateOffset.VaultScoped)
        {
            result.AddError("Pseudonymization with deterministic date offset requires vault-scoped offset.");
        }

        return result;
    }

    /// <summary>
    /// Log a message to the UI text box (for user visibility).
    /// </summary>
    private void LogUi(string message)
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }

    /// <summary>
    /// Handle UI errors: log, display message box, and record in structured logs.
    /// </summary>
    private void HandleUiError(string operation, Exception ex)
    {
        var message = $"Operation failed: {operation}\n\n{ex.GetType().Name}: {ex.Message}";
        LogUi($"ERROR [{operation}]: {ex.Message}");
        MessageBox.Show(message, "Operation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        _logger.Error(ex, "UI operation failed: {Operation}", operation);
    }
}
