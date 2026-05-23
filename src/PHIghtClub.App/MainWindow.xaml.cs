using System.IO;
using System.Text.Json;
using System.Windows;
using PHIghtClub.Core;
using PHIghtClub.Dicom;
using PHIghtClub.Export;
using PHIghtClub.Storage;

namespace PHIghtClub.App;

public partial class MainWindow : Window
{
    private readonly ManifestFactory _manifestFactory = new();
    private readonly IManifestIntegrityService _manifestIntegrity = new ManifestIntegrityService();
    private readonly IDicomImportService _dicomImport = new NoopDicomImportService();

    public MainWindow()
    {
        InitializeComponent();
        Log("PHIght Club v1.0.0 source release shell started.");
        Log("OCR is advisory. No OCR findings does not guarantee that images are free from burned-in PHI.");
    }

    private void StartListener_Click(object sender, RoutedEventArgs e)
    {
        Log($"Listener start requested: AE={AeTitleTextBox.Text}, Port={PortTextBox.Text}, Bind={BindTextBox.Text}");
        Log("v1.0.0 source release placeholder: real Storage SCP implementation is pending fo-dicom integration.");
    }

    private void StopListener_Click(object sender, RoutedEventArgs e)
    {
        Log("Listener stop requested.");
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = ImportFolderTextBox.Text;
        var result = await _dicomImport.ImportFolderAsync(folder, CancellationToken.None);
        Log($"Folder scan: files={result.FilesScanned}, accepted={result.InstancesAccepted}, quarantine={result.InstancesQuarantined}");
        foreach (var warning in result.Warnings)
        {
            Log("WARNING: " + warning);
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        Log("Preview/mask requested. v1.0.0 source release placeholder: pixel preview is pending pixel pipeline integration.");
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

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        Log("Export requested. Real export is intentionally blocked in this source release until DICOM and pixel pipelines are validated.");
        Log("Use Dry run to generate a signed manifest preview.");
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

        var manifest = _manifestFactory.CreateDryRunManifest(job, warnings);
        var devKey = System.Text.Encoding.UTF8.GetBytes("PHIghtClub-MVP-Development-Key-Replace-Me-32-bytes-minimum");
        manifest.ManifestIntegrity = _manifestIntegrity.Sign(manifest, [], devKey, "mvp-dev-key");

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

    private void Log(string message)
    {
        LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogTextBox.ScrollToEnd();
    }
}
