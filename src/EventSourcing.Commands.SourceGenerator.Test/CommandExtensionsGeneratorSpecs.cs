namespace EventSourcing.Commands.SourceGenerator.Test;

public class Run_command_extensions_generator : VerifySourceGenerator
{
    [Fact]
    public Task For_error_only_attribute() => Verify(
        """
        using EventSourcing.Commands;
        namespace Application.Commands;
        [CommandExtensions<string>]
        public static partial class CommandExtensions;

        public abstract class AsyncProcessor : CommandProcessor<Command>;
        public abstract class SyncProcessor : SynchronousCommandProcessor<Command>;

        public static class Consumer
        {
            public static async System.Threading.Tasks.Task Use(ICommandBus bus, Command command,
                System.IObservable<EventSourcing.Event> events,
                System.IObservable<EventSourcing.Event<CommandProcessed<string>>> processed)
            {
                await bus.SendAndWaitForProcessedEvent(command, events);
                await bus.SendAndWaitForProcessedEvent(command, processed);
                await bus.SendAndWaitForProcessedEvent(command, events, System.TimeSpan.FromSeconds(1));
                await bus.SendAndWaitForProcessedEvent(command, processed, System.TimeSpan.FromSeconds(1));
                _ = ProcessingResult.Ok("done");
                _ = ProcessingResult.Failed("error");
            }
        }
        """, 1);

    [Fact]
    public Task For_partial_result_type() => Verify(ResultFixture("Application.Commands", true), 2);

    [Fact]
    public Task For_existing_result_implementation() => Verify(ResultFixture("Application.Commands", false), 2);

    [Fact]
    public Task For_global_namespace_error_extensions() => Verify(
        """
        using EventSourcing.Commands;
        [CommandExtensions<string>]
        public static partial class CommandExtensions;

        public abstract class Processor : CommandProcessor<Command>;
        public static class Consumer
        {
            public static System.Threading.Tasks.Task Use(ICommandBus bus, Command command,
                System.IObservable<EventSourcing.Event> events) =>
                bus.SendAndWaitForProcessedEvent(command, events, System.TimeSpan.FromSeconds(1));
        }
        """, 1);

    [Fact]
    public Task For_global_namespace_extensions_with_named_result() => Verify(ResultFixture(null, true), 2);

    [Fact]
    public Task For_nested_namespace_declarations() => Verify(
        """
        namespace Application
        {
            namespace Commands
            {
                [EventSourcing.Commands.CommandExtensions<string>]
                public static partial class CommandExtensions;
            }
        }
        """, 1);

    [Fact]
    public Task For_multiple_extension_classes() => Verify(
        """
        namespace Application.First
        {
            [EventSourcing.Commands.CommandExtensions<string>]
            public static partial class CommandExtensions;
        }
        namespace Application.Second
        {
            [EventSourcing.Commands.CommandExtensions<int>]
            public static partial class CommandExtensions;
        }
        """, 2);

    [Fact]
    public Task Without_matching_attribute() => Verify(
        """
        namespace Application.Commands;
        [System.Obsolete]
        public static partial class CommandExtensions;
        """, 0);

    static string ResultFixture(string? extensionNamespace, bool partialResult)
    {
        var extensions = """
            [EventSourcing.Commands.CommandExtensions<Application.Results.Result<System.Reactive.Unit>, Application.Errors.Error>]
            public static partial class CommandExtensions;

            public static class Consumer
            {
                public static async System.Threading.Tasks.Task Use(EventSourcing.Commands.ICommandBus bus,
                    EventSourcing.Commands.Command command, System.IObservable<EventSourcing.Event> events,
                    System.IObservable<EventSourcing.Event<EventSourcing.Commands.CommandProcessed<Application.Errors.Error>>> processed)
                {
                    Application.Results.Result<System.Reactive.Unit> result = await bus.SendCommandAndWaitUntilApplied(command, events);
                    result = await bus.SendCommandAndWaitUntilApplied(command, processed);
                    result = await bus.SendCommandAndWaitUntilApplied(command, events, System.TimeSpan.FromSeconds(1));
                    result = await bus.SendCommandAndWaitUntilApplied(command, processed, System.TimeSpan.FromSeconds(1));
                    result = new EventSourcing.Commands.CommandProcessed<Application.Errors.Error>(
                        EventSourcing.Commands.CommandResult<Application.Errors.Error>.Cancelled(command.Id)).ToResult();
                }
            }
            """;
        var resultDeclaration = partialResult
            ? "public partial class Result<T>"
            : "public class Result<T> : EventSourcing.Commands.IResult<T, Application.Errors.Error, Result<T>>";
        return $$"""
            {{(extensionNamespace == null ? extensions : $"namespace {extensionNamespace} {{\n{extensions}\n}}")}}
            namespace Application.Errors
            {
                public record Error(string Message) : EventSourcing.Commands.IError<Error>
                {
                    public static Error Internal(string message) => new(message);
                    public static Error Cancelled(string message) => new(message);
                }
            }
            namespace Application.Results
            {
                {{resultDeclaration}}
                {
                    public static Result<T> Ok(T value) => new();
                    public static Result<T> Error(Application.Errors.Error failure) => new();
                    public TResult Match<TResult>(System.Func<T, TResult> ok, System.Func<Application.Errors.Error, TResult> error)
                        => error(new("fixture"));
                }
            }
            """;
    }
}
