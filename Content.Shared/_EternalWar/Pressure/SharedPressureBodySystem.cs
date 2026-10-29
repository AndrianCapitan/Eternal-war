using Content.Shared.Movement.Systems;

namespace Content.Shared._EternalWar.Pressure;

/// <summary>
/// The speed of a body with decompression sickness, predicted on the client. The rest is on the server.
/// </summary>
public abstract partial class SharedPressureBodySystem : EntitySystem
{
    [Dependency] protected MovementSpeedModifierSystem Speed = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PressureBodyComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<PressureBodyComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnRefreshSpeed(Entity<PressureBodyComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var modifier = ent.Comp.Stage switch
        {
            DecompressionStage.Bends => ent.Comp.BendsSpeedModifier,
            DecompressionStage.Severe => ent.Comp.SevereSpeedModifier,
            _ => 1f,
        };

        if (modifier < 1f)
            args.ModifySpeed(modifier, modifier);
    }

    private void OnHandleState(Entity<PressureBodyComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Speed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}
