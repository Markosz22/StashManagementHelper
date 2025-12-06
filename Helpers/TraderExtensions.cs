using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using SPT.Reflection.Utils;

namespace StashManagementHelper.Helpers;

public static class TraderExtensions
{
    private static readonly FieldInfo SupplyDataField = typeof(TraderClass).GetField("supplyData_0", BindingFlags.NonPublic | BindingFlags.Instance);
    private static ISession _session;
    private static List<TraderClass> _cachedTraders;
    private static volatile bool _isUpdating;
    private static DateTime _lastUpdate = DateTime.MinValue;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public static ISession Session => _session ??= ClientAppUtils.GetMainApp().GetClientBackEndSession();

    /// <summary>
    /// Cached list of traders.
    /// </summary>
    public static List<TraderClass> Traders => _cachedTraders ??= Session?.Traders?.Where(t => !t.Settings.AvailableInRaid).ToList() ?? [];

    public static SupplyData GetSupplyData(this TraderClass trader)
    {
        return SupplyDataField?.GetValue(trader) as SupplyData;
    }

    /// <summary>
    /// Ensures supply data is fetched for all traders.
    /// </summary>
    public static void EnsureSupplyDataUpdated()
    {
        if (SupplyDataField == null || _isUpdating) return;
        if (DateTime.UtcNow - _lastUpdate < CacheDuration) return;

        var tradersNeedingData = Traders.Where(t => t.GetSupplyData() == null).ToList();
        if (tradersNeedingData.Count == 0)
        {
            _lastUpdate = DateTime.UtcNow;
            return;
        }

        _isUpdating = true;
        Task.Run(() => UpdateAllAsync(tradersNeedingData))
            .ContinueWith(_ =>
            {
                _isUpdating = false;
                _lastUpdate = DateTime.UtcNow;
            });
    }

    private static async Task UpdateAllAsync(List<TraderClass> traders)
    {
        var tasks = traders.Select(UpdateTraderAsync);
        await Task.WhenAll(tasks);
    }

    private static async Task UpdateTraderAsync(TraderClass trader)
    {
        try
        {
            var result = await Session.GetSupplyData(trader.Id);
            if (result.Succeed)
            {
                SupplyDataField.SetValue(trader, result.Value);
            }
        }
        catch (Exception ex)
        {
            ItemManager.Logger.LogError($"Failed to update supply data for {trader.Id}: {ex.Message}");
        }
    }
}
