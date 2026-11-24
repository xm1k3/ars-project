using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArsStudio.Core;
using ArsStudio.Core.Scanning;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace ArsStudio.ViewModels;

public partial class ScanViewModel
{
    private const int MinTrackedStickers = 8;
    private const double TrackedCellRatio = 0.7;
    private const double FixedCellRatio = 0.92;
    private static readonly TimeSpan StableDuration = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan FaceHoldDuration = TimeSpan.FromSeconds(0.3);

    private readonly record struct Target(PixelRect Area, double Pitch);

    private readonly SemaphoreSlim _cameraGate = new(1, 1);
    private readonly Lock _frameLock = new();
    private readonly PreviewSurface _surface = new();
    private readonly FaceDetector _detector = new();
    private FrameImage? _pendingFrame;
    private bool _framePosted;
    private FrameImage? _lastFrame;
    private DateTime _faceSeen;
    private bool _canAutoCapture;

    partial void OnIsActiveChanged(bool value)
    {
        if (value && Cameras.Count == 0)
        {
            RefreshCameras();
        }

        if (value && SelectedCamera is null && Cameras.Count == 1)
        {
            SelectedCamera = Cameras[0];
            return;
        }

        _ = UpdateCaptureAsync();
    }

    partial void OnSelectedCameraChanged(string? value) => _ = UpdateCaptureAsync();

    partial void OnZoomChanged(double value) => Rerender();

    partial void OnBrightnessChanged(double value) => Rerender();

    partial void OnContrastChanged(double value) => Rerender();

    partial void OnSaturationChanged(double value) => Rerender();

    [RelayCommand]
    private void RefreshCameras()
    {
        if (_camera is null)
        {
            return;
        }

        var selected = SelectedCamera;
        try
        {
            Cameras = _camera.GetCameraNames();
        }
        catch (Exception exception) when (IsCameraError(exception))
        {
            Cameras = [];
        }

        if (selected is not null && !Cameras.Contains(selected))
        {
            SelectedCamera = null;
        }
    }

    private async Task UpdateCaptureAsync()
    {
        if (_camera is null)
        {
            return;
        }

        await _cameraGate.WaitAsync();
        try
        {
            await _camera.StopAsync();
            ClearPreview();

            if (!IsActive || SelectedCamera is not { } name)
            {
                return;
            }

            IsStarting = true;
            await _camera.StartAsync(name, OnFrameArrived);
        }
        catch (Exception exception) when (IsCameraError(exception))
        {
            var name = SelectedCamera;
            SelectedCamera = null;
            if (_showMessage is not null)
            {
                await _showMessage($"Impossibile avviare la videocamera {name}.");
            }
        }
        finally
        {
            IsStarting = false;
            _cameraGate.Release();
        }
    }

    private void ClearPreview()
    {
        lock (_frameLock)
        {
            _pendingFrame = null;
        }

        _lastFrame = null;
        Preview = null;
        HideFace();
    }

    private void OnFrameArrived(FrameImage frame)
    {
        lock (_frameLock)
        {
            _pendingFrame = frame;
            if (_framePosted)
            {
                return;
            }

            _framePosted = true;
        }

        Dispatcher.UIThread.Post(ProcessPendingFrame, DispatcherPriority.Background);
    }

    private void ProcessPendingFrame()
    {
        FrameImage? frame;
        lock (_frameLock)
        {
            frame = _pendingFrame;
            _pendingFrame = null;
            _framePosted = false;
        }

        if (frame is null || !IsActive || IsFrozen || SelectedCamera is null)
        {
            return;
        }

        _lastFrame = frame;
        Render(frame);
        DetectStableFace();
    }

    private void Rerender()
    {
        if (_lastFrame is not null)
        {
            Render(_lastFrame);
        }
    }

    private void Render(FrameImage frame)
    {
        var crop = ScanGrid.Crop(frame.Width, frame.Height, Zoom);
        var adjustment = new ColorAdjustment(Brightness, Contrast, Saturation);

        Preview = _surface.Draw(frame, crop, adjustment);
        PreviewWidth = _surface.Width;
        PreviewHeight = _surface.Height;

        var now = DateTime.UtcNow;
        if (FindTargets(frame, crop) is not { } targets)
        {
            _canAutoCapture = false;
            if (now - _faceSeen > FaceHoldDuration)
            {
                HideFace();
            }

            return;
        }

        _faceSeen = now;
        var scale = (double)_surface.Width / crop.Width;
        var ratio = IsTracking ? TrackedCellRatio : FixedCellRatio;
        for (var index = 0; index < targets.Length; index++)
        {
            var (area, pitch) = targets[index];
            var cell = Cells[index];
            _liveSamples[index] = adjustment.Apply(frame.Median(area));
            cell.Color = StickerClassifier.Classify(_liveSamples[index], _session.References);
            cell.Size = pitch * scale * ratio;
            cell.X = (area.X + area.Width / 2.0 - crop.X) * scale - cell.Size / 2;
            cell.Y = (area.Y + area.Height / 2.0 - crop.Y) * scale - cell.Size / 2;
        }

        _hasLiveSamples = true;
        HasFace = true;
    }

    private Target[]? FindTargets(FrameImage frame, PixelRect crop)
    {
        if (!IsTracking)
        {
            var pitch = ScanGrid.CellSize(crop);
            _canAutoCapture = true;
            return ScanGrid.Cells(crop).Select(area => new Target(area, pitch)).ToArray();
        }

        if (_detector.Detect(frame, crop) is not { } face)
        {
            return null;
        }

        _canAutoCapture = face.FoundCount >= MinTrackedStickers;
        return Enumerable.Range(0, CubeState.StickersPerFace)
            .Select(index => new Target(face.SampleArea(index), face.Spacing))
            .ToArray();
    }

    private void HideFace()
    {
        _hasLiveSamples = false;
        _canAutoCapture = false;
        HasFace = false;
        _stability.Reset();
        foreach (var cell in Cells)
        {
            cell.Color = CubeColor.Empty;
        }
    }

    private void DetectStableFace()
    {
        if (!IsAutoCapture || !_canAutoCapture || _session.IsComplete)
        {
            return;
        }

        var colors = Cells.Select(cell => cell.Color).ToArray();
        if (!IsTracking && colors.Distinct().Count() == 1)
        {
            _stability.Reset();
            return;
        }

        if (_stability.IsStable(colors, DateTime.UtcNow, StableDuration))
        {
            CaptureFace(overwrite: false);
        }
    }

    private static bool IsCameraError(Exception exception) =>
        exception is not (OutOfMemoryException or StackOverflowException or AccessViolationException);
}
