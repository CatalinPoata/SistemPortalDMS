using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Threading;

namespace API_DMS_TESTS.Infrastructure;

internal sealed class RequestSqlCommandCounter : DbCommandInterceptor
{
    internal const string HeaderName = "X-N7-Query-Count";

    private readonly IHttpContextAccessor httpContextAccessor =
        new HttpContextAccessor();

    private int selectCommandCount;

    internal string RequestId { get; } = Guid.NewGuid().ToString("N");

    internal int SelectCommandCount => Volatile.Read(ref selectCommandCount);

    internal void Reset() => Interlocked.Exchange(ref selectCommandCount, 0);

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        CountSelect(command);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        CountSelect(command);
        return base.ReaderExecutedAsync(
            command,
            eventData,
            result,
            cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        CountSelect(command);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        CountSelect(command);
        return base.ScalarExecutedAsync(
            command,
            eventData,
            result,
            cancellationToken);
    }

    private void CountSelect(DbCommand command)
    {
        var currentRequestId = httpContextAccessor.HttpContext?
            .Request.Headers[HeaderName]
            .ToString();

        if (!string.Equals(
                currentRequestId,
                RequestId,
                StringComparison.Ordinal) ||
            !command.CommandText.TrimStart().StartsWith(
                "SELECT",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Interlocked.Increment(ref selectCommandCount);
    }
}
