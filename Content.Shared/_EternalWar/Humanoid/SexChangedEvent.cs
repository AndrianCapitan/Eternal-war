using Content.Shared.Humanoid;

namespace Content.Shared._EternalWar.Humanoid;

/// <summary>
/// Raised on a humanoid when its sex changes: when the character's profile is applied or the genome changes it.
/// </summary>
[ByRefEvent]
public readonly record struct SexChangedEvent(Sex OldSex, Sex NewSex);
