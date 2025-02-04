﻿using FunicularSwitch.Generators;

namespace EventSourcing.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record FunctionalResult<TFailure> where TFailure : notnull
{
    public sealed record Ok_(string ResultMessage) : FunctionalResult<TFailure>;

    public sealed record Failed_(TFailure Failure) : FunctionalResult<TFailure>;

    public override string ToString() =>
        $"{GetType().Name.TrimEnd('_')}: {this.Match(ok: ok => ok.ResultMessage, failed: failed => failed.Failure.ToString()!)}";
}