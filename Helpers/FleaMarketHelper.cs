using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI.Ragfair;

namespace StashManagementHelper.Helpers;

public static class FleaMarketHelper
{
    private static readonly Dictionary<string, double> PriceCache = [];
    private static bool _isFetching;


    public static double GetItemFleaPrice(Item item)
    {
        PriceCache.TryGetValue(item.TemplateId, out var cachedPrice);
        if (cachedPrice == 0)
        {
            var session = TraderExtensions.Session;
            session.GetMarketPrices(item.TemplateId, result =>
            {
                if (result.Failed)
                {
                    return;
                }
                cachedPrice = result.Value.min;
                PriceCache[item.TemplateId] = cachedPrice;
            });
        }

        ItemManager.Logger.LogInfo($"Returning price for {item.LocalizedShortName()}: {cachedPrice}");

        return cachedPrice * item.StackObjectsCount;
    }

    public static void StartCachingPricesForItems(IEnumerable<Item> items)
    {
        if (_isFetching)
        {
            return;
        }

        var itemsToFetch = items.Where(i => !PriceCache.ContainsKey(i.TemplateId)).Select(i => i.TemplateId).Distinct().ToList();
        if (!itemsToFetch.Any())
        {
            return;
        }

        _isFetching = true;
        CachePricesForItems([.. itemsToFetch.Select(id => (string)id)]).ContinueWith(_ => _isFetching = false);
    }

    private static async Task CachePricesForItems(List<string> itemsToFetch)
    {
        var tasks = itemsToFetch.Select(FetchAndCachePrice).ToList();
        await Task.WhenAll(tasks);
    }

    private static async Task FetchAndCachePrice(string templateId)
    {
        var marketPrices = await GetMarketPricesAsync(templateId);

        if (marketPrices == null) return;

        var price = marketPrices.min;
        if (price > 0)
        {
            PriceCache[templateId] = price;
        }
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
            if (result.Failed)
            {
                ItemManager.Logger.LogError($"Failed to get market price for {templateId}: {result.Error}");
                tcs.TrySetResult(null);
                return;
            }
            tcs.TrySetResult(result.Value);
        });

        return tcs.Task;
    }
}