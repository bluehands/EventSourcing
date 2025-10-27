using System;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Commands.Infrastructure.Internal;

public delegate CommandProcessor<TError>? GetCommandProcessor<TError>(Type commandType, IServiceScope serviceScope) where TError : notnull;
