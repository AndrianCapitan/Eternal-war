#nullable enable
using System.Linq;
using System.Text.RegularExpressions;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.Localizations;
using Robust.Shared.ContentPack;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// The Russian medicine translation and the language switch in the options.
/// </summary>
[TestOf(typeof(ContentLocalizationManager))]
public sealed class LanguageTest : GameTest
{
    private static readonly ResPath MedicalEntities = new("/Locale/ru-RU/_EternalWar/medical-entities.ftl");
    private static readonly Regex EntKey = new(@"^ent-(\S+) =", RegexOptions.Multiline);

    [SidedDependency(Side.Server)] private readonly IResourceManager _sRes = default!;
    [SidedDependency(Side.Server)] private readonly IPrototypeManager _sProto = default!;
    [SidedDependency(Side.Server)] private readonly ILocalizationManager _sLoc = default!;
    [SidedDependency(Side.Client)] private readonly ILocalizationManager _cLoc = default!;

    [Test]
    [RunOnSide(Side.Server)]
    public void MedicineIsInRussian()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_sLoc.GetEntityData("HandheldHealthAnalyzer").Name, Is.EqualTo("анализатор здоровья"));
            // A child prototype without its own name in YAML needs its own entry too.
            Assert.That(_sLoc.GetEntityData("MedkitFilled").Name, Is.EqualTo("аптечка"));
            Assert.That(_sLoc.GetString("reagent-name-bicaridine"), Is.EqualTo("бикаридин"));
            Assert.That(_sLoc.GetString("health-analyzer-window-entity-dead-text"), Is.EqualTo("Мёртв"));
        });
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void TranslatedEntitiesExist()
    {
        var text = _sRes.ContentFileReadAllText(MedicalEntities);
        var ids = EntKey.Matches(text).Select(m => m.Groups[1].Value).ToList();

        Assert.That(ids, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var id in ids)
            {
                Assert.That(_sProto.HasIndex<EntityPrototype>(id), $"{MedicalEntities} translates unknown prototype {id}");
            }
        });
    }

    [Test]
    public async Task SwitchesLanguageAndBack()
    {
        await Client.WaitAssertion(() =>
            Assert.That(_cLoc.GetEntityData("HandheldHealthAnalyzer").Name, Is.EqualTo("анализатор здоровья")));

        await OverrideCVar(Side.Client, CCVars.Language, "en-US");
        await Client.WaitAssertion(() =>
        {
            Assert.That(_cLoc.DefaultCulture?.Name, Is.EqualTo("en-US"));
            // The cached Russian name must be dropped.
            Assert.That(_cLoc.GetEntityData("HandheldHealthAnalyzer").Name, Is.EqualTo("health analyzer"));
            Assert.That(_cLoc.GetString("reagent-name-bicaridine"), Is.EqualTo("bicaridine"));
        });

        // Unknown values fall back to Russian instead of breaking the client.
        await OverrideCVar(Side.Client, CCVars.Language, "xx-XX");
        await Client.WaitAssertion(() =>
        {
            Assert.That(_cLoc.DefaultCulture?.Name, Is.EqualTo(ContentLocalizationManager.DefaultLanguage));
            Assert.That(_cLoc.GetEntityData("HandheldHealthAnalyzer").Name, Is.EqualTo("анализатор здоровья"));
        });
    }
}
