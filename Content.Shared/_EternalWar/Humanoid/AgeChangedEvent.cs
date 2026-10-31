namespace Content.Shared._EternalWar.Humanoid;

/// <summary>
/// Raised on a humanoid when its age changes: when the character's profile is applied or something ages it.
/// </summary>
[ByRefEvent]
public readonly record struct AgeChangedEvent(int OldAge, int NewAge);
