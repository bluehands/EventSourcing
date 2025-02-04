#nullable enable

namespace EventSourcing.Funicular.Commands.Templates
{
    public static partial class CommandBusExtensions
    {
        public static global::System.Threading.Tasks.Task<ResultTypeName<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Funicular.Commands.ICommandBus commandBus,
            global::EventSourcing.Funicular.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> eventStream)
            => commandBus.SendCommandAndWaitUntilApplied(command, global::System.Reactive.Linq.Observable.OfType<global::EventSourcing.Event<global::EventSourcing.Funicular.Commands.CommandProcessed<FailureTypeName>>>(eventStream));

        public static async global::System.Threading.Tasks.Task<ResultTypeName<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Funicular.Commands.ICommandBus commandBus,
            global::EventSourcing.Funicular.Commands.Command command, global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Funicular.Commands.CommandProcessed<FailureTypeName>>> commandProcessedEvents)
            => (await commandBus.SendAndWaitForProcessedEvent(command, commandProcessedEvents)).Payload
                .ToResult();

        public static ResultTypeName<global::System.Reactive.Unit> ToResult(this global::EventSourcing.Funicular.Commands.CommandProcessed<FailureTypeName> commandProcessed) => 
            global::EventSourcing.Funicular.Commands.Extensions.CommandProcessedExtensions.ToResult<ResultTypeName<global::System.Reactive.Unit>, FailureTypeName>(commandProcessed);
    }
}
