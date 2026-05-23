using PHIghtClub.Core;

namespace PHIghtClub.Pixel;

public sealed record MaskRegion(
    int X,
    int Y,
    int Width,
    int Height,
    int? Frame = null,
    string Scope = "Series");

public sealed record PixelScrubResult(
    bool PixelDataModified,
    bool TransferSyntaxPreserved,
    bool LossyCompressionIntroduced,
    bool Blocked,
    string Message);

public interface IPixelScrubber
{
    Task<PixelScrubResult> ApplyAsync(
        string dicomFilePath,
        IReadOnlyList<MaskRegion> regions,
        PixelScrubAction action,
        ImageSafetyPolicy safetyPolicy,
        CancellationToken cancellationToken);
}

public sealed class NoopPixelScrubber : IPixelScrubber
{
    public Task<PixelScrubResult> ApplyAsync(string dicomFilePath, IReadOnlyList<MaskRegion> regions, PixelScrubAction action, ImageSafetyPolicy safetyPolicy, CancellationToken cancellationToken)
    {
        return Task.FromResult(new PixelScrubResult(false, true, false, false, "No pixel scrub performed in this source release placeholder."));
    }
}
