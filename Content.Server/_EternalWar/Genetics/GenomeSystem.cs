using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Polymorph.Systems;
using Content.Shared._EternalWar.Genetics;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Polymorph;
using Content.Shared.Speech.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._EternalWar.Genetics;

/// <summary>
/// Reads the genome of humans from their appearance and applies edited genomes back to them.
/// </summary>
public sealed partial class GenomeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private HumanoidProfileSystem _humanoid = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public static readonly ProtoId<SpeciesPrototype> Human = "Human";

    /// <summary>
    /// Used when a human has neither hair nor beard to take the color from.
    /// </summary>
    private static readonly Color DefaultHairColor = Color.FromHex("#3B2A1E");

    /// <summary>
    /// Finds the genome of a creature: its own, or the one of the human an animal form was made from.
    /// A human without a genome gets one read from their current appearance.
    /// </summary>
    public bool TryGetGenome(EntityUid uid, out Entity<GenomeComponent> genome)
    {
        if (TryComp<GenomeFormComponent>(uid, out var form) && TryComp<GenomeComponent>(form.Original, out var original))
        {
            genome = (form.Original, original);
            return true;
        }

        if (TryComp<GenomeComponent>(uid, out var comp))
        {
            genome = (uid, comp);
            return true;
        }

        if (!TryComp<HumanoidProfileComponent>(uid, out var profile) || profile.Species != Human)
        {
            genome = default;
            return false;
        }

        comp = AddComp<GenomeComponent>(uid);
        comp.Blocks = ReadAppearance((uid, profile));
        genome = (uid, comp);
        return true;
    }

    /// <summary>
    /// Builds a genome that matches how the human looks now. Blocks that don't show
    /// in the appearance get a random value inside the human range.
    /// </summary>
    private int[] ReadAppearance(Entity<HumanoidProfileComponent> ent)
    {
        var blocks = new int[Genome.BlockCount];

        blocks[(int) GenomeBlock.Sentience] = _random.Next(Genome.SentienceThreshold, Genome.SentienceMax + 1);
        blocks[(int) GenomeBlock.Race] = _random.Next(Genome.HumanRaceMin, Genome.RaceMax + 1);
        blocks[(int) GenomeBlock.Sex] = ent.Comp.Sex switch
        {
            Sex.Female => _random.Next(0, Genome.FemaleMax + 1),
            Sex.Male => Genome.Male,
            _ => _random.Next(Genome.Male + 1, Genome.SexMax + 1),
        };

        var skin = Color.White;
        var eyes = Color.Black;
        Color? rightEye = null;
        Color? hair = null;
        Color? beard = null;

        if (_visualBody.TryGatherMarkingsData(ent.Owner, null, out var profiles, out _, out var applied))
        {
            if (profiles.Count > 0)
            {
                var profile = profiles.Values.First();
                skin = profile.SkinColor;
                eyes = profile.EyeColor;
                rightEye = profile.RightEyeColor;
            }

            hair = FirstMarkingColor(applied, HumanoidVisualLayers.Hair);
            beard = FirstMarkingColor(applied, HumanoidVisualLayers.FacialHair);
        }

        Genome.SetColor(blocks, GenomeBlock.SkinR, skin);
        Genome.SetColor(blocks, GenomeBlock.EyeR, eyes);
        Genome.SetColor(blocks, GenomeBlock.RightEyeR, rightEye ?? eyes);
        Genome.SetColor(blocks, GenomeBlock.HairR, hair ?? beard ?? DefaultHairColor);
        Genome.SetColor(blocks, GenomeBlock.BeardR, beard ?? hair ?? DefaultHairColor);

        return blocks;
    }

    private static Color? FirstMarkingColor(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> applied,
        HumanoidVisualLayers layer)
    {
        foreach (var layers in applied.Values)
        {
            if (layers.TryGetValue(layer, out var markings)
                && markings.FirstOrDefault(m => m.MarkingColors.Count > 0) is { } marking)
            {
                return marking.MarkingColors[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Writes a new genome and makes the body match it.
    /// </summary>
    /// <returns>The body the genome lives in now: the human, or the animal they were turned into.</returns>
    public EntityUid SetGenome(Entity<GenomeComponent> ent, int[] blocks)
    {
        for (var i = 0; i < Genome.BlockCount; i++)
        {
            ent.Comp.Blocks[i] = Math.Clamp(blocks[i], 0, Genome.MaxValue((GenomeBlock) i));
        }

        ApplyAppearance(ent);
        var body = UpdateForm(ent);

        var sentient = Genome.IsSentient(ent.Comp.Blocks[(int) GenomeBlock.Sentience]);
        UpdateSentience(ent, sentient);
        if (body != ent.Owner)
            UpdateSentience(body, sentient);

        return body;
    }

    private void ApplyAppearance(Entity<GenomeComponent> ent)
    {
        var blocks = ent.Comp.Blocks;
        var sex = Genome.GetSex(blocks[(int) GenomeBlock.Sex]);

        var eyes = Genome.GetColor(blocks, GenomeBlock.EyeR);
        var rightEye = Genome.GetColor(blocks, GenomeBlock.RightEyeR);

        _humanoid.SetSex(ent.Owner, sex);
        _visualBody.ApplyProfile(ent,
            new OrganProfileData
            {
                Sex = sex,
                SkinColor = Genome.GetColor(blocks, GenomeBlock.SkinR),
                EyeColor = eyes,
                // Different eye blocks are heterochromia.
                RightEyeColor = rightEye == eyes ? null : rightEye,
            });

        // Markings are applied again even when hair didn't change: the ones that follow
        // the skin color only pick up a new skin color this way.
        if (!_visualBody.TryGatherMarkingsData(ent.Owner, null, out _, out _, out var applied))
            return;

        var hair = Genome.GetColor(blocks, GenomeBlock.HairR);
        var beard = Genome.GetColor(blocks, GenomeBlock.BeardR);

        var recolored = applied.ToDictionary(
            organ => organ.Key,
            organ => organ.Value.ToDictionary(
                layer => layer.Key,
                layer => layer.Key switch
                {
                    HumanoidVisualLayers.Hair => layer.Value.Select(m => m.WithColor(hair)).ToList(),
                    HumanoidVisualLayers.FacialHair => layer.Value.Select(m => m.WithColor(beard)).ToList(),
                    _ => layer.Value.ToList(),
                }));

        _visualBody.ApplyMarkings(ent, recolored);
    }

    /// <summary>
    /// Turns the human into the animal of their race block, back into a human, or into another animal.
    /// </summary>
    private EntityUid UpdateForm(Entity<GenomeComponent> ent)
    {
        // The animal could have been gibbed, which already brought the human back.
        if (ent.Comp.Form is { } old && TerminatingOrDeleted(old))
        {
            ent.Comp.Form = null;
            ent.Comp.FormPolymorph = null;
        }

        ProtoId<PolymorphPrototype>? wanted = null;
        if (Genome.TryGetRace(_proto, ent.Comp.Blocks[(int) GenomeBlock.Race], out var race))
            wanted = race.Polymorph;

        if (ent.Comp.FormPolymorph == wanted)
            return ent.Comp.Form ?? ent.Owner;

        if (ent.Comp.Form is { } form)
        {
            // Polymorph refuses to revert e.g. on a grid that is being deleted.
            // Then the animal stays as it is: a second body made from the same human would have no mind.
            if (_polymorph.Revert(form) == null)
                return form;

            ent.Comp.Form = null;
            ent.Comp.FormPolymorph = null;
        }

        if (wanted is not { } polymorph || _polymorph.PolymorphEntity(ent, polymorph) is not { } child)
            return ent.Owner;

        EnsureComp<GenomeFormComponent>(child).Original = ent;
        ent.Comp.Form = child;
        ent.Comp.FormPolymorph = polymorph;
        return child;
    }

    private void UpdateSentience(EntityUid body, bool sentient)
    {
        if (sentient)
        {
            // Only take away the accent the genome gave, not one the creature has on its own.
            if (RemComp<GenomeMonkeyAccentComponent>(body))
                RemComp<MonkeyAccentComponent>(body);

            return;
        }

        if (HasComp<MonkeyAccentComponent>(body))
            return;

        AddComp<MonkeyAccentComponent>(body);
        AddComp<GenomeMonkeyAccentComponent>(body);
    }
}
