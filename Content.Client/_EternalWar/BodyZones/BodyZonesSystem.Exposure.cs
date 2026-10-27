using Content.Shared._EternalWar.BodyZones;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._EternalWar.BodyZones;

/// <summary>
/// Draws a body laid bare, zone by zone, by the outermost layer the zone still has: the skin, the bare muscles where
/// the skin is gone, the bones where the muscles are gone too, and nothing where the bone is gone as well
/// (a torn open chest has spilled its ribs).
/// The upstream body parts are bigger than the zones (the arm is the upper arm and the forearm), so every zone has
/// its own <c>Exposed*</c> layer above its part (<c>species_appearance.yml</c>). While any zone of a part is laid
/// bare, the part's own layer is hidden and the part is drawn from the zone pieces; the pieces that still have
/// their skin are tinted with the skin color.
/// </summary>
public sealed partial class BodyZonesSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private static readonly ResPath ExposedRsi = new("/Textures/_EternalWar/Mobs/Species/Human/exposed.rsi");

    /// <summary>
    /// The sprites are cut to the human body.
    /// </summary>
    private static readonly ProtoId<SpeciesPrototype> ExposedSpecies = "Human";

    /// <summary>
    /// The piece of a body part that shows one zone: its layer and its state name (sexed pieces get <c>_m</c> or <c>_f</c>).
    /// </summary>
    private readonly record struct ExposedPiece(BodyZone Zone, EternalWarHumanoidLayers Layer, string State);

    /// <summary>
    /// A body part of the sprite, the zone whose organ draws it (the torso is the body itself) and its pieces.
    /// </summary>
    private readonly record struct ExposedPart(HumanoidVisualLayers Layer, BodyZone? DrawnBy, bool Sexed, ExposedPiece[] Pieces);

    private static readonly ExposedPart[] ExposedParts =
    {
        new(HumanoidVisualLayers.Head, BodyZone.Head, true, new ExposedPiece[]
        {
            new(BodyZone.Head, EternalWarHumanoidLayers.ExposedHead, "head"),
        }),
        new(HumanoidVisualLayers.Chest, null, true, new ExposedPiece[]
        {
            new(BodyZone.Chest, EternalWarHumanoidLayers.ExposedChest, "chest"),
            new(BodyZone.Abdomen, EternalWarHumanoidLayers.ExposedAbdomen, "abdomen"),
        }),
        // The upstream arm and leg are drawn by the forearm and the shin: without them there is nothing to draw.
        new(HumanoidVisualLayers.LArm, BodyZone.LeftForearm, false, new ExposedPiece[]
        {
            new(BodyZone.LeftUpperArm, EternalWarHumanoidLayers.ExposedLUpperArm, "l_upper_arm"),
            new(BodyZone.LeftForearm, EternalWarHumanoidLayers.ExposedLForearm, "l_forearm"),
        }),
        new(HumanoidVisualLayers.RArm, BodyZone.RightForearm, false, new ExposedPiece[]
        {
            new(BodyZone.RightUpperArm, EternalWarHumanoidLayers.ExposedRUpperArm, "r_upper_arm"),
            new(BodyZone.RightForearm, EternalWarHumanoidLayers.ExposedRForearm, "r_forearm"),
        }),
        new(HumanoidVisualLayers.LHand, BodyZone.LeftHand, false, new ExposedPiece[]
        {
            new(BodyZone.LeftHand, EternalWarHumanoidLayers.ExposedLHand, "l_hand"),
        }),
        new(HumanoidVisualLayers.RHand, BodyZone.RightHand, false, new ExposedPiece[]
        {
            new(BodyZone.RightHand, EternalWarHumanoidLayers.ExposedRHand, "r_hand"),
        }),
        new(HumanoidVisualLayers.LLeg, BodyZone.LeftShin, false, new ExposedPiece[]
        {
            new(BodyZone.LeftThigh, EternalWarHumanoidLayers.ExposedLThigh, "l_thigh"),
            new(BodyZone.LeftShin, EternalWarHumanoidLayers.ExposedLShin, "l_shin"),
        }),
        new(HumanoidVisualLayers.RLeg, BodyZone.RightShin, false, new ExposedPiece[]
        {
            new(BodyZone.RightThigh, EternalWarHumanoidLayers.ExposedRThigh, "r_thigh"),
            new(BodyZone.RightShin, EternalWarHumanoidLayers.ExposedRShin, "r_shin"),
        }),
        new(HumanoidVisualLayers.LFoot, BodyZone.LeftFoot, false, new ExposedPiece[]
        {
            new(BodyZone.LeftFoot, EternalWarHumanoidLayers.ExposedLFoot, "l_foot"),
        }),
        new(HumanoidVisualLayers.RFoot, BodyZone.RightFoot, false, new ExposedPiece[]
        {
            new(BodyZone.RightFoot, EternalWarHumanoidLayers.ExposedRFoot, "r_foot"),
        }),
    };

    protected override void UpdateExposure(Entity<BodyZonesComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp)
            || !TryComp<HumanoidProfileComponent>(ent, out var profile)
            || profile.Species != ExposedSpecies)
            return;

        var sprite = (ent.Owner, spriteComp);
        var sex = profile.Sex == Sex.Female ? "_f" : "_m";
        foreach (var part in ExposedParts)
        {
            if (!_sprite.LayerMapTryGet(sprite, part.Layer, out var layer, false))
                continue;

            // Nothing to draw if the part itself is gone.
            var present = part.DrawnBy is not { } drawnBy || !IsMissing(ent.Comp, drawnBy);

            var bare = false;
            foreach (var piece in part.Pieces)
            {
                bare |= present && !IsMissing(ent.Comp, piece.Zone) && GetExposure(ent.Comp, piece.Zone) > 0;
            }

            // Whole: the part's own layer draws it, with its markings.
            _sprite.LayerSetVisible(sprite, layer, !bare);
            var skinColor = spriteComp[layer].Color;

            foreach (var piece in part.Pieces)
            {
                if (!_sprite.LayerMapTryGet(sprite, piece.Layer, out var pieceLayer, false))
                    continue;

                var exposure = GetExposure(ent.Comp, piece.Zone);

                // Gone, or nothing left in it (a torn open chest spilled its skin, muscles and ribs): an empty space.
                if (!bare || IsMissing(ent.Comp, piece.Zone) || exposure >= 3)
                {
                    _sprite.LayerSetVisible(sprite, pieceLayer, false);
                    continue;
                }

                var state = part.Sexed ? piece.State + sex : piece.State;
                var kind = exposure switch
                {
                    0 => "skin",
                    1 => "flesh",
                    _ => "bone",
                };

                // An open fracture shows: a shard of bone out of the wound, or a break across the bare bone.
                if (exposure > 0 && ent.Comp.OpenFractures.Contains(piece.Zone))
                    kind += "_open";

                _sprite.LayerSetRsi(sprite, pieceLayer, ExposedRsi, $"{kind}_{state}");
                _sprite.LayerSetColor(sprite, pieceLayer, exposure == 0 ? skinColor : Color.White);
                _sprite.LayerSetVisible(sprite, pieceLayer, true);
            }
        }
    }
}
