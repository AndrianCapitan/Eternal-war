using Content.Shared.Interaction.Events;
using Content.Shared.Sound.Components;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._EternalWar.Vision;

/// <summary>
/// An item let go of from the hands lands with the sound it makes when thrown, only quieter, so whoever doesn't see
/// it (the field of view only looks forward) still hears it. Thrown items already sound when they land.
/// </summary>
public sealed partial class DropNoiseSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    /// <summary>
    /// Laid down rather than thrown.
    /// </summary>
    private const float VolumeChange = -4f;

    /// <summary>
    /// Items dropped this tick. Checked in <see cref="Update"/>, as a throw drops the item before throwing it.
    /// </summary>
    private readonly HashSet<EntityUid> _dropped = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmitSoundOnLandComponent, DroppedEvent>(OnDropped);
    }

    private void OnDropped(EntityUid uid, EmitSoundOnLandComponent comp, DroppedEvent args)
    {
        _dropped.Add(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_dropped.Count == 0)
            return;

        foreach (var uid in _dropped)
        {
            if (TerminatingOrDeleted(uid)
                || HasComp<ThrownItemComponent>(uid)
                || _container.IsEntityOrParentInContainer(uid)
                || !TryComp<EmitSoundOnLandComponent>(uid, out var land)
                || land.Sound is not { } sound)
            {
                continue;
            }

            _audio.PlayPvs(sound, uid, sound.Params.AddVolume(VolumeChange));
        }

        _dropped.Clear();
    }
}
