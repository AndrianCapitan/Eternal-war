#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.BodyZones;
using Content.Server._EternalWar.Hygiene;
using Content.Server.Decals;
using Content.Server.Gravity;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Hygiene;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// Blood on clothes, footprints after puddles, the shower.
/// </summary>
[TestOf(typeof(HygieneSystem))]
public sealed class HygieneTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly BodyZonesSystem _zones = default!;
    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageable = default!;
    [SidedDependency(Side.Server)] private readonly DecalSystem _decals = default!;
    [SidedDependency(Side.Server)] private readonly GravitySystem _gravity = default!;
    [SidedDependency(Side.Server)] private readonly HygieneSystem _hygiene = default!;
    [SidedDependency(Side.Server)] private readonly InventorySystem _inventory = default!;
    [SidedDependency(Side.Server)] private readonly SharedPuddleSystem _puddle = default!;

    private EntityUid _victim;
    private EntityUid _suit;
    private EntityUid _shoes;

    /// <summary>
    /// The middle of a tile in the row of the target, relative to the grid (puddles and decals live on the grid).
    /// </summary>
    private EntityCoordinates Tile(int x)
    {
        return Transform.WithEntityId(SEntMan.GetCoordinates(TargetCoords).Offset(new Vector2(x, 0)), MapData.Grid.Owner);
    }

    private Vector2 GridPosition(int x)
    {
        return Tile(x).Position;
    }

    private async Task SpawnDressedVictim()
    {
        await AddAtmosphere();
        await Server.WaitPost(() =>
        {
            _victim = SEntMan.SpawnAtPosition("MobHuman", Tile(0));
            _suit = SEntMan.SpawnAtPosition("ClothingUniformJumpsuitColorGrey", Tile(0));
            _shoes = SEntMan.SpawnAtPosition("ClothingShoesColorBlack", Tile(0));
            Assert.That(_inventory.TryEquip(_victim, _suit, "jumpsuit", silent: true, force: true));
            Assert.That(_inventory.TryEquip(_victim, _shoes, "shoes", silent: true, force: true));
        });
        await RunTicks(5);
    }

    private bool ClientHasLayer(EntityUid uid, string key)
    {
        var sprite = CEntMan.GetComponent<SpriteComponent>(CEntMan.GetEntity(SEntMan.GetNetEntity(uid)));
        return CEntMan.System<SpriteSystem>().LayerMapTryGet((sprite.Owner, sprite), key, out var index, false)
               && sprite[index].Visible;
    }

    [Test]
    public async Task BleedingStainsTheClothesOverTheWoundAndTheShowerWashesThem()
    {
        await SpawnDressedVictim();

        // A cut across the chest: the jumpsuit over it gets bloody, the shoes don't.
        await Server.WaitPost(() =>
        {
            _zones.SetTarget(SPlayer, BodyZone.Chest);
            var slash = new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>("Slash"), FixedPoint2.New(20));
            _damageable.TryChangeDamage(_victim, slash, origin: SPlayer);
        });
        await RunSeconds(4);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.TryGetComponent<BloodStainedComponent>(_suit, out var stained) && stained.Amount > 0);
            Assert.That(SEntMan.HasComponent<BloodStainedComponent>(_shoes), Is.False);
        });
        await RunTicks(5);
        await Client.WaitAssertion(() => Assert.That(ClientHasLayer(_victim, "jumpsuit-blood"), "the stain shows on the wearer"));

        // Stop the bleeding and stand under the shower.
        await Server.WaitPost(() =>
        {
            SEntMan.System<Content.Shared.Body.Systems.BloodstreamSystem>().TryModifyBleedAmount(_victim, -100);
            var shower = SEntMan.SpawnAtPosition("Shower", Tile(0));
            _hygiene.SetShower((shower, SEntMan.GetComponent<ShowerComponent>(shower)), true);
        });
        await RunSeconds(6);

        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<BloodStainedComponent>(_suit), Is.False, "washed clean"));
        await RunTicks(5);
        await Client.WaitAssertion(() => Assert.That(ClientHasLayer(_victim, "jumpsuit-blood"), Is.False));
    }

    [Test]
    public async Task WalkingThroughBloodLeavesFootprints()
    {
        for (var x = 1; x <= 4; x++)
        {
            await SetTile(Plating, SEntMan.GetNetCoordinates(Tile(x)), MapData.Grid);
        }

        await SpawnDressedVictim();
        await Server.WaitPost(() =>
        {
            // The test grid has no gravity generator: without gravity nobody walks.
            var gravity = SEntMan.GetComponent<GravityComponent>(MapData.Grid.Owner);
            _gravity.EnableGravity(MapData.Grid.Owner, gravity);
            gravity.Inherent = true;

            Assert.That(_puddle.TrySpillAt(Tile(1), new Solution("Blood", FixedPoint2.New(20)), out _, sound: false));
        });
        await RunTicks(5);

        // Step into the blood, then walk on.
        for (var x = 1; x <= 4; x++)
        {
            await Server.WaitPost(() => Transform.SetCoordinates(_victim, Tile(x)));
            await RunTicks(2);
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.TryGetComponent<BloodStainedComponent>(_shoes, out var shoes) && shoes.Amount > 0, "blood on the shoes");
            Assert.That(SEntMan.GetComponent<FootprintsComponent>(_victim).PrintsLeft, Is.EqualTo(9), "12 prints, 3 used");

            for (var x = 2; x <= 4; x++)
            {
                var prints = _decals.GetDecalsInRange(MapData.Grid.Owner, GridPosition(x), 0.9f)
                    .Count(d => d.Decal.Id.StartsWith("FootprintShoe"));
                Assert.That(prints, Is.EqualTo(1), $"a shoe print on tile {x}");
            }

            Assert.That(_decals.GetDecalsInRange(MapData.Grid.Owner, GridPosition(1), 0.4f)
                .Count(d => d.Decal.Id.StartsWith("Footprint")), Is.Zero, "none in the puddle itself");
        });

        // The shower washes the shoes and the feet stop leaving prints.
        await Server.WaitPost(() => _hygiene.WashEntity(_victim, 1f));
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<BloodStainedComponent>(_shoes), Is.False);
            Assert.That(SEntMan.GetComponent<FootprintsComponent>(_victim).PrintsLeft, Is.Zero);
        });
    }
}
