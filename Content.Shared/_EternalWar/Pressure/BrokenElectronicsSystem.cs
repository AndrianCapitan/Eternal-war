using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;

namespace Content.Shared._EternalWar.Pressure;

/// <summary>
/// Keeps broken electronics from working and repairs them, see <see cref="BrokenElectronicsComponent"/>.
/// </summary>
public sealed partial class BrokenElectronicsSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrokenElectronicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BrokenElectronicsComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<BrokenElectronicsComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        SubscribeLocalEvent<BrokenElectronicsComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BrokenElectronicsComponent, RepairElectronicsDoAfterEvent>(OnRepaired);
        SubscribeLocalEvent<BrokenElectronicsComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// Breaks the electronics of a device. Whatever was on goes off.
    /// </summary>
    public void Break(EntityUid uid)
    {
        if (HasComp<BrokenElectronicsComponent>(uid))
            return;

        AddComp<BrokenElectronicsComponent>(uid);
        Dirty(uid, Comp<BrokenElectronicsComponent>(uid));
    }

    private void OnStartup(Entity<BrokenElectronicsComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<ItemToggleComponent>(ent, out var toggle) && toggle.Activated)
            _toggle.TryDeactivate((ent, toggle), predicted: false, showPopup: false);
    }

    private void OnActivateAttempt(Entity<BrokenElectronicsComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        args.Cancelled = true;
        args.Popup = Loc.GetString("broken-electronics-dead", ("item", ent.Owner));
    }

    private void OnUIOpenAttempt(EntityUid uid, BrokenElectronicsComponent comp, ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("broken-electronics-dead", ("item", uid)), uid, args.User);
    }

    private void OnInteractUsing(Entity<BrokenElectronicsComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, ent.Comp.RepairQuality))
            return;

        args.Handled = _tool.UseTool(args.Used,
            args.User,
            ent,
            (float) ent.Comp.RepairTime.TotalSeconds,
            ent.Comp.RepairQuality,
            new RepairElectronicsDoAfterEvent());
    }

    private void OnRepaired(Entity<BrokenElectronicsComponent> ent, ref RepairElectronicsDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        RemComp<BrokenElectronicsComponent>(ent);
        _popup.PopupClient(Loc.GetString("broken-electronics-repaired", ("item", ent.Owner)), ent, args.User);
    }

    private void OnExamined(Entity<BrokenElectronicsComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("broken-electronics-examine"));
    }
}
