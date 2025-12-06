using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI.Ragfair;

namespace StashManagementHelper.Helpers;

public static class FleaMarketHelper
{
    private static readonly ConcurrentDictionary<string, double> PriceCache = new();
    private static volatile bool _isFetching;

    private const double FailedMarker = -1d;

    /// <summary>
    /// Returns cached flea price. Returns 0 if not yet cached or failed.
    /// </summary>
    public static double GetItemFleaPrice(Item item)
    {
        if (PriceCache.TryGetValue(item.TemplateId, out var price) && price >= 0)
        {
            return price * item.StackObjectsCount;
        }
        return 0;
    }

    /// <summary>
    /// Pre-caches prices for all items. Call once before sorting.
    /// </summary>
    public static void StartCachingPricesForItems(IEnumerable<Item> items)
    {
        if (_isFetching) return;

        var templateIds = items
            .Select(i => (string)i.TemplateId)
            .Where(id => !PriceCache.ContainsKey(id))
            .Distinct()
            .ToList();

        if (templateIds.Count == 0) return;

        _isFetching = true;
        Task.Run(() => CachePricesAsync(templateIds)).ContinueWith(_ => _isFetching = false);
    }

    private static async Task CachePricesAsync(List<string> templateIds)
    {
        var tasks = templateIds.Select(FetchPriceAsync);
        await Task.WhenAll(tasks);
    }

    private static async Task FetchPriceAsync(string templateId)
    {
        var prices = await GetMarketPricesAsync(templateId);
        PriceCache[templateId] = prices?.min ?? FailedMarker;
    }

    private static Task<ItemMarketPrices> GetMarketPricesAsync(string templateId)
    {
        var tcs = new TaskCompletionSource<ItemMarketPrices>();
        var session = TraderExtensions.Session;

        if (session == null)
        {
            tcs.TrySetResult(null);
            return tcs.Task;
        }

        session.GetMarketPrices(templateId, result =>
        {
            tcs.TrySetResult(result.Succeed ? result.Value : null);
        });

        return tcs.Task;
    }
}
