using System;

namespace EventSourcing.Funicular.Commands;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class CommandExtensionsAttribute<TResult, TFailure> : Attribute;