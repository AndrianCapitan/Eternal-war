#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.Underwear;
using Content.Shared._EternalWar.Underwear;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// A player spawns wearing underwear items in their own slots instead of underwear drawn on the body, and can
/// take them off, but not over the jumpsuit.
/// </summary>
[TestOf(typeof(UnderwearDressingSystem))]
public sealed class UnderwearTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly UnderwearDressingSystem _dressing = default!;
    [SidedDependency(Side.Server)] private readonly HumanoidProfileSystem _humanoid = default!;
    [SidedDependency(Side.Server)] private readonly InventorySystem _inventory = default!;
    [SidedDependency(Side.Server)] private readonly SharedVisualBodySystem _visualBody = default!;
    [SidedDependency(Side.Client)] private readonly SpriteSystem _sprite = default!;

    private const string Top = "underwearTop";
    private const string Bottom = "underwearBottom";

    private string? Worn(string slot)
    {
        return _inventory.TryGetSlotEntity(SPlayer, slot, out var item)
            ? SEntMan.GetComponent<MetaDataComponent>(item.Value).EntityPrototype?.ID
            : null;
    }

    private void Wear(Sex sex, params string[] markings)
    {
        var chosen = new Dictionary<HumanoidVisualLayers, List<Marking>>();
        foreach (var marking in markings)
        {
            var layer = marking.StartsWith("UndergarmentTop") ? HumanoidVisualLayers.UndergarmentTop : HumanoidVisualLayers.UndergarmentBottom;
            chosen.GetOrNew(layer).Add(new Marking(marking, new[] { Color.White }));
        }

        var profile = new HumanoidCharacterProfile
        {
            Name = "Anna Ivanova",
            Species = "Human",
            Age = 30,
            Appearance = new(Color.Brown, Color.Beige,
                new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> { ["Torso"] = chosen }, null),
        }.WithSex(sex);

        _humanoid.ApplyProfileTo(SPlayer, profile);
        _visualBody.ApplyProfileTo(SPlayer, profile);
        _dressing.Dress((SPlayer, SEntMan.GetComponent<UnderwearWearerComponent>(SPlayer)));
    }

    private List<Marking> BodyUnderwear()
    {
        Assert.That(_visualBody.TryGatherMarkingsData(SPlayer, null, out _, out _, out var applied));
        return applied!.Values
            .SelectMany(l => l.Where(kv => kv.Key is HumanoidVisualLayers.UndergarmentTop or HumanoidVisualLayers.UndergarmentBottom))
            .SelectMany(kv => kv.Value)
            .ToList();
    }

    [Test]
    public async Task ChosenUnderwearIsWornAsItems()
    {
        await Server.WaitAssertion(() =>
        {
            // A sports bra chosen in the editor, the bottom left to the default.
            Wear(Sex.Female, "UndergarmentTopSportsbra");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(Worn(Top), Is.EqualTo("EternalWarUnderwearSportsBra"));
                Assert.That(Worn(Bottom), Is.EqualTo("EternalWarUnderwearBriefs"), "A woman's default bottom.");
                Assert.That(BodyUnderwear(), Is.Empty, "The underwear should move off the body into the slots.");
            }
        });

        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var uid = CEntMan.GetEntity(SEntMan.GetNetEntity(SPlayer));
            var sprite = (uid, CEntMan.GetComponent<SpriteComponent>(uid));
            Assert.That(_sprite.LayerMapTryGet(sprite, Top, out var index, false));
            Assert.That(sprite.Item2[index + 1].RsiState.Name, Is.EqualTo("sports-equipped"), "The bra should be drawn right over its slot's place.");
        });
    }

    [Test]
    public async Task ManWearsBoxersByDefault()
    {
        await Server.WaitAssertion(() =>
        {
            Wear(Sex.Male);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(Worn(Top), Is.Null);
                Assert.That(Worn(Bottom), Is.EqualTo("EternalWarUnderwearBoxers"));
            }
        });
    }

    [Test]
    public async Task UnderwearComesOffOnlyWithoutTheJumpsuit()
    {
        await Server.WaitAssertion(() => Wear(Sex.Female));
        await Server.WaitAssertion(() =>
        {
            var jumpsuit = SEntMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            Assert.That(_inventory.TryEquip(SPlayer, jumpsuit, "jumpsuit", silent: true));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(_inventory.TryUnequip(SPlayer, Bottom, silent: true), Is.False, "Not from under the jumpsuit.");
                Assert.That(_inventory.TryUnequip(SPlayer, Top, silent: true), Is.False, "Not from under the jumpsuit.");
            }

            Assert.That(_inventory.TryUnequip(SPlayer, "jumpsuit", silent: true));
            Assert.That(_inventory.TryUnequip(SPlayer, Top, out var bra, silent: true));

            // Over the jumpsuit it doesn't go back on either.
            var again = SEntMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            Assert.That(_inventory.TryEquip(SPlayer, again, "jumpsuit", silent: true));
            Assert.That(_inventory.TryEquip(SPlayer, bra!.Value, Top, silent: true), Is.False);
        });
    }
}
