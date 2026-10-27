using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;

namespace Content.Server._EternalWar.BodyZones;

/// <summary>
/// What the client needs to draw a body laid bare: which zones lost their muscles, and the hair that goes with
/// the skin of the face. The drawing itself is on the client (<c>BodyZonesSystem.Exposure</c>).
/// </summary>
public sealed partial class BodyZonesSystem
{
    [Dependency] private SharedHideableHumanoidLayersSystem _hideableLayers = default!;

    /// <summary>
    /// The "slot" that hides the hair of a skinned head. Not a real inventory slot, so no clothing ever clears it.
    /// </summary>
    private const SlotFlags ScalpOcclusion = (SlotFlags) (1 << 20);

    /// <summary>
    /// Layers that grow out of the skin of the head: gone with it.
    /// </summary>
    private static readonly HumanoidVisualLayers[] ScalpLayers =
    {
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair,
        HumanoidVisualLayers.Snout,
    };

    private BodyZone? GetTissueZoneOf(BodyZonesComponent comp, EntityUid organ)
    {
        if (!_organQuery.TryComp(organ, out var organComp) || organComp.Category is not { } category)
            return null;

        foreach (var (zone, data) in comp.Zones)
        {
            foreach (var proto in data.Tissues)
            {
                if (GetCategory(proto) == category)
                    return zone;
            }
        }

        return null;
    }

    /// <summary>
    /// A muscle or vessel organ left the body: once the zone has none of them, only its bone is left to see.
    /// </summary>
    private void OnTissueChanged(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (!ent.Comp.Zones.TryGetValue(zone, out var data))
            return;

        // Leaving together with the limb: there is nothing left to see.
        if (IsMissing(ent.Comp, zone))
        {
            if (ent.Comp.TissuesTorn.Remove(zone))
                Dirty(ent);
            return;
        }

        var left = false;
        foreach (var proto in data.Tissues)
        {
            if (GetCategory(proto) is { } category && GetOrgans(ent, category).Count > 0)
                left = true;
        }

        var changed = left ? ent.Comp.TissuesTorn.Remove(zone) : ent.Comp.TissuesTorn.Add(zone);
        if (changed)
            Dirty(ent);

        RefreshScalp(ent);
    }

    private BodyZone? GetBoneZoneOf(BodyZonesComponent comp, EntityUid organ)
    {
        if (!_organQuery.TryComp(organ, out var organComp) || organComp.Category is not { } category)
            return null;

        foreach (var (zone, data) in comp.Zones)
        {
            if (data.Bone != null && GetCategory(data.Bone) == category)
                return zone;
        }

        return null;
    }

    /// <summary>
    /// The bone of a zone left the body (or came back): without it there is nothing left to see in the zone.
    /// </summary>
    private void OnBoneChanged(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        var changed = false;
        if (IsMissing(ent.Comp, zone))
            changed = ent.Comp.BonesLost.Remove(zone);
        else if (GetCategory(ent.Comp.Zones[zone].Bone) is { } category)
            changed = GetOrgans(ent, category).Count > 0 ? ent.Comp.BonesLost.Remove(zone) : ent.Comp.BonesLost.Add(zone);

        if (changed)
            Dirty(ent);
    }

    /// <summary>
    /// Hides the hair and beard while the head has no skin, shows them again when it has.
    /// </summary>
    private void RefreshScalp(Entity<BodyZonesComponent> ent)
    {
        var bare = !IsMissing(ent.Comp, BodyZone.Head) && GetExposure(ent.Comp, BodyZone.Head) > 0;
        foreach (var layer in ScalpLayers)
        {
            _hideableLayers.SetLayerOcclusion(ent.Owner, layer, bare, ScalpOcclusion);
        }
    }
}
