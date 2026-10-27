using Content.Shared._EternalWar.BodyZones;
using Robust.Client.Player;

namespace Content.Client._EternalWar.BodyZones;

/// <inheritdoc/>
public sealed partial class BodyZonesSystem : SharedBodyZonesSystem
{
    [Dependency] private IPlayerManager _player = default!;

    /// <summary>
    /// Aims the local player at a zone. Applied locally right away so the doll reacts instantly.
    /// </summary>
    public void SelectZone(BodyZone zone)
    {
        if (_player.LocalEntity is not { } player || !TryComp<TargetingComponent>(player, out var targeting))
            return;

        SetTarget((player, targeting), zone);
        RaiseNetworkEvent(new SelectTargetZoneEvent(zone));
    }
}
