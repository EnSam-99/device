using System.IO.Ports;

namespace LiveCasino.TableController.Services;

public class SerialPortOptions
{
    public bool Enabled { get; set; } = true;
    public string PortName { get; set; } = "/dev/ttyUSB0";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Handshake Handshake { get; set; } = Handshake.None;
    public int ReadTimeoutMs { get; set; } = 500;
    public int PollDelayMs { get; set; } = 200;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public bool AllowMockWhenUnavailable { get; set; } = true;
}
