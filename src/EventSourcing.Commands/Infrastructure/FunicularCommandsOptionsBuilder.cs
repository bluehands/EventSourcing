using System.Collections.Immutable;
using EventSourcing.Infrastructure;
using System.Reflection;
using EventSourcing.Funicular.Commands.Infrastructure.Internal;

namespace EventSourcing.Funicular.Commands.Infrastructure;

public class FunicularCommandsOptionsBuilder(EventSourcingOptionsBuilder optionsBuilder)
    : EventSourcingOptionsExtensionBuilder<FunicularCommandsOptionsBuilder>(optionsBuilder)
{
    public FunicularCommandsOptionsBuilder PayloadAssemblies(Assembly assembly, params Assembly[] assemblies) =>
        WithOption<FunicularCommandsOptionsExtension>(e => e with
        {
            CommandProcessorAssemblies = ImmutableList.Create<Assembly>().Add(assembly).AddRange(assemblies)
        });
}
