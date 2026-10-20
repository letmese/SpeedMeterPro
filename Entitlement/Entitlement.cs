using SpeedMeterPro.Models;

namespace SpeedMeterPro.Entitlement;

/// <summary>
/// Entitlement abstraction: our own app ships with every tier unlocked.
/// A different provider (Store license, license file, server) can be swapped in later
/// without touching feature code — gating logic stays testable.
/// </summary>
public interface IEntitlementProvider
{
    UserTier CurrentTier { get; }
    bool HasAccess(UserTier requiredTier);
    event EventHandler? EntitlementChanged;
}

/// <summary>Default provider: everything unlocked ("Pro included").</summary>
public sealed class AllAccessEntitlementProvider : IEntitlementProvider
{
    public static AllAccessEntitlementProvider Instance { get; } = new();
    public UserTier CurrentTier => UserTier.Subscribed;
#pragma warning disable CS0067 // event intentionally unused: all-access provider never changes tier
    public event EventHandler? EntitlementChanged;
#pragma warning restore CS0067
    public bool HasAccess(UserTier requiredTier) => UserTier.Subscribed >= requiredTier;
}
