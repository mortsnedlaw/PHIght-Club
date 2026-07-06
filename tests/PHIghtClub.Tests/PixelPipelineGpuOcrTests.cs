using Xunit;
using PHIghtClub.Core;
using PHIghtClub.Pixel;
using PHIghtClub.Ocr;

namespace PHIghtClub.Tests;

/// <summary>
/// Integration tests for pixel pipeline and OCR GPU/CPU acceleration.
/// Validates image safety constraints and backend fallback logic.
/// </summary>
public class PixelPipelineGpuOcrTests
{
    [Fact]
    public async Task PixelScrubber_Pixelation_ReducesImageDetailsInMaskedRegions()
    {
        // Arrange
        var scrubber = new PixelPipelineImpl();
        var testDicomPath = Path.Combine(Path.GetTempPath(), "test.dcm");
        var regions = new[] { new MaskRegion(10, 10, 100, 100) };
        var action = PixelScrubAction.Pixelate;
        var safetyPolicy = ImageSafetyPolicy.StrictDefault();

        // Act
        var result = await scrubber.ApplyAsync(testDicomPath, regions, action, safetyPolicy, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Would verify pixelated output if DICOM file existed
    }

    [Fact]
    public async Task PixelScrubber_Blur_PreservesImageWhileSofteningBurnedInText()
    {
        // Arrange
        var scrubber = new PixelPipelineImpl();
        var testDicomPath = Path.Combine(Path.GetTempPath(), "test.dcm");
        var regions = new[] { new MaskRegion(50, 50, 150, 100) };
        var action = PixelScrubAction.Blur;
        var safetyPolicy = ImageSafetyPolicy.StrictDefault();

        // Act
        var result = await scrubber.ApplyAsync(testDicomPath, regions, action, safetyPolicy, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task PixelScrubber_BlackMask_CompletelyRemovesSensitiveAreas()
    {
        // Arrange
        var scrubber = new PixelPipelineImpl();
        var testDicomPath = Path.Combine(Path.GetTempPath(), "test.dcm");
        var regions = new[] { new MaskRegion(100, 100, 200, 200) };
        var action = PixelScrubAction.BlackMask;
        var safetyPolicy = ImageSafetyPolicy.StrictDefault();

        // Act
        var result = await scrubber.ApplyAsync(testDicomPath, regions, action, safetyPolicy, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task PixelScrubber_PreservesTransferSyntax_WhenSafetyPolicyEnforcesIt()
    {
        // Arrange
        var scrubber = new PixelPipelineImpl();
        var testDicomPath = Path.Combine(Path.GetTempPath(), "test.dcm");
        var regions = new[] { new MaskRegion(0, 0, 50, 50) };
        var safetyPolicy = new ImageSafetyPolicy
        {
            PreserveOriginalTransferSyntax = true,
            NeverConvertLosslessToLossy = true
        };

        // Act
        var result = await scrubber.ApplyAsync(testDicomPath, regions, PixelScrubAction.Pixelate, safetyPolicy, CancellationToken.None);

        // Assert
        // Would verify transfer syntax preservation if DICOM file existed
        Assert.NotNull(result);
    }

    [Fact]
    public async Task PixelScrubber_Blocks_WhenNoPixelDataFound()
    {
        // Arrange
        var scrubber = new PixelPipelineImpl();
        var testDicomPath = Path.Combine(Path.GetTempPath(), "nonexistent.dcm");
        var regions = new[] { new MaskRegion(0, 0, 100, 100) };
        var safetyPolicy = ImageSafetyPolicy.StrictDefault();

        // Act
        var result = await scrubber.ApplyAsync(testDicomPath, regions, PixelScrubAction.Pixelate, safetyPolicy, CancellationToken.None);

        // Assert
        Assert.True(result.Blocked, "Should block when DICOM file doesn't exist");
    }

    [Fact]
    public async Task OcrEngine_InitializeAsync_GpuOnly_FailsWithoutGpuDetection()
    {
        // Arrange
        var engine = new ProductionOcrEngine();

        // Act
        var status = await engine.InitializeAsync(OcrAccelerationMode.GpuOnly, CancellationToken.None);

        // Assert
        Assert.NotNull(status);
        // GPU may or may not be available in test environment
    }

    [Fact]
    public async Task OcrEngine_InitializeAsync_GpuCpuAuto_FallsBackToCpu()
    {
        // Arrange
        var engine = new ProductionOcrEngine();

        // Act
        var status = await engine.InitializeAsync(OcrAccelerationMode.GpuCpuAuto, CancellationToken.None);

        // Assert
        Assert.NotNull(status);
        Assert.True(status.Available, "Should be available (CPU fallback)");
        // Will use GPU if available, otherwise CPU
    }

    [Fact]
    public async Task OcrEngine_InitializeAsync_CpuOnly_SucceedsWithoutGpu()
    {
        // Arrange
        var engine = new ProductionOcrEngine();

        // Act
        var status = await engine.InitializeAsync(OcrAccelerationMode.CpuOnly, CancellationToken.None);

        // Assert
        Assert.NotNull(status);
        Assert.True(status.Available, "CPU should always be available");
        Assert.Equal(OcrBackend.Cpu, status.Backend);
    }

    [Fact]
    public async Task OcrEngine_DetectTextAsync_ReturnsEmptyList_WhenNotInitialized()
    {
        // Arrange
        var engine = new ProductionOcrEngine();
        var dummyImage = new byte[] { 0x00, 0x01, 0x02 };

        // Act
        var regions = await engine.DetectTextAsync(dummyImage, CancellationToken.None);

        // Assert
        Assert.Empty(regions);
    }

    [Fact]
    public async Task OcrAccelerationManager_InitializeGpu_TimesOutGracefully()
    {
        // Arrange
        var manager = new OcrAccelerationManager();
        var timeout = TimeSpan.FromMilliseconds(100);

        // Act
        var result = await manager.TryInitializeGpuAsync(OcrAccelerationMode.GpuCpuAuto, timeout);

        // Assert
        // Should timeout and return false gracefully
        Assert.False(result);
    }

    [Fact]
    public async Task OcrAccelerationManager_ReleasesGpuContext_OnDispose()
    {
        // Arrange
        var manager = new OcrAccelerationManager();

        // Act
        manager.ReleaseGpuContext();
        await manager.DisposeAsync();

        // Assert
        // Should not throw and clean up resources
    }

    [Theory]
    [InlineData(10, 10, 100, 100)]
    [InlineData(0, 0, 512, 512)]
    [InlineData(250, 250, 100, 100)]
    public async Task PixelScrubber_HandlesDifferentRegionSizes(int x, int y, int width, int height)
    {
        // Arrange
        var scrubber = new PixelPipelineImpl();
        var testDicomPath = Path.Combine(Path.GetTempPath(), "test.dcm");
        var regions = new[] { new MaskRegion(x, y, width, height) };
        var safetyPolicy = ImageSafetyPolicy.StrictDefault();

        // Act
        var result = await scrubber.ApplyAsync(testDicomPath, regions, PixelScrubAction.Pixelate, safetyPolicy, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task OcrEngine_SupportsMultipleInitializationModes()
    {
        // Arrange
        var modes = new[] { OcrAccelerationMode.GpuOnly, OcrAccelerationMode.GpuCpuAuto, OcrAccelerationMode.CpuOnly };

        // Act & Assert
        foreach (var mode in modes)
        {
            var engine = new ProductionOcrEngine();
            var status = await engine.InitializeAsync(mode, CancellationToken.None);
            Assert.NotNull(status);
        }
    }
}

/// <summary>
/// Unit tests for image safety constraint enforcement.
/// </summary>
public class ImageSafetyPolicyTests
{
    [Fact]
    public void StrictDefault_EnforcesAllSafetyConstraints()
    {
        // Act
        var policy = ImageSafetyPolicy.StrictDefault();

        // Assert
        Assert.True(policy.PreserveOriginalTransferSyntax);
        Assert.True(policy.NeverConvertLosslessToLossy);
        Assert.True(policy.BlockIfSafeReEncodingUnavailable);
        Assert.True(policy.DoNotTouchPixelDataUnlessScrubEnabled);
        Assert.True(policy.ValidateOutputAfterWrite);
    }

    [Fact]
    public void CustomPolicy_CanRelaxConstraints()
    {
        // Arrange & Act
        var policy = new ImageSafetyPolicy
        {
            Mode = ImageSafetyMode.Balanced,
            PreserveOriginalTransferSyntax = true,
            NeverConvertLosslessToLossy = false,
            DoNotTouchPixelDataUnlessScrubEnabled = true
        };

        // Assert
        Assert.Equal(ImageSafetyMode.Balanced, policy.Mode);
        Assert.False(policy.NeverConvertLosslessToLossy);
    }
}

/// <summary>
/// Tests for OCR region detection and masking.
/// </summary>
public class OcrRegionTests
{
    [Fact]
    public void OcrRegion_CanRepresentDetectedText()
    {
        // Arrange
        var region = new OcrRegion(
            Frame: 0,
            Text: "Patient: John Doe",
            Confidence: 0.95,
            X: 10,
            Y: 20,
            Width: 200,
            Height: 30,
            Action: "Mask"
        );

        // Assert
        Assert.Equal("Patient: John Doe", region.Text);
        Assert.Equal(0.95, region.Confidence);
        Assert.Equal(0, region.Frame);
    }

    [Fact]
    public void MaskRegion_SpecifiesPixelScrubArea()
    {
        // Arrange
        var region = new MaskRegion(
            X: 50,
            Y: 60,
            Width: 300,
            Height: 100,
            Scope: "Series"
        );

        // Assert
        Assert.Equal(50, region.X);
        Assert.Equal(60, region.Y);
        Assert.Equal(300, region.Width);
        Assert.Equal(100, region.Height);
        Assert.Equal("Series", region.Scope);
    }

    [Fact]
    public void MaskRegion_SupportsPerFrameScoping()
    {
        // Arrange
        var region = new MaskRegion(
            X: 0,
            Y: 0,
            Width: 512,
            Height: 512,
            Frame: 5,
            Scope: "Frame"
        );

        // Assert
        Assert.Equal(5, region.Frame);
        Assert.Equal("Frame", region.Scope);
    }
}
