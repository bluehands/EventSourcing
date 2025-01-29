using System.Collections.Immutable;
using System.Reactive;
using EventSourcing.Infrastructure;
using System.Reflection;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;
using EventSourcing.Funicular.Commands.JsonPayload;

namespace EventSourcing.Funicular.Commands.Infrastructure;

public class FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TResult>(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TResult>, FunicularCommandsOptionsExtension<TFailure, TFailurePayload, TResult>>(optionsBuilder)
    where TFailure : IFailure<TFailure>
    where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload>
    where TResult : IResult<Unit, TFailure, TResult>
{
    public FunicularCommandsOptionsBuilder<TFailure, TFailurePayload, TResult> CommandProcessorAssemblies(Assembly assembly, params Assembly[] assemblies) =>
        WithOption(e => e with
        {
            CommandProcessorAssemblies = ImmutableList.Create<Assembly>().Add(assembly).AddRange(assemblies)
        });
}
