using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArsStudio.Controls;
using ArsStudio.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ArsStudio.ViewModels;

public partial class MachineViewModel : ViewModelBase
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");

    private readonly MachineTimings _timings = MachineTimings.Default;
    private IReadOnlyList<MachineStep> _steps = [];
    private IReadOnlyList<TimeSpan> _elapsedAtStep = [TimeSpan.Zero];
    private MachineState _initial = MachineSimulator.Initial(CubeState.Empty);
    private CancellationTokenSource? _playback;
    private bool _isPlaybackStep;

    public IReadOnlyList<double> SpeedOptions { get; } = [0.5, 1, 2, 5, 10];

    [ObservableProperty]
    public partial MachineFrame Frame { get; set; } = new(MachineSimulator.Initial(CubeState.Empty));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSteps), nameof(TimeText), nameof(StepCaption))]
    public partial int StepCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimeText), nameof(StepCaption))]
    public partial int CurrentStep { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedText))]
    public partial double Speed { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    public bool HasSteps => StepCount > 0;

    public string SpeedText => Speed.ToString("0.#", Italian) + "×";

    public string TimeText => $"{Format(_elapsedAtStep[Math.Min(CurrentStep, _elapsedAtStep.Count - 1)])} / {Format(_elapsedAtStep[^1])}";

    public string StepCaption => $"Passaggio {CurrentStep} di {StepCount}";

    public Func<Task>? SolveRequested { get; set; }

    public void Load(CubeState start, string machineCode)
    {
        StopPlayback();
        _initial = MachineSimulator.Initial(start);
        _steps = MachineSimulator.Simulate(start, machineCode);
        _elapsedAtStep = ElapsedTimes(_steps);
        StepCount = _steps.Count;
        SetStepSilently(0);
    }

    public void ShowCube(CubeState cube)
    {
        StopPlayback();
        _initial = MachineSimulator.Initial(cube);
        _steps = [];
        _elapsedAtStep = [TimeSpan.Zero];
        StepCount = 0;
        SetStepSilently(0);
    }

    public void Stop() => StopPlayback();

    partial void OnCurrentStepChanged(int oldValue, int newValue)
    {
        if (!_isPlaybackStep)
        {
            StopPlayback();
        }

        ShowStep(newValue, animate: _isPlaybackStep && newValue == oldValue + 1);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task TogglePlayAsync()
    {
        if (IsPlaying)
        {
            StopPlayback();
            return;
        }

        if (!HasSteps && SolveRequested is not null)
        {
            await SolveRequested();
        }

        if (!HasSteps)
        {
            return;
        }

        if (CurrentStep >= StepCount)
        {
            CurrentStep = 0;
        }

        var playback = new CancellationTokenSource();
        _playback = playback;
        IsPlaying = true;

        try
        {
            while (CurrentStep < StepCount)
            {
                var duration = ScaledDuration(_steps[CurrentStep]);
                _isPlaybackStep = true;
                CurrentStep++;
                _isPlaybackStep = false;
                await Task.Delay(duration, playback.Token);
            }
        }
        catch (TaskCanceledException)
        {
        }
        finally
        {
            _isPlaybackStep = false;
            if (_playback == playback)
            {
                _playback = null;
                IsPlaying = false;
            }
        }
    }

    [RelayCommand]
    private void StepForward()
    {
        if (CurrentStep >= StepCount)
        {
            return;
        }

        StopPlayback();
        _isPlaybackStep = true;
        CurrentStep++;
        _isPlaybackStep = false;
    }

    [RelayCommand]
    private void StepBack()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
        }
    }

    [RelayCommand]
    private void GoToStart() => CurrentStep = 0;

    [RelayCommand]
    private void GoToEnd() => CurrentStep = StepCount;

    [RelayCommand]
    private void SetSpeed(string speed) => Speed = double.Parse(speed, CultureInfo.InvariantCulture);

    private void ShowStep(int step, bool animate)
    {
        step = Math.Clamp(step, 0, StepCount);
        var state = step == 0 ? _initial : _steps[step - 1].After;
        Frame = animate
            ? new MachineFrame(state, _steps[step - 1], ScaledDuration(_steps[step - 1]))
            : new MachineFrame(state);
    }

    private void SetStepSilently(int step)
    {
        _isPlaybackStep = true;
        CurrentStep = step;
        _isPlaybackStep = false;
        ShowStep(step, animate: false);
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(StepCaption));
    }

    private TimeSpan ScaledDuration(MachineStep step) => _timings.DurationOf(step) / Speed;

    private void StopPlayback()
    {
        _playback?.Cancel();
        _playback = null;
        IsPlaying = false;
    }

    private IReadOnlyList<TimeSpan> ElapsedTimes(IReadOnlyList<MachineStep> steps)
    {
        var elapsed = new List<TimeSpan> { TimeSpan.Zero };
        foreach (var step in steps)
        {
            elapsed.Add(elapsed[^1] + _timings.DurationOf(step));
        }

        return elapsed;
    }

    private static string Format(TimeSpan time) =>
        $"{(int)time.TotalMinutes}:{time.Seconds:00},{time.Milliseconds / 100}";
}
