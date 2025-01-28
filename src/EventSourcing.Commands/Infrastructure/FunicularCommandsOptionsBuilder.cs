using System.Collections.Immutable;
using System.Reactive;
using EventSourcing.Infrastructure;
using System.Reflection;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;
using EventSourcing.Funicular.Commands.JsonPayload;

namespace EventSourcing.Funicular.Commands.Infrastructure;

public class FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TOperationResult>(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TOperationResult>, FunicularCommandsOptionsExtension<TFailure, TFailurePayload, TOperationResult>>(optionsBuilder)
    where TFailure : IFailure<TFailure>
    where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload>
    where TOperationResult : IResult<Unit, TFailure, TOperationResult>
{
    public FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TOperationResult> CommandProcessorAssemblies(Assembly assembly, params Assembly[] assemblies) =>
        WithOption(e => e with
        {
            CommandProcessorAssemblies = ImmutableList.Create<Assembly>().Add(assembly).AddRange(assemblies)
        });
}
