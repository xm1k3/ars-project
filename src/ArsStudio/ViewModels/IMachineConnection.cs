using System;
using System.Collections.Generic;

namespace ArsStudio.ViewModels;

public interface IMachineConnection : IDisposable
{
    bool IsOpen { get; }

    IReadOnlyList<string> GetPortNames();

    void Open(string portName);

    void Close();

    void Send(string data);
}
