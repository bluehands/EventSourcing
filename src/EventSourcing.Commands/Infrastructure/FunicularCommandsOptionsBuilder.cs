using System.Collections.Immutable;
using EventSourcing.Infrastructure;
using System.Reflection;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;
using EventSourcing.Funicular.Commands.SerializablePayloads;

namespace EventSourcing.Funicular.Commands.Infrastructure;

public class FunicularCommandsOptionsBuilder<TFailure, TFailurePayload>(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<FunicularCommandsOptionsBuilder<TFailure, TFailurePayload>, FunicularCommandsOptionsExtension<TFailure, TFailurePayload>>(optionsBuilder)
    where TFailure : IFailure<TFailure>
    where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload>
{
    public FunicularCommandsOptionsBuilder<TFailure, TFailurePayload> CommandProcessorAssemblies(Assembly assembly, params Assembly[] assemblies) =>
        WithOption(e => e with
        {
            CommandProcessorAssemblies = ImmutableList.Create<Assembly>().Add(assembly).AddRange(assemblies)
        });
}
