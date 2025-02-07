#nullable enable

namespace EventSourcing.Commands.Templates
{
    public static partial class CommandBusExtensions
    {
        public static global::System.Threading.Tasks.Task<ResultTypeName<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Commands.ICommandBus commandBus,
            global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> eventStream)
            => commandBus.SendCommandAndWaitUntilApplied(command, global::System.Reactive.Linq.Observable.OfType<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<ErrorTypeName>>>(eventStream));

        public static async global::System.Threading.Tasks.Task<ResultTypeName<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Commands.ICommandBus commandBus,
            global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<ErrorTypeName>>> commandProcessedEvents)
            => (await commandBus.SendAndWaitForProcessedEvent(command, commandProcessedEvents)).Payload
                .ToResult();

        public static ResultTypeName<global::System.Reactive.Unit> ToResult(this global::EventSourcing.Commands.CommandProcessed<ErrorTypeName> commandProcessed) => 
            global::EventSourcing.Commands.Extensions.CommandProcessedExtensions.ToResult<ResultTypeName<global::System.Reactive.Unit>, ErrorTypeName>(commandProcessed);
    }
}
