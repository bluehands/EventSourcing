using System.Collections.Immutable;
using System.Reflection;
using EventSourcing.Commands.Infrastructure.Internal;
using EventSourcing.Commands.SerializablePayloads;
using EventSourcing.Infrastructure;

namespace EventSourcing.Commands.Infrastructure;

public class FunicularCommandsOptionsBuilder<TFailure, TFailurePayload>(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<FunicularCommandsOptionsBuilder<TFailure, TFailurePayload>, FunicularCommandsOptionsExtension<TFailure, TFailurePayload>>(optionsBuilder)
    where TFailurePayload : class, IFailurePayload<TFailure, TFailurePayload> where TFailure : notnull
{
    public FunicularCommandsOptionsBuilder<TFailure, TFailurePayload> CommandProcessorAssemblies(Assembly assembly, params Assembly[] assemblies) =>
        WithOption(e => e with
        {
            CommandProcessorAssemblies = ImmutableList.Create<Assembly>().Add(assembly).AddRange(assemblies)
        });
}
