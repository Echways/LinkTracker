using System.Diagnostics;
using LinkTracker.Scrapper.Infrastructure.Outbox.Abstractions;
using LinkTracker.Scrapper.Infrastructure.Outbox.Models;
using LinkTracker.Scrapper.Infrastructure.Outbox.Sql;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using LinkTracker.Shared.Contracts.Bot;
using Npgsql;

namespace LinkTracker.Scrapper.Infrastructure.Outbox;

internal sealed class PostgresOutboxStore(
    NpgsqlDataSource dataSource,
    IOutboxMessageSerializer serializer,
    ScrapperMetrics metrics) : IOutboxStore
{
    public Task AddAsync(LinkUpdate update, CancellationToken ct)
    {
        return MeasureAsync("add", async () =>
        {
            var payload = serializer.Serialize(update);

            await using var command = dataSource.CreateCommand(OutboxMessageCommands.Add);
            command.Parameters.AddWithValue("payload", payload);

            await command.ExecuteNonQueryAsync(ct);
        });
    }

    public Task AddRangeAndSetCursorAsync(
        long linkId,
        DateTimeOffset? lastUpdatedAt,
        string? lastEventKey,
        IReadOnlyCollection<LinkUpdate> updates,
        CancellationToken ct)
    {
        return MeasureAsync("add_range_and_set_cursor", async () =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);

            try
            {
                foreach (var update in updates)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = OutboxMessageCommands.Add;
                    command.Transaction = transaction;
                    command.Parameters.AddWithValue("payload", serializer.Serialize(update));

                    await command.ExecuteNonQueryAsync(ct);
                }

                if (lastUpdatedAt is not null)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = OutboxMessageCommands.SetCursor;
                    command.Transaction = transaction;
                    command.Parameters.AddWithValue("linkId", linkId);
                    command.Parameters.AddWithValue("lastUpdatedAt", lastUpdatedAt.Value);
                    command.Parameters.AddWithValue("lastEventKey", (object?)lastEventKey ?? DBNull.Value);

                    var affectedRows = await command.ExecuteNonQueryAsync(ct);

                    if (affectedRows == 0)
                    {
                        throw new InvalidOperationException($"Link with id '{linkId}' was not found.");
                    }
                }

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public Task<IReadOnlyList<OutboxMessage>> ClaimUnprocessedBatchAsync(
        int batchSize,
        int maxRetryCount,
        TimeSpan lockDuration,
        CancellationToken ct)
    {
        return MeasureAsync("claim_unprocessed_batch", async () =>
        {
            await using var command = dataSource.CreateCommand(OutboxMessageCommands.ClaimUnprocessedBatch);
            command.Parameters.AddWithValue("batchSize", batchSize);
            command.Parameters.AddWithValue("maxRetryCount", maxRetryCount);
            command.Parameters.AddWithValue("lockSeconds", lockDuration.TotalSeconds);

            await using var reader = await command.ExecuteReaderAsync(ct);

            var result = new List<OutboxMessage>();

            while (await reader.ReadAsync(ct))
            {
                var payload = serializer.Deserialize(reader.GetString(1));

                if (payload is null)
                {
                    continue;
                }

                result.Add(new OutboxMessage
                {
                    Id = reader.GetInt64(0),
                    Payload = payload,
                    CreatedAt = reader.GetFieldValue<DateTimeOffset>(2),
                    ProcessedAt = await reader.IsDBNullAsync(3, ct)
                        ? null
                        : reader.GetFieldValue<DateTimeOffset>(3),
                    Error = await reader.IsDBNullAsync(4, ct)
                        ? null
                        : reader.GetString(4),
                    RetryCount = reader.GetInt32(5)
                });
            }

            return (IReadOnlyList<OutboxMessage>)result
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToArray();
        });
    }

    public Task MarkProcessedAsync(long id, CancellationToken ct)
    {
        return MeasureAsync("mark_processed", async () =>
        {
            await using var command = dataSource.CreateCommand(OutboxMessageCommands.MarkProcessed);
            command.Parameters.AddWithValue("id", id);

            await command.ExecuteNonQueryAsync(ct);
        });
    }

    public Task MarkFailedAsync(long id, string error, CancellationToken ct)
    {
        return MeasureAsync("mark_failed", async () =>
        {
            await using var command = dataSource.CreateCommand(OutboxMessageCommands.MarkFailed);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("error", error);

            await command.ExecuteNonQueryAsync(ct);
        });
    }

    private async Task MeasureAsync(string operation, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await action();
        }
        catch
        {
            RecordError(operation);
            throw;
        }
        finally
        {
            sw.Stop();
            RecordCompleted(operation, sw.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            return await action();
        }
        catch
        {
            RecordError(operation);
            throw;
        }
        finally
        {
            sw.Stop();
            RecordCompleted(operation, sw.Elapsed.TotalMilliseconds);
        }
    }

    private void RecordCompleted(string operation, double elapsedMs)
    {
        metrics.DbQueries.Add(
            1,
            new KeyValuePair<string, object?>("operation", operation));

        metrics.DbQueryDuration.Record(
            elapsedMs,
            new KeyValuePair<string, object?>("operation", operation));
    }

    private void RecordError(string operation)
    {
        metrics.DbErrors.Add(
            1,
            new KeyValuePair<string, object?>("operation", operation));

        metrics.Errors.Add(
            1,
            new KeyValuePair<string, object?>("scope", "database"),
            new KeyValuePair<string, object?>("scope_type", operation),
            new KeyValuePair<string, object?>("reason", "db_exception"));
    }
}