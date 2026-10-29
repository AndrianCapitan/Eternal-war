using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._EternalWar.Underwear;

/// <summary>
/// Underwear can't be put on or taken off over the clothes covering it (<see cref="UnderwearComponent"/>).
/// </summary>
public sealed partial class UnderwearSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnderwearComponent, BeingEquippedAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<UnderwearComponent, BeingUnequippedAttemptEvent>(OnUnequipAttempt);
    }

    private void OnEquipAttempt(EntityUid uid, UnderwearComponent comp, BeingEquippedAttemptEvent args)
    {
        if (IsCovered((uid, comp), args.EquipTarget))
        {
            args.Reason = "underwear-covered-equip";
            args.Cancel();
        }
    }

    private void OnUnequipAttempt(EntityUid uid, UnderwearComponent comp, BeingUnequippedAttemptEvent args)
    {
        if (IsCovered((uid, comp), args.UnEquipTarget))
        {
            args.Reason = "underwear-covered-unequip";
            args.Cancel();
        }
    }

    /// <summary>
    /// Whether the wearer has clothes on over this underwear.
    /// </summary>
    public bool IsCovered(Entity<UnderwearComponent> ent, EntityUid wearer)
    {
        foreach (var slot in ent.Comp.CoveredBy)
        {
            if (_inventory.TryGetSlotEntity(wearer, slot, out _))
                return true;
        }

        return false;
    }
}
