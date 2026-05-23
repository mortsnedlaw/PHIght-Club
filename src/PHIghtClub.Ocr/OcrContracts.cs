using PHIghtClub.Core;

namespace PHIghtClub.Ocr;

public sealed record OcrRegion(
    int Frame,
    string Text,
    double Confidence,
    int X,
    int Y,
    int Width,
    int Height,
    string Action);

public sealed record OcrEngineStatus(
    bool Available,
    OcrBackend Backend,
    bool CpuFallbackUsed,
    string Message);

public interface IOcrEngine
{
    string Name { get; }
    OcrBackend Backend { get; }
    Task<OcrEngineStatus> InitializeAsync(OcrAccelerationMode mode, CancellationToken cancellationToken);
    Task<IReadOnlyList<OcrRegion>> DetectTextAsync(object imageFrame, CancellationToken cancellationToken);
}
