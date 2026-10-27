using Content.Shared._EternalWar.Genetics;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;

namespace Content.Server._EternalWar.Genetics;

/// <inheritdoc/>
public sealed partial class GenomeScannerSystem : SharedGenomeScannerSystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private GenomeSystem _genome = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenomeScannerComponent, AfterInteractEvent>(OnAfterInteract);

        Subs.BuiEvents<GenomeScannerComponent>(GenomeScannerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<GenomeScannerApplyMessage>(OnApply);
        });
    }

    private void OnAfterInteract(Entity<GenomeScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!Admin.IsAdmin(args.User))
            return;

        args.Handled = true;

        ent.Comp.Target = target;
        _ui.OpenUi(ent.Owner, GenomeScannerUiKey.Key, args.User);
        UpdateUi(ent);
    }

    private void OnUiOpened(Entity<GenomeScannerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnApply(Entity<GenomeScannerComponent> ent, ref GenomeScannerApplyMessage args)
    {
        // The interface is only a view: the server checks everything again.
        if (!Admin.IsAdmin(args.Actor)
            || args.Blocks.Length != Genome.BlockCount
            || ent.Comp.Target is not { } target
            || TerminatingOrDeleted(target)
            || !_genome.TryGetGenome(target, out var genome))
        {
            return;
        }

        var before = Genome.FormatCode(genome.Comp.Blocks);
        ent.Comp.Target = _genome.SetGenome(genome, args.Blocks);

        _adminLogger.Add(LogType.Action,
            LogImpact.High,
            $"{ToPrettyString(args.Actor):user} changed the genome of {ToPrettyString(genome):target} from {before} to {Genome.FormatCode(genome.Comp.Blocks)}");

        UpdateUi(ent);
    }

    private void UpdateUi(Entity<GenomeScannerComponent> ent)
    {
        if (ent.Comp.Target is not { } target || TerminatingOrDeleted(target))
        {
            _ui.SetUiState(ent.Owner, GenomeScannerUiKey.Key, new GenomeScannerBoundUserInterfaceState(null, string.Empty, null));
            return;
        }

        int[]? blocks = null;
        if (_genome.TryGetGenome(target, out var genome))
            blocks = (int[]) genome.Comp.Blocks.Clone();

        _ui.SetUiState(ent.Owner,
            GenomeScannerUiKey.Key,
            new GenomeScannerBoundUserInterfaceState(GetNetEntity(target), Name(target), blocks));
    }
}
