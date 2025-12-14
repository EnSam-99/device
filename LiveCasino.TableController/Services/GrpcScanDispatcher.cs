using Grpc.Core;
using LiveCasino.TableController.Protos;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace LiveCasino.TableController.Services;

public class GrpcScanDispatcher : IScanDispatcher
{
    private readonly ScannerBackend.ScannerBackendClient _client;
    private readonly BackendClientOptions _options;
    private readonly ILogger<GrpcScanDispatcher> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public GrpcScanDispatcher(
        ScannerBackend.ScannerBackendClient client,
        IOptions<BackendClientOptions> options,
        ILogger<GrpcScanDispatcher> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<RpcException>()
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(_options.RetryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * _options.BaseRetryDelaySeconds),
                (exception, delay, attempt, _) =>
                {
                    _logger.LogWarning(exception, "Retrying gRPC send attempt {Attempt} in {Delay}s", attempt, delay.TotalSeconds);
                });
    }

    public Task<DispatchResult> DispatchAsync(string payload, string source, CancellationToken cancellationToken = default)
    {
        return _retryPolicy.ExecuteAsync(async token =>
        {
            var request = new ScanRequest
            {
                TableId = _options.TableId,
                Payload = payload,
                Source = source,
                CapturedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var response = await _client.SubmitScanAsync(request, cancellationToken: token);
            _logger.LogInformation("Sent scan payload from {Source} with result: {Accepted} - {Message}", source, response.Accepted, response.Message);
            return new DispatchResult(response.Accepted, response.Message);
        }, cancellationToken);
    }
}
