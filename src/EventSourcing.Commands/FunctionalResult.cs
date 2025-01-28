using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record FunctionalResult<TFailure>
{
    public sealed record Ok_(string ResultMessage) : FunctionalResult<TFailure>;

    public sealed record Failed_(TFailure Failure) : FunctionalResult<TFailure>;
}