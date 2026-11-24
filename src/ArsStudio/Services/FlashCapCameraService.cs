using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArsStudio.Core.Scanning;
using ArsStudio.ViewModels;
using FlashCap;

namespace ArsStudio.Services;

public sealed class FlashCapCameraService : ICameraService
{
    private const int PreferredWidth = 640;
    private const int MaxFrameWidth = 960;

    private readonly CaptureDevices _devices = new();
    private CaptureDevice? _device;
    private (int Width, int Height)[] _knownSizes = [];

    public IReadOnlyList<string> GetCameraNames() =>
        Descriptors().Select(descriptor => descriptor.Name).Distinct().ToArray();

    public async Task StartAsync(string name, Action<FrameImage> frameArrived)
    {
        await StopAsync();

        var descriptor = Descriptors()
            .OrderBy(candidate => candidate.DeviceType == DeviceTypes.VideoForWindows)
            .FirstOrDefault(candidate => candidate.Name == name)
            ?? throw new InvalidOperationException($"Videocamera {name} non trovata.");

        var characteristics = Supported(descriptor)
            .OrderBy(candidate => Math.Abs(candidate.Width - PreferredWidth))
            .ThenBy(candidate => candidate.PixelFormat is PixelFormats.RGB32 or PixelFormats.ARGB32 or PixelFormats.RGB24 ? 0 : 1)
            .First();

        _knownSizes = descriptor.Characteristics
            .Select(candidate => (candidate.Width, candidate.Height))
            .Distinct()
            .ToArray();

        var knownSizes = _knownSizes;
        var device = await descriptor.OpenAsync(
            characteristics,
            TranscodeFormats.Auto,
            false,
            1,
            scope => OnFrame(scope, knownSizes, frameArrived));

        _device = device;
        await device.StartAsync();
    }

    public async Task StopAsync()
    {
        var device = _device;
        _device = null;
        if (device is null)
        {
            return;
        }

        try
        {
            await device.StopAsync();
        }
        finally
        {
            device.Dispose();
        }
    }

    public void Dispose()
    {
        _device?.Dispose();
        _device = null;
    }

    private IEnumerable<CaptureDeviceDescriptor> Descriptors() =>
        _devices.EnumerateDescriptors().Where(descriptor => Supported(descriptor).Any());

    private static IEnumerable<VideoCharacteristics> Supported(CaptureDeviceDescriptor descriptor) =>
        descriptor.Characteristics.Where(candidate =>
            candidate.PixelFormat is not (PixelFormats.Unknown or PixelFormats.JPEG or PixelFormats.PNG));

    private static void OnFrame(PixelBufferScope scope, (int Width, int Height)[] knownSizes, Action<FrameImage> frameArrived)
    {
        try
        {
            var image = scope.Buffer.ReferImage();
            var frame = BitmapFrameReader.Read(image.AsSpan(), knownSizes, MaxFrameWidth);
            if (frame is not null)
            {
                frameArrived(frame);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }
}
