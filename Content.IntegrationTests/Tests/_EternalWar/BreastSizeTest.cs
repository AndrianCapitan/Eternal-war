#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._EternalWar.BodyZones;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Breasts;
using Content.Shared.Body;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// A female human chooses her breast size in the character editor: a marking drawn over the torso, gone with the
/// skin of the chest, told on examine and taken away if she stops being female.
/// </summary>
[TestOf(typeof(BreastSizeSystem))]
public sealed class BreastSizeTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [SidedDependency(Side.Server)] private readonly BodyZonesSystem _zones = default!;
    [SidedDependency(Side.Server)] private readonly ExamineSystemShared _examine = default!;
    [SidedDependency(Side.Server)] private readonly HumanoidProfileSystem _humanoid = default!;
    [SidedDependency(Side.Server)] private readonly MarkingManager _markings = default!;
    [SidedDependency(Side.Server)] private readonly SharedVisualBodySystem _visualBody = default!;
    [SidedDependency(Side.Server)] private readonly ILocalizationManager _loc = default!;
    [SidedDependency(Side.Client)] private readonly SpriteSystem _sprite = default!;

    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";
    private const string Large = "EternalWarBreastsLarge";

    private static readonly string[] Sizes =
    {
        "EternalWarBreastsSmall",
        "EternalWarBreastsLarge",
        "EternalWarBreastsVeryLarge",
        "EternalWarBreastsHuge",
    };

    private static HumanoidCharacterAppearance Appearance(string size)
    {
        var markings = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>
        {
            [Torso] = new() { [HumanoidVisualLayers.Breasts] = new() { new Marking(size, 1) } },
        };
        return new(Color.Brown, Color.Beige, markings, null);
    }

    private List<Marking> TorsoBreasts(EntityUid body)
    {
        var torso = SEntMan.GetComponent<BodyComponent>(body).Organs!.ContainedEntities
            .Single(o => SEntMan.GetComponent<OrganComponent>(o).Category == Torso);
        var markings = SEntMan.GetComponent<VisualOrganMarkingsComponent>(torso).Markings;
        return markings.GetValueOrDefault(HumanoidVisualLayers.Breasts) ?? new();
    }

    [Test]
    public async Task OnlyFemaleHumansChooseASize()
    {
        await Server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(_markings.MarkingsByLayerAndGroupAndSex(HumanoidVisualLayers.Breasts, "Human", Sex.Female).Keys,
                    Is.EquivalentTo(Sizes));
                Assert.That(_markings.MarkingsByLayerAndGroupAndSex(HumanoidVisualLayers.Breasts, "Human", Sex.Male), Is.Empty);
                Assert.That(_markings.GetMarkingData("Human")[Torso].Layers, Does.Contain(HumanoidVisualLayers.Breasts));
                Assert.That(_markings.GetMarkingData("Human")[Torso].Group.Id, Is.EqualTo("Human"),
                    "The torso should keep the marking group of the human parts.");
                Assert.That(_markings.GetMarkingData("Dwarf")[Torso].Layers, Does.Not.Contain(HumanoidVisualLayers.Breasts),
                    "The sizes are for humans only.");

                var woman = HumanoidCharacterAppearance.EnsureValid(Appearance(Large), "Human", Sex.Female);
                Assert.That(woman.Markings[Torso][HumanoidVisualLayers.Breasts].Select(m => m.MarkingId.Id), Is.EqualTo(new[] { Large }));

                var man = HumanoidCharacterAppearance.EnsureValid(Appearance(Large), "Human", Sex.Male);
                Assert.That(man.Markings.GetValueOrDefault(Torso)?.GetValueOrDefault(HumanoidVisualLayers.Breasts), Is.Null.Or.Empty,
                    "A man's profile should not keep a breast size.");
            }
        });
    }

    [Test]
    public async Task SizeIsDrawnToldAndGoesWithTheSkin()
    {
        EntityUid woman = default;
        await Server.WaitPost(() =>
        {
            woman = SEntMan.SpawnEntity("MobHuman", SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            var profile = new HumanoidCharacterProfile
            {
                Name = "Anna Ivanova",
                Species = "Human",
                Age = 30,
                Appearance = Appearance(Large),
            }.WithSex(Sex.Female).WithGender(Gender.Female);
            _humanoid.ApplyProfileTo(woman, profile);
            _visualBody.ApplyProfileTo(woman, profile);
        });
        await RunTicks(5);

        var told = _loc.GetString("breast-size-examine-large");
        string Examine() => _examine.GetExamineText(woman, SPlayer).ToMarkup();

        await Server.WaitAssertion(() =>
        {
            Assert.That(TorsoBreasts(woman).Select(m => m.MarkingId.Id), Is.EqualTo(new[] { Large }));
            Assert.That(Examine(), Does.Contain(told));
        });

        var layerId = _markings.MarkingsByLayer(HumanoidVisualLayers.Breasts)[Large].Sprites[0].GetLayerID(Large);
        (bool Drawn, bool Visible, bool Between) Drawing()
        {
            var uid = CEntMan.GetEntity(SEntMan.GetNetEntity(woman));
            var sprite = (uid, CEntMan.GetComponent<SpriteComponent>(uid));
            if (!_sprite.LayerMapTryGet(sprite, layerId, out var index, false))
                return (false, false, false);

            _sprite.LayerMapTryGet(sprite, HumanoidVisualLayers.Breasts, out var breasts, false);
            _sprite.LayerMapTryGet(sprite, EternalWarHumanoidLayers.ExposedChest, out var exposedChest, false);
            return (true, sprite.Item2[index].Visible, breasts < index && index < exposedChest);
        }

        await Client.WaitAssertion(() =>
            Assert.That(Drawing(), Is.EqualTo((true, true, true)), "The size should be drawn over the chest and under its wounds."));

        // Skinned, the chest has no breasts to show.
        await Server.WaitPost(() => _zones.RemoveSkin((woman, SEntMan.GetComponent<BodyZonesComponent>(woman)), BodyZone.Chest));
        await RunTicks(5);
        await Client.WaitAssertion(() => Assert.That(Drawing().Visible, Is.False));

        // A man has no breast size.
        await Server.WaitPost(() => _humanoid.SetSex(woman, Sex.Male));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(TorsoBreasts(woman), Is.Empty);
            Assert.That(Examine(), Does.Not.Contain(told));
        });
        await Client.WaitAssertion(() => Assert.That(Drawing().Drawn, Is.False));
    }
}
