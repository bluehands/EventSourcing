#nullable enable

namespace Meetup
{
    public static partial class CommandExtensions
    {
        public static global::System.Threading.Tasks.Task<global::Meetup.Result<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Commands.ICommandBus commandBus,
            global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event> eventStream)
            => commandBus.SendCommandAndWaitUntilApplied(command, global::System.Reactive.Linq.Observable.OfType<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Meetup.Error>>>(eventStream));

        public static async global::System.Threading.Tasks.Task<global::Meetup.Result<global::System.Reactive.Unit>> SendCommandAndWaitUntilApplied(this global::EventSourcing.Commands.ICommandBus commandBus,
            global::EventSourcing.Commands.Command command, global::System.IObservable<global::EventSourcing.Event<global::EventSourcing.Commands.CommandProcessed<global::Meetup.Error>>> commandProcessedEvents)
            => (await commandBus.SendAndWaitForProcessedEvent(command, commandProcessedEvents)).Payload
                .ToResult();

        public static global::Meetup.Result<global::System.Reactive.Unit> ToResult(this global::EventSourcing.Commands.CommandProcessed<global::Meetup.Error> commandProcessed) => 
            global::EventSourcing.Commands.Extensions.CommandProcessedExtensions.ToResult<global::Meetup.Result<global::System.Reactive.Unit>, global::Meetup.Error>(commandProcessed);
    }
}
namespace Meetup
{
   public partial class Result<T> : EventSourcing.Commands.IResult<T, global::Meetup.Error, global::Meetup.Result<T>>{};
}