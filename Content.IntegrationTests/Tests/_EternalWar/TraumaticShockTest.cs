#nullable enable
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Gravity;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._EternalWar.Shock;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Gravity;
using Content.Shared.Jittering;
using Content.Shared.Movement.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// A lot of trauma in a short time puts a human into shock; painkillers keep it out, adrenaline ends it.
/// </summary>
[TestOf(typeof(TraumaticShockSystem))]
public sealed class TraumaticShockTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageable = default!;
    [SidedDependency(Side.Server)] private readonly AlertsSystem _alerts = default!;
    [SidedDependency(Side.Server)] private readonly GravitySystem _gravity = default!;
    [SidedDependency(Side.Server)] private readonly StatusEffectsSystem _statusEffects = default!;

    private EntityUid _victim;

    private TraumaticShockComponent Shock => SEntMan.GetComponent<TraumaticShockComponent>(_victim);

    private async Task SpawnVictim()
    {
        await AddAtmosphere();
        await Server.WaitPost(() =>
        {
            // The test grid has no gravity generator, and nobody falls down while weightless.
            var gravity = SEntMan.GetComponent<GravityComponent>(MapData.Grid.Owner);
            _gravity.EnableGravity(MapData.Grid.Owner, gravity);
            gravity.Inherent = true;

            _victim = SEntMan.SpawnAtPosition("MobHuman", SEntMan.GetCoordinates(TargetCoords));
        });
        await RunTicks(1);
    }

    private async Task Hurt(string type, float amount)
    {
        await Server.WaitPost(() =>
            _damageable.TryChangeDamage(_victim, new DamageSpecifier { DamageDict = { [type] = amount } }, ignoreResistances: true));
        await RunTicks(1);
    }

    [Test]
    public async Task BurstOfTraumaIsShock()
    {
        await SpawnVictim();
        var move = SEntMan.GetComponent<MovementSpeedModifierComponent>(_victim);
        var shockedWalk = 0f;

        await Hurt("Blunt", 40);

        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Shock.InShock);
                Assert.That(_alerts.IsShowingAlert(_victim, Shock.Alert));
                Assert.That(SEntMan.HasComponent<KnockedDownComponent>(_victim) || SEntMan.HasComponent<StunnedComponent>(_victim),
                    "Shock should knock down.");
                Assert.That(SEntMan.GetComponent<BlurryVisionComponent>(_victim).Magnitude, Is.GreaterThanOrEqualTo(Shock.Blur));
                Assert.That(SEntMan.HasComponent<JitteringComponent>(_victim));
                Assert.That(_statusEffects.HasStatusEffect(_victim, Shock.StutterEffect));
            }
        });

        // Back on its feet, still in shock.
        await RunSeconds((float) Shock.KnockdownTime.TotalSeconds + 2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Shock.InShock);
            Assert.That(SEntMan.HasComponent<KnockedDownComponent>(_victim), Is.False);
            shockedWalk = move.CurrentWalkSpeed;
        });

        // It wears off by itself. The damage stays, so the speed is compared with the speed after the shock.
        await RunSeconds((float) Shock.Duration.TotalSeconds);
        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Shock.InShock, Is.False);
                Assert.That(_alerts.IsShowingAlert(_victim, Shock.Alert), Is.False);
                Assert.That(shockedWalk, Is.EqualTo(move.CurrentWalkSpeed * Shock.SpeedModifier).Within(0.01f));
                Assert.That(SEntMan.HasComponent<BlurryVisionComponent>(_victim), Is.False);
            }
        });
    }

    [Test]
    public async Task HitsAddUpOnlyWhenClose()
    {
        await SpawnVictim();

        // Two hits far apart are not a shock.
        await Hurt("Blunt", 20);
        await RunSeconds((float) Shock.Window.TotalSeconds + 1);
        await Hurt("Blunt", 20);
        await Server.WaitAssertion(() => Assert.That(Shock.InShock, Is.False));

        // A third one soon after the second is.
        await RunSeconds(1);
        await Hurt("Heat", 20);
        await Server.WaitAssertion(() => Assert.That(Shock.InShock));
    }

    [Test]
    public async Task OnlyTraumaCounts()
    {
        await SpawnVictim();

        await Hurt("Asphyxiation", 60);
        await Hurt("Radiation", 60);
        await Server.WaitAssertion(() => Assert.That(Shock.InShock, Is.False));
    }

    [Test]
    public async Task PainkillersKeepOutOfShock()
    {
        await SpawnVictim();

        await Server.WaitPost(() =>
            _statusEffects.TryUpdateStatusEffectDuration(_victim, "StatusEffectPainNumbness", TimeSpan.FromMinutes(1)));
        await Hurt("Slash", 40);
        await Server.WaitAssertion(() => Assert.That(Shock.InShock, Is.False));
    }

    [Test]
    public async Task AdrenalineEndsShock()
    {
        await SpawnVictim();

        await Hurt("Piercing", 40);
        await Server.WaitAssertion(() => Assert.That(Shock.InShock));

        await Server.WaitPost(() =>
            _statusEffects.TryUpdateStatusEffectDuration(_victim, "StatusEffectAdrenaline", TimeSpan.FromMinutes(1)));
        await RunTicks(2);
        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Shock.InShock, Is.False);
                Assert.That(_alerts.IsShowingAlert(_victim, Shock.Alert), Is.False);
                Assert.That(SEntMan.HasComponent<JitteringComponent>(_victim), Is.False);
                Assert.That(_statusEffects.HasStatusEffect(_victim, Shock.StutterEffect), Is.False);
            }
        });

        // And keeps it from coming back.
        await Hurt("Piercing", 40);
        await Server.WaitAssertion(() => Assert.That(Shock.InShock, Is.False));
    }
}
