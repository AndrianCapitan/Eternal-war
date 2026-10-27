using Content.Server.Power.EntitySystems;
using Content.Shared._EternalWar.MindUpload;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Power;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._EternalWar.MindUpload;

/// <inheritdoc/>
public sealed partial class MindUploadConsoleSystem : SharedMindUploadConsoleSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindUploadConsoleComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<MindUploadConsoleComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<MindUploadConsoleComponent, PowerChangedEvent>(OnPowerChanged);

        Subs.BuiEvents<MindUploadConsoleComponent>(MindUploadConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<MindUploadConsoleStartMessage>(OnStartMessage);
            subs.Event<MindUploadConsoleEjectPositronicMessage>(OnEjectPositronicMessage);
            subs.Event<MindUploadConsoleEjectSourceMessage>(OnEjectSourceMessage);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<MindUploadConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.UploadEnd is { } end && curTime >= end)
                FinishUpload((uid, comp));
        }
    }

    private void OnContainerChanged<T>(Entity<MindUploadConsoleComponent> ent, ref T args)
    {
        UpdateUi(ent);
    }

    private void OnUiOpened(Entity<MindUploadConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnPowerChanged(Entity<MindUploadConsoleComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered && ent.Comp.UploadEnd != null)
        {
            StopUpload(ent);
            _popup.PopupEntity(Loc.GetString("mind-upload-console-interrupted"), ent, PopupType.MediumCaution);
        }

        UpdateUi(ent);
    }

    private void OnStartMessage(Entity<MindUploadConsoleComponent> ent, ref MindUploadConsoleStartMessage args)
    {
        TryStartUpload(ent, args.Actor);
    }

    /// <summary>
    /// Starts uploading the mind of the inserted brain or personal AI into the inserted positronic brain.
    /// </summary>
    public bool TryStartUpload(Entity<MindUploadConsoleComponent> ent, EntityUid? user)
    {
        var (uid, comp) = ent;
        if (comp.UploadEnd != null || !this.IsPowered(uid, EntityManager))
            return false;

        if (!TryGetContents(ent, out var positronic, out var source, out var sourceType))
            return false;

        if (!IsPositronicFree(positronic) || !HasMind(source))
        {
            _audio.PlayPvs(comp.FailSound, uid);
            return false;
        }

        comp.UploadStart = _timing.CurTime;
        comp.UploadEnd = comp.UploadStart + (sourceType == MindUploadSourceType.Pai ? comp.PaiUploadTime : comp.BrainUploadTime);

        // Nothing may leave the console until the upload is done.
        ItemSlots.SetLock(uid, comp.PositronicSlotId, true);
        ItemSlots.SetLock(uid, comp.SourceSlotId, true);

        _audio.PlayPvs(comp.StartSound, uid);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} started uploading the mind of {ToPrettyString(source):source} into {ToPrettyString(positronic):positronic} using {ToPrettyString(uid):console}");

        UpdateUi(ent);
        return true;
    }

    private void OnEjectPositronicMessage(Entity<MindUploadConsoleComponent> ent, ref MindUploadConsoleEjectPositronicMessage args)
    {
        Eject(ent, ent.Comp.PositronicSlotId, args.Actor);
    }

    private void OnEjectSourceMessage(Entity<MindUploadConsoleComponent> ent, ref MindUploadConsoleEjectSourceMessage args)
    {
        Eject(ent, ent.Comp.SourceSlotId, args.Actor);
    }

    private void Eject(Entity<MindUploadConsoleComponent> ent, string slotId, EntityUid user)
    {
        if (ent.Comp.UploadEnd != null || !ItemSlots.TryGetSlot(ent.Owner, slotId, out var slot))
            return;

        ItemSlots.TryEjectToHands(ent, slot, user, true);
    }

    private void FinishUpload(Entity<MindUploadConsoleComponent> ent)
    {
        var (uid, comp) = ent;
        StopUpload(ent);

        // Re-check everything: the mind may have left the source while the upload was running.
        if (!TryGetContents(ent, out var positronic, out var source, out var sourceType)
            || !IsPositronicFree(positronic)
            || !_mind.TryGetMind(source, out var mindId, out var mind))
        {
            _audio.PlayPvs(comp.FailSound, uid);
            _popup.PopupEntity(Loc.GetString("mind-upload-console-failed"), uid, PopupType.MediumCaution);
            UpdateUi(ent);
            return;
        }

        _mind.TransferTo(mindId, positronic, mind: mind);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(uid):console} uploaded mind {ToPrettyString(mindId):mind} from {ToPrettyString(source):source} into {ToPrettyString(positronic):positronic}");

        // The organic brain is used up by the digitization. A personal AI simply ends up switched off
        // (its ghost role handling resets it once the mind leaves) and can be turned on again.
        if (sourceType == MindUploadSourceType.Brain)
            QueueDel(source);

        _audio.PlayPvs(comp.CompleteSound, uid);
        _popup.PopupEntity(Loc.GetString("mind-upload-console-complete"), uid, PopupType.Medium);
        UpdateUi(ent);
    }

    private void StopUpload(Entity<MindUploadConsoleComponent> ent)
    {
        ent.Comp.UploadStart = null;
        ent.Comp.UploadEnd = null;
        ItemSlots.SetLock(ent.Owner, ent.Comp.PositronicSlotId, false);
        ItemSlots.SetLock(ent.Owner, ent.Comp.SourceSlotId, false);
    }

    private bool TryGetContents(Entity<MindUploadConsoleComponent> ent,
        out EntityUid positronic,
        out EntityUid source,
        out MindUploadSourceType sourceType)
    {
        positronic = ItemSlots.GetItemOrNull(ent.Owner, ent.Comp.PositronicSlotId) ?? EntityUid.Invalid;
        source = ItemSlots.GetItemOrNull(ent.Owner, ent.Comp.SourceSlotId) ?? EntityUid.Invalid;
        sourceType = GetSourceType(source);

        return positronic.IsValid() && source.IsValid() && sourceType != MindUploadSourceType.None;
    }

    private void UpdateUi(Entity<MindUploadConsoleComponent> ent)
    {
        var positronic = ItemSlots.GetItemOrNull(ent.Owner, ent.Comp.PositronicSlotId);
        var source = ItemSlots.GetItemOrNull(ent.Owner, ent.Comp.SourceSlotId);

        var state = new MindUploadConsoleBoundUserInterfaceState(
            GetNetEntity(positronic),
            GetNetEntity(source),
            GetSourceType(source),
            HasMind(source),
            this.IsPowered(ent, EntityManager),
            ent.Comp.UploadStart,
            ent.Comp.UploadEnd);

        _ui.SetUiState(ent.Owner, MindUploadConsoleUiKey.Key, state);
    }
}
