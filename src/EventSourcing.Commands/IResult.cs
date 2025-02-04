using System;

namespace EventSourcing.Commands;

//Optional interfaces to implement. If implemented on Result and Failure types, additional methods for command processing are generated,
//which allow using Result in command processors with ease.

public interface IResult<out TValue, out TFailure>
{
    TResult Match<TResult>(
        Func<TValue, TResult> ok,
        Func<TFailure, TResult> error);
}

public interface IResult<TValue, TFailure, out TResult>
    : IResult<TValue, TFailure>
{
    static abstract TResult Ok(TValue value);

    static abstract TResult Error(TFailure failure);
}

public interface IFailure<out TFailure>
    where TFailure : IFailure<TFailure>
{
    static abstract TFailure Internal(string message);

    static abstract TFailure Cancelled(string message);
}