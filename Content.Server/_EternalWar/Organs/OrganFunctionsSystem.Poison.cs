using System.Linq;
using Content.Server._EternalWar.BodyZones;
using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Organs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._EternalWar.Organs;

/// <summary>
/// The liver and the kidneys clean the body of poison: a little poison damage a second they make harmless,
/// more than that they take on themselves, and only once they failed does it reach the body.
/// </summary>
public sealed partial class OrganFunctionsSystem
{
    /// <summary>
    /// Set while the poison the organs took is being taken out of a hit and the rest dealt again.
    /// </summary>
    private bool _redirectingPoison;

    private void InitializePoison()
    {
        // After the body zones have picked where the hit lands: dealing it again picks the same.
        SubscribeLocalEvent<OrganFunctionsComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged,
            after: new[] { typeof(BodyZonesSystem) });
    }

    /// <summary>
    /// The working organs that clean the body of poison, with how much poison damage a second each makes harmless.
    /// </summary>
    private IEnumerable<(EntityUid Organ, OrganHealthComponent Health, float Rate)> GetDetoxOrgans(
        Entity<OrganFunctionsComponent> ent,
        Entity<BodyZonesComponent> body)
    {
        foreach (var (category, rate) in ent.Comp.Detox)
        {
            foreach (var organ in _zones.GetOrgans(body, category))
            {
                if (TryComp<OrganHealthComponent>(organ, out var health) && !health.Failed && health.Health > 0f)
                    yield return (organ, health, rate);
            }
        }
    }

    private void RefillDetox(Entity<OrganFunctionsComponent> ent, Entity<BodyZonesComponent> body)
    {
        var capacity = GetDetoxOrgans(ent, body).Sum(o => o.Rate) * (float) ent.Comp.UpdateInterval.TotalSeconds;
        ent.Comp.DetoxBudget = MathF.Min(ent.Comp.DetoxBudget + capacity, capacity * 2f);
    }

    private void OnBeforeDamageChanged(Entity<OrganFunctionsComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (_redirectingPoison || args.Cancelled || !TryComp<BodyZonesComponent>(ent, out var zones))
            return;

        var poison = args.Damage.DamageDict.GetValueOrDefault(ent.Comp.PoisonDamage).Float();
        if (poison < 0f)
        {
            HealDetoxOrgans(ent, (ent.Owner, zones), -poison);
            return;
        }

        if (poison <= 0f)
            return;

        var taken = TakePoison(ent, (ent.Owner, zones), poison);
        if (taken <= 0f)
            return;

        // What the organs took doesn't reach the body: the hit is dealt again without it.
        args.Cancelled = true;

        var rest = new DamageSpecifier(args.Damage);
        var left = poison - taken;
        if (left > 0f)
            rest.DamageDict[ent.Comp.PoisonDamage] = left;
        else
            rest.DamageDict.Remove(ent.Comp.PoisonDamage);

        if (rest.Empty)
            return;

        // Poison on its own comes from reagents, which ignore resistances; a hit with poison in it (a bite) doesn't.
        var reagent = args.Damage.DamageDict.Count == 1;
        _redirectingPoison = true;
        try
        {
            _damageable.TryChangeDamage(ent.Owner, rest, ignoreResistances: reagent, interruptsDoAfters: !reagent, origin: args.Origin);
        }
        finally
        {
            _redirectingPoison = false;
        }
    }

    /// <summary>
    /// Makes poison damage harmless as far as the liver and kidneys can, then lets them take the rest on themselves.
    /// </summary>
    /// <returns>How much of the poison damage the body is spared.</returns>
    public float TakePoison(Entity<OrganFunctionsComponent> ent, Entity<BodyZonesComponent> body, float poison)
    {
        var neutralized = MathF.Min(poison, ent.Comp.DetoxBudget);
        ent.Comp.DetoxBudget -= neutralized;
        var left = poison - neutralized;

        // Shared out evenly; what an organ can't take goes to the others.
        var organs = GetDetoxOrgans(ent, body).ToList();
        while (left > 0.001f && organs.Count > 0)
        {
            var share = left / organs.Count;
            foreach (var (organ, health, _) in organs.ToList())
            {
                var damage = MathF.Min(share, health.Health);
                _zones.DamagePiece(body, organ, damage);
                left -= damage;

                if (health.Health <= 0f)
                    organs.RemoveAll(o => o.Organ == organ);
            }
        }

        return poison - MathF.Max(left, 0f);
    }

    /// <summary>
    /// What heals poison and is more than the body has heals the liver and kidneys. A failed organ stays failed.
    /// </summary>
    private void HealDetoxOrgans(Entity<OrganFunctionsComponent> ent, Entity<BodyZonesComponent> body, float heal)
    {
#pragma warning disable CS0618
        var bodyPoison = _damageable.GetAllDamage(ent.Owner).DamageDict.GetValueOrDefault(ent.Comp.PoisonDamage).Float();
#pragma warning restore CS0618
        var left = heal - bodyPoison;
        if (left <= 0f)
            return;

        foreach (var (organ, health, _) in GetDetoxOrgans(ent, body))
        {
            if (left <= 0f)
                break;

            var amount = MathF.Min(left, health.Damage);
            if (amount <= 0f)
                continue;

            _zones.DamagePiece(body, organ, -amount);
            left -= amount;
        }
    }
}
