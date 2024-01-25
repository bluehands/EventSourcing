using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record FunctionalResult()
{
    public sealed record Ok_(string Message) : FunctionalResult();
    public sealed record Failed_(Failure Failure) : FunctionalResult();

    public string GetMessage => this.Match(ok => ok.Message, failed => failed.Failure.Message);
}