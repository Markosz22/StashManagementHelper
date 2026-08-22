using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Trading;
using SPT.Reflection.Utils;

namespace StashManagementHelper.Helpers;

public static class TraderExtensions
{
    private static readonly FieldInfo SupplyDataField = typeof(Trader).GetField("_supplyData", BindingFlags.NonPublic | BindingFlags.Instance);
    private static IEftSession _session;
    private static DateTime _lastUpdate = DateTime.MinValue;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(3);

    private static IEftSession Session => _session ??= ClientAppUtils.GetMainApp().GetClientBackEndSession();

    public static IEnumerable<Trader> Traders =>
        Session?.Traders?.Where(t => !t.Settings.AvailableInRaid) ?? [];

    public static SupplyData GetSupplyData(this Trader trader)
    {
        return SupplyDataField?.GetValue(trader) as SupplyData;
    }

    /// <summary>
    /// Fetches missing trader supply data on the calling thread so the current value-sort can use it.
    /// </summary>
    public static void EnsureSupplyDataUpdated()
    {
        if (SupplyDataField == null) return;
        if (DateTime.UtcNow - _lastUpdate < CacheDuration) return;

        var tradersNeedingData = Traders.Where(t => t.GetSupplyData() == null).ToList();
        if (tradersNeedingData.Count == 0)
        {
            _lastUpdate = DateTime.UtcNow;
            return;
        }

        var session = Session;
        if (session == null) return;

        try
        {
            var tasks = new Task<Result<SupplyData>>[tradersNeedingData.Count];
            for (var i = 0; i < tradersNeedingData.Count; i++)
            {
                tasks[i] = session.GetSupplyData(tradersNeedingData[i].Id);
            }

            if (!Task.WaitAll(tasks, FetchTimeout))
            {
                ItemManager.Logger.LogWarning("Timed out waiting for trader supply data; some values may be 0 this sort.");
            }

            for (var i = 0; i < tradersNeedingData.Count; i++)
            {
                var task = tasks[i];
                if (!task.IsCompleted || task.IsFaulted || task.IsCanceled)
                {
                    continue;
                }

                var result = task.Result;
                if (result.Succeed && result.Value != null)
                {
                    SupplyDataField.SetValue(tradersNeedingData[i], result.Value);
                }
            }

            if (tradersNeedingData.All(t => t.GetSupplyData() != null))
            {
                _lastUpdate = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            ItemManager.Logger.LogError($"Failed to update trader supply data: {ex.Message}");
        }
    }
}
