#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._EternalWar.Genetics;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Genetics;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Item;
using Content.Shared.Polymorph;
using Content.Shared.Preferences;
using Content.Shared.Speech.Components;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._EternalWar;

[TestOf(typeof(GenomeSystem))]
public sealed class GenomeTest : GameTest
{
    private const string Human = "MobHuman";
    private const string Scanner = "AdminGenomeScanner";
    private static readonly ProtoId<MarkingPrototype> Hair = "HumanHairAfro";
    private static readonly ProtoId<MarkingPrototype> Beard = "HumanFacialHairAbe";

    [SidedDependency(Side.Server)] private readonly GenomeSystem _genome = default!;
    [SidedDependency(Side.Server)] private readonly HumanoidProfileSystem _humanoid = default!;
    [SidedDependency(Side.Server)] private readonly SharedVisualBodySystem _visualBody = default!;
    [SidedDependency(Side.Server)] private readonly SharedContainerSystem _container = default!;

    /// <summary>
    /// A human with a random look, plus hair and a beard of the given colors.
    /// </summary>
    private EntityUid SpawnHuman(Sex sex, Color hair, Color beard, EntityCoordinates? at = null)
    {
        var uid = at is { } coords ? SSpawnAtPosition(Human, coords) : SSpawn(Human);
        var profile = HumanoidCharacterProfile.RandomWithSpecies(GenomeSystem.Human).WithSex(sex);
        _humanoid.ApplyProfileTo(uid, profile);
        _visualBody.ApplyProfileTo(uid, profile);

        Assert.That(_visualBody.TryGatherMarkingsData(uid, null, out _, out var markingData, out _));
        var markings = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();
        foreach (var (category, data) in markingData!)
        {
            if (data.Layers.Contains(HumanoidVisualLayers.Hair))
                markings.GetOrNew(category)[HumanoidVisualLayers.Hair] = new() { new Marking(Hair, new[] { hair }) };
            if (data.Layers.Contains(HumanoidVisualLayers.FacialHair))
                markings.GetOrNew(category)[HumanoidVisualLayers.FacialHair] = new() { new Marking(Beard, new[] { beard }) };
        }
        _visualBody.ApplyMarkings(uid, markings);

        return uid;
    }

    private IEnumerable<VisualOrganComponent> VisualOrgans(EntityUid body)
    {
        return _container.GetContainer(body, BodyComponent.ContainerID).ContainedEntities
            .Where(SEntMan.HasComponent<VisualOrganComponent>)
            .Select(SEntMan.GetComponent<VisualOrganComponent>);
    }

    private List<Marking> Markings(EntityUid body, HumanoidVisualLayers layer)
    {
        Assert.That(_visualBody.TryGatherMarkingsData(body, null, out _, out _, out var applied));
        return applied!.Values.SelectMany(l => l.TryGetValue(layer, out var m) ? m : new List<Marking>()).ToList();
    }

    private static void AssertColor(Color actual, Color expected, string what)
    {
        Assert.That(Genome.FormatCode(Blocks(actual)), Is.EqualTo(Genome.FormatCode(Blocks(expected))), what);
    }

    private static int[] Blocks(Color color)
    {
        var blocks = new int[Genome.BlockCount];
        Genome.SetColor(blocks, GenomeBlock.SkinR, color);
        return blocks;
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void HexFormat()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Genome.Format(GenomeBlock.Sex, Genome.Male), Is.EqualTo("535"));
            Assert.That(Genome.Format(GenomeBlock.SkinR, 0xA), Is.EqualTo("0A"));

            Assert.That(Genome.TryParse(GenomeBlock.Sentience, "1ff", out var value) && value == 0x1FF);
            Assert.That(Genome.TryParse(GenomeBlock.Race, "201", out _), Is.False, "race can't go above 200 yet");
            Assert.That(Genome.TryParse(GenomeBlock.Sex, "801", out _), Is.False);
            Assert.That(Genome.TryParse(GenomeBlock.SkinR, "100", out _), Is.False, "a color byte is two digits");
            Assert.That(Genome.TryParse(GenomeBlock.SkinR, "G0", out _), Is.False);
            Assert.That(Genome.TryParse(GenomeBlock.SkinR, "", out _), Is.False);

            Assert.That(Genome.GetSex(0x534), Is.EqualTo(Sex.Female));
            Assert.That(Genome.GetSex(0x535), Is.EqualTo(Sex.Male));
            Assert.That(Genome.GetSex(0x536), Is.EqualTo(Sex.Unsexed));
            Assert.That(Genome.GetSex(0x800), Is.EqualTo(Sex.Unsexed));

            Assert.That(Genome.IsSentient(0x1FF), Is.False);
            Assert.That(Genome.IsSentient(0x200));
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void RacesCoverTheWholeBlock()
    {
        Assert.Multiple(() =>
        {
            for (var value = 0; value <= Genome.RaceMax; value++)
            {
                var races = SProtoMan.EnumeratePrototypes<GenomeRacePrototype>().Where(r => r.Contains(value)).ToList();
                Assert.That(races, Has.Count.EqualTo(1), $"race value {value:X} must belong to exactly one race");
            }

            foreach (var race in SProtoMan.EnumeratePrototypes<GenomeRacePrototype>())
            {
                if (race.Polymorph is { } polymorph)
                    Assert.That(SProtoMan.HasIndex(polymorph), $"{race.ID} turns into unknown polymorph {polymorph}");

                // Animals live below 100, humans from 100.
                Assert.That(race.Polymorph == null, Is.EqualTo(race.FromValue >= Genome.HumanRaceMin), race.ID);
            }
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void GenomeMatchesAppearance()
    {
        var hair = Color.FromHex("#A0522D");
        var beard = Color.FromHex("#202020");
        var uid = SpawnHuman(Sex.Male, hair, beard);
        var organ = VisualOrgans(uid).First();

        Assert.That(_genome.TryGetGenome(uid, out var genome));
        var blocks = genome.Comp.Blocks;

        Assert.Multiple(() =>
        {
            Assert.That(Genome.IsSentient(blocks[(int) GenomeBlock.Sentience]));
            Assert.That(Genome.TryGetRace(SProtoMan, blocks[(int) GenomeBlock.Race], out var race) && race.Polymorph == null,
                "a human must have a human race value");
            Assert.That(blocks[(int) GenomeBlock.Sex], Is.EqualTo(Genome.Male));
            AssertColor(Genome.GetColor(blocks, GenomeBlock.SkinR), organ.Profile.SkinColor, "skin");
            AssertColor(Genome.GetColor(blocks, GenomeBlock.EyeR), organ.Profile.EyeColor, "eyes");
            AssertColor(Genome.GetColor(blocks, GenomeBlock.RightEyeR), organ.Profile.EyeColor, "no heterochromia: the right eye is the same");
            AssertColor(Genome.GetColor(blocks, GenomeBlock.HairR), hair, "hair");
            AssertColor(Genome.GetColor(blocks, GenomeBlock.BeardR), beard, "beard");
        });

        // Not human: nothing to read.
        Assert.That(_genome.TryGetGenome(SSpawn("MobMouse"), out _), Is.False);
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void HeterochromiaIsInTheGenome()
    {
        var eyes = Color.FromHex("#4B3621");
        var rightEye = Color.FromHex("#3070C0");
        var uid = SSpawn(Human);
        var profile = HumanoidCharacterProfile.RandomWithSpecies(GenomeSystem.Human);
        profile = profile.WithCharacterAppearance(profile.Appearance.WithEyeColor(eyes).WithRightEyeColor(rightEye));
        _humanoid.ApplyProfileTo(uid, profile);
        _visualBody.ApplyProfileTo(uid, profile);

        Assert.That(_genome.TryGetGenome(uid, out var genome));
        Assert.Multiple(() =>
        {
            AssertColor(Genome.GetColor(genome.Comp.Blocks, GenomeBlock.EyeR), eyes, "left eye");
            AssertColor(Genome.GetColor(genome.Comp.Blocks, GenomeBlock.RightEyeR), rightEye, "right eye");
        });

        // The same blocks for both eyes take the heterochromia away.
        var blocks = (int[]) genome.Comp.Blocks.Clone();
        Genome.SetColor(blocks, GenomeBlock.RightEyeR, eyes);
        _genome.SetGenome(genome, blocks);

        foreach (var organ in VisualOrgans(uid))
        {
            var layer = organ.Layer;
            if (layer.Equals(EternalWarHumanoidLayers.RightEye))
            {
                Assert.That(organ.Profile.RightEyeColor, Is.Null);
                AssertColor(organ.Data.Color!.Value, eyes, "right eye");
            }
        }
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void EditedGenomeChangesTheBody()
    {
        var uid = SpawnHuman(Sex.Male, Color.Black, Color.Black);
        Assert.That(_genome.TryGetGenome(uid, out var genome));

        var skin = Color.FromHex("#3070C0");
        var eyes = Color.FromHex("#20E040");
        var rightEye = Color.FromHex("#C04020");
        var hair = Color.FromHex("#FF8000");
        var beard = Color.FromHex("#FFFFFF");

        var blocks = (int[]) genome.Comp.Blocks.Clone();
        blocks[(int) GenomeBlock.Sentience] = 0x1FF;
        blocks[(int) GenomeBlock.Sex] = 0x534;
        Genome.SetColor(blocks, GenomeBlock.SkinR, skin);
        Genome.SetColor(blocks, GenomeBlock.EyeR, eyes);
        Genome.SetColor(blocks, GenomeBlock.RightEyeR, rightEye);
        Genome.SetColor(blocks, GenomeBlock.HairR, hair);
        Genome.SetColor(blocks, GenomeBlock.BeardR, beard);

        Assert.That(_genome.SetGenome(genome, blocks), Is.EqualTo(uid));

        var profile = SEntMan.GetComponent<HumanoidProfileComponent>(uid);
        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<MonkeyAccentComponent>(uid), "below 200 the human can't talk");
            Assert.That(profile.Sex, Is.EqualTo(Sex.Female));
            Assert.That(profile.Gender, Is.EqualTo(Gender.Female));

            foreach (var organ in VisualOrgans(uid))
            {
                var layer = organ.Layer;
                // Different eye blocks give heterochromia.
                var expected = layer.Equals(HumanoidVisualLayers.Eyes) ? eyes
                    : layer.Equals(EternalWarHumanoidLayers.RightEye) ? rightEye
                    : skin;
                AssertColor(organ.Data.Color!.Value, expected, $"organ layer {layer}");
                Assert.That(organ.Profile.Sex, Is.EqualTo(Sex.Female));
            }

            var hairMarkings = Markings(uid, HumanoidVisualLayers.Hair);
            var beardMarkings = Markings(uid, HumanoidVisualLayers.FacialHair);
            Assert.That(hairMarkings, Is.Not.Empty);
            Assert.That(beardMarkings, Is.Not.Empty);
            Assert.That(hairMarkings.SelectMany(m => m.MarkingColors), Is.All.EqualTo(hair));
            Assert.That(beardMarkings.SelectMany(m => m.MarkingColors), Is.All.EqualTo(beard));
        });

        blocks[(int) GenomeBlock.Sentience] = 0x200;
        blocks[(int) GenomeBlock.Sex] = 0x536;
        _genome.SetGenome(genome, blocks);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<MonkeyAccentComponent>(uid), Is.False);
            Assert.That(profile.Sex, Is.EqualTo(Sex.Unsexed));
        });

        // Values out of range are clamped by the server, whatever the client sends.
        blocks[(int) GenomeBlock.Race] = 0xFFF;
        blocks[(int) GenomeBlock.SkinR] = 0x1000;
        _genome.SetGenome(genome, blocks);
        Assert.That(genome.Comp.Blocks[(int) GenomeBlock.Race], Is.EqualTo(Genome.RaceMax));
        Assert.That(genome.Comp.Blocks[(int) GenomeBlock.SkinR], Is.EqualTo(Genome.ColorMax));
    }

    [Test]
    public async Task RaceTurnsHumanIntoAnimalAndBack()
    {
        EntityUid human = default;
        EntityUid monkey = default;
        EntityUid gorilla = default;
        Entity<GenomeComponent> genome = default;
        int[] blocks = null!;

        // Polymorph only reverts on a real grid, not in nullspace.
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            human = SpawnHuman(Sex.Female, Color.Black, Color.Black, map.GridCoords);
            Assert.That(_genome.TryGetGenome(human, out genome));
            blocks = (int[]) genome.Comp.Blocks.Clone();

            blocks[(int) GenomeBlock.Race] = 0xC5;
            monkey = _genome.SetGenome(genome, blocks);

            Assert.That(monkey, Is.Not.EqualTo(human));
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(monkey).EntityPrototype?.ID, Is.EqualTo("MobMonkey"));

            // Scanning the animal finds the genome of the human it came from.
            Assert.That(_genome.TryGetGenome(monkey, out var fromAnimal) && fromAnimal.Owner == human);

            // From one animal straight into another.
            blocks[(int) GenomeBlock.Race] = 0xE0;
            gorilla = _genome.SetGenome(genome, blocks);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(gorilla).EntityPrototype?.ID, Is.EqualTo("MobGorilla"));

            // Staying the same animal keeps the same body.
            blocks[(int) GenomeBlock.HairR] = 0x10;
            Assert.That(_genome.SetGenome(genome, blocks), Is.EqualTo(gorilla));

            blocks[(int) GenomeBlock.Race] = 0x150;
            Assert.That(_genome.SetGenome(genome, blocks), Is.EqualTo(human));
            Assert.That(genome.Comp.Form, Is.Null);
        });

        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(monkey), "the old animal body is removed when turning into another one");
            Assert.That(SEntMan.Deleted(gorilla), "the animal body is removed when the human comes back");
            Assert.That(SEntMan.Deleted(human), Is.False);
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void OnlyAdminsCanPickUpTheScanner()
    {
        var player = SSpawn(Human);
        var scanner = SSpawn(Scanner);

        var ev = new GettingPickedUpAttemptEvent(player, scanner, false);
        SEntMan.EventBus.RaiseLocalEvent(scanner, ev);
        Assert.That(ev.Cancelled, "a player without admin rights must not pick up the scanner");
    }
}
