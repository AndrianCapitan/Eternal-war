using Content.Shared._EternalWar.Humanoid;
using Content.Shared.Examine;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Preferences;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid;

public sealed partial class HumanoidProfileSystem : EntitySystem
{
    [Dependency] private GrammarSystem _grammar = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, ExaminedEvent>(OnExamined);
    }

    public void ApplyProfileTo(Entity<HumanoidProfileComponent?> ent, HumanoidCharacterProfile profile)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var oldSex = ent.Comp.Sex; // Eternal War
        ent.Comp.Gender = profile.Gender;
        ent.Comp.Age = profile.Age;
        ent.Comp.Species = profile.Species;
        ent.Comp.Voice = profile.Voice;
        ent.Comp.Sex = profile.Sex;
        Dirty(ent);

        // Eternal War: sex organs follow the sex.
        if (oldSex != profile.Sex)
        {
            var sexChanged = new SexChangedEvent(oldSex, profile.Sex);
            RaiseLocalEvent(ent, ref sexChanged);
        }

        var voiceChanged = new VoiceChangedEvent(ent.Comp.Voice, profile.Voice);
        RaiseLocalEvent(ent, ref voiceChanged);

        if (TryComp<GrammarComponent>(ent, out var grammar))
        {
            _grammar.SetGender((ent, grammar), profile.Gender);
        }
    }

    // Eternal War: the genome changes sex in game.
    /// <summary>
    /// Changes the sex of the humanoid, and its grammatical gender to match.
    /// </summary>
    public void SetSex(Entity<HumanoidProfileComponent?> ent, Sex sex)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        var oldSex = ent.Comp.Sex;
        ent.Comp.Sex = sex;
        ent.Comp.Gender = sex switch
        {
            Sex.Male => Gender.Male,
            Sex.Female => Gender.Female,
            _ => Gender.Epicene,
        };
        Dirty(ent);

        if (TryComp<GrammarComponent>(ent, out var grammar))
            _grammar.SetGender((ent, grammar), ent.Comp.Gender);

        if (oldSex != sex)
        {
            var sexChanged = new SexChangedEvent(oldSex, sex);
            RaiseLocalEvent(ent, ref sexChanged);
        }
    }

    private void OnExamined(Entity<HumanoidProfileComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Examinable)
            return;

        var identity = Identity.Entity(ent, EntityManager);
        var species = GetSpeciesRepresentation(ent.Comp.Species).ToLower();
        var age = GetAgeRepresentation(ent.Comp.Species, ent.Comp.Age);

        args.PushText(Loc.GetString("humanoid-appearance-component-examine", ("user", identity), ("age", age), ("species", species)));
    }

    /// <summary>
    /// Takes ID of the species prototype, returns UI-friendly name of the species.
    /// </summary>
    public string GetSpeciesRepresentation(ProtoId<SpeciesPrototype> species)
    {
        if (ProtoMan.TryIndex(species, out var speciesPrototype))
            return Loc.GetString(speciesPrototype.Name);

        Log.Error("Tried to get representation of unknown species: {speciesId}");
        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    /// <summary>
    /// Takes ID of the species prototype and an age, returns an approximate description
    /// </summary>
    public string GetAgeRepresentation(ProtoId<SpeciesPrototype> species, int age)
    {
        if (!ProtoMan.TryIndex(species, out var speciesPrototype))
        {
            Log.Error("Tried to get age representation of species that couldn't be indexed: " + species);
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.YoungAge)
        {
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.OldAge)
        {
            return Loc.GetString("identity-age-middle-aged");
        }

        return Loc.GetString("identity-age-old");
    }
}
