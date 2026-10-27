#nullable enable
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared._EternalWar.Movement;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// Walking is the default, running is on Shift and costs stamina, food and water.
/// </summary>
[TestOf(typeof(SprintCostSystem))]
public sealed class SprintCostTest : MovementTest
{
    protected override int Tiles => 8;

    [SidedDependency(Side.Server)] private readonly SatiationSystem _satiation = default!;
    [SidedDependency(Side.Server)] private readonly SharedStaminaSystem _stamina = default!;

    private (float Hunger, float Thirst) Satiation()
    {
        var satiation = SEntMan.GetComponent<SatiationComponent>(SPlayer);
        return (_satiation.GetValueOrNull((SPlayer, satiation), SatiationSystem.Hunger)!.Value,
            _satiation.GetValueOrNull((SPlayer, satiation), SatiationSystem.Thirst)!.Value);
    }

    [Test]
    public async Task RunningCostsStaminaFoodAndWater()
    {
        var stamina = SEntMan.GetComponent<StaminaComponent>(SPlayer);
        var mover = SEntMan.GetComponent<InputMoverComponent>(SPlayer);
        Assert.That(mover.Sprinting, Is.False, "Walking should be the default.");

        // Walking is free.
        var before = Satiation();
        await Move(DirectionFlag.East, 2f, run: false);
        await RunSeconds(1);
        var walked = Satiation();
        Assert.That(stamina.StaminaDamage, Is.Zero, "Walking should not tire.");

        // Running is faster and costs stamina, a little food and more water.
        var start = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates.X;
        await Move(DirectionFlag.West, 2f);
        await RunSeconds(1);
        var ran = Satiation();
        var distance = start - SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates.X;

        var cost = SEntMan.GetComponent<SprintCostComponent>(SPlayer);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(distance, Is.GreaterThan(6f), "Running should cover more than walking would.");
            Assert.That(stamina.StaminaDamage, Is.GreaterThan(cost.StaminaPerSecond), "Running should tire.");
            Assert.That(walked.Thirst - ran.Thirst - (before.Thirst - walked.Thirst), Is.GreaterThan(cost.ThirstPerSecond),
                "Running should cost water.");
            Assert.That(walked.Hunger - ran.Hunger - (before.Hunger - walked.Hunger), Is.GreaterThan(cost.HungerPerSecond),
                "Running should cost food.");
        }
    }

    [Test]
    public async Task OutOfBreathOnlyWalks()
    {
        var stamina = SEntMan.GetComponent<StaminaComponent>(SPlayer);
        var cost = SEntMan.GetComponent<SprintCostComponent>(SPlayer);
        var move = SEntMan.GetComponent<MovementSpeedModifierComponent>(SPlayer);

        await Server.WaitPost(() => _stamina.TakeStaminaDamage(SPlayer, stamina.CritThreshold * cost.ExhaustedAt - 2f, stamina));
        await Move(DirectionFlag.West, 2f);
        await RunSeconds(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cost.Exhausted, Is.True);
            Assert.That(stamina.Critical, Is.False, "Running out of breath should not knock down.");
            Assert.That(move.CurrentSprintSpeed, Is.EqualTo(move.CurrentWalkSpeed).Within(0.01f));
        }

        // Catching breath lets it run again.
        await RunSeconds(30);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cost.Exhausted, Is.False);
            Assert.That(move.CurrentSprintSpeed, Is.GreaterThan(move.CurrentWalkSpeed));
        }
    }
}
