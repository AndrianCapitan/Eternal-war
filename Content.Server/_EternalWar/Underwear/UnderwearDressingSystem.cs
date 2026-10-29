using Content.Shared._EternalWar.Underwear;
using Content.Shared.Body;
using Content.Shared.Clothing.Components;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._EternalWar.Underwear;

/// <summary>
/// Dresses a player's mob in underwear items when it spawns (<see cref="UnderwearWearerComponent"/>): the underwear
/// of the character editor moves off the body into the underwear slots, where it can be taken off.
/// </summary>
public sealed partial class UnderwearDressingSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    private static readonly HumanoidVisualLayers[] UnderwearLayers =
    {
        HumanoidVisualLayers.UndergarmentTop,
        HumanoidVisualLayers.UndergarmentBottom,
    };

    public override void Initialize()
    {
        base.Initialize();

        // After the profile and the job gear, so the chosen underwear is known and goes under the jumpsuit.
        SubscribeLocalEvent<UnderwearWearerComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(EntityUid uid, UnderwearWearerComponent comp, PlayerSpawnCompleteEvent args)
    {
        Dress((uid, comp));
    }

    /// <summary>
    /// Takes the underwear markings off the body and puts the matching items into the empty underwear slots,
    /// then the defaults for the sex into the slots still empty.
    /// </summary>
    public void Dress(Entity<UnderwearWearerComponent> ent)
    {
        var items = new List<EntProtoId>();

        if (_visualBody.TryGatherMarkingsData(ent.Owner, null, out _, out _, out var applied))
        {
            var cleared = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();
            foreach (var (organ, layers) in applied)
            {
                foreach (var layer in UnderwearLayers)
                {
                    if (!layers.TryGetValue(layer, out var markings) || markings.Count == 0)
                        continue;

                    foreach (var marking in markings)
                    {
                        if (ent.Comp.Items.TryGetValue(marking.MarkingId, out var item))
                            items.Add(item);
                    }

                    cleared.GetOrNew(organ)[layer] = new();
                }
            }

            if (cleared.Count > 0)
                _visualBody.ApplyMarkings(ent, cleared);
        }

        var sex = CompOrNull<HumanoidProfileComponent>(ent)?.Sex ?? Sex.Unsexed;
        if (ent.Comp.Defaults.TryGetValue(sex, out var defaults))
            items.AddRange(defaults);

        foreach (var item in items)
        {
            TryWear(ent, item);
        }
    }

    /// <summary>
    /// Puts a new item into the first empty slot it fits; deletes it if there is none.
    /// </summary>
    private void TryWear(EntityUid wearer, EntProtoId proto)
    {
        var item = Spawn(proto, Transform(wearer).Coordinates);
        if (TryComp<ClothingComponent>(item, out var clothing) && _inventory.TryGetSlots(wearer, out var slots))
        {
            foreach (var slot in slots)
            {
                if ((slot.SlotFlags & clothing.Slots) == 0 || _inventory.TryGetSlotEntity(wearer, slot.Name, out _))
                    continue;

                // Forced: the job's jumpsuit is already on.
                if (_inventory.TryEquip(wearer, item, slot.Name, silent: true, force: true))
                    return;
            }
        }

        Del(item);
    }
}
