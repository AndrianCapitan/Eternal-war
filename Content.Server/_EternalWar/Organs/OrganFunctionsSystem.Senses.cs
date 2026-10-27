using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Organs;
using Content.Shared.Popups;
using Content.Shared.Speech;

namespace Content.Server._EternalWar.Organs;

/// <summary>
/// The tongue speaks, the inner ears hear.
/// </summary>
public sealed partial class OrganFunctionsSystem
{
    private void InitializeSenses()
    {
        SubscribeLocalEvent<OrganFunctionsComponent, SpeakAttemptEvent>(OnSpeakAttempt);
    }

    #region Speech

    /// <summary>
    /// Whether the body has a tongue to speak with.
    /// </summary>
    public bool CanSpeak(Entity<OrganFunctionsComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !TryComp<BodyZonesComponent>(ent, out var zones))
            return true;

        return _zones.HasWorkingOrgan((ent.Owner, zones), ent.Comp.Tongue);
    }

    private void OnSpeakAttempt(Entity<OrganFunctionsComponent> ent, ref SpeakAttemptEvent args)
    {
        if (args.Cancelled || CanSpeak(ent.AsNullable()))
            return;

        _popup.PopupEntity(Loc.GetString("organ-functions-no-tongue"), ent, ent, PopupType.Medium);
        args.Cancel();
    }

    #endregion

    #region Hearing

    /// <summary>
    /// How well the entity hears, from 0 (deaf) to 1. Anything without inner ears of its own hears fine.
    /// </summary>
    public float GetHearing(EntityUid? listener)
    {
        return listener != null && TryComp<OrganFunctionsComponent>(listener, out var comp) ? comp.Hearing : 1f;
    }

    /// <summary>
    /// How far a sound at this distance seems to the listener: twice as far with one ear, out of reach for the deaf.
    /// Chat compares this with the range of speech.
    /// </summary>
    public float HeardDistance(EntityUid? listener, float distance)
    {
        var hearing = GetHearing(listener);
        return hearing <= 0f ? float.PositiveInfinity : distance / hearing;
    }

    private void UpdateHearing(Entity<OrganFunctionsComponent> ent, Entity<BodyZonesComponent> body)
    {
        var ears = 0;
        foreach (var ear in ent.Comp.Ears)
        {
            if (_zones.HasWorkingOrgan(body, ear))
                ears++;
        }

        var hearing = ent.Comp.Ears.Count == 0 ? 1f : (float) ears / ent.Comp.Ears.Count;
        if (MathHelper.CloseTo(hearing, ent.Comp.Hearing))
            return;

        var worse = hearing < ent.Comp.Hearing;
        ent.Comp.Hearing = hearing;
        Dirty(ent);

        var message = hearing <= 0f ? "organ-functions-deaf"
            : !worse ? "organ-functions-hearing-better"
            : "organ-functions-hearing-worse";
        _popup.PopupEntity(Loc.GetString(message), ent, ent, worse ? PopupType.LargeCaution : PopupType.Medium);
    }

    #endregion
}
