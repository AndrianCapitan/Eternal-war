#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Serialization.Manager;

namespace Content.IntegrationTests.Tests._EternalWar;

/// <summary>
/// The right eye color of heterochromia survives saving the character.
/// </summary>
[TestOf(typeof(HumanoidCharacterAppearance))]
public sealed class HeterochromiaPreferencesTest : GameTest
{
    private ServerDbSqlite GetDb()
    {
        var cfg = Server.ResolveDependency<IConfigurationManager>();
        var serialization = Server.ResolveDependency<ISerializationManager>();
        var opsLog = Server.ResolveDependency<ILogManager>().GetSawmill("db.ops");
        var builder = new DbContextOptionsBuilder<SqliteServerDbContext>();
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        builder.UseSqlite(conn);
        return new ServerDbSqlite(() => builder.Options, true, cfg, true, opsLog, serialization, false);
    }

    private static HumanoidCharacterProfile Profile(Color? rightEye)
    {
        return new()
        {
            Name = "Ivan Ivanov",
            Species = "Human",
            Age = 30,
            Appearance = new(Color.Brown, Color.Beige, new(), rightEye),
        };
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task RightEyeColorIsSaved(bool heterochromia)
    {
        var db = GetDb();
        var preferences = (ServerPreferencesManager) Server.ResolveDependency<IServerPreferencesManager>();
        var user = new NetUserId(Guid.NewGuid());
        var profile = Profile(heterochromia ? Color.SteelBlue : null);

        await db.InitPrefsAsync(user, profile);
        var prefs = await db.GetPlayerPreferencesAsync(user);
        var fetched = preferences.ConvertProfiles(prefs!.Profiles.Find(p => p.Slot == 0)!);

        Assert.That(fetched.Appearance.RightEyeColor, Is.EqualTo(profile.Appearance.RightEyeColor));
        Assert.That(fetched.MemberwiseEquals(profile));
    }

    [Test]
    public void HeterochromiaIsPartOfTheAppearance()
    {
        var plain = Profile(null).Appearance;
        var hetero = plain.WithRightEyeColor(Color.SteelBlue);

        Assert.Multiple(() =>
        {
            Assert.That(hetero, Is.Not.EqualTo(plain));
            Assert.That(hetero.WithEyeColor(Color.Gray).RightEyeColor, Is.EqualTo(Color.SteelBlue), "changing the left eye keeps the right one");
            Assert.That(hetero.WithRightEyeColor(null), Is.EqualTo(plain));
        });
    }
}
