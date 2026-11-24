using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArsStudio.Core.Scanning;

namespace ArsStudio.ViewModels;

public interface ICameraService : IDisposable
{
    IReadOnlyList<string> GetCameraNames();

    Task StartAsync(string name, Action<FrameImage> frameArrived);

    Task StopAsync();
}
