using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record FunctionalResult()
{
    public sealed record Ok_(string ResultMessage) : FunctionalResult();
    public sealed record Failed_(Failure Failure) : FunctionalResult();

    public string Message => this.Match(ok => ok.ResultMessage, failed => failed.Failure.Message);
}