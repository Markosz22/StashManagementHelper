using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;

namespace StashManagementHelper.Helpers
{
    public static class FleaMarketHelper
    {
        private static readonly Dictionary<string, double> PriceCache = [];
        private static bool _isFetching;

        public static double GetItemFleaPrice(Item item)
        {
            return PriceCache.TryGetValue(item.TemplateId, out var cachedPrice) ? cachedPrice * item.StackObjectsCount : 0;
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
            CachePricesForItems(itemsToFetch.Select(id => (string)id).ToList()).ContinueWith(_ => _isFetching = false);
        }

        private static async Task CachePricesForItems(List<string> itemsToFetch)
        {
            ItemManager.Logger.LogInfo($"Fetching flea prices for {itemsToFetch.Count} items.");
            var tasks = itemsToFetch.Select(FetchAndCachePrice).ToList();
            await Task.WhenAll(tasks);
            ItemManager.Logger.LogInfo("Finished fetching flea prices.");
            NotificationManagerClass.DisplayMessageNotification("Flea market prices updated. Please sort again.");
        }

        private static async Task FetchAndCachePrice(string templateId)
        {
            ItemManager.Logger.LogInfo($"Getting flea price for {templateId}");

            var marketPrices = await GetMarketPricesAsync(templateId);

            if (marketPrices == null) return;

            var price = marketPrices.avg > 0 ? marketPrices.avg : marketPrices.min;
            if (price > 0)
            {
                PriceCache[templateId] = price;
            }
            ItemManager.Logger.LogInfo($"Market prices for {templateId}: avg={marketPrices.avg}, min={marketPrices.min}, max={marketPrices.max}, using: {price}");
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
}