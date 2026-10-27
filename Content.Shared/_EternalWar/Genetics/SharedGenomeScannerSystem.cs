using Content.Shared.Administration.Managers;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Popups;

namespace Content.Shared._EternalWar.Genetics;

/// <summary>
/// Keeps the genome scanner out of non-admin hands.
/// </summary>
public abstract partial class SharedGenomeScannerSystem : EntitySystem
{
    [Dependency] protected ISharedAdminManager Admin = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenomeScannerComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<GenomeScannerComponent, ExaminedEvent>(OnExamined);
    }

    private void OnPickupAttempt(Entity<GenomeScannerComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (Admin.IsAdmin(args.User))
            return;

        args.Cancel();
        if (args.ShowPopup)
            _popup.PopupClient(Loc.GetString("genome-scanner-not-admin"), ent, args.User);
    }

    private void OnExamined(Entity<GenomeScannerComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("genome-scanner-examine"));
    }
}
