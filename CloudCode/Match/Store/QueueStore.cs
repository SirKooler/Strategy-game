using Match.Rules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace Match.Store;

public class QueueStore
{
    public const string QueueId = "matchmaking-queue";
    public const string TicketsKey = "tickets";

    static readonly JsonSerializerSettings JsonSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore
    };

    readonly IGameApiClient _gameApi;

    public QueueStore(IGameApiClient gameApi)
    {
        _gameApi = gameApi;
    }

    public async Task<LoadedQueue> Load(IExecutionContext context)
    {
        try
        {
            var response = await _gameApi.CloudSaveData.GetCustomItemsAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                QueueId,
                new List<string> { TicketsKey });

            var results = response?.Data?.Results;
            if (results == null || results.Count == 0)
                return Empty();

            var item = results.Find(r => r.Key == TicketsKey);
            if (item == null)
                return Empty();

            List<QueueTicket> tickets = ReadTickets(item.Value);
            return new LoadedQueue
            {
                Tickets = tickets ?? new List<QueueTicket>(),
                WriteLock = item.WriteLock
            };
        }
        catch (Exception ex) when (IsMissing(ex))
        {
            return Empty();
        }
    }

    static bool IsMissing(Exception ex)
    {
        string text = ex.ToString();
        return text.Contains("404", StringComparison.Ordinal) || text.Contains("Not Found", StringComparison.OrdinalIgnoreCase);
    }

    public async Task Save(IExecutionContext context, LoadedQueue loaded)
    {
        string json = JsonConvert.SerializeObject(loaded.Tickets ?? new List<QueueTicket>(), JsonSettings);
        var item = new SetItemBody(TicketsKey, json);
        if (!string.IsNullOrEmpty(loaded.WriteLock))
            item.WriteLock = loaded.WriteLock;

        await _gameApi.CloudSaveData.SetCustomItemBatchAsync(
            context,
            context.ServiceToken,
            context.ProjectId,
            QueueId,
            new SetItemBatchBody(new List<SetItemBody> { item }));
    }

    static LoadedQueue Empty()
    {
        return new LoadedQueue { Tickets = new List<QueueTicket>() };
    }

    static List<QueueTicket> ReadTickets(object value)
    {
        if (value == null)
            return new List<QueueTicket>();

        string json = null;
        if (value is string text)
            json = text;
        else if (value is JToken token)
            json = token.Type == JTokenType.String ? token.ToObject<string>() : token.ToString(Formatting.None);
        else
            json = value.ToString();

        if (string.IsNullOrEmpty(json))
            return new List<QueueTicket>();

        return JsonConvert.DeserializeObject<List<QueueTicket>>(json, JsonSettings) ?? new List<QueueTicket>();
    }
}

public class LoadedQueue
{
    public List<QueueTicket> Tickets { get; set; } = new();
    public string WriteLock { get; set; }
}
