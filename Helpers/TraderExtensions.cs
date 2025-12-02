using System.Reflection;
using EFT;
using SPT.Reflection.Utils;

namespace StashManagementHelper.Helpers;

// Extension methods to fetch supply data and price from TraderClass
public static class TraderExtensions
{
    private static ISession _session;

    private static readonly FieldInfo SupplyDataField = typeof(TraderClass).GetField("supplyData_0", BindingFlags.NonPublic | BindingFlags.Instance);

    public static ISession Session => _session ??= ClientAppUtils.GetMainApp().GetClientBackEndSession();

    public static SupplyData GetSupplyData(this TraderClass trader)
    {
        return SupplyDataField != null ? SupplyDataField.GetValue(trader) as SupplyData : null;
    }

    public static void SetSupplyData(this TraderClass trader, SupplyData data)
    {
        if (SupplyDataField == null)
        {
            ItemManager.Logger.LogWarning("SupplyData field not found on TraderClass; skipping SetSupplyData.");
            return;
        }

        SupplyDataField.SetValue(trader, data);
    }

    public static async void UpdateSupplyData(this TraderClass trader)
    {
        if (SupplyDataField == null)
        {
            return;
        }

        var result = await Session.GetSupplyData(trader.Id);
        if (result.Succeed)
        {
            trader.SetSupplyData(result.Value);
        }
        else
        {
            ItemManager.Logger.LogError("Failed to download supply data");
        }
    }
}