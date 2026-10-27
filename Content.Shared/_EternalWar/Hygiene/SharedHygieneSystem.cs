using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._EternalWar.Hygiene;

/// <summary>
/// Blood on clothes and the shower that washes it off. The stains are added on the server (bleeding, puddles),
/// the client draws them on the worn clothing.
/// </summary>
public abstract partial class SharedHygieneSystem : EntitySystem
{
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodStainedComponent, ExaminedEvent>(OnStainedExamined);
        SubscribeLocalEvent<ShowerComponent, ActivateInWorldEvent>(OnShowerActivate);
        SubscribeLocalEvent<ShowerComponent, ComponentStartup>(OnShowerStartup);
    }

    #region Blood stains

    /// <summary>
    /// Gets blood on the item. Different blood mixes its color in.
    /// </summary>
    public void Stain(EntityUid item, float amount, Color color)
    {
        if (amount <= 0f)
            return;

        var stained = EnsureComp<BloodStainedComponent>(item);
        stained.Color = stained.Amount > 0f
            ? Color.InterpolateBetween(stained.Color, color, amount / (stained.Amount + amount))
            : color;
        stained.Amount = MathF.Min(1f, stained.Amount + amount);
        Dirty(item, stained);
    }

    /// <summary>
    /// Washes some of the blood off. Clean once none is left.
    /// </summary>
    public void Wash(Entity<BloodStainedComponent?> item, float amount)
    {
        if (!Resolve(item, ref item.Comp, false))
            return;

        item.Comp.Amount -= amount;
        if (item.Comp.Amount <= 0f)
        {
            RemComp<BloodStainedComponent>(item);
            return;
        }

        Dirty(item);
    }

    private void OnStainedExamined(Entity<BloodStainedComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.Level >= 2 ? "blood-stained-examine-soaked" : "blood-stained-examine",
            ("color", ent.Comp.Color.ToHex())));
    }

    #endregion

    #region Shower

    private void OnShowerStartup(Entity<ShowerComponent> ent, ref ComponentStartup args)
    {
        UpdateShowerVisuals(ent);
    }

    private void OnShowerActivate(Entity<ShowerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        SetShower(ent, !ent.Comp.Enabled, args.User);
    }

    public void SetShower(Entity<ShowerComponent> ent, bool enabled, EntityUid? user = null)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);
        _audio.PlayPredicted(ent.Comp.ToggleSound, ent, user);
        UpdateShowerVisuals(ent);
    }

    private void UpdateShowerVisuals(Entity<ShowerComponent> ent)
    {
        _appearance.SetData(ent, ShowerVisuals.Enabled, ent.Comp.Enabled);
        _ambient.SetAmbience(ent, ent.Comp.Enabled);
    }

    #endregion
}
