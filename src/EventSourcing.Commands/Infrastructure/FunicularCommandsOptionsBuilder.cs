using System.Collections.Immutable;
using System.Reflection;
using EventSourcing.Commands.Infrastructure.Internal;
using EventSourcing.Commands.SerializablePayloads;
using EventSourcing.Infrastructure;

namespace EventSourcing.Commands.Infrastructure;

public class FunicularCommandsOptionsBuilder<TError, TErrorPayload>(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<FunicularCommandsOptionsBuilder<TError, TErrorPayload>, FunicularCommandsOptionsExtension<TError, TErrorPayload>>(optionsBuilder)
    where TErrorPayload : class, IErrorPayload<TError, TErrorPayload> where TError : notnull
{
    public FunicularCommandsOptionsBuilder<TError, TErrorPayload> CommandProcessorAssemblies(Assembly assembly, params Assembly[] assemblies) =>
        WithOption(e => e with
        {
            CommandProcessorAssemblies = ImmutableList.Create<Assembly>().Add(assembly).AddRange(assemblies)
        });
}
