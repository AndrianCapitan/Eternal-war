using Content.Client.Clothing;
using Content.Shared._EternalWar.Hygiene;
using Content.Shared.Clothing;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._EternalWar.Hygiene;

/// <summary>
/// Draws the blood on worn clothing: a stain cut to the body part the slot covers, tinted with the blood color.
/// </summary>
public sealed partial class HygieneSystem : SharedHygieneSystem
{
    [Dependency] private SharedItemSystem _item = default!;

    private static readonly ResPath StainsRsi = new("/Textures/_EternalWar/Objects/Misc/blood_stains.rsi");

    /// <summary>
    /// The part of the body the clothing in each slot covers. Other slots show no stains when worn.
    /// </summary>
    private static readonly Dictionary<string, string> SlotStains = new()
    {
        ["jumpsuit"] = "body",
        ["outerClothing"] = "body",
        ["gloves"] = "hands",
        ["shoes"] = "feet",
        ["head"] = "head",
        ["mask"] = "face",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodStainedComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals, after: new[] { typeof(ClientClothingSystem) });
        SubscribeLocalEvent<BloodStainedComponent, AfterAutoHandleStateEvent>(OnStainState);
        SubscribeLocalEvent<BloodStainedComponent, ComponentShutdown>(OnStainShutdown);
    }

    private void OnGetEquipmentVisuals(Entity<BloodStainedComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running
            || ent.Comp.Level == 0
            || !SlotStains.TryGetValue(args.Slot, out var part))
            return;

        args.Layers.Add(($"{args.Slot}-blood", new PrototypeLayerData
        {
            RsiPath = StainsRsi.ToString(),
            State = $"{part}_{ent.Comp.Level}",
            Color = ent.Comp.Color,
        }));
    }

    private void OnStainState(Entity<BloodStainedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _item.VisualsChanged(ent);
    }

    private void OnStainShutdown(Entity<BloodStainedComponent> ent, ref ComponentShutdown args)
    {
        _item.VisualsChanged(ent);
    }
}
