using FunicularSwitch.Generators;

namespace EventSourcing.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record FunctionalResult<TError> where TError : notnull
{
    public sealed record Ok_(string ResultMessage) : FunctionalResult<TError>;

    public sealed record Failed_(TError Error) : FunctionalResult<TError>;

    public override string ToString() =>
        $"{GetType().Name.TrimEnd('_')}: {this.Match(ok: ok => ok.ResultMessage, failed: failed => failed.Error.ToString()!)}";
}