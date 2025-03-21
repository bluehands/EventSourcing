﻿using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using EventSourcing.Commands.Infrastructure.Internal;
using AsyncLock = EventSourcing.Commands.Infrastructure.Internal.AsyncLock;

namespace EventSourcing.Commands.Infrastructure;

public sealed class CommandBus : IObservable<Command>, IDisposable, ICommandBus
{
    readonly AsyncLock _lock = new();
    readonly Subject<Command> _innerStream;
    readonly IObservable<Command> _commands;

    public CommandBus()
    {
        _innerStream = new();
        _commands = _innerStream.Publish().RefCount();
    }

    public IDisposable Subscribe(IObserver<Command> observer) => _commands.Subscribe(observer);

    public async Task SendCommand(Command command) => await _lock.ExecuteGuarded(() => _innerStream.OnNext(command)).ConfigureAwait(false);

    public void Dispose()
    {
        _lock.Dispose();
        _innerStream.Dispose();
    }
}