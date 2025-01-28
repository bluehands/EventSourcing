using System;

namespace EventSourcing.Funicular.Commands;

public interface IResult<out TValue, out TFailure>
    where TFailure : IFailure<TFailure>
{
    TResult Match<TResult>(
        Func<TValue, TResult> ok,
        Func<TFailure, TResult> error);
}

public interface IResult<TValue, TFailure, out TOperationResult>
    : IResult<TValue, TFailure>
    where TFailure : IFailure<TFailure>
{
    static abstract TOperationResult Ok(TValue value);

    static abstract TOperationResult Error(TFailure failure);
}