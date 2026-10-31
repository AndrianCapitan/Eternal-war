using Content.Shared._EternalWar.Humanoid;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Systems;

namespace Content.Shared._EternalWar.Aging;

/// <summary>
/// What the age does to the body: an old body is slower and tires sooner, a frail one even more so.
/// Bone fragility and slower healing are read by the body systems through <see cref="GetBoneFragility"/>
/// and <see cref="GetHealing"/>.
/// </summary>
public abstract partial class SharedAgingSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AgingComponent, AgeChangedEvent>(OnAgeChanged);
        SubscribeLocalEvent<AgingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<AgingComponent, RefreshStaminaCritThresholdEvent>(OnRefreshStamina);
        SubscribeLocalEvent<AgingComponent, ExaminedEvent>(OnExamined);
    }

    protected virtual void OnAgeChanged(Entity<AgingComponent> ent, ref AgeChangedEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        _stamina.RefreshStaminaCritThreshold(ent.Owner);
    }

    private void OnRefreshSpeed(Entity<AgingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var speed = GetStage(ent) switch
        {
            AgeStage.Old => ent.Comp.OldSpeed,
            AgeStage.Frail or AgeStage.Dying => ent.Comp.FrailSpeed,
            _ => 1f,
        };

        if (speed < 1f)
            args.ModifySpeed(speed);
    }

    private void OnRefreshStamina(Entity<AgingComponent> ent, ref RefreshStaminaCritThresholdEvent args)
    {
        args.ThresholdValue *= GetStage(ent) switch
        {
            AgeStage.Old => ent.Comp.OldStamina,
            AgeStage.Frail or AgeStage.Dying => ent.Comp.FrailStamina,
            _ => 1f,
        };
    }

    private void OnExamined(Entity<AgingComponent> ent, ref ExaminedEvent args)
    {
        var message = GetStage(ent) switch
        {
            AgeStage.Frail => "aging-examine-frail",
            AgeStage.Dying => "aging-examine-dying",
            _ => null,
        };

        if (message != null)
            args.PushMarkup(Loc.GetString(message, ("target", ent.Owner)));
    }

    public int GetAge(EntityUid uid)
    {
        return CompOrNull<HumanoidProfileComponent>(uid)?.Age ?? 0;
    }

    /// <summary>
    /// <see cref="AgeStage.Dying"/> is known only to the server: elsewhere a dying body is just frail.
    /// </summary>
    public AgeStage GetStage(Entity<AgingComponent> ent)
    {
        if (ent.Comp.Dying)
            return AgeStage.Dying;

        var age = GetAge(ent);
        if (age >= ent.Comp.FrailAge)
            return AgeStage.Frail;
        if (age >= ent.Comp.OldAge)
            return AgeStage.Old;

        return AgeStage.Adult;
    }

    /// <summary>
    /// Damage to the bones of the body is multiplied by this.
    /// </summary>
    public float GetBoneFragility(EntityUid uid)
    {
        if (!TryComp<AgingComponent>(uid, out var aging))
            return 1f;

        return GetStage((uid, aging)) switch
        {
            AgeStage.Old => aging.OldBoneFragility,
            AgeStage.Frail or AgeStage.Dying => aging.FrailBoneFragility,
            _ => 1f,
        };
    }

    /// <summary>
    /// The body heals by itself and grows bones together this much as fast as usual.
    /// </summary>
    public float GetHealing(EntityUid uid)
    {
        if (!TryComp<AgingComponent>(uid, out var aging))
            return 1f;

        return GetStage((uid, aging)) switch
        {
            AgeStage.Old => aging.OldHealing,
            AgeStage.Frail or AgeStage.Dying => aging.FrailHealing,
            _ => 1f,
        };
    }
}
