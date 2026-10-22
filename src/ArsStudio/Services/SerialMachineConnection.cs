using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using ArsStudio.ViewModels;

namespace ArsStudio.Services;

public sealed class SerialMachineConnection : IMachineConnection
{
    private const int BaudRate = 9600;

    private SerialPort? _port;

    public bool IsOpen => _port?.IsOpen == true;

    public IReadOnlyList<string> GetPortNames() =>
        SerialPort.GetPortNames().Distinct().Order().ToArray();

    public void Open(string portName)
    {
        Close();

        var port = new SerialPort
        {
            PortName = portName,
            BaudRate = BaudRate,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            Encoding = Encoding.ASCII,
        };

        port.Open();
        _port = port;
    }

    public void Close()
    {
        _port?.Dispose();
        _port = null;
    }

    public void Send(string data) => _port?.Write(data);

    public void Dispose() => Close();
}
