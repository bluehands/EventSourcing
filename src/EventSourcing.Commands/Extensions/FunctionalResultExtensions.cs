namespace EventSourcing.Funicular.Commands.Extensions;

public static class FunctionalResultExtensions
{
    public static string GetMessage<TFailure>(this IResult<string, TFailure> result)
        where TFailure : IFailure<TFailure>
    {
        return result.Match(
            ok: ok => ok,
            error: failure => failure.Message);
    }
}