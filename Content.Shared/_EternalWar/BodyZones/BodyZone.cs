using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// A part of the body that can be aimed at, damaged and protected by armor separately.
/// </summary>
[Serializable, NetSerializable]
public enum BodyZone : byte
{
    Head,
    LeftEye,
    RightEye,
    Mouth,
    Chest,
    Abdomen,
    Genitals,
    LeftUpperArm,
    RightUpperArm,
    LeftForearm,
    RightForearm,
    LeftHand,
    RightHand,
    LeftThigh,
    RightThigh,
    LeftShin,
    RightShin,
    LeftFoot,
    RightFoot,
}

public static class BodyZoneExtensions
{
    public static readonly BodyZone[] All =
    {
        BodyZone.Head,
        BodyZone.LeftEye,
        BodyZone.RightEye,
        BodyZone.Mouth,
        BodyZone.Chest,
        BodyZone.Abdomen,
        BodyZone.Genitals,
        BodyZone.LeftUpperArm,
        BodyZone.RightUpperArm,
        BodyZone.LeftForearm,
        BodyZone.RightForearm,
        BodyZone.LeftHand,
        BodyZone.RightHand,
        BodyZone.LeftThigh,
        BodyZone.RightThigh,
        BodyZone.LeftShin,
        BodyZone.RightShin,
        BodyZone.LeftFoot,
        BodyZone.RightFoot,
    };

    /// <summary>
    /// The zone this one is attached to. Hits aimed at a missing zone land there,
    /// e.g. shooting at a missing hand hits the forearm, and at a missing upper arm hits the chest.
    /// </summary>
    public static BodyZone? Parent(this BodyZone zone)
    {
        return zone switch
        {
            BodyZone.Head => BodyZone.Chest,
            BodyZone.LeftEye or BodyZone.RightEye or BodyZone.Mouth => BodyZone.Head,
            BodyZone.Genitals => BodyZone.Abdomen,
            BodyZone.LeftUpperArm or BodyZone.RightUpperArm => BodyZone.Chest,
            BodyZone.LeftForearm => BodyZone.LeftUpperArm,
            BodyZone.RightForearm => BodyZone.RightUpperArm,
            BodyZone.LeftHand => BodyZone.LeftForearm,
            BodyZone.RightHand => BodyZone.RightForearm,
            BodyZone.LeftThigh or BodyZone.RightThigh => BodyZone.Abdomen,
            BodyZone.LeftShin => BodyZone.LeftThigh,
            BodyZone.RightShin => BodyZone.RightThigh,
            BodyZone.LeftFoot => BodyZone.LeftShin,
            BodyZone.RightFoot => BodyZone.RightShin,
            _ => null,
        };
    }

    /// <summary>
    /// Torso zones can't be torn off, they only spill their organs.
    /// </summary>
    public static bool IsTorso(this BodyZone zone)
    {
        return zone is BodyZone.Chest or BodyZone.Abdomen;
    }

    public static bool IsEye(this BodyZone zone)
    {
        return zone is BodyZone.LeftEye or BodyZone.RightEye;
    }

    public static bool IsThigh(this BodyZone zone)
    {
        return zone is BodyZone.LeftThigh or BodyZone.RightThigh;
    }

    public static bool IsShin(this BodyZone zone)
    {
        return zone is BodyZone.LeftShin or BodyZone.RightShin;
    }

    public static bool IsFoot(this BodyZone zone)
    {
        return zone is BodyZone.LeftFoot or BodyZone.RightFoot;
    }

    /// <summary>
    /// Zones whose broken bone makes the hand on that side useless.
    /// </summary>
    public static bool IsArm(this BodyZone zone)
    {
        return zone is BodyZone.LeftUpperArm or BodyZone.RightUpperArm
            or BodyZone.LeftForearm or BodyZone.RightForearm
            or BodyZone.LeftHand or BodyZone.RightHand;
    }

    /// <summary>
    /// The hand zone on the same side as an arm zone.
    /// </summary>
    public static BodyZone? HandOf(this BodyZone zone)
    {
        return zone switch
        {
            BodyZone.LeftUpperArm or BodyZone.LeftForearm or BodyZone.LeftHand => BodyZone.LeftHand,
            BodyZone.RightUpperArm or BodyZone.RightForearm or BodyZone.RightHand => BodyZone.RightHand,
            _ => null,
        };
    }

    public static string LocKey(this BodyZone zone)
    {
        return zone switch
        {
            BodyZone.Head => "body-zone-head",
            BodyZone.LeftEye => "body-zone-left-eye",
            BodyZone.RightEye => "body-zone-right-eye",
            BodyZone.Mouth => "body-zone-mouth",
            BodyZone.Chest => "body-zone-chest",
            BodyZone.Abdomen => "body-zone-abdomen",
            BodyZone.Genitals => "body-zone-genitals",
            BodyZone.LeftUpperArm => "body-zone-left-upper-arm",
            BodyZone.RightUpperArm => "body-zone-right-upper-arm",
            BodyZone.LeftForearm => "body-zone-left-forearm",
            BodyZone.RightForearm => "body-zone-right-forearm",
            BodyZone.LeftHand => "body-zone-left-hand",
            BodyZone.RightHand => "body-zone-right-hand",
            BodyZone.LeftThigh => "body-zone-left-thigh",
            BodyZone.RightThigh => "body-zone-right-thigh",
            BodyZone.LeftShin => "body-zone-left-shin",
            BodyZone.RightShin => "body-zone-right-shin",
            BodyZone.LeftFoot => "body-zone-left-foot",
            _ => "body-zone-right-foot",
        };
    }
}
