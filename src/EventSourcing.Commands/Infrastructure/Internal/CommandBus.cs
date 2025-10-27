using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace EventSourcing.Commands.Infrastructure.Internal;

public sealed class CommandBus : IObservable<ScopedCommand>, IDisposable
{
    readonly AsyncLock _lock = new();
    readonly Subject<ScopedCommand> _innerStream;
    readonly IObservable<ScopedCommand> _commands;

    public CommandBus()
    {
        _innerStream = new();
        _commands = _innerStream.Publish().RefCount();
    }

    public IDisposable Subscribe(IObserver<ScopedCommand> observer) => _commands.Subscribe(observer);

    public async Task SendCommand(ScopedCommand command) => await _lock.ExecuteGuarded(() => _innerStream.OnNext(command)).ConfigureAwait(false);

    public void Dispose()
    {
        _lock.Dispose();
        _innerStream.Dispose();
    }
}