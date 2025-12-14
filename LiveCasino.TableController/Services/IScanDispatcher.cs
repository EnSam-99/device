namespace LiveCasino.TableController.Services;

public record DispatchResult(bool Accepted, string Message);

public interface IScanDispatcher
{
    Task<DispatchResult> DispatchAsync(string payload, string source, CancellationToken cancellationToken = default);
}
