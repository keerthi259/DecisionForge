using DecisionForge.Api.Foundation;
using Microsoft.AspNetCore.Antiforgery;

namespace DecisionForge.Api.Identity;

public sealed class AntiforgeryEndpointFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        IAntiforgeryValidationFeature? validation = context.HttpContext.Features
            .Get<IAntiforgeryValidationFeature>();
        if (validation is not null)
        {
            return validation.IsValid
                ? await next(context)
                : InvalidToken(context.HttpContext);
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext)
                .WaitAsync(context.HttpContext.RequestAborted);
        }
        catch (AntiforgeryValidationException)
        {
            return InvalidToken(context.HttpContext);
        }

        return await next(context);
    }

    private static IResult InvalidToken(HttpContext context)
    {
        return ApiProblemWriter.Result(
            context,
            StatusCodes.Status400BadRequest,
            "The antiforgery token is invalid or missing.",
            "authentication.antiforgery-invalid");
    }
}
