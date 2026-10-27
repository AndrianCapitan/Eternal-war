using System.Globalization;
using Content.Shared.Localizations;

namespace Content.Server.Silicons.Laws;

public sealed partial class IonLawLocalizationSystem : EntitySystem
{
    [Dependency] private IonLawSystem _ionLaw = default!;

    private static readonly string[] IonFunctions =
    [
        "ION-NUMBER-BASE",
        "ION-NUMBER-MOD",
        "ION-ADJECTIVE",
        "ION-SUBJECT",
        "ION-WHO",
        "ION-MUST",
        "ION-THING",
        "ION-JOB",
        "ION-WHO-GENERAL",
        "ION-PLURAL",
        "ION-REQUIRE",
        "ION-SEVERITY",
        "ION-ALLERGY",
        "ION-FEELING",
        "ION-CONCEPT",
        "ION-FOOD",
        "ION-DRINK",
        "ION-CHANGE",
        "ION-WHO-RANDOM",
        "ION-AREA",
        "ION-PART",
        "ION-OBJECT",
        "ION-HARM-PROTECT",
        "ION-VERB",
    ];

    public override void Initialize()
    {
        base.Initialize();

        var culture = Loc.DefaultCulture;

        if (culture == null)
        {
            Log.Error("Culture was null when trying to generate Ion Law");
            return;
        }

        // Eternal War: the ion law strings only exist in en-US, which is the fallback culture of the fork,
        // so the functions have to be known there as well, not only in the default culture.
        var cultures = new HashSet<CultureInfo> { culture, new(ContentLocalizationManager.FallbackCulture) };

        foreach (var c in cultures)
        {
            foreach (var name in IonFunctions)
            {
                Loc.AddFunction(c, name, _ => GetIonLawValue(name));
            }
        }
    }

    /// <summary>
    /// Returns a localized value for an ion law token.
    /// </summary>
    private ILocValue GetIonLawValue(string s)
    {
        var val = _ionLaw.GetOrGenerateValue(s);
        return new LocValueString(val.ToString() ?? string.Empty);
    }
}
