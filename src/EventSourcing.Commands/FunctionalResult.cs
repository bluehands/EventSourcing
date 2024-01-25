using FunicularSwitch.Generators;

namespace EventSourcing.Funicular.Commands;

[UnionType(CaseOrder = CaseOrder.AsDeclared)]
public abstract partial record FunctionalResult()
{
    public sealed record Ok(string Message) : FunctionalResult();
    public sealed record Failed(Failure Failure) : FunctionalResult();

    public string Message => this.Match(ok => ok.Message, failed => failed.Message);
}