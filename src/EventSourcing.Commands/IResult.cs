using System;

namespace EventSourcing.Commands;

//Optional interfaces to implement. If implemented on Result and Error types, additional methods for command processing are generated,
//which allow using Result in command processors with ease.

public interface IResult<out TValue, out TError>
{
    TResult Match<TResult>(
        Func<TValue, TResult> ok,
        Func<TError, TResult> error);
}

public interface IResult<TValue, TError, out TResult>
    : IResult<TValue, TError>
{
    static abstract TResult Ok(TValue value);

    static abstract TResult Error(TError failure);
}

public interface IError<out TError>
    where TError : IError<TError>
{
    static abstract TError Internal(string message);

    static abstract TError Cancelled(string message);
}