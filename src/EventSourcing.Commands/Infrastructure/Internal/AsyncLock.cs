﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventSourcing.Commands.Infrastructure.Internal;

class AsyncLock : SemaphoreSlim
{
    public AsyncLock() : base(1, 1)
    {
    }
}

static class SemaphoreSlimExtensions
{
    public static async Task<T> ExecuteGuarded<T>(this SemaphoreSlim semaphore, Func<Task<T>> f)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            return await f().ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task<T> ExecuteGuarded<T>(this SemaphoreSlim semaphore, Func<T> f)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            return f();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task ExecuteGuarded(this SemaphoreSlim semaphore, Action action)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            action();
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static async Task ExecuteGuarded(this SemaphoreSlim semaphore, Func<Task> f)
    {
        await semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            await f().ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
        }
    }
}