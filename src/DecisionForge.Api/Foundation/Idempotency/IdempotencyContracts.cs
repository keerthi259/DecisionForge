namespace DecisionForge.Api.Foundation.Idempotency;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RequireIdempotencyAttribute : Attribute;

public sealed record ApiIdempotencyRequest(
    string Scope,
    string Key,
    string Fingerprint);

public sealed record ApiIdempotencyResponse(
    int StatusCode,
    string? ContentType,
    ReadOnlyMemory<byte> Body,
    IReadOnlyDictionary<string, string> Headers);

public enum ApiIdempotencyBeginStatus
{
    Acquired = 1,
    Replay = 2,
    Conflict = 3,
    InProgress = 4,
}

public sealed record ApiIdempotencyBeginResult(
    ApiIdempotencyBeginStatus Status,
    ApiIdempotencyResponse? Response = null);

public interface IApiIdempotencyStore
{
    Task<ApiIdempotencyBeginResult> BeginAsync(
        ApiIdempotencyRequest request,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        ApiIdempotencyRequest request,
        ApiIdempotencyResponse response,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        ApiIdempotencyRequest request,
        CancellationToken cancellationToken);
}
