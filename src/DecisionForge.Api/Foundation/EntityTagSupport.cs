using DecisionForge.Domain.ValueObjects;
using Microsoft.Extensions.Primitives;

namespace DecisionForge.Api.Foundation;

public static class EntityTagSupport
{
    public static string Format(ConcurrencyToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return $"\"{token.Value:N}\"";
    }

    public static void Set(HttpResponse response, ConcurrencyToken token)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Headers.ETag = Format(token);
    }

    public static ConcurrencyToken ParseRequired(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        StringValues values = request.Headers.IfMatch;
        if (values.Count == 0)
        {
            throw new ApiPreconditionException(
                StatusCodes.Status428PreconditionRequired,
                ApiErrorCodes.PreconditionRequired,
                "An If-Match header is required.");
        }

        if (values.Count != 1)
        {
            throw InvalidHeader();
        }

        string value = values[0]!;
        if (value.Length != 34
            || value[0] != '"'
            || value[^1] != '"'
            || !Guid.TryParseExact(value[1..^1], "N", out Guid token)
            || token == Guid.Empty)
        {
            throw InvalidHeader();
        }

        return ConcurrencyToken.Create(token);
    }

    private static ApiPreconditionException InvalidHeader()
    {
        return new ApiPreconditionException(
            StatusCodes.Status400BadRequest,
            ApiErrorCodes.ValidationField,
            "The If-Match header must contain one strong DecisionForge ETag.");
    }
}
