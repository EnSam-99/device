using System.IO;
using System.IO.Ports;
using Microsoft.Extensions.Options;

namespace LiveCasino.TableController.Services;

public class SerialScannerService : BackgroundService
{
    private readonly ILogger<SerialScannerService> _logger;
    private readonly SerialPortOptions _options;
    private readonly IScanDispatcher _dispatcher;
    private readonly ScannerStatus _status;

    public SerialScannerService(
        ILogger<SerialScannerService> logger,
        IOptions<SerialPortOptions> options,
        IScanDispatcher dispatcher,
        ScannerStatus status)
    {
        _logger = logger;
        _options = options.Value;
        _dispatcher = dispatcher;
        _status = status;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Serial port reader disabled by configuration.");
            _status.SetHardwareAvailable(false);
            _status.SetFatalError("Serial port reader disabled by configuration.");
            return;
        }

        SerialPort? port = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                port ??= CreatePort();

                if (!port.IsOpen)
                {
                    port.Open();
                    _status.SetHardwareAvailable(true);
                    _status.SetFatalError(null);
                    _logger.LogInformation("Serial port {Port} opened for scan reading.", _options.PortName);
                }

                try
                {
                    var line = port.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        await Task.Delay(_options.PollDelayMs, stoppingToken);
                        continue;
                    }

                    var payload = line.Trim();
                    _logger.LogInformation("Received scan data from serial: {Payload}", payload);
                    await _dispatcher.DispatchAsync(payload, "serial", stoppingToken);
                }
                catch (TimeoutException)
                {
                    // Expected when no data arrives during ReadTimeout.
                }
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is InvalidOperationException ||
                ex is ArgumentException ||
                ex is PlatformNotSupportedException)
            {
                _status.SetHardwareAvailable(false);
                _status.SetFatalError($"Serial port unavailable: {ex.Message}");
                _logger.LogError(ex, "Serial port read failed. Will retry after backoff.");
                port?.Dispose();
                port = null;
                await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown.
            }
        }

        port?.Dispose();
    }

    private SerialPort CreatePort()
    {
        return new SerialPort(_options.PortName, _options.BaudRate, _options.Parity, _options.DataBits, _options.StopBits)
        {
            Handshake = _options.Handshake,
            ReadTimeout = _options.ReadTimeoutMs,
            NewLine = "\n"
        };
    }
}
