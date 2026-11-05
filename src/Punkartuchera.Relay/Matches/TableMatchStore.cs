using Azure;
using Azure.Data.Tables;
using Azure.Identity;

namespace Punkartuchera.Relay.Matches;

/// <summary>
/// Partidas en Azure Table Storage: una fila por partida, con el código como clave de partición.
/// Cuesta céntimos al mes y ya hace falta una cuenta de almacenamiento para la Function App.
/// </summary>
public sealed class TableMatchStore : IMatchStore
{
    private const string RowKey = "partida";

    private readonly TableClient _table;
    private readonly Lazy<Task> _ensureTable;

    public TableMatchStore(RelayOptions options)
    {
        _table = !string.IsNullOrWhiteSpace(options.TablesConnectionString)
            ? new TableClient(options.TablesConnectionString, options.TableName)
            : new TableClient(
                new Uri(options.TablesEndpoint ?? throw new InvalidOperationException("Falta Relay:TablesEndpoint o Relay:TablesConnectionString.")),
                options.TableName,
                new DefaultAzureCredential());
        _ensureTable = new Lazy<Task>(() => _table.CreateIfNotExistsAsync());
    }

    public async Task<bool> TryAddAsync(MatchRecord match, CancellationToken cancellationToken = default)
    {
        await _ensureTable.Value;
        try
        {
            await _table.AddEntityAsync(ToEntity(match), cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return false;
        }
    }

    public async Task<MatchRecord?> GetAsync(string code, CancellationToken cancellationToken = default)
    {
        await _ensureTable.Value;
        var response = await _table.GetEntityIfExistsAsync<TableEntity>(code, RowKey, cancellationToken: cancellationToken);
        return response.HasValue && response.Value is { } entity ? FromEntity(entity) : null;
    }

    public async Task<bool> TryReplaceAsync(MatchRecord match, CancellationToken cancellationToken = default)
    {
        await _ensureTable.Value;
        try
        {
            await _table.UpdateEntityAsync(ToEntity(match), new ETag(match.Version!), TableUpdateMode.Replace, cancellationToken);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 412)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        await _ensureTable.Value;
        await _table.DeleteEntityAsync(code, RowKey, ETag.All, cancellationToken);
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await _ensureTable.Value;
        var filter = TableClient.CreateQueryFilter($"ExpiresAt le {now}");
        var count = 0;
        await foreach (var entity in _table.QueryAsync<TableEntity>(filter, select: ["PartitionKey", "RowKey"], cancellationToken: cancellationToken))
        {
            await _table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All, cancellationToken);
            count++;
        }

        return count;
    }

    private static TableEntity ToEntity(MatchRecord m) => new(m.Code, RowKey)
    {
        ["HostId"] = m.HostId,
        ["HostTokenHash"] = m.HostTokenHash,
        ["GuestId"] = m.GuestId,
        ["GuestTokenHash"] = m.GuestTokenHash,
        ["CreatedAt"] = m.CreatedAt,
        ["ExpiresAt"] = m.ExpiresAt,
    };

    private static MatchRecord FromEntity(TableEntity e) => new()
    {
        Code = e.PartitionKey,
        HostId = e.GetString("HostId"),
        HostTokenHash = e.GetString("HostTokenHash"),
        GuestId = e.GetString("GuestId"),
        GuestTokenHash = e.GetString("GuestTokenHash"),
        CreatedAt = e.GetDateTimeOffset("CreatedAt") ?? DateTimeOffset.MinValue,
        ExpiresAt = e.GetDateTimeOffset("ExpiresAt") ?? DateTimeOffset.MinValue,
        Version = e.ETag.ToString(),
    };
}
