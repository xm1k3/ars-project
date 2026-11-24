using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArsStudio.Controls;
using ArsStudio.Core;
using ArsStudio.Core.Scanning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArsStudio.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan JumpDuration = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan SingleMoveDuration = TimeSpan.FromMilliseconds(350);

    private readonly IDialogService? _dialogs;
    private readonly IMachineConnection? _machine;
    private IReadOnlyList<CubeState> _timeline = [];
    private IReadOnlyList<CubeMove> _moves = [];
    private CubeState? _initialState;
    private int _solveVersion;
    private CancellationTokenSource? _playback;
    private TimeSpan _stepDuration;
    private bool _isPlaybackStep;

    public MainViewModel()
        : this(null, null, null)
    {
    }

    public MainViewModel(IDialogService? dialogs, IMachineConnection? machine, ICameraService? camera)
    {
        _dialogs = dialogs;
        _machine = machine;

        Palette = Enum.GetValues<CubeColor>()
            .Select(color => new PaletteItemViewModel(color, SelectColor))
            .ToArray();

        Up = CreateFace();
        Left = CreateFace();
        Front = CreateFace();
        Right = CreateFace();
        Back = CreateFace();
        Down = CreateFace();
        Faces = [Up, Left, Front, Right, Back, Down];
        Machine.SolveRequested = SolveWithoutPlaybackAsync;
        Scan = new ScanViewModel(camera, ApplyScan, ShowMessageAsync, ShowSolve);

        RefreshSerialPorts();
    }

    public FaceViewModel Up { get; }
    public FaceViewModel Left { get; }
    public FaceViewModel Front { get; }
    public FaceViewModel Right { get; }
    public FaceViewModel Back { get; }
    public FaceViewModel Down { get; }

    public IReadOnlyList<FaceViewModel> Faces { get; }

    public IReadOnlyList<PaletteItemViewModel> Palette { get; }

    [ObservableProperty]
    public partial IReadOnlyList<string> SerialPorts { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TightenGrippersCommand), nameof(SendSolutionCommand))]
    public partial bool IsMachineConnected { get; set; }

    [ObservableProperty]
    public partial CubeColor SelectedColor { get; set; }

    [ObservableProperty]
    public partial string? SelectedSerialPort { get; set; }

    public MachineViewModel Machine { get; } = new();

    public ScanViewModel Scan { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSolveTab), nameof(IsScanTab), nameof(IsMachineTab))]
    public partial MainTab SelectedTab { get; set; }

    public bool IsSolveTab => SelectedTab == MainTab.Solve;

    public bool IsScanTab => SelectedTab == MainTab.Scan;

    public bool IsMachineTab => SelectedTab == MainTab.Machine;

    [ObservableProperty]
    public partial bool Is3D { get; set; }

    [ObservableProperty]
    public partial CubeFrame Frame { get; set; } = new(CubeState.Empty);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSolution), nameof(MachineCode), nameof(SolutionCaption))]
    [NotifyCanExecuteChangedFor(nameof(SendSolutionCommand))]
    public partial CubeSolution? LastSolution { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoveCount), nameof(HasMoves), nameof(SolutionCaption), nameof(StepText))]
    public partial IReadOnlyList<MoveChipViewModel> MoveChips { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepText))]
    public partial int CurrentStep { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackSecondsText))]
    public partial double PlaybackSeconds { get; set; } = 1.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SolveMethodText), nameof(SolveMethodHint), nameof(IsOptimized), nameof(IsKociemba), nameof(IsLayers))]
    public partial SolveMethod SolveMethod { get; set; } = SolveMethod.Optimized;

    public string SolveMethodText => SolveMethod switch
    {
        SolveMethod.Kociemba => "Kociemba",
        SolveMethod.Layers => "A strati",
        _ => "Ottimizzato",
    };

    public string PlaybackSecondsText =>
        PlaybackSeconds.ToString("0.#", CultureInfo.GetCultureInfo("it-IT")) + " s";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SolutionCaption), nameof(ShowMoveList))]
    public partial bool ShowMachineCode { get; set; }

    public bool HasSolution => LastSolution is not null;

    public bool ShowMoveList => !ShowMachineCode;

    public int MoveCount => MoveChips.Count;

    public bool HasMoves => MoveCount > 0;

    public bool IsOptimized => SolveMethod == SolveMethod.Optimized;

    public bool IsKociemba => SolveMethod == SolveMethod.Kociemba;

    public bool IsLayers => SolveMethod == SolveMethod.Layers;

    public string SolveMethodHint => SolveMethod switch
    {
        SolveMethod.Kociemba => "Due fasi: la prima soluzione trovata, di solito poco più di 20 mosse",
        SolveMethod.Layers => "Il metodo dei principianti: croce, primo strato, strato centrale, ultimo strato",
        _ => "Cerca la soluzione più corta possibile, di solito 20 mosse o meno",
    };

    public string StepText => $"Mossa {Math.Min(CurrentStep, MoveCount)} di {MoveCount}";

    public string MachineCode => LastSolution?.MachineCode ?? string.Empty;

    public string SolutionCaption => LastSolution switch
    {
        null => string.Empty,
        { Moves: "" } => "già risolto",
        _ when ShowMachineCode => $"{MachineCode.Length} comandi",
        _ => $"{MoveCount} mosse",
    };

    public CubeState CurrentState() => new(Faces.SelectMany(face => face.Colors));

    public void ApplyState(CubeState state)
    {
        ClearSolution();
        SetFaces(state);
        Frame = new CubeFrame(state);
        Machine.ShowCube(state);
    }

    public void Dispose()
    {
        StopPlayback();
        Machine.Stop();
        Scan.Dispose();
        _machine?.Dispose();
    }

    partial void OnSelectedTabChanged(MainTab value) => Scan.IsActive = value == MainTab.Scan;

    partial void OnSelectedSerialPortChanged(string? value) => _ = ConnectAsync(value);

    partial void OnCurrentStepChanged(int oldValue, int newValue)
    {
        if (!_isPlaybackStep)
        {
            StopPlayback();
        }

        ShowStep(newValue, oldValue, _isPlaybackStep ? _stepDuration * 0.9 : TimeSpan.Zero);
    }

    [RelayCommand]
    private void SelectColor(CubeColor color) => SelectedColor = color;

    [RelayCommand]
    private void ShowSolve() => SelectedTab = MainTab.Solve;

    [RelayCommand]
    private void ShowScan() => SelectedTab = MainTab.Scan;

    [RelayCommand]
    private void ShowMachine() => SelectedTab = MainTab.Machine;

    [RelayCommand]
    private void Show2D() => Is3D = false;

    [RelayCommand]
    private void Show3D() => Is3D = true;

    [RelayCommand]
    private async Task SolveAsync()
    {
        if (await TrySolveAsync())
        {
            await PlayAsync();
        }
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (IsMachineTab)
        {
            Machine.StepBackCommand.Execute(null);
        }
        else if (IsSolveTab && CurrentStep > 0)
        {
            _ = AnimateToAsync(CurrentStep - 1, SingleMoveDuration);
        }
    }

    [RelayCommand]
    private void NextStep()
    {
        if (IsMachineTab)
        {
            Machine.StepForwardCommand.Execute(null);
        }
        else if (IsSolveTab && CurrentStep < MoveCount)
        {
            _ = AnimateToAsync(CurrentStep + 1, SingleMoveDuration);
        }
    }

    partial void OnSolveMethodChanged(SolveMethod value)
    {
        if (_initialState is not { } initial)
        {
            return;
        }

        StopPlayback();
        SetFaces(initial);
        _ = TrySolveAsync();
    }

    private Task SolveWithoutPlaybackAsync() => TrySolveAsync();

    private async Task<bool> TrySolveAsync()
    {
        var state = CurrentState();
        var version = ++_solveVersion;
        CubeSolution solution;
        try
        {
            var method = SolveMethod;
            solution = await Task.Run(() => CubeSolver.Solve(state, method));
        }
        catch (InvalidCubeException exception)
        {
            if (version == _solveVersion)
            {
                await ShowMessageAsync(exception.Message);
            }

            return false;
        }

        if (version != _solveVersion)
        {
            return false;
        }

        StopPlayback();
        _moves = CubeMoves.Split(solution.Moves).Select(CubeMove.Parse).ToArray();
        _timeline = BuildTimeline(CubeMoves.Normalize(state), _moves);
        if (_moves.Count > 0)
        {
            _initialState = _timeline[0];
        }

        ShowMachineCode = false;
        LastSolution = solution;
        Machine.Load(state, solution.MachineCode);
        MoveChips = CubeMoves.Split(solution.Moves)
            .Select((move, index) => new MoveChipViewModel(index, move, ShowMove))
            .ToArray();

        _isPlaybackStep = true;
        CurrentStep = 0;
        _isPlaybackStep = false;
        ShowStep(0, 0, TimeSpan.Zero);
        return true;
    }

    [RelayCommand]
    private async Task PlayAsync()
    {
        if (_moves.Count == 0)
        {
            return;
        }

        if (CurrentStep >= _moves.Count)
        {
            CurrentStep = 0;
        }

        await AnimateToAsync(_moves.Count, TimeSpan.FromSeconds(PlaybackSeconds) / _moves.Count);
    }

    private async Task AnimateToAsync(int target, TimeSpan stepDuration)
    {
        StopPlayback();
        var playback = new CancellationTokenSource();
        _playback = playback;
        _stepDuration = stepDuration;
        var direction = Math.Sign(target - CurrentStep);

        try
        {
            while (CurrentStep != target)
            {
                _isPlaybackStep = true;
                CurrentStep += direction;
                _isPlaybackStep = false;
                await Task.Delay(stepDuration, playback.Token);
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _isPlaybackStep = false;
        }
    }

    [RelayCommand]
    private void SetSolveMethod(SolveMethod method) => SolveMethod = method;

    [RelayCommand]
    private void SetPlaybackSeconds(string seconds) =>
        PlaybackSeconds = double.Parse(seconds, CultureInfo.InvariantCulture);

    [RelayCommand]
    private void ShowMoves() => ShowMachineCode = false;

    [RelayCommand]
    private void ShowCode() => ShowMachineCode = LastSolution is { Moves.Length: > 0 };

    [RelayCommand]
    private void ToggleSolutionFormat()
    {
        if (LastSolution is { Moves.Length: > 0 })
        {
            ShowMachineCode = !ShowMachineCode;
        }
    }

    [RelayCommand]
    private void ClearCube() => ApplyState(CubeState.Empty);

    [RelayCommand]
    private void RefreshSerialPorts()
    {
        if (_machine is null)
        {
            return;
        }

        var selected = SelectedSerialPort;
        SerialPorts = _machine.GetPortNames();
        if (selected is not null && !SerialPorts.Contains(selected))
        {
            SelectedSerialPort = null;
        }
    }

    [RelayCommand(CanExecute = nameof(IsMachineConnected))]
    private Task TightenGrippersAsync() => SendToMachineAsync(",");

    [RelayCommand(CanExecute = nameof(CanSendSolution))]
    private Task SendSolutionAsync() => SendToMachineAsync(MachineCode);

    private bool CanSendSolution() => IsMachineConnected && MachineCode.Length > 0;

    private async Task ConnectAsync(string? portName)
    {
        if (_machine is null)
        {
            return;
        }

        _machine.Close();
        IsMachineConnected = false;
        if (portName is null)
        {
            return;
        }

        try
        {
            _machine.Open(portName);
            IsMachineConnected = true;
        }
        catch (Exception exception) when (IsSerialError(exception) || exception is ArgumentException)
        {
            SelectedSerialPort = null;
            await ShowMessageAsync($"Impossibile aprire la porta {portName}.");
        }
    }

    private async Task SendToMachineAsync(string data)
    {
        try
        {
            _machine?.Send(data);
        }
        catch (Exception exception) when (IsSerialError(exception))
        {
            var portName = SelectedSerialPort;
            SelectedSerialPort = null;
            await ShowMessageAsync($"Impossibile comunicare con la porta {portName}.");
        }
    }

    private static bool IsSerialError(Exception exception) =>
        exception is IOException or InvalidOperationException or UnauthorizedAccessException or TimeoutException;

    [RelayCommand]
    private async Task SaveCubeAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        try
        {
            var name = $"{DateTime.Today:dd-MM-yyyy}.{CubeStateFile.Extension}";
            await _dialogs.SaveCubeFileAsync(name, CubeStateFile.Serialize(CurrentState()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await ShowMessageAsync("Non è possibile salvare il cubo");
        }
    }

    [RelayCommand]
    private async Task LoadCubeAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        try
        {
            var content = await _dialogs.OpenCubeFileAsync();
            if (content is not null)
            {
                ApplyState(CubeStateFile.Parse(content));
            }
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or IOException or UnauthorizedAccessException)
        {
            await ShowMessageAsync("Non è possibile caricare il cubo");
        }
    }

    private void ShowMove(int index)
    {
        var steps = Math.Abs(index + 1 - CurrentStep);
        if (steps > 0)
        {
            _ = AnimateToAsync(index + 1, steps == 1 ? SingleMoveDuration : JumpDuration / steps);
        }
    }

    private void ShowStep(int step, int previousStep, TimeSpan duration)
    {
        if (_timeline.Count == 0)
        {
            return;
        }

        step = Math.Clamp(step, 0, _timeline.Count - 1);
        var state = _timeline[step];
        SetFaces(state);
        Frame = duration > TimeSpan.Zero && Math.Abs(step - previousStep) == 1
            ? new CubeFrame(state, _timeline[previousStep], MoveBetween(previousStep, step), duration)
            : new CubeFrame(state);

        foreach (var chip in MoveChips)
        {
            chip.IsCurrent = chip.Index == step - 1;
            chip.IsUpcoming = chip.Index >= step;
        }
    }

    private CubeMove MoveBetween(int fromStep, int toStep) =>
        toStep > fromStep
            ? _moves[fromStep]
            : _moves[toStep] with { QuarterTurns = 4 - _moves[toStep].QuarterTurns };

    private void ApplyScan(ScanReading reading, IReadOnlyList<CubeFace> changed)
    {
        foreach (var face in changed)
        {
            Faces[(int)face].SetColors(reading.State.Face(face));
        }

        for (var sticker = 0; sticker < CubeState.StickerCount; sticker++)
        {
            Faces[sticker / CubeState.StickersPerFace].Stickers[sticker % CubeState.StickersPerFace].IsDoubtful =
                reading.Doubtful.Contains(sticker);
        }

        OnStickerPainted();
    }

    private void OnStickerPainted()
    {
        ClearSolution();
        Frame = new CubeFrame(CurrentState());
        Machine.ShowCube(CurrentState());
    }

    private void ClearSolution()
    {
        StopPlayback();
        _solveVersion++;
        _timeline = [];
        _moves = [];
        _initialState = null;
        MoveChips = [];
        LastSolution = null;
    }

    private void StopPlayback()
    {
        _playback?.Cancel();
        _playback = null;
    }

    private void SetFaces(CubeState state)
    {
        for (var face = 0; face < Faces.Count; face++)
        {
            Faces[face].SetColors(state.Face((CubeFace)face));
        }
    }

    private static IReadOnlyList<CubeState> BuildTimeline(CubeState start, IReadOnlyList<CubeMove> moves)
    {
        var timeline = new List<CubeState> { start };
        foreach (var move in moves)
        {
            timeline.Add(CubeMoves.Apply(timeline[^1], move));
        }

        return timeline;
    }

    private Task ShowMessageAsync(string message) => _dialogs?.ShowMessageAsync(message) ?? Task.CompletedTask;

    private FaceViewModel CreateFace() => new(() => SelectedColor, OnStickerPainted);
}
