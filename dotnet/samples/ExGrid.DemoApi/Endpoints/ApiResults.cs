using System.Globalization;

namespace ExGrid.DemoApi;

/// <summary>The answers every endpoint gives alike.</summary>
internal static class ApiResults
{
    /// <summary>
    /// <c>503</c> while the trades are not ready yet, saying how far generation has got. A page
    /// asking this early is told to wait, never handed an empty answer that looks like no trades.
    /// </summary>
    public static IResult NotReady(TradeStore store, HttpResponse response)
    {
        response.Headers.RetryAfter = "1";
        var detail = store.Generating is { } progress
            ? $"The trades are being generated: {progress.Percent.ToString(CultureInfo.InvariantCulture)}%."
            : store.State == TradeStoreState.Failed
                ? "The trades could not be made ready; the server's log says why."
                : "The trades are being made ready.";
        return Results.Problem(detail, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary><c>400</c>, naming what was wrong with the question.</summary>
    public static IResult BadRequest(string detail) =>
        Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
}
