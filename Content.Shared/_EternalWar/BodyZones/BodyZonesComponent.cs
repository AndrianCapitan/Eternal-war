using Content.Shared.Body;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Tools;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.BodyZones;

/// <summary>
/// Splits the health of a humanoid between body zones.
/// The overall <see cref="Content.Shared.Damage.Components.DamageableComponent"/> still decides crit and death;
/// this component only tracks which part of the body the brute and burn damage went to,
/// tears limbs off and makes armor protect only the parts it covers.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BodyZonesComponent : Component
{
    /// <summary>
    /// Per-zone settings. Max health of all zones adds up to the mob's death threshold.
    /// The defaults describe a human; organs a species doesn't have are simply skipped.
    /// </summary>
    [DataField]
    public Dictionary<BodyZone, BodyZoneData> Zones = new()
    {
        [BodyZone.Head] = new()
        {
            MaxHealth = 24,
            Skin = "SkinFace",
            Organ = "Head",
            TearOff = false,
            Burst = true,
            Bone = "BoneSkull",
            Tissues = new() { "MusclesFace", "VesselsHead" },
            Drop = new() { "Brain", "EarLeft", "EarRight", "EyeLeft", "EyeRight", "Tongue", "BoneJaw" },
            DropSlots = new() { "head", "mask", "eyes", "ears" },
            Fatal = true,
        },
        [BodyZone.LeftEye] = new()
        {
            MaxHealth = 3,
            Organ = "EyeLeft",
            TearOff = false,
            Message = "body-zones-eye-knocked-out",
        },
        [BodyZone.RightEye] = new()
        {
            MaxHealth = 3,
            Organ = "EyeRight",
            TearOff = false,
            Message = "body-zones-eye-knocked-out",
        },
        [BodyZone.Mouth] = new()
        {
            MaxHealth = 6,
            Organ = "BoneJaw",
            Inside = "Head",
            TearOff = false,
            Bone = "BoneJaw",
            Drop = new() { "Tongue" },
            Message = "body-zones-jaw-destroyed",
        },
        [BodyZone.Chest] = new()
        {
            MaxHealth = 44,
            Skin = "SkinChest",
            Bone = "BoneRibs",
            Contents = new() { "BoneSpineThoracic" },
            Tissues = new() { "MusclesChest", "VesselsChest" },
            Drop = new() { "Heart", "LungLeft", "LungRight" },
        },
        [BodyZone.Abdomen] = new()
        {
            MaxHealth = 34,
            Skin = "SkinAbdomen",
            Bone = "BonePelvis",
            Contents = new() { "BoneSpineLumbar" },
            Tissues = new() { "MusclesAbdomen", "VesselsAbdomen" },
            SexContents = new()
            {
                [Sex.Male] = new() { "OrganTestes" },
                [Sex.Female] = new() { "OrganOvaries" },
            },
            Drop = new() { "Stomach", "Liver", "KidneyLeft", "KidneyRight", "Appendix", "Spleen", "Intestines" },
        },
        [BodyZone.Genitals] = new()
        {
            MaxHealth = 4,
            Organ = "Genitals",
            Inside = "Torso",
            TearOff = false,
            SexContents = new()
            {
                [Sex.Male] = new() { "OrganPenis" },
                [Sex.Female] = new() { "OrganVagina" },
            },
            DestroyStun = TimeSpan.FromSeconds(3),
            Message = "body-zones-torn-off",
        },
        [BodyZone.LeftUpperArm] = new()
        {
            ArterialBleed = 4f,
            MaxHealth = 8,
            Skin = "SkinUpperArmLeft",
            Organ = "UpperArmLeft",
            Bone = "BoneHumerusLeft",
            Tissues = new() { "MusclesUpperArmLeft", "VesselsUpperArmLeft" },
            DropSlots = new() { "gloves" },
        },
        [BodyZone.RightUpperArm] = new()
        {
            ArterialBleed = 4f,
            MaxHealth = 8,
            Skin = "SkinUpperArmRight",
            Organ = "UpperArmRight",
            Bone = "BoneHumerusRight",
            Tissues = new() { "MusclesUpperArmRight", "VesselsUpperArmRight" },
            DropSlots = new() { "gloves" },
        },
        [BodyZone.LeftForearm] = new()
        {
            ArterialBleed = 2.5f,
            MaxHealth = 7,
            Skin = "SkinForearmLeft",
            Organ = "ArmLeft",
            Bone = "BoneForearmLeft",
            Tissues = new() { "MusclesForearmLeft", "VesselsForearmLeft" },
            DropSlots = new() { "gloves" },
        },
        [BodyZone.RightForearm] = new()
        {
            ArterialBleed = 2.5f,
            MaxHealth = 7,
            Skin = "SkinForearmRight",
            Organ = "ArmRight",
            Bone = "BoneForearmRight",
            Tissues = new() { "MusclesForearmRight", "VesselsForearmRight" },
            DropSlots = new() { "gloves" },
        },
        [BodyZone.LeftHand] = new()
        {
            ArterialBleed = 1.5f,
            MaxHealth = 5,
            Skin = "SkinHandLeft",
            Organ = "HandLeft",
            Bone = "BoneHandLeft",
            Tissues = new() { "MusclesHandLeft" },
            DropSlots = new() { "gloves" },
        },
        [BodyZone.RightHand] = new()
        {
            ArterialBleed = 1.5f,
            MaxHealth = 5,
            Skin = "SkinHandRight",
            Organ = "HandRight",
            Bone = "BoneHandRight",
            Tissues = new() { "MusclesHandRight" },
            DropSlots = new() { "gloves" },
        },
        [BodyZone.LeftThigh] = new()
        {
            ArterialBleed = 5f,
            MaxHealth = 9,
            Skin = "SkinThighLeft",
            Organ = "ThighLeft",
            Bone = "BoneFemurLeft",
            Tissues = new() { "MusclesThighLeft", "VesselsThighLeft" },
            DropSlots = new() { "shoes" },
        },
        [BodyZone.RightThigh] = new()
        {
            ArterialBleed = 5f,
            MaxHealth = 9,
            Skin = "SkinThighRight",
            Organ = "ThighRight",
            Bone = "BoneFemurRight",
            Tissues = new() { "MusclesThighRight", "VesselsThighRight" },
            DropSlots = new() { "shoes" },
        },
        [BodyZone.LeftShin] = new()
        {
            ArterialBleed = 3f,
            MaxHealth = 7,
            Skin = "SkinShinLeft",
            Organ = "LegLeft",
            Bone = "BoneShinLeft",
            Tissues = new() { "MusclesShinLeft", "VesselsShinLeft" },
            DropSlots = new() { "shoes" },
        },
        [BodyZone.RightShin] = new()
        {
            ArterialBleed = 3f,
            MaxHealth = 7,
            Skin = "SkinShinRight",
            Organ = "LegRight",
            Bone = "BoneShinRight",
            Tissues = new() { "MusclesShinRight", "VesselsShinRight" },
            DropSlots = new() { "shoes" },
        },
        [BodyZone.LeftFoot] = new()
        {
            ArterialBleed = 1.5f,
            MaxHealth = 5,
            Skin = "SkinFootLeft",
            Organ = "FootLeft",
            Bone = "BoneFootLeft",
            Tissues = new() { "MusclesFootLeft", "VesselsFootLeft" },
            DropSlots = new() { "shoes" },
        },
        [BodyZone.RightFoot] = new()
        {
            ArterialBleed = 1.5f,
            MaxHealth = 5,
            Skin = "SkinFootRight",
            Organ = "FootRight",
            Bone = "BoneFootRight",
            Tissues = new() { "MusclesFootRight", "VesselsFootRight" },
            DropSlots = new() { "shoes" },
        },
    };

    /// <summary>
    /// A zone without a bone of its own (an eye, the genitals) is destroyed once its health drops to minus this many
    /// times its max health. Zones with a bone are destroyed when the bone is crushed.
    /// </summary>
    [DataField]
    public float DestroyRatio = 1f;

    /// <summary>
    /// The chance that a bone of a destroyed zone is knocked out of the body. Otherwise the bone of the head or
    /// the torso stays in the body, to be pulled out later (the take-apart key), and the bone of a limb part is
    /// gone with the part.
    /// </summary>
    [DataField]
    public float BoneDropChance = 0.05f;

    /// <summary>
    /// How long it takes to pull the bones laid bare out of a body.
    /// </summary>
    [DataField]
    public TimeSpan BoneExtractDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Damage types that land on a specific zone (brute and burn). Everything else (suffocation, poison...) is body-wide.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> TrackedTypes = new() { "Blunt", "Slash", "Piercing", "Heat", "Shock", "Cold", "Caustic" };

    /// <summary>
    /// Damage currently attributed to each zone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<BodyZone, FixedPoint2> Damage = new();

    /// <summary>
    /// Zones that have been destroyed: limbs that are gone, or torso zones that already spilled their organs.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> Destroyed = new();

    /// <summary>
    /// Zones that are there but have lost their skin.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> Skinless = new();

    /// <summary>
    /// Zones that are there but have lost their muscles and vessels: only the bone is left.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> TissuesTorn = new();

    /// <summary>
    /// Zones that are there but have lost their bone (a torn open chest spilled its ribs): nothing is left to see.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> BonesLost = new();

    /// <summary>
    /// Zones whose bone is broken.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> Fractures = new();

    /// <summary>
    /// Zones whose bone is cracked (not broken yet).
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> Cracks = new();

    /// <summary>
    /// Broken zones whose fracture is open: the skin is gone and muscles or vessels are torn, the broken bone is
    /// out in the wound. Seen on the body. A closed fracture is only found by examining.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<BodyZone> OpenFractures = new();

    /// <summary>
    /// Organs that were crushed and stopped working (a heart, a lung, an eye).
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<OrganCategoryPrototype>> FailedOrgans = new();

    /// <summary>
    /// How each damage type goes through the layers of a zone. Types not listed stay in the zone's health only.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, LayerPenetration> Penetration = new()
    {
        // A blade cuts the skin, then the muscles and vessels, then what lies under them. It doesn't cut bone.
        ["Slash"] = new() { Order = new() { TissueLayer.Skin, TissueLayer.Soft, TissueLayer.Organs } },
        // Burns eat the skin, then the flesh.
        ["Heat"] = new() { Order = new() { TissueLayer.Skin, TissueLayer.Soft } },
        ["Cold"] = new() { Order = new() { TissueLayer.Skin, TissueLayer.Soft } },
        ["Caustic"] = new() { Order = new() { TissueLayer.Skin, TissueLayer.Soft } },
        ["Shock"] = new() { Order = new() { TissueLayer.Skin, TissueLayer.Soft } },
        // A bullet or a spear goes through everything.
        ["Piercing"] = new()
        {
            Shares = new()
            {
                [TissueLayer.Skin] = new(0.15f, 0.15f),
                [TissueLayer.Soft] = new(0.35f, 0.35f),
                [TissueLayer.Organs] = new(0.3f, 0.3f),
                [TissueLayer.Bone] = new(0.2f, 0.2f),
            },
        },
        // Crushing: goes through the skin and the flesh to the bones and organs. Random, but mostly the bone.
        ["Blunt"] = new()
        {
            Shares = new()
            {
                [TissueLayer.Skin] = new(0f, 0.08f),
                [TissueLayer.Soft] = new(0.05f, 0.15f),
                [TissueLayer.Organs] = new(0.15f, 0.35f),
                [TissueLayer.Bone] = new(0.45f, 0.7f),
            },
        },
    };

    /// <summary>
    /// Health of the organs that have no <see cref="OrganHealthComponent"/> of their own in YAML (the upstream ones),
    /// by category. Others get <see cref="DefaultOrganHealth"/>.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, float> OrganMaxHealth = new()
    {
        ["Brain"] = 40f,
        ["Heart"] = 25f,
        ["LungLeft"] = 20f,
        ["LungRight"] = 20f,
        ["EyeLeft"] = 6f,
        ["EyeRight"] = 6f,
        ["EarLeft"] = 5f,
        ["EarRight"] = 5f,
        ["Tongue"] = 8f,
        ["Stomach"] = 20f,
        ["Liver"] = 20f,
        ["KidneyLeft"] = 15f,
        ["KidneyRight"] = 15f,
        ["Spleen"] = 15f,
        ["Intestines"] = 20f,
        ["Appendix"] = 5f,
    };

    [DataField]
    public float DefaultOrganHealth = 15f;

    /// <summary>
    /// The organ whose failure kills for good.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype> Brain = "Brain";

    /// <summary>
    /// Health per second a splinted bone grows back.
    /// </summary>
    [DataField]
    public float SplintHealRate = 0.05f;

    /// <summary>
    /// Speed multiplier for every cracked bone of a leg (thigh, shin, foot).
    /// </summary>
    [DataField]
    public float CrackedLegSpeedModifier = 0.9f;

    /// <summary>
    /// Bleeding added when torn muscles fall out. Torn vessels bleed <see cref="TissueLossBleed"/>.
    /// </summary>
    [DataField]
    public float MuscleLossBleed = 1f;

    /// <summary>
    /// Bleeding added when the blood vessels of a zone are torn.
    /// </summary>
    [DataField]
    public float TissueLossBleed = 5f;

    /// <summary>
    /// Bleeding added when a limb is cut off in one blow (<see cref="LimbSeverComponent"/>).
    /// </summary>
    [DataField]
    public float SeverBleed = 5f;

    /// <summary>
    /// Bleeding added when a zone loses its skin.
    /// </summary>
    [DataField]
    public float SkinLossBleed = 3f;

    /// <summary>
    /// Brute and burn damage to a zone without skin is multiplied by this.
    /// </summary>
    [DataField]
    public float SkinlessDamageModifier = 1.25f;

    /// <summary>
    /// A tool of this quality (a knife, a scalpel) can take the skin off. Melee weapons that slash can too.
    /// </summary>
    [DataField]
    public ProtoId<ToolQualityPrototype> SkinningQuality = "Slicing";

    [DataField]
    public TimeSpan SkinningDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Skinning yourself is awkward and takes longer.
    /// </summary>
    [DataField]
    public float SkinningSelfDelayMultiplier = 2f;

    /// <summary>
    /// Speed multiplier for every leg missing from the knee or the hip down.
    /// </summary>
    [DataField]
    public float MissingLegSpeedModifier = 0.5f;

    /// <summary>
    /// Speed multiplier for every missing foot (on a leg that is still there).
    /// </summary>
    [DataField]
    public float MissingFootSpeedModifier = 0.75f;

    [DataField]
    public float BrokenThighSpeedModifier = 0.6f;

    [DataField]
    public float BrokenShinSpeedModifier = 0.7f;

    [DataField]
    public float BrokenFootSpeedModifier = 0.8f;

    /// <summary>
    /// A broken pelvis slows both walking and running.
    /// </summary>
    [DataField]
    public float BrokenPelvisSpeedModifier = 0.7f;

    /// <summary>
    /// Broken ribs make running hard, walking is fine.
    /// </summary>
    [DataField]
    public float BrokenRibsSprintModifier = 0.7f;

    /// <summary>
    /// How long a skull fracture knocks the mob out.
    /// </summary>
    [DataField]
    public TimeSpan SkullFractureStun = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The two lungs. With only one of them left running is harder and every breath takes in less air.
    /// </summary>
    [DataField]
    public List<ProtoId<OrganCategoryPrototype>> Lungs = new() { "LungLeft", "LungRight" };

    /// <summary>
    /// Whether the mob was born with both <see cref="Lungs"/>. Species with a single lungs organ aren't affected.
    /// </summary>
    [DataField]
    public bool PairedLungs;

    /// <summary>
    /// How many of <see cref="Lungs"/> are gone.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MissingLungs;

    [DataField]
    public float OneLungSprintModifier = 0.8f;

    /// <summary>
    /// Oxygen saturation a single lung loses on every breath. A breath normally gives a bit more than the body uses,
    /// so in good air one lung is enough, but in thin air the mob starts to suffocate sooner:
    /// two lungs are fine down to about 90% of normal pressure, one lung only down to about 95%.
    /// </summary>
    [DataField]
    public float OneLungSaturationLoss = 0.25f;

    /// <summary>
    /// Blur while one eye is missing. Without both eyes the mob is blind.
    /// </summary>
    [DataField]
    public float OneEyeBlur = 2f;

    /// <summary>
    /// Zone chosen for the damage instance that is being applied right now.
    /// Null means the damage is spread over the whole body (explosions, environment).
    /// </summary>
    [ViewVariables]
    public BodyZone? PendingZone;

    /// <summary>
    /// Whether <see cref="PendingZone"/> belongs to the damage instance being applied right now.
    /// </summary>
    [ViewVariables]
    public bool HasPendingZone;

    /// <summary>
    /// Whether this system made the mob unrevivable (so rejuvenating can undo it).
    /// </summary>
    [DataField]
    public bool MadeUnrevivable;
}

[DataDefinition]
public sealed partial class BodyZoneData
{
    [DataField]
    public FixedPoint2 MaxHealth = 20;

    /// <summary>
    /// The organ this zone is, e.g. the left forearm. The zone is missing once the organ is gone.
    /// Torso zones have none: they can't be lost, only torn open.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Organ;

    /// <summary>
    /// The organ that holds the zone's bone and <see cref="Contents"/>.
    /// Defaults to <see cref="Organ"/>, or the torso for torso zones.
    /// </summary>
    [DataField]
    public ProtoId<OrganCategoryPrototype>? Inside;

    /// <summary>
    /// If true the zone is a limb part: when it is destroyed whatever is still in it (the bone, layers that held on)
    /// falls out and the part is no more.
    /// The limb parts hanging on it are still whole and fall off as one piece, with everything inside them.
    /// If false everything inside the zone falls out of the body one by one: <see cref="Drop"/>, the bone, the skin,
    /// the tissues, <see cref="Contents"/>, the sex organs and the zone's organ itself (an eye, the genitals).
    /// </summary>
    [DataField]
    public bool TearOff = true;

    /// <summary>
    /// With <see cref="TearOff"/> false: the zone's organ doesn't fall out but is destroyed (a head bursts),
    /// after everything inside it fell out.
    /// </summary>
    [DataField]
    public bool Burst;

    /// <summary>
    /// The bone of the zone, the one that can break. Spawned inside <see cref="Inside"/>.
    /// </summary>
    [DataField]
    public EntProtoId? Bone;

    /// <summary>
    /// The skin of the zone. Spawned inside <see cref="Inside"/>, it can be torn or cut off.
    /// </summary>
    [DataField]
    public EntProtoId? Skin;

    /// <summary>
    /// Muscles and blood vessels of the zone, spawned inside it. They are the layer under the skin:
    /// each tears off once its own health (<see cref="OrganHealthComponent"/>) runs out.
    /// </summary>
    [DataField]
    public List<EntProtoId> Tissues = new();

    /// <summary>
    /// Other organs spawned inside the zone, e.g. the spine.
    /// </summary>
    [DataField]
    public List<EntProtoId> Contents = new();

    /// <summary>
    /// Organs that depend on the mob's sex, spawned inside the zone. They are swapped when the sex changes.
    /// </summary>
    [DataField]
    public Dictionary<Sex, List<EntProtoId>> SexContents = new();

    /// <summary>
    /// Organs of the body (not spawned by this system) that lie in the zone and fall out when it is destroyed.
    /// The bone, the skin, <see cref="Contents"/> and the sex organs fall out without being listed here.
    /// </summary>
    [DataField]
    public List<ProtoId<OrganCategoryPrototype>> Drop = new();

    /// <summary>
    /// Inventory slots whose clothing falls off when the zone is lost.
    /// </summary>
    [DataField]
    public List<string> DropSlots = new();

    /// <summary>
    /// Blood (per second) that gushes out of the stump when this limb part is lost, until a tourniquet stops it.
    /// </summary>
    [DataField]
    public float ArterialBleed;

    /// <summary>
    /// Losing this zone kills the mob for good.
    /// </summary>
    [DataField]
    public bool Fatal;

    /// <summary>
    /// How long the pain of losing the zone knocks the mob down.
    /// </summary>
    [DataField]
    public TimeSpan DestroyStun = TimeSpan.Zero;

    /// <summary>
    /// Popup when the zone is destroyed, instead of the usual "torn off" or "bursts".
    /// </summary>
    [DataField]
    public LocId? Message;
}

/// <summary>
/// The layers of a body zone, from the outside in.
/// </summary>
public enum TissueLayer : byte
{
    /// <summary>The skin of the zone.</summary>
    Skin,

    /// <summary>The muscles and blood vessels (<see cref="BodyZoneData.Tissues"/>).</summary>
    Soft,

    /// <summary>The organs inside the zone: the brain, the heart and lungs, the belly organs, an eye.</summary>
    Organs,

    /// <summary>The bone of the zone.</summary>
    Bone,
}

/// <summary>
/// How a damage type goes into a zone's layers. Either in <see cref="Order"/>: the outermost layer that is still there
/// takes the damage until it is gone, the rest goes deeper (a blade). Or by <see cref="Shares"/>: every hit splits
/// the damage between the layers at random within these bounds (a club).
/// Damage for a layer the zone doesn't have (a limb has no organs, an eye no bone) goes to the others.
/// </summary>
[DataDefinition]
public sealed partial class LayerPenetration
{
    [DataField]
    public List<TissueLayer> Order = new();

    /// <summary>
    /// Smallest and biggest share of the damage each layer takes. Normalized after rolling.
    /// </summary>
    [DataField]
    public Dictionary<TissueLayer, System.Numerics.Vector2> Shares = new();
}
