﻿using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing;

public sealed class EventStream<T> : IDisposable, IObservable<T>
{
    readonly IServiceScope _serviceScope;
    readonly IConnectableObservable<T> _stream;
    IDisposable? _connection;

    public EventStream(IObservable<T> events, IServiceScope serviceScope)
    {
        _serviceScope = serviceScope;
        _stream = events.Publish();
    }

    public void Start()
    {
        Stop();
        _connection = _stream.Connect();
    }

    public void Stop()
    {
        _connection?.Dispose();
        _connection = null;
    }

    public void Dispose()
    {
        Stop();
        _serviceScope.Dispose();
    }

    public IDisposable Subscribe(IObserver<T> observer)
        => _stream.Subscribe(observer);
}