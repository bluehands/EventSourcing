using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EventSourcing.Persistence.EntityFramework.SqlServer.Infrastructure.Internal;

partial class ExclusiveWriteInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ManipulateCommand(command);

        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ManipulateCommand(command);
        return new(result);
    }

    static void ManipulateCommand(DbCommand command)
    {
        var commandText = command.CommandText;
        command.CommandText = EventInsert().Replace(commandText, "${tableAlias} WITH (TABLOCKX)");
    }

    [GeneratedRegex(@"(?<tableAlias>INSERT INTO \[(.*\.)?Events\](?! WITH \(.*LOCK.*\)))", RegexOptions.IgnoreCase | RegexOptions.Multiline, "de-DE")]
    private static partial Regex EventInsert();
}