using Robust.Shared.Input;

namespace Content.Shared._EternalWar.Input;

[KeyFunctions]
public static class EternalWarKeyFunctions
{
    /// <summary>
    /// Takes something apart: pulls the bones out of a body laid bare, the jaw off a skull, a skeleton to pieces.
    /// The middle mouse button by default, on a thing in the world or on a hand.
    /// </summary>
    public static readonly BoundKeyFunction TakeApart = "EternalWarTakeApart";
}
