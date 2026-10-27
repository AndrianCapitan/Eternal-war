#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Silicons.Laws;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// The fork runs in ru-RU while the ion law strings only exist in the en-US fallback.
/// Their ION-* functions must work there too, otherwise every ion law logs "Unknown function" errors.
/// </summary>
[TestOf(typeof(IonLawLocalizationSystem))]
public sealed class IonLawLocalizationTest : GameTest
{
    [SidedDependency(Side.Server)] private readonly IonLawSystem _ionLaw = default!;

    [Test]
    [RunOnSide(Side.Server)]
    public void IonLawsResolveInFallbackCulture()
    {
        // Laws are picked at random, so roll enough of them to hit most templates.
        // Any "Unknown function" error is logged and fails the test on its own.
        for (var i = 0; i < 100; i++)
        {
            var law = _ionLaw.GetIonLaw();
            Assert.That(law, Is.Not.Empty);
            Assert.That(law, Does.Not.Contain("ION-"), law);
        }
    }
}
