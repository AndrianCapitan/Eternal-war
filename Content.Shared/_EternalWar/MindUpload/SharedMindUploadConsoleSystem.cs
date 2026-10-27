using Content.Shared.Body.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.PAI;
using Content.Shared.Popups;

namespace Content.Shared._EternalWar.MindUpload;

public abstract partial class SharedMindUploadConsoleSystem : EntitySystem
{
    [Dependency] protected ItemSlotsSystem ItemSlots = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindUploadConsoleComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(Entity<MindUploadConsoleComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled || args.Slot.ID != ent.Comp.PositronicSlotId)
            return;

        // Only an empty, switched off positronic brain can receive a mind,
        // otherwise two minds would end up fighting over one brain.
        if (IsPositronicFree(args.Item))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("mind-upload-console-positronic-not-off"), ent, args.User);
    }

    /// <summary>
    /// A positronic brain is free when nobody controls it and it is not searching for a ghost.
    /// </summary>
    public bool IsPositronicFree(EntityUid positronic)
    {
        if (HasMind(positronic))
            return false;

        return !_appearance.TryGetData<ToggleableGhostRoleStatus>(positronic, ToggleableGhostRoleVisuals.Status, out var status)
               || status == ToggleableGhostRoleStatus.Off;
    }

    public MindUploadSourceType GetSourceType(EntityUid? source)
    {
        if (HasComp<BrainComponent>(source))
            return MindUploadSourceType.Brain;

        if (HasComp<PAIComponent>(source))
            return MindUploadSourceType.Pai;

        return MindUploadSourceType.None;
    }

    public bool HasMind(EntityUid? uid)
    {
        return TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind;
    }
}
