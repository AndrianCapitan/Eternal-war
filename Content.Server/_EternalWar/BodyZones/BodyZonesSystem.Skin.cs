using System.Linq;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._EternalWar.BodyZones;

/// <summary>
/// The skin of the body zones: torn off in pieces by slashes and burns, cut off with a sharp tool.
/// Also gibbing, which leaves every part of the body on the floor.
/// </summary>
public sealed partial class BodyZonesSystem
{
    [Dependency] private SharedToolSystem _tool = default!;

    private static readonly SoundSpecifier SkinningSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    private void InitializeSkin()
    {
        SubscribeLocalEvent<BodyZonesComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BodyZonesComponent, SkinningDoAfterEvent>(OnSkinningDoAfter);
        SubscribeLocalEvent<BodyZonesComponent, BeingGibbedEvent>(OnBeingGibbed);
    }

    /// <summary>
    /// Takes the skin off a zone; it falls on the floor.
    /// </summary>
    /// <returns>False if the zone had no skin to lose.</returns>
    public bool RemoveSkin(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (IsMissing(ent.Comp, zone) || GetSkin(ent, zone) is not { } skin)
            return false;

        DropOrgan(ent, skin);
        _adminLogger.Add(LogType.Damaged, LogImpact.Medium, $"{ToPrettyString(ent):target} lost the skin of their {zone}");
        return true;
    }

    public EntityUid? GetSkin(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (!ent.Comp.Zones.TryGetValue(zone, out var data) || GetCategory(data.Skin) is not { } category)
            return null;

        var skins = GetOrgans(ent, category);
        return skins.Count > 0 ? skins[0] : null;
    }

    private BodyZone? GetSkinZoneOf(BodyZonesComponent comp, EntityUid organ)
    {
        if (!_organQuery.TryComp(organ, out var organComp) || organComp.Category is not { } category)
            return null;

        foreach (var (zone, data) in comp.Zones)
        {
            if (data.Skin != null && GetCategory(data.Skin) == category)
                return zone;
        }

        return null;
    }

    /// <summary>
    /// A zone without skin bleeds and takes more damage, see <see cref="SharedBodyZonesSystem"/>.
    /// Skin that falls out of a zone being destroyed doesn't count: the zone is gone or torn open anyway.
    /// </summary>
    private void OnSkinLost(Entity<BodyZonesComponent> ent, BodyZone zone)
    {
        if (ent.Comp.Destroyed.Contains(zone) || !ent.Comp.Skinless.Add(zone))
            return;

        _bloodstream.TryModifyBleedAmount(ent.Owner, ent.Comp.SkinLossBleed);
        Dirty(ent);
    }

    #region Skinning

    /// <summary>
    /// Whether the item can cut skin and flesh: a tool of the quality (slicing) or a weapon that slashes.
    /// </summary>
    private bool IsSharp(EntityUid item, ProtoId<ToolQualityPrototype> quality)
    {
        if (_tool.HasQuality(item, quality))
            return true;

        return TryComp<MeleeWeaponComponent>(item, out var melee)
               && melee.Damage.DamageDict.GetValueOrDefault("Slash") > 0;
    }

    private void OnInteractUsing(Entity<BodyZonesComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsSharp(args.Used, ent.Comp.SkinningQuality))
            return;

        args.Handled = true;

        var selected = TryComp<TargetingComponent>(args.User, out var targeting) ? targeting.Selected : BodyZone.Chest;
        var zone = Redirect(ent.Comp, selected);
        var part = Loc.GetString(zone.LocKey());

        if (GetSkin(ent, zone) == null)
        {
            _popup.PopupEntity(Loc.GetString("skinning-no-skin", ("part", part)), ent, args.User);
            return;
        }

        var delay = args.User == args.Target
            ? ent.Comp.SkinningDelay * ent.Comp.SkinningSelfDelayMultiplier
            : ent.Comp.SkinningDelay;
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new SkinningDoAfterEvent(zone), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString("skinning-start",
                ("user", Identity.Entity(args.User, EntityManager)),
                ("target", Identity.Entity(ent, EntityManager)),
                ("part", part)),
            ent,
            PopupType.MediumCaution);
    }

    private void OnSkinningDoAfter(Entity<BodyZonesComponent> ent, ref SkinningDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        if (!RemoveSkin(ent, args.Zone))
            return;

        _audio.PlayPvs(SkinningSound, ent);
        _popup.PopupEntity(Loc.GetString("skinning-done",
                ("target", Identity.Entity(ent, EntityManager)),
                ("part", Loc.GetString(args.Zone.LocKey()))),
            ent,
            PopupType.MediumCaution);
        _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(args.User):user} cut the skin off the {args.Zone} of {ToPrettyString(ent):target} with {ToPrettyString(args.Used):tool}");
    }

    #endregion

    #region Gibbing

    /// <summary>
    /// A gibbed body is torn apart completely: every organ, bone, piece of skin, muscle and vessel flies off on its own.
    /// The head and the limb parts are nothing but those layers, so nothing is left of them; the torso goes with the body.
    /// </summary>
    private void OnBeingGibbed(Entity<BodyZonesComponent> ent, ref BeingGibbedEvent args)
    {
        if (!_container.TryGetContainer(ent, BodyComponent.ContainerID, out var container))
            return;

        var parts = new List<EntityUid>();
        foreach (var organ in container.ContainedEntities.ToList())
        {
            if (_organQuery.CompOrNull(organ)?.Category == TorsoCategory)
                continue;

            if (HasComp<DetachableOrganComponent>(organ) && !HasComp<InternalChildOrganComponent>(organ))
            {
                parts.Add(organ);
                continue;
            }

            DropOrgan(ent, organ, fling: false);
            args.Giblets.Add(organ);
        }

        foreach (var part in parts)
        {
            DeleteOrgan(ent, part);
        }
    }

    #endregion
}
