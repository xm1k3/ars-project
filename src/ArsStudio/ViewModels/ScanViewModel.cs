using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArsStudio.Core;
using ArsStudio.Core.Scanning;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArsStudio.ViewModels;

public partial class ScanViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan CaptureFlashDuration = TimeSpan.FromSeconds(1.2);
    private static readonly CubeFace[] AllFaces = Enum.GetValues<CubeFace>();
    private static readonly IReadOnlySet<int> NoDoubts = new HashSet<int>();

    private readonly ICameraService? _camera;
    private readonly Action<ScanReading, IReadOnlyList<CubeFace>>? _scanChanged;
    private readonly Func<string, Task>? _showMessage;
    private readonly Action? _completed;
    private readonly ScanSession _session = new();
    private readonly FaceStability _stability = new();
    private readonly Rgb[] _liveSamples = new Rgb[CubeState.StickersPerFace];
    private bool _hasLiveSamples;
    private int _assemblyVersion;
    private int _captureFlash;

    public ScanViewModel()
        : this(null, null, null, null)
    {
    }

    public ScanViewModel(
        ICameraService? camera,
        Action<ScanReading, IReadOnlyList<CubeFace>>? scanChanged,
        Func<string, Task>? showMessage,
        Action? completed)
    {
        _completed = completed;
        _camera = camera;
        _scanChanged = scanChanged;
        _showMessage = showMessage;

        Cells = Enumerable.Range(0, CubeState.StickersPerFace).Select(_ => new ScanCellViewModel()).ToArray();
        Steps = AllFaces
            .Select(face => new ScanStepViewModel(face, StickerClassifier.SlotColor(face), ColorName(StickerClassifier.SlotColor(face)), ClearFace))
            .ToArray();
    }

    public IReadOnlyList<ScanCellViewModel> Cells { get; }

    public IReadOnlyList<ScanStepViewModel> Steps { get; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Cameras { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial string? SelectedCamera { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview), nameof(StatusText))]
    public partial Bitmap? Preview { get; set; }

    [ObservableProperty]
    public partial double PreviewWidth { get; set; } = 640;

    [ObservableProperty]
    public partial double PreviewHeight { get; set; } = 480;

    [ObservableProperty]
    public partial bool HasFace { get; set; }

    [ObservableProperty]
    public partial double Zoom { get; set; } = 1;

    [ObservableProperty]
    public partial double Brightness { get; set; }

    [ObservableProperty]
    public partial double Contrast { get; set; }

    [ObservableProperty]
    public partial double Saturation { get; set; }

    [ObservableProperty]
    public partial bool IsFrozen { get; set; }

    [ObservableProperty]
    public partial bool IsAutoCapture { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Instruction))]
    public partial bool IsTracking { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Instruction))]
    public partial int ScannedCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Instruction))]
    public partial bool IsAssembling { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Instruction), nameof(IsCubeValid), nameof(IsCubeInvalid))]
    public partial bool? IsAssembled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Instruction))]
    public partial int DoubtCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Instruction))]
    public partial bool IsMirrored { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial bool IsStarting { get; set; }

    [ObservableProperty]
    public partial bool IsJustCaptured { get; set; }

    [ObservableProperty]
    public partial string LastCaptured { get; set; } = string.Empty;

    public bool HasPreview => Preview is not null;

    public bool IsCubeValid => IsAssembled == true;

    public bool IsCubeInvalid => IsAssembled == false;

    public int FaceCount => CubeState.FaceCount;

    public string StatusText => IsStarting
        ? "Avvio della videocamera..."
        : SelectedCamera is null
            ? "Scegli una videocamera dal menu a sinistra"
            : "In attesa delle immagini";

    public string Instruction => (ScannedCount, IsAssembling, IsAssembled) switch
    {
        (_, true, _) => "Ricostruzione del cubo...",
        (CubeState.FaceCount, _, true) when IsMirrored =>
            "Cubo acquisito e valido. Il tuo cubo ha rosso e arancione invertiti rispetto allo schema standard: qui li vedi scambiati, la soluzione resta corretta",
        (CubeState.FaceCount, _, true) => "Cubo acquisito e valido: puoi passare alla soluzione",
        (CubeState.FaceCount, _, false) when DoubtCount > 0 =>
            $"Il cubo letto non è valido. Le caselle più incerte sono segnate con un punto a destra ({DoubtCount}): correggile oppure clicca una faccia in alto per rifarla",
        (CubeState.FaceCount, _, false) =>
            "Il cubo letto non è valido: almeno un colore è sbagliato. Correggilo a destra oppure clicca una faccia in alto per rifarla",
        (0, _, _) when IsTracking => "Mostra il cubo alla videocamera, una faccia alla volta e di fronte: ogni faccia viene riconosciuta e salvata da sola",
        (0, _, _) => "Mostra alla videocamera una faccia alla volta, dentro la griglia, in qualsiasi verso",
        _ => $"Facce acquisite: {ScannedCount} di {CubeState.FaceCount}. Mostra la prossima faccia",
    };

    public void Dispose()
    {
        IsActive = false;
        _camera?.Dispose();
    }

    partial void OnIsAutoCaptureChanged(bool value) => _stability.Reset();

    partial void OnIsTrackingChanged(bool value)
    {
        _stability.Reset();
        Rerender();
    }

    [RelayCommand]
    private void ToggleFreeze() => IsFrozen = !IsFrozen;

    [RelayCommand]
    private void ToggleAutoCapture() => IsAutoCapture = !IsAutoCapture;

    [RelayCommand]
    private void ToggleTracking() => IsTracking = !IsTracking;

    [RelayCommand]
    private void Restart()
    {
        _session.Reset();
        IsFrozen = false;
        AfterSamplesChanged(AllFaces);
    }

    [RelayCommand]
    private void Capture() => CaptureFace(overwrite: true);

    private bool CaptureFace(bool overwrite)
    {
        if (!_hasLiveSamples || _session.ChooseSlot(_liveSamples, overwrite) is not { } face)
        {
            return false;
        }

        _session.Store(face, _liveSamples);
        _stability.Ignore(Cells.Select(cell => cell.Color).ToArray());
        IsFrozen = false;
        AfterSamplesChanged([face]);
        ShowCaptured(face);
        return true;
    }

    private void ClearFace(CubeFace face)
    {
        _session.Clear(face);
        AfterSamplesChanged([face]);
    }

    private void AfterSamplesChanged(IReadOnlyList<CubeFace> changed)
    {
        foreach (var step in Steps)
        {
            step.IsDone = _session.IsScanned(step.Face);
        }

        ScannedCount = _session.ScannedCount;
        IsAssembled = null;
        IsAssembling = false;
        IsMirrored = false;
        DoubtCount = 0;
        _assemblyVersion++;

        var reading = _session.Read();
        _scanChanged?.Invoke(reading with { Doubtful = NoDoubts }, changed);

        if (_session.IsComplete)
        {
            _ = AssembleAsync(reading);
        }
    }

    private async Task AssembleAsync(ScanReading reading)
    {
        var version = _assemblyVersion;
        IsAssembling = true;
        var assembled = await Task.Run(() => FaceAssembler.Assemble(reading.State));
        if (version != _assemblyVersion)
        {
            return;
        }

        IsAssembling = false;
        DoubtCount = assembled is null ? reading.Doubtful.Count : 0;
        IsMirrored = assembled is { IsMirrored: true };
        IsAssembled = assembled is not null;
        _scanChanged?.Invoke(assembled is null ? reading : new ScanReading(assembled.State, NoDoubts), AllFaces);

        if (assembled is { IsMirrored: false } && IsActive)
        {
            _completed?.Invoke();
        }
    }

    private void ShowCaptured(CubeFace face)
    {
        var version = ++_captureFlash;
        LastCaptured = $"Faccia {FaceAdjective(StickerClassifier.SlotColor(face))} acquisita";
        IsJustCaptured = true;
        DispatcherTimer.RunOnce(() =>
        {
            if (version == _captureFlash)
            {
                IsJustCaptured = false;
            }
        }, CaptureFlashDuration);
    }

    private static string FaceAdjective(CubeColor color) => color switch
    {
        CubeColor.White => "bianca",
        CubeColor.Yellow => "gialla",
        CubeColor.Orange => "arancione",
        CubeColor.Red => "rossa",
        CubeColor.Green => "verde",
        CubeColor.Blue => "blu",
        _ => string.Empty,
    };

    private static string ColorName(CubeColor color) => color switch
    {
        CubeColor.White => "Bianco",
        CubeColor.Yellow => "Giallo",
        CubeColor.Orange => "Arancione",
        CubeColor.Red => "Rosso",
        CubeColor.Green => "Verde",
        CubeColor.Blue => "Blu",
        _ => string.Empty,
    };
}
