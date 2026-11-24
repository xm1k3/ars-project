namespace ArsStudio.Core.Scanning;

public sealed class FaceDetector
{
    private readonly StickerBlobFinder _finder = new();

    public DetectedFace? Detect(FrameImage frame, PixelRect crop) =>
        FaceLattice.Fit(_finder.Find(frame, crop));
}
