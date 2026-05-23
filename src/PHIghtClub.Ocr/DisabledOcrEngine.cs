using PHIghtClub.Core;

namespace PHIghtClub.Ocr;

public sealed class DisabledOcrEngine : IOcrEngine
{
    public string Name => "Disabled OCR";
    public OcrBackend Backend => OcrBackend.Disabled;

    public Task<OcrEngineStatus> InitializeAsync(OcrAccelerationMode mode, CancellationToken cancellationToken)
    {
        return Task.FromResult(new OcrEngineStatus(true, OcrBackend.Disabled, false, "OCR disabled."));
    }

    public Task<IReadOnlyList<OcrRegion>> DetectTextAsync(object imageFrame, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<OcrRegion>>([]);
    }
}

public sealed class PlaceholderOcrEngine : IOcrEngine
{
    public string Name => "Placeholder OCR";
    public OcrBackend Backend { get; private set; } = OcrBackend.Cpu;

    public Task<OcrEngineStatus> InitializeAsync(OcrAccelerationMode mode, CancellationToken cancellationToken)
    {
        // Source-release placeholder. Real implementation should try DirectML/CUDA/CPU based on mode.
        return mode switch
        {
            OcrAccelerationMode.GpuOnly => Task.FromResult(new OcrEngineStatus(false, OcrBackend.Disabled, false, "GPU OCR backend is not implemented in this source release.")),
            OcrAccelerationMode.GpuCpuAuto => Task.FromResult(new OcrEngineStatus(true, OcrBackend.Cpu, true, "GPU backend not implemented. Falling back to CPU placeholder.")),
            OcrAccelerationMode.CpuOnly => Task.FromResult(new OcrEngineStatus(true, OcrBackend.Cpu, false, "CPU placeholder initialized.")),
            _ => Task.FromResult(new OcrEngineStatus(false, OcrBackend.Disabled, false, "Unknown OCR mode."))
        };
    }

    public Task<IReadOnlyList<OcrRegion>> DetectTextAsync(object imageFrame, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<OcrRegion>>([]);
    }
}
