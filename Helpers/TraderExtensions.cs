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
    private static DateTime _lastUpdate;
    private static bool _isUpdating;

    public static ISession Session => _session ??= ClientAppUtils.GetMainApp().GetClientBackEndSession();

    public static List<TraderClass> Traders => Session?.Traders?.Where(t => !t.Settings.AvailableInRaid).ToList() ?? [];

    public static SupplyData GetSupplyData(this TraderClass trader)
    {
        return SupplyDataField?.GetValue(trader) as SupplyData;
    }

    /// <summary>
    /// Triggers async refresh of trader supply data if needed. Non-blocking.
    /// </summary>
    public static void EnsureSupplyDataUpdated()
    {
        if (SupplyDataField == null || _isUpdating)
            return;

        var traders = Traders;
        if (traders.Count == 0)
            return;

        var timeSinceUpdate = (DateTime.UtcNow - _lastUpdate).TotalSeconds;
        var hasAllData = traders.All(t => t.GetSupplyData() != null);

        if (timeSinceUpdate < 30 && hasAllData)
            return;

        _isUpdating = true;
        _lastUpdate = DateTime.UtcNow;
        _ = RefreshAllTradersAsync(traders);
    }

    private static async Task RefreshAllTradersAsync(List<TraderClass> traders)
    {
        try
        {
            for (var i = 0; i < traders.Count; i++)
            {
                await RefreshTraderAsync(traders[i]);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private static async Task RefreshTraderAsync(TraderClass trader)
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
            ItemManager.Logger.LogError($"Supply data update failed for {trader.Id}: {ex.Message}");
        }
    }
}
