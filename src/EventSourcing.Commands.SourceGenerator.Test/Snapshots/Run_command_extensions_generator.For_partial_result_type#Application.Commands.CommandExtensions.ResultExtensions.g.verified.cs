//HintName: Application.Commands.CommandExtensions.ResultExtensions.g.cs
#nullable enable

namespace Application.Commands
{
    public static partial class CommandExtensions
    {
        public static global::System.Threading.Tasks.Task<global::Application.Results.Result<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Commands.ICommandBus commandBus,
            global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> eventStream)
            => commandBus.SendCommandAndWaitUntilApplied(command, global::System.Reactive.Linq.Observable.OfType<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>>(eventStream));

        public static async global::System.Threading.Tasks.Task<global::Application.Results.Result<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Commands.ICommandBus commandBus,
            global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>> commandProcessedEvents)
            => (await commandBus.SendAndWaitForProcessedEvent(command, commandProcessedEvents)).Payload
                .ToResult();

        public static global::System.Threading.Tasks.Task<global::Application.Results.Result<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Commands.ICommandBus commandBus,
            global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> eventStream, global::System.TimeSpan timeout)
            => commandBus.SendCommandAndWaitUntilApplied(command, global::System.Reactive.Linq.Observable.OfType<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>>(eventStream), timeout);

        public static async global::System.Threading.Tasks.Task<global::Application.Results.Result<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Commands.ICommandBus commandBus,
            global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error>>> commandProcessedEvents, global::System.TimeSpan timeout)
            => (await commandBus.SendAndWaitForProcessedEvent(command, commandProcessedEvents, timeout)).Payload
                .ToResult();

        public static global::Application.Results.Result<global::System.Reactive.Unit> ToResult(this global::EventSourcing.Commands.CommandProcessed<global::Application.Errors.Error> commandProcessed) => 
            global::EventSourcing.Commands.Extensions.CommandProcessedExtensions.ToResult<global::Application.Results.Result<global::System.Reactive.Unit>, global::Application.Errors.Error>(commandProcessed);
    }
}
namespace Application.Results
{
   public partial class Result<T> : EventSourcing.Commands.IResult<T, global::Application.Errors.Error, global::Application.Results.Result<T>>{};
}