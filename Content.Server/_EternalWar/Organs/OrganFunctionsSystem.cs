using Content.Server._EternalWar.BodyZones;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Organs;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._EternalWar.Organs;

/// <summary>
/// The organs at work: speech, hearing, poison, digestion, the appendix and the blood. See <see cref="OrganFunctionsComponent"/>.
/// Which organs a body has and whether they failed comes from <see cref="BodyZonesSystem"/>.
/// </summary>
public sealed partial class OrganFunctionsSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private BodyZonesSystem _zones = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private VomitSystem _vomit = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganFunctionsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OrganFunctionsComponent, RejuvenateEvent>(OnRejuvenate);

        InitializeSenses();
        InitializePoison();
    }

    private void OnMapInit(Entity<OrganFunctionsComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        ent.Comp.NextUpdate = now + ent.Comp.UpdateInterval;

#pragma warning disable CS0618 // Solutions on the body, like the bloodstream's.
        _solutions.EnsureSolution(ent.Owner, ent.Comp.LiverSolution, out var liver);
        _solutions.EnsureSolution(ent.Owner, ent.Comp.IntestinesSolution, out var intestines);
        _solutions.EnsureSolution(ent.Owner, ent.Comp.AppendixSolution, out var appendix);
#pragma warning restore CS0618
        liver.Comp.Solution.MaxVolume = ent.Comp.LiverVolume;
        intestines.Comp.Solution.MaxVolume = ent.Comp.IntestinesVolume;
        appendix.Comp.Solution.MaxVolume = ent.Comp.AppendixVolume;

        // The spleen makes the blood now, not the body itself.
        if (TryComp<BloodstreamComponent>(ent, out var bloodstream))
        {
            ent.Comp.SpleenBloodRefresh ??= bloodstream.BloodRefreshAmount;
            _bloodstream.ChangeBloodRefreshAmount((ent.Owner, bloodstream), 0);
            ent.Comp.NextSpleenUpdate = now + bloodstream.AdjustedUpdateInterval;
        }
    }

    private void OnRejuvenate(Entity<OrganFunctionsComponent> ent, ref RejuvenateEvent args)
    {
        foreach (var name in new[] { ent.Comp.LiverSolution, ent.Comp.IntestinesSolution, ent.Comp.AppendixSolution })
        {
            if (_solutions.TryGetSolution(ent.Owner, name, out var solution))
                _solutions.RemoveAllSolution(solution.Value);
        }

        SetAppendicitis(ent, false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<OrganFunctionsComponent, BodyZonesComponent>();
        while (query.MoveNext(out var uid, out var comp, out var zones))
        {
            Entity<OrganFunctionsComponent> ent = (uid, comp);
            Entity<BodyZonesComponent> body = (uid, zones);

            if (now >= comp.NextSpleenUpdate)
                UpdateSpleen(ent, body, now);

            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.UpdateInterval;

            // A dead body neither hears nor digests: it is left as it is until it is revived.
            if (_mobState.IsDead(uid))
                continue;

            UpdateHearing(ent, body);
            RefillDetox(ent, body);
            UpdateDigestion(ent, body);
            UpdateAppendix(ent, body, now);
        }
    }

    /// <summary>
    /// The spleen makes the blood the body used to regenerate by itself.
    /// </summary>
    private void UpdateSpleen(Entity<OrganFunctionsComponent> ent, Entity<BodyZonesComponent> body, TimeSpan now)
    {
        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream))
        {
            ent.Comp.NextSpleenUpdate = TimeSpan.MaxValue;
            return;
        }

        ent.Comp.NextSpleenUpdate = now + bloodstream.AdjustedUpdateInterval;

        if (_mobState.IsDead(ent) || HasComp<ZombieComponent>(ent) || !_zones.HasWorkingOrgan(body, ent.Comp.Spleen))
            return;

        if (ent.Comp.SpleenBloodRefresh is { } refresh && refresh > FixedPoint2.Zero)
            _bloodstream.TryRegulateBloodLevel((ent.Owner, bloodstream), refresh);
    }
}
