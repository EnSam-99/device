using System.Threading;

namespace LiveCasino.TableController.Services;

public class ScannerStatus
{
    private int _hardwareAvailable = 0;
    private string? _fatalError;

    public bool HardwareAvailable
    {
        get => Interlocked.CompareExchange(ref _hardwareAvailable, 0, 0) == 1;
        private set => Interlocked.Exchange(ref _hardwareAvailable, value ? 1 : 0);
    }

    public string? FatalError => Interlocked.CompareExchange(ref _fatalError, null, null);

    public void SetHardwareAvailable(bool available) => HardwareAvailable = available;

    public void SetFatalError(string? error)
    {
        Interlocked.Exchange(ref _fatalError, error);
    }
}
