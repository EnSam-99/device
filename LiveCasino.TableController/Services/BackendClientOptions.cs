namespace LiveCasino.TableController.Services;

public class BackendClientOptions
{
    public string Endpoint { get; set; } = "http://localhost:7100";
    public string TableId { get; set; } = "table-1";
    public int RetryCount { get; set; } = 5;
    public double BaseRetryDelaySeconds { get; set; } = 0.5;
}
