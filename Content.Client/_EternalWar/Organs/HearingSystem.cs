using Content.Shared._EternalWar.Organs;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._EternalWar.Organs;

/// <summary>
/// The game is as loud as the player's character hears: half as loud with one inner ear, silent for the deaf
/// (<see cref="OrganFunctionsComponent.Hearing"/>). The volume setting itself is left alone.
/// </summary>
public sealed partial class HearingSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private float _applied = -1f;
    private TimeSpan _nextRefresh;

    public override void Shutdown()
    {
        base.Shutdown();
        _audio.SetMasterVolume(_cfg.GetCVar(CVars.AudioMasterVolume));
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var hearing = 1f;
        if (_player.LocalEntity is { } local && TryComp<OrganFunctionsComponent>(local, out var organs))
            hearing = organs.Hearing;

        var gain = _cfg.GetCVar(CVars.AudioMasterVolume) * hearing;

        // The engine sets the volume back from the setting now and then (a new audio device): while it is turned
        // down, set it again every second.
        var now = _timing.RealTime;
        var refresh = hearing < 1f && now >= _nextRefresh;
        if (!refresh && MathHelper.CloseTo(gain, _applied))
            return;

        _applied = gain;
        _nextRefresh = now + TimeSpan.FromSeconds(1);
        _audio.SetMasterVolume(gain);
    }
}
