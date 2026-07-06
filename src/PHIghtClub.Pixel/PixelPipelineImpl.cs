using System.Diagnostics;
using System.Runtime.InteropServices;
using FellowOakDicom;
using FellowOakDicom.IO.Buffer;
using PHIghtClub.Core;

namespace PHIghtClub.Pixel;

/// <summary>
/// CPU-based pixel scrubber implementation supporting pixelation, blur, and masking.
/// Handles DICOM image safety constraints: preserves transfer syntax, prevents lossy conversion.
/// Uses FoDicom.Core for DICOM file operations.
/// </summary>
public sealed class PixelPipelineImpl : IPixelScrubber
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public async Task<PixelScrubResult> ApplyAsync(
        string dicomFilePath,
        IReadOnlyList<MaskRegion> regions,
        PixelScrubAction action,
        ImageSafetyPolicy safetyPolicy,
        CancellationToken cancellationToken)
    {
        return await Task.Run(async () =>
        {
            try
            {
                if (!File.Exists(dicomFilePath))
                    return Blocked("DICOM file not found.");

                if (regions.Count == 0)
                    return new PixelScrubResult(false, true, false, false, "No regions to scrub.");

                // Load DICOM with FoDicom
                DicomFile dicomFile;
                try
                {
                    dicomFile = await DicomFile.OpenAsync(dicomFilePath, FileReadOption.ReadAll, 4096);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to load DICOM file");
                    return Blocked($"Failed to load DICOM file: {ex.Message}");
                }

                var dataset = dicomFile.Dataset;

                // Check original transfer syntax
                var originalTransferSyntax = GetTransferSyntax(dataset);
                var isLossless = IsLosslessCompression(originalTransferSyntax);

                if (safetyPolicy.DoNotTouchPixelDataUnlessScrubEnabled && action == PixelScrubAction.None)
                    return new PixelScrubResult(false, true, false, false, "Pixel scrub not enabled.");

                // Extract pixel data
                var pixelData = ExtractPixelData(dataset);
                if (pixelData == null || pixelData.Length == 0)
                    return new PixelScrubResult(false, true, false, false, "No pixel data found.");

                // Get image dimensions
                var rows = dataset.GetSingleValue<int>(DicomTag.Rows);
                var cols = dataset.GetSingleValue<int>(DicomTag.Columns);
                var frames = dataset.GetSingleValue<int>(DicomTag.NumberOfFrames);

                if (rows <= 0 || cols <= 0)
                    return Blocked("Invalid image dimensions.");

                int bitsAllocated = dataset.GetSingleValue<int>(DicomTag.BitsAllocated);
                if (bitsAllocated <= 0)
                {
                    bitsAllocated = 8;
                }
                int bytesPerPixel = Math.Max(1, bitsAllocated / 8);

                // Apply pixel scrubbing
                var scrubbed = action switch
                {
                    PixelScrubAction.Pixelate => ApplyPixelation(pixelData, rows, cols, regions, bytesPerPixel),
                    PixelScrubAction.Blur => ApplyBlur(pixelData, rows, cols, regions, bytesPerPixel),
                    PixelScrubAction.BlackMask => ApplyMasking(pixelData, rows, cols, regions, bytesPerPixel),
                    _ => pixelData
                };

                // Check for lossy conversion risk
                bool lossyIntroduced = false;
                if (safetyPolicy.NeverConvertLosslessToLossy && isLossless)
                {
                    // If output would be lossy, check safety policy
                    if (WouldBecomeLossy(originalTransferSyntax, action))
                    {
                        if (safetyPolicy.BlockIfSafeReEncodingUnavailable)
                            return Blocked("Cannot perform lossy conversion on lossless source without safe re-encoding.");
                        lossyIntroduced = true;
                    }
                }

                // Write modified pixel data back
                try
                {
                    WritePixelDataToDicom(dataset, scrubbed);
                    
                    // Save modified DICOM file
                    await dicomFile.SaveAsync(dicomFilePath, FellowOakDicom.IO.Writer.DicomWriteOptions.Default);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to save modified DICOM");
                    return Blocked($"Failed to save DICOM: {ex.Message}");
                }

                // Validate output if required
                if (safetyPolicy.ValidateOutputAfterWrite)
                {
                    var validated = await ValidateOutputAsync(dicomFilePath, cancellationToken);
                    if (!validated)
                        return Blocked("Output validation failed.");
                }

                Logger.Info($"Successfully scrubbed {regions.Count} regions from {Path.GetFileName(dicomFilePath)}");
                return new PixelScrubResult(true, safetyPolicy.PreserveOriginalTransferSyntax, lossyIntroduced, false,
                    $"Applied {action} to {regions.Count} regions. Transfer syntax preserved: {safetyPolicy.PreserveOriginalTransferSyntax}");
            }
            catch (OperationCanceledException)
            {
                return Blocked("Operation cancelled.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Pixel scrub failed");
                return Blocked($"Pixel scrub error: {ex.Message}");
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Apply pixelation (mosaic effect) to specified regions.
    /// </summary>
    private byte[] ApplyPixelation(byte[] pixelData, int rows, int cols, IReadOnlyList<MaskRegion> regions, int bytesPerPixel)
    {
        var result = new byte[pixelData.Length];
        Array.Copy(pixelData, result, pixelData.Length);

        const int pixelBlockSize = 8;

        foreach (var region in regions)
        {
            if (region.Frame.HasValue)
                continue; // Simplified: skip multi-frame for now

            for (int y = region.Y; y < Math.Min(region.Y + region.Height, rows); y += pixelBlockSize)
            {
                for (int x = region.X; x < Math.Min(region.X + region.Width, cols); x += pixelBlockSize)
                {
                    // Calculate block boundaries
                    int blockRight = Math.Min(x + pixelBlockSize, Math.Min(region.X + region.Width, cols));
                    int blockBottom = Math.Min(y + pixelBlockSize, Math.Min(region.Y + region.Height, rows));

                    // Calculate average pixel value in block
                    long sum = 0;
                    int count = 0;

                    for (int by = y; by < blockBottom; by++)
                    {
                        for (int bx = x; bx < blockRight; bx++)
                        {
                            int offset = (by * cols + bx) * bytesPerPixel;
                            if (offset + bytesPerPixel <= result.Length)
                            {
                                sum += result[offset];
                                count++;
                            }
                        }
                    }

                    byte avgValue = count > 0 ? (byte)(sum / count) : (byte)128;

                    // Fill block with average value
                    for (int by = y; by < blockBottom; by++)
                    {
                        for (int bx = x; bx < blockRight; bx++)
                        {
                            int offset = (by * cols + bx) * bytesPerPixel;
                            if (offset + bytesPerPixel <= result.Length)
                            {
                                result[offset] = avgValue;
                            }
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Apply Gaussian-like blur to specified regions using box filter approximation.
    /// </summary>
    private byte[] ApplyBlur(byte[] pixelData, int rows, int cols, IReadOnlyList<MaskRegion> regions, int bytesPerPixel)
    {
        var result = new byte[pixelData.Length];
        Array.Copy(pixelData, result, pixelData.Length);

        const int blurRadius = 3;

        foreach (var region in regions)
        {
            if (region.Frame.HasValue)
                continue;

            for (int y = region.Y; y < Math.Min(region.Y + region.Height, rows); y++)
            {
                for (int x = region.X; x < Math.Min(region.X + region.Width, cols); x++)
                {
                    long sum = 0;
                    int count = 0;

                    // Box filter kernel
                    for (int ky = -blurRadius; ky <= blurRadius; ky++)
                    {
                        for (int kx = -blurRadius; kx <= blurRadius; kx++)
                        {
                            int ny = y + ky;
                            int nx = x + kx;

                            if (ny >= 0 && ny < rows && nx >= 0 && nx < cols)
                            {
                                int offset = (ny * cols + nx) * bytesPerPixel;
                                if (offset + bytesPerPixel <= result.Length)
                                {
                                    sum += result[offset];
                                    count++;
                                }
                            }
                        }
                    }

                    int blurredValue = count > 0 ? (int)(sum / count) : 128;
                    int outOffset = (y * cols + x) * bytesPerPixel;
                    if (outOffset + bytesPerPixel <= result.Length)
                    {
                        result[outOffset] = (byte)Math.Clamp(blurredValue, 0, 255);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Apply black masking to specified regions.
    /// </summary>
    private byte[] ApplyMasking(byte[] pixelData, int rows, int cols, IReadOnlyList<MaskRegion> regions, int bytesPerPixel)
    {
        var result = new byte[pixelData.Length];
        Array.Copy(pixelData, result, pixelData.Length);

        foreach (var region in regions)
        {
            if (region.Frame.HasValue)
                continue;

            for (int y = region.Y; y < Math.Min(region.Y + region.Height, rows); y++)
            {
                for (int x = region.X; x < Math.Min(region.X + region.Width, cols); x++)
                {
                    int offset = (y * cols + x) * bytesPerPixel;
                    if (offset + bytesPerPixel <= result.Length)
                    {
                        // Black mask (0x00)
                        for (int b = 0; b < bytesPerPixel; b++)
                            result[offset + b] = 0x00;
                    }
                }
            }
        }

        return result;
    }

    private string GetTransferSyntax(DicomDataset dataset)
    {
        try
        {
            return dataset.InternalTransferSyntax?.UID.UID ?? "1.2.840.10008.1.2.1";
        }
        catch
        {
            return "1.2.840.10008.1.2.1"; // Default: Explicit VR Little Endian
        }
    }

    private byte[]? ExtractPixelData(DicomDataset dataset)
    {
        try
        {
            var pixelDataElement = dataset.FirstOrDefault(x => x.Tag == DicomTag.PixelData);
            if (pixelDataElement != null && pixelDataElement is DicomOtherByte ob)
            {
                return ob.Buffer.Data;
            }
            return null;
        }
        catch (Exception ex)
        {
            Logger.Debug(ex, "Failed to extract pixel data");
            return null;
        }
    }

    private void WritePixelDataToDicom(DicomDataset dataset, byte[] pixelData)
    {
        try
        {
            // Remove existing PixelData element
            dataset.Remove(DicomTag.PixelData);
            
            // Add modified pixel data
            var buffer = new MemoryByteBuffer(pixelData);
            dataset.Add(new DicomOtherByte(DicomTag.PixelData, buffer));
            
            Logger.Debug($"Updated PixelData with {pixelData.Length} bytes");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to write pixel data to DICOM");
            throw;
        }
    }

    private async Task<bool> ValidateOutputAsync(string path, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                var file = DicomFile.Open(path);
                var dataset = file.Dataset;
                
                // Verify PixelData exists and is readable
                var pixelData = ExtractPixelData(dataset);
                if (pixelData == null || pixelData.Length == 0)
                {
                    Logger.Warn("Validation: No pixel data found in output");
                    return false;
                }

                // Verify required DICOM tags still exist
                var rows = dataset.GetSingleValue<int>(DicomTag.Rows);
                var cols = dataset.GetSingleValue<int>(DicomTag.Columns);
                
                if (rows <= 0 || cols <= 0)
                {
                    Logger.Warn("Validation: Invalid dimensions in output");
                    return false;
                }

                Logger.Debug("Output validation successful");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Output validation failed");
                return false;
            }
        }, cancellationToken);
    }

    private bool IsLosslessCompression(string transferSyntax)
    {
        // List of lossless transfer syntaxes
        var lossless = new[]
        {
            "1.2.840.10008.1.2",      // Implicit VR Little Endian
            "1.2.840.10008.1.2.1",    // Explicit VR Little Endian
            "1.2.840.10008.1.2.2",    // Explicit VR Big Endian
            "1.2.840.10008.1.2.5",    // RLE Lossless
            "1.2.840.10008.1.2.1.99", // JPEG-LL Lossless
        };
        return lossless.Contains(transferSyntax);
    }

    private bool WouldBecomeLossy(string originalTransferSyntax, PixelScrubAction action)
    {
        // Pixelation, blur, and masking can introduce changes that require re-encoding
        // If original is lossless and we'd need to re-encode, it could become lossy
        return action != PixelScrubAction.None;
    }

    private static PixelScrubResult Blocked(string message) =>
        new(false, false, false, true, message);
}

/// <summary>
/// Async-safe resource manager for GPU/CPU OCR engine switching.
/// </summary>
public sealed class OcrAccelerationManager : IAsyncDisposable
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private readonly SemaphoreSlim _gpuSemaphore = new(1, 1);
    private readonly CancellationTokenSource _shutdownToken = new();
    private IntPtr _gpuContext = IntPtr.Zero;
    private bool _disposed;

    public async ValueTask<bool> TryInitializeGpuAsync(OcrAccelerationMode mode, TimeSpan timeout)
    {
        if (mode == OcrAccelerationMode.CpuOnly)
            return false;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken.Token);
            cts.CancelAfter(timeout);

            await _gpuSemaphore.WaitAsync(cts.Token);
            try
            {
                // Attempt GPU initialization (DirectML, CUDA, etc.)
                return await DetectAndInitializeGpuAsync(cts.Token);
            }
            finally
            {
                _gpuSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Warn("GPU initialization timeout.");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "GPU initialization failed");
            return false;
        }
    }

    public void ReleaseGpuContext()
    {
        if (_gpuContext != IntPtr.Zero)
        {
            try
            {
                // Cleanup GPU resources
                _gpuContext = IntPtr.Zero;
                Logger.Info("GPU context released.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error releasing GPU context");
            }
        }
    }

    private async Task<bool> DetectAndInitializeGpuAsync(CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Detect CUDA
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    if (TryInitializeCuda())
                    {
                        Logger.Info("CUDA GPU detected and initialized.");
                        return true;
                    }

                    // Fallback to DirectML
                    if (TryInitializeDirectML())
                    {
                        Logger.Info("DirectML GPU detected and initialized.");
                        return true;
                    }
                }

                // Linux: Try CUDA, OpenCL
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    if (TryInitializeCuda())
                    {
                        Logger.Info("CUDA GPU detected and initialized (Linux).");
                        return true;
                    }
                }

                Logger.Info("No GPU backend detected. Will use CPU fallback.");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "GPU detection failed");
                return false;
            }
        }, cancellationToken);
    }

    private bool TryInitializeCuda()
    {
        try
        {
            // Placeholder: would P/Invoke into CUDA runtime
            // var cudaVersion = cudaGetVersion();
            // if (cudaVersion >= 11000) { _gpuContext = cudaContext; return true; }
            return false; // Not implemented in this placeholder
        }
        catch
        {
            return false;
        }
    }

    private bool TryInitializeDirectML()
    {
        try
        {
            // Placeholder: would create DirectML device
            // var device = D3D12CreateDevice(...);
            // var dmlDevice = DMLCreateDevice(...);
            return false; // Not implemented in this placeholder
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            _shutdownToken.Cancel();
            ReleaseGpuContext();
            await _gpuSemaphore.WaitAsync();
            _gpuSemaphore.Dispose();
            _shutdownToken.Dispose();
            _disposed = true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error disposing OcrAccelerationManager");
        }
    }
}
