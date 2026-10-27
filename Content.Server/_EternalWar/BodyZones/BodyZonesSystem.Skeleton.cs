using System.Linq;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Input;
using Content.Shared.ActionBlocker;
using Content.Shared.Body;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._EternalWar.BodyZones;

/// <summary>
/// The skeleton: bones laid bare pulled out of a body together, as they hold together (<see cref="BoneAssemblyComponent"/>),
/// and taken apart again: by hand with the take-apart key, the spine with a saw or crushing blows.
/// </summary>
public sealed partial class BodyZonesSystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    private static readonly SoundSpecifier BoneRattleSound = new SoundPathSpecifier("/Audio/Effects/bone_rattle.ogg");
    private static readonly SoundSpecifier SawSound = new SoundPathSpecifier("/Audio/Effects/saw.ogg");

    /// <summary>
    /// The zones whose bones come out of a body together, and the zone that has to be laid bare for that:
    /// the jaw is under the skin of the face.
    /// </summary>
    private static readonly (BodyZone Zone, BodyZone Cover)[] SkeletonZones =
    {
        (BodyZone.Head, BodyZone.Head),
        (BodyZone.Mouth, BodyZone.Head),
        (BodyZone.Chest, BodyZone.Chest),
        (BodyZone.Abdomen, BodyZone.Abdomen),
    };

    /// <summary>
    /// Every assembly prototype with the bones it is made of, the biggest first.
    /// </summary>
    private readonly List<(EntProtoId Id, HashSet<ProtoId<OrganCategoryPrototype>> Bones)> _assemblies = new();

    /// <summary>
    /// Assemblies broken in two by a blow, taken apart in <see cref="Update"/>, outside of the damage event.
    /// </summary>
    private readonly HashSet<EntityUid> _toSplit = new();

    private void InitializeSkeleton()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<BoneAssemblyComponent, DamageDealtEvent>(OnAssemblyDamaged);
        SubscribeLocalEvent<BoneAssemblyComponent, ExaminedEvent>(OnAssemblyExamined);
        SubscribeLocalEvent<BoneAssemblyComponent, BoneSawDoAfterEvent>(OnSawDoAfter);
        SubscribeLocalEvent<BodyZonesComponent, BoneExtractDoAfterEvent>(OnExtractDoAfter);
        SubscribeNetworkEvent<TakeApartInHandEvent>(OnTakeApartInHand);

        CommandBinds.Builder
            .Bind(EternalWarKeyFunctions.TakeApart, new PointerInputCmdHandler(OnTakeApartInput))
            .Register<BodyZonesSystem>();

        LoadAssemblies();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<BodyZonesSystem>();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            LoadAssemblies();
    }

    private void LoadAssemblies()
    {
        _assemblies.Clear();
        foreach (var proto in ProtoMan.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.Abstract && proto.TryComp<BoneAssemblyComponent>(out var assembly, _componentFactory))
                _assemblies.Add((proto.ID, assembly.Bones.ToHashSet()));
        }

        _assemblies.Sort((a, b) => b.Bones.Count.CompareTo(a.Bones.Count));
    }

    #region Input

    private bool OnTakeApartInput(ICommonSession? session, EntityCoordinates coords, EntityUid target)
    {
        if (session?.AttachedEntity is not { } user || !target.IsValid() || TerminatingOrDeleted(target))
            return false;

        return TryTakeApart(user, target);
    }

    private void OnTakeApartInHand(TakeApartInHandEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !_hands.TryGetHeldItem(user, ev.HandId, out var held))
            return;

        TryTakeApart(user, held.Value);
    }

    /// <summary>
    /// The take-apart key on something: pulls the bones out of a body laid bare, takes bones apart.
    /// </summary>
    public bool TryTakeApart(EntityUid user, EntityUid target)
    {
        if (!HasComp<BoneAssemblyComponent>(target) && !HasComp<BodyZonesComponent>(target))
            return false;

        if (!_actionBlocker.CanComplexInteract(user)
            || !_actionBlocker.CanInteract(user, target)
            || !_hands.IsHolding(user, target) && !_interaction.InRangeAndAccessible(user, target))
            return false;

        if (TryComp<BoneAssemblyComponent>(target, out var assembly))
            return TryTakeApartAssembly(user, (target, assembly));

        return TryStartExtract(user, (target, Comp<BodyZonesComponent>(target)));
    }

    #endregion

    #region Pulling bones out of a body

    /// <summary>
    /// Whether the bones of the zone can be seen: it has neither skin nor muscles, or it is torn open.
    /// </summary>
    private bool IsLaidBare(BodyZonesComponent comp, BodyZone zone)
    {
        return IsMissing(comp, zone) || GetExposure(comp, zone) >= 2;
    }

    /// <summary>
    /// The bones of the head and the torso that are still in the body and laid bare.
    /// </summary>
    public List<EntityUid> GetBareSkeleton(Entity<BodyZonesComponent> ent)
    {
        var result = new List<EntityUid>();
        foreach (var (zone, cover) in SkeletonZones)
        {
            if (!ent.Comp.Zones.TryGetValue(zone, out var data) || !IsLaidBare(ent.Comp, cover))
                continue;

            var protos = data.Contents.Select(p => (EntProtoId?) p).Prepend(data.Bone);
            foreach (var proto in protos)
            {
                if (GetCategory(proto) is not { } category)
                    continue;

                foreach (var organ in GetOrgans(ent, category))
                {
                    if (HasComp<BoneComponent>(organ) && !result.Contains(organ))
                        result.Add(organ);
                }
            }
        }

        return result;
    }

    private bool TryStartExtract(EntityUid user, Entity<BodyZonesComponent> body)
    {
        if (GetBareSkeleton(body).Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("skeleton-extract-nothing"), body, user);
            return true;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, body.Comp.BoneExtractDelay, new BoneExtractDoAfterEvent(), body, target: body)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return true;

        _popup.PopupEntity(Loc.GetString("skeleton-extract-start",
                ("user", Identity.Entity(user, EntityManager)),
                ("target", Identity.Entity(body, EntityManager))),
            body,
            PopupType.MediumCaution);
        return true;
    }

    private void OnExtractDoAfter(Entity<BodyZonesComponent> ent, ref BoneExtractDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        var parts = ExtractSkeleton(ent);
        if (parts.Count == 0)
            return;

        _hands.PickupOrDrop(args.User, parts[0]);
        _audio.PlayPvs(BoneRattleSound, ent);
        _popup.PopupEntity(Loc.GetString("skeleton-extract-done",
                ("user", Identity.Entity(args.User, EntityManager)),
                ("target", Identity.Entity(ent, EntityManager)),
                ("bones", Name(parts[0]))),
            ent,
            PopupType.MediumCaution);
        _adminLogger.Add(LogType.Damaged, LogImpact.High, $"{ToPrettyString(args.User):user} pulled the bones out of {ToPrettyString(ent):target}");
    }

    /// <summary>
    /// Pulls the bones laid bare out of the body, put together as they hold together. Without its skull or spine
    /// the body is dead for good.
    /// </summary>
    /// <returns>What came out, the biggest piece first.</returns>
    public List<EntityUid> ExtractSkeleton(Entity<BodyZonesComponent> ent)
    {
        var bones = GetBareSkeleton(ent);
        if (bones.Count == 0)
            return new List<EntityUid>();

        var vital = false;
        foreach (var bone in bones)
        {
            vital |= Comp<BoneComponent>(bone).Vital;
            TakeOutOfBody(ent, bone);
        }

        if (vital)
            KillForGood(ent);

        return Assemble(bones, _transform.GetMoverCoordinates(ent));
    }

    /// <summary>
    /// Takes an organ out of the body as it is: unlike a torn out bone, a pulled out one isn't broken by it.
    /// </summary>
    private void TakeOutOfBody(EntityUid body, EntityUid organ)
    {
        if (_childQuery.HasComp(organ))
            _organRelation.Orphan(organ);

        if (_container.TryGetContainer(body, BodyComponent.ContainerID, out var container))
            _container.Remove(organ, container);

        _transform.SetCoordinates(organ, _transform.GetMoverCoordinates(body));
    }

    #endregion

    #region Putting bones together

    /// <summary>
    /// Puts loose bones together into the biggest pieces their combination has a prototype for;
    /// the bones that fit none stay loose. The bones have to be out of any container.
    /// </summary>
    /// <returns>The pieces and the loose bones, the biggest first.</returns>
    public List<EntityUid> Assemble(List<EntityUid> bones, EntityCoordinates coords)
    {
        var left = new Dictionary<ProtoId<OrganCategoryPrototype>, EntityUid>();
        var result = new List<EntityUid>();
        foreach (var bone in bones)
        {
            if (_organQuery.CompOrNull(bone)?.Category is { } category && left.TryAdd(category, bone))
                continue;

            result.Add(bone);
        }

        var pieces = new List<EntityUid>();
        foreach (var (id, set) in _assemblies)
        {
            if (!set.All(left.ContainsKey))
                continue;

            var piece = SpawnAtPosition(id, coords);
            var container = _container.EnsureContainer<Container>(piece, BoneAssemblyComponent.ContainerId);
            var assembly = Comp<BoneAssemblyComponent>(piece);
            foreach (var category in set)
            {
                var bone = left[category];
                left.Remove(category);
                _container.Insert(bone, container, force: true);
                assembly.Broken |= Comp<BoneComponent>(bone).Broken;
            }

            Dirty(piece, assembly);
            pieces.Add(piece);
        }

        pieces.AddRange(left.Values);
        pieces.AddRange(result);
        return pieces;
    }

    /// <summary>
    /// Takes these bones off the assembly. The bones taken off and the bones left are each put together again.
    /// What was held in a hand stays in it (the part left), the part taken off goes into the user's other hand.
    /// </summary>
    /// <returns>The pieces taken off and the pieces left.</returns>
    public (List<EntityUid> Off, List<EntityUid> Left) SplitAssembly(Entity<BoneAssemblyComponent> ent,
        IReadOnlyCollection<ProtoId<OrganCategoryPrototype>> off,
        EntityUid? user = null)
    {
        var coords = _transform.GetMoverCoordinates(ent);
        string? hand = null;
        EntityUid? holder = null;
        if (_container.TryGetContainingContainer(ent.Owner, out var holding)
            && _hands.IsHolding(holding.Owner, ent, out var inHand))
        {
            holder = holding.Owner;
            hand = inHand;
        }

        var offBones = new List<EntityUid>();
        var leftBones = new List<EntityUid>();
        if (_container.TryGetContainer(ent, BoneAssemblyComponent.ContainerId, out var container))
        {
            foreach (var bone in container.ContainedEntities.ToList())
            {
                _container.Remove(bone, container);
                _transform.SetCoordinates(bone, coords);
                var taken = _organQuery.CompOrNull(bone)?.Category is { } category && off.Contains(category);
                (taken ? offBones : leftBones).Add(bone);
            }
        }

        // Out of the hand before the pieces go into it.
        Del(ent);

        var leftPieces = Assemble(leftBones, coords);
        var offPieces = Assemble(offBones, coords);

        if (holder != null && hand != null && leftPieces.Count > 0)
            _hands.TryPickup(holder.Value, leftPieces[0], hand, checkActionBlocker: false, animate: false);

        if (user != null)
        {
            foreach (var piece in offPieces)
            {
                _hands.PickupOrDrop(user, piece, animate: false);
            }
        }

        return (offPieces, leftPieces);
    }

    #endregion

    #region Taking assemblies apart

    private bool TryTakeApartAssembly(EntityUid user, Entity<BoneAssemblyComponent> ent)
    {
        if (ent.Comp.TakeOut.Count > 0)
        {
            var name = Name(ent);
            var (off, _) = SplitAssembly(ent, ent.Comp.TakeOut, user);
            _audio.PlayPvs(BoneRattleSound, user);
            if (off.Count > 0)
            {
                _popup.PopupEntity(Loc.GetString("skeleton-take-out",
                        ("user", Identity.Entity(user, EntityManager)),
                        ("bones", Name(off[0])),
                        ("from", name)),
                    user,
                    PopupType.Medium);
            }

            return true;
        }

        if (ent.Comp.SplitOff.Count == 0)
            return false;

        if (!_hands.TryGetActiveItem(user, out var saw) || !_tool.HasQuality(saw.Value, ent.Comp.SawQuality))
        {
            _popup.PopupEntity(Loc.GetString("skeleton-too-strong", ("bones", ent.Owner)), ent, user);
            return true;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.SawDelay, new BoneSawDoAfterEvent(), ent, target: ent, used: saw)
        {
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return true;

        _audio.PlayPvs(SawSound, ent);
        _popup.PopupEntity(Loc.GetString("skeleton-saw-start",
                ("user", Identity.Entity(user, EntityManager)),
                ("bones", ent.Owner)),
            ent,
            PopupType.Medium);
        return true;
    }

    private void OnSawDoAfter(Entity<BoneAssemblyComponent> ent, ref BoneSawDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        var name = Name(ent);
        SplitAssembly(ent, ent.Comp.SplitOff, args.User);
        _popup.PopupEntity(Loc.GetString("skeleton-saw-done", ("bones", name)), args.User, PopupType.Medium);
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):user} sawed {name} in two with {ToPrettyString(args.Used):tool}");
    }

    private void OnAssemblyDamaged(Entity<BoneAssemblyComponent> ent, ref DamageDealtEvent args)
    {
        if (ent.Comp.SplitOff.Count == 0
            || args.Damage.DamageDict.GetValueOrDefault("Blunt").Float() < ent.Comp.BluntThreshold
            || _toSplit.Contains(ent))
            return;

        if (ent.Comp.Broken)
        {
            if (_random.Prob(ent.Comp.BrokenSplitChance))
                _toSplit.Add(ent);
            return;
        }

        if (_random.Prob(ent.Comp.SplitChance))
        {
            _toSplit.Add(ent);
            return;
        }

        if (!_random.Prob(ent.Comp.BreakChance))
            return;

        ent.Comp.Broken = true;
        Dirty(ent);
        if (_container.TryGetContainer(ent, BoneAssemblyComponent.ContainerId, out var container))
        {
            foreach (var bone in container.ContainedEntities)
            {
                if (TryComp<BoneComponent>(bone, out var boneComp))
                    SetBroken((bone, boneComp), true);
            }
        }

        _audio.PlayPvs(FractureSound, ent);
        _popup.PopupEntity(Loc.GetString("skeleton-cracked", ("bones", ent.Owner)), ent, PopupType.MediumCaution);
    }

    /// <summary>
    /// Assemblies a blow broke in two.
    /// </summary>
    private void UpdateSkeleton()
    {
        if (_toSplit.Count == 0)
            return;

        var split = _toSplit.ToArray();
        _toSplit.Clear();

        foreach (var uid in split)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<BoneAssemblyComponent>(uid, out var assembly))
                continue;

            var coords = Transform(uid).Coordinates;
            var name = Name(uid);
            SplitAssembly((uid, assembly), assembly.SplitOff);
            _audio.PlayPvs(FractureSound, coords);
            _popup.PopupCoordinates(Loc.GetString("skeleton-broken-in-two", ("bones", name)), coords, PopupType.MediumCaution);
        }
    }

    private void OnAssemblyExamined(Entity<BoneAssemblyComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Broken)
            args.PushMarkup(Loc.GetString("skeleton-examine-broken"));

        if (ent.Comp.TakeOut.Count > 0)
            args.PushMarkup(Loc.GetString("skeleton-examine-take-apart"));
        else if (ent.Comp.SplitOff.Count > 0)
            args.PushMarkup(Loc.GetString("skeleton-examine-saw"));
    }

    #endregion
}
