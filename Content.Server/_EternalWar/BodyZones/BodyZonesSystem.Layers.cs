using System.Linq;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server._EternalWar.BodyZones;

/// <summary>
/// The health of the layers of a zone: the skin, the muscles and vessels, the organs and the bone each have their own
/// (<see cref="OrganHealthComponent"/>). Hits go through them as <see cref="BodyZonesComponent.Penetration"/> says:
/// a blade cuts from the skin inwards and never into the bone, a club crushes mostly the bone and the organs.
/// The skin is torn off and the muscles and vessels torn at 0, organs fail, bones crack and break.
/// </summary>
public sealed partial class BodyZonesSystem
{
    private static readonly TimeSpan SplintInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextSplintHeal;

    #region Health of the pieces

    /// <summary>
    /// Gives every organ of the body its health. The skin, muscles, vessels and bones have it in YAML,
    /// the upstream organs get it here (<see cref="BodyZonesComponent.OrganMaxHealth"/>).
    /// </summary>
    private void EnsureOrganHealth(Entity<BodyZonesComponent> ent)
    {
        if (!_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container))
            return;

        foreach (var organ in container.ContainedEntities)
        {
            EnsureOrganHealth(ent.Comp, organ);
        }
    }

    private void EnsureOrganHealth(BodyZonesComponent comp, EntityUid organ)
    {
        if (HasComp<OrganHealthComponent>(organ) || !_organQuery.TryComp(organ, out var organComp)
            || organComp.Category is not { } category)
            return;

        // Limb parts and the torso are the zones themselves: their health is the zone's.
        if (comp.Zones.Values.Any(z => z.Organ == category && z.TearOff) || category == TorsoCategory)
            return;

        var health = AddComp<OrganHealthComponent>(organ);
        health.MaxHealth = comp.OrganMaxHealth.GetValueOrDefault(category, comp.DefaultOrganHealth);
        Dirty(organ, health);
    }

    private static bool IsAlive(OrganHealthComponent health)
    {
        return !health.Failed && health.Health > 0f;
    }

    #endregion

    #region Damage going through the layers

    /// <summary>
    /// Splits a hit between the layers of the zone it landed on. Body-wide damage reaches every zone that is there,
    /// in proportion to its size.
    /// </summary>
    private void RecordLayerDamage(Entity<BodyZonesComponent> ent, BodyZone? zone, DamageSpecifier delta)
    {
        foreach (var (type, value) in delta.DamageDict)
        {
            if (value <= 0 || !ent.Comp.Penetration.TryGetValue(type, out var penetration))
                continue;

            if (zone is { } target)
            {
                DamageLayers(ent, Redirect(ent.Comp, target), penetration, value.Float());
                continue;
            }

            var zones = ent.Comp.Zones.Where(z => !IsMissing(ent.Comp, z.Key)).ToList();
            var total = zones.Sum(z => z.Value.MaxHealth.Float());
            if (total <= 0f)
                continue;

            foreach (var (z, data) in zones)
            {
                DamageLayers(ent, z, penetration, value.Float() * data.MaxHealth.Float() / total);
            }
        }
    }

    private void DamageLayers(Entity<BodyZonesComponent> ent, BodyZone zone, LayerPenetration penetration, float amount)
    {
        if (amount <= 0f)
            return;

        if (penetration.Order.Count > 0)
        {
            // From the outside in: each layer takes what it can, the rest goes deeper.
            foreach (var layer in penetration.Order)
            {
                amount = DamageLayer(ent, zone, layer, amount);
                if (amount <= 0f)
                    return;
            }

            return;
        }

        // A random split, only between the layers the zone has.
        var shares = new Dictionary<TissueLayer, float>();
        foreach (var (layer, bounds) in penetration.Shares)
        {
            if (GetLayer(ent, zone, layer).Count > 0)
                shares[layer] = _random.NextFloat(bounds.X, MathF.Max(bounds.X, bounds.Y) + 0.0001f);
        }

        var sum = shares.Values.Sum();
        if (sum <= 0f)
            return;

        foreach (var (layer, share) in shares)
        {
            DamageLayer(ent, zone, layer, amount * share / sum);
        }
    }

    /// <summary>
    /// Damages one piece of the body directly (admin tools, tests). What happens to it shows in the next update.
    /// </summary>
    public void DamagePiece(Entity<BodyZonesComponent> ent, EntityUid organ, float amount)
    {
        if (!TryComp<OrganHealthComponent>(organ, out var health))
            return;

        health.Damage = Math.Clamp(health.Damage + amount, 0f, health.MaxHealth);
        Dirty(organ, health);
        _toCheck.Add(ent);
    }

    /// <summary>
    /// Damages random living pieces of a layer; what one can't take goes to the next.
    /// </summary>
    /// <returns>The damage the layer couldn't take, to go deeper.</returns>
    private float DamageLayer(Entity<BodyZonesComponent> ent, BodyZone zone, TissueLayer layer, float amount)
    {
        var pieces = GetLayer(ent, zone, layer);
        while (amount > 0f && pieces.Count > 0)
        {
            var piece = _random.PickAndTake(pieces);
            var health = piece.Comp;
            var taken = MathF.Min(amount, health.Health);
            health.Damage += taken;
            amount -= taken;
            Dirty(piece);
        }

        return amount;
    }

    /// <summary>
    /// The living pieces of a layer of the zone.
    /// </summary>
    private List<Entity<OrganHealthComponent>> GetLayer(Entity<BodyZonesComponent> ent, BodyZone zone, TissueLayer layer)
    {
        var result = new List<Entity<OrganHealthComponent>>();
        if (!ent.Comp.Zones.TryGetValue(zone, out var data))
            return result;

        void Add(EntityUid organ)
        {
            if (TryComp<OrganHealthComponent>(organ, out var health) && IsAlive(health))
                result.Add((organ, health));
        }

        switch (layer)
        {
            case TissueLayer.Skin:
                if (GetSkin(ent, zone) is { } skin)
                    Add(skin);
                break;
            case TissueLayer.Soft:
                foreach (var proto in data.Tissues)
                {
                    if (GetCategory(proto) is { } category)
                        GetOrgans(ent, category).ForEach(Add);
                }

                break;
            case TissueLayer.Organs:
                foreach (var organ in GetInternalOrgans(ent, zone, data))
                {
                    Add(organ);
                }

                break;
            case TissueLayer.Bone:
                if (GetBone(ent, zone) is { } bone)
                    Add(bone);
                break;
        }

        return result;
    }

    /// <summary>
    /// The organs inside a zone: those it drops (but not bones, and not the eyes and jaw, which are zones of their
    /// own), its sex organs, and the zone's own organ if it is one (an eye, the genitals).
    /// </summary>
    private IEnumerable<EntityUid> GetInternalOrgans(Entity<BodyZonesComponent> ent, BodyZone zone, BodyZoneData data)
    {
        var categories = new HashSet<Robust.Shared.Prototypes.ProtoId<OrganCategoryPrototype>>(data.Drop);
        foreach (var proto in data.SexContents.Values.SelectMany(p => p))
        {
            if (GetCategory(proto) is { } category)
                categories.Add(category);
        }

        if (data is { TearOff: false, Burst: false, Organ: { } own })
            categories.Add(own);

        foreach (var (other, otherData) in ent.Comp.Zones)
        {
            if (other != zone && otherData.Organ is { } otherOrgan)
                categories.Remove(otherOrgan);
        }

        foreach (var category in categories)
        {
            foreach (var organ in GetOrgans(ent, category))
            {
                if (!HasComp<BoneComponent>(organ))
                    yield return organ;
            }
        }
    }

    #endregion

    #region What happens at 0

    /// <summary>
    /// Torn skin and flesh fall off, crushed organs fail, bones crack and break.
    /// </summary>
    private void CheckLayers(Entity<BodyZonesComponent> ent)
    {
        var body = ent.Owner;
        foreach (var (zone, data) in ent.Comp.Zones)
        {
            if (IsMissing(ent.Comp, zone))
                continue;

            var part = Loc.GetString(zone.LocKey());

            if (GetSkin(ent, zone) is { } skin && TryComp<OrganHealthComponent>(skin, out var skinHealth)
                && skinHealth.Health <= 0f && RemoveSkin(ent, zone))
            {
                _popup.PopupEntity(Loc.GetString("body-zones-skin-torn", ("target", Identity.Entity(body, EntityManager)), ("part", part)),
                    body,
                    PopupType.MediumCaution);
            }

            foreach (var proto in data.Tissues)
            {
                if (GetCategory(proto) is not { } category)
                    continue;

                foreach (var organ in GetOrgans(ent, category))
                {
                    if (!TryComp<OrganHealthComponent>(organ, out var health) || health.Health > 0f)
                        continue;

                    var name = Name(organ);
                    DropOrgan(body, organ);
                    _bloodstream.TryModifyBleedAmount(body, IsVessel(proto) ? ent.Comp.TissueLossBleed : ent.Comp.MuscleLossBleed);
                    _popup.PopupEntity(Loc.GetString("body-zones-tissue-torn", ("target", Identity.Entity(body, EntityManager)), ("organ", name)),
                        body,
                        PopupType.MediumCaution);
                    _adminLogger.Add(LogType.Damaged, LogImpact.Medium, $"{ToPrettyString(body):target} had the {name} of their {zone} torn");
                }
            }

            foreach (var organ in GetInternalOrgans(ent, zone, data).ToList())
            {
                if (TryComp<OrganHealthComponent>(organ, out var health) && !health.Failed && health.Health <= 0f)
                    FailOrgan(ent, (organ, health));
            }

            UpdateBone(ent, zone);
        }

        UpdateOpenFractures(ent);
    }

    /// <summary>
    /// A crushed organ stops working for good: the heart stops, a lung stops breathing, an eye goes blind,
    /// and without a brain there is nobody left.
    /// </summary>
    private void FailOrgan(Entity<BodyZonesComponent> ent, Entity<OrganHealthComponent> organ)
    {
        organ.Comp.Failed = true;
        Dirty(organ);

        var name = Name(organ);
        _popup.PopupEntity(Loc.GetString("body-zones-organ-failed", ("organ", name)), ent, ent, PopupType.LargeCaution);
        _adminLogger.Add(LogType.Damaged, LogImpact.High, $"{ToPrettyString(ent):target} had their {name} crushed");

        RefreshFailedOrgans(ent);
        UpdateLungs(ent);
        UpdateEyes(ent);

        if (_organQuery.CompOrNull(organ)?.Category == ent.Comp.Brain)
            KillForGood(ent);
    }

    /// <summary>
    /// Keeps <see cref="BodyZonesComponent.FailedOrgans"/> to the organs that are in the body and failed.
    /// </summary>
    private void RefreshFailedOrgans(Entity<BodyZonesComponent> ent)
    {
        var failed = new HashSet<Robust.Shared.Prototypes.ProtoId<OrganCategoryPrototype>>();
        if (_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container))
        {
            foreach (var organ in container.ContainedEntities)
            {
                if (TryComp<OrganHealthComponent>(organ, out var health) && health.Failed
                    && _organQuery.CompOrNull(organ)?.Category is { } category)
                    failed.Add(category);
            }
        }

        if (failed.SetEquals(ent.Comp.FailedOrgans))
            return;

        ent.Comp.FailedOrgans = failed;
        Dirty(ent);
    }

    /// <summary>
    /// Whether the body has an organ of the category that works.
    /// </summary>
    public bool HasWorkingOrgan(Entity<BodyZonesComponent> ent, Robust.Shared.Prototypes.ProtoId<OrganCategoryPrototype> category)
    {
        foreach (var organ in GetOrgans(ent, category))
        {
            if (!TryComp<OrganHealthComponent>(organ, out var health) || !health.Failed)
                return true;
        }

        return false;
    }

    #endregion

    #region Bones

    /// <summary>
    /// Sets the zone's crack and fracture by the health of its bone.
    /// </summary>
    private void UpdateBone(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (GetBone(ent, zone) is not { } bone || !TryComp<OrganHealthComponent>(bone, out var health))
            return;

        var broken = health.Health < health.MaxHealth * bone.Comp.FractureRatio;
        var cracked = !broken && health.Health <= health.MaxHealth * bone.Comp.CrackRatio;

        if (bone.Comp.Cracked != cracked)
        {
            bone.Comp.Cracked = cracked;
            Dirty(bone);
        }

        if (cracked ? ent.Comp.Cracks.Add(zone) : ent.Comp.Cracks.Remove(zone))
        {
            if (cracked && !ent.Comp.Fractures.Contains(zone))
                _popup.PopupEntity(Loc.GetString("body-zones-crack", ("part", Loc.GetString(zone.LocKey()))), ent, ent, PopupType.MediumCaution);

            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
            Dirty(ent);
        }

        if (broken)
            BreakBone(ent, zone);
        else if (ent.Comp.Fractures.Contains(zone))
            UnbreakBone(ent, zone);
    }

    /// <summary>
    /// A fracture is open when the skin is gone and the muscles or vessels are torn: the broken bone is in the wound.
    /// </summary>
    private void UpdateOpenFractures(Entity<BodyZonesComponent> ent)
    {
        foreach (var zone in ent.Comp.Zones.Keys)
        {
            var open = ent.Comp.Fractures.Contains(zone) && !IsMissing(ent.Comp, zone)
                       && ent.Comp.Skinless.Contains(zone) && HasTornTissue(ent, zone);

            if (!(open ? ent.Comp.OpenFractures.Add(zone) : ent.Comp.OpenFractures.Remove(zone)))
                continue;

            Dirty(ent);
            if (open)
            {
                _popup.PopupEntity(Loc.GetString("body-zones-open-fracture",
                        ("target", Identity.Entity(ent, EntityManager)),
                        ("part", Loc.GetString(zone.LocKey()))),
                    ent,
                    PopupType.MediumCaution);
            }
        }
    }

    private bool HasTornTissue(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        foreach (var proto in ent.Comp.Zones[zone].Tissues)
        {
            if (GetCategory(proto) is { } category && GetOrgans(ent, category).Count == 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// A splinted bone grows back together, a bit every second, until it is whole.
    /// </summary>
    private void UpdateSplints()
    {
        var now = _timing.CurTime;
        if (now < _nextSplintHeal)
            return;

        _nextSplintHeal = now + SplintInterval;
        var dt = (float) SplintInterval.TotalSeconds;

        var query = EntityQueryEnumerator<BoneComponent, OrganHealthComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var bone, out var health, out var organ))
        {
            if (!bone.Splinted || organ.Body is not { } body || !TryComp<BodyZonesComponent>(body, out var zones))
                continue;

            var rate = zones.SplintHealRate * dt;
            health.Damage = MathF.Max(0f, health.Damage - rate);
            if (health.Damage <= 0f)
                bone.Splinted = false;

            Dirty(uid, health);
            Dirty(uid, bone);
            _toCheck.Add(body);
        }
    }

    #endregion

    #region Healing

    /// <summary>
    /// When a zone heals, its skin, flesh and organs heal by the same share. Bones don't: they need a splint.
    /// Failed organs stay failed.
    /// </summary>
    private void HealLayers(Entity<BodyZonesComponent> ent, BodyZone zone, float share)
    {
        if (share <= 0f || !ent.Comp.Zones.ContainsKey(zone))
            return;

        share = MathF.Min(1f, share);
        foreach (var layer in new[] { TissueLayer.Skin, TissueLayer.Soft, TissueLayer.Organs })
        {
            foreach (var piece in GetLayer(ent, zone, layer))
            {
                piece.Comp.Damage *= 1f - share;
                Dirty(piece);
            }
        }
    }

    /// <summary>
    /// Rejuvenation: every piece whole, no organ failed, no crack, no fracture, no splint.
    /// </summary>
    private void RestoreLayers(Entity<BodyZonesComponent> ent)
    {
        EnsureOrganHealth(ent);
        if (_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container))
        {
            foreach (var organ in container.ContainedEntities)
            {
                if (TryComp<OrganHealthComponent>(organ, out var health))
                {
                    health.Damage = 0f;
                    health.Failed = false;
                    Dirty(organ, health);
                }

                if (TryComp<BoneComponent>(organ, out var bone))
                {
                    bone.Cracked = false;
                    bone.Splinted = false;
                    Dirty(organ, bone);
                }
            }
        }

        ent.Comp.Cracks.Clear();
        ent.Comp.OpenFractures.Clear();
        ent.Comp.FailedOrgans.Clear();
    }

    #endregion
}
