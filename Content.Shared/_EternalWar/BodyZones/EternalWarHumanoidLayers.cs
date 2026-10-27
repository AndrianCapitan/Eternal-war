using Robust.Shared.Serialization;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// Body sprite layers upstream doesn't have. The left eye is drawn on <see cref="Content.Shared.Humanoid.HumanoidVisualLayers.Eyes"/>,
/// the right one on its own layer, so losing one eye removes only that eye from the face.
/// The <c>Exposed*</c> layers lie right above each body part, one per body zone (an arm has the upper arm and the
/// forearm): the skin, the bare muscles where the skin is gone, the bones where the muscles are gone too
/// (see the client's <c>BodyZonesSystem.Exposure</c>).
/// </summary>
[Serializable, NetSerializable]
public enum EternalWarHumanoidLayers : byte
{
    RightEye,
    ExposedChest,
    ExposedAbdomen,
    ExposedHead,
    ExposedRUpperArm,
    ExposedRForearm,
    ExposedLUpperArm,
    ExposedLForearm,
    ExposedRThigh,
    ExposedRShin,
    ExposedLThigh,
    ExposedLShin,
    ExposedLFoot,
    ExposedRFoot,
    ExposedLHand,
    ExposedRHand,
}
