using Content.Shared._EternalWar.Humanoid;
using Content.Shared.Body;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.IdentityManagement;
using Robust.Shared.Prototypes;

namespace Content.Shared._EternalWar.Breasts;

/// <summary>
/// Tells the breast size on examine, see <see cref="BreastSizeComponent"/>, and takes it away when the mob stops
/// being female (the genome can change the sex). The size itself is only a look: the marking is drawn on the torso
/// under the underwear and clothing.
/// </summary>
public sealed partial class BreastSizeSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreastSizeComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BreastSizeComponent, SexChangedEvent>(OnSexChanged);
    }

    private void OnExamined(Entity<BreastSizeComponent> ent, ref ExaminedEvent args)
    {
        foreach (var organ in _body.EnumerateOrgans<VisualOrganMarkingsComponent>(ent.Owner))
        {
            // Markings may only be read from outside the body systems.
            var all = organ.Comp2.Markings;
            if (!all.TryGetValue(HumanoidVisualLayers.Breasts, out var markings))
                continue;

            foreach (var marking in markings)
            {
                if (ent.Comp.Examine.TryGetValue(marking.MarkingId, out var text))
                {
                    args.PushMarkup(Loc.GetString(text, ("target", Identity.Entity(ent, EntityManager))));
                    return;
                }
            }
        }
    }

    private void OnSexChanged(Entity<BreastSizeComponent> ent, ref SexChangedEvent args)
    {
        if (args.NewSex == Sex.Female)
            return;

        var cleared = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();
        foreach (var organ in _body.EnumerateOrgans<VisualOrganMarkingsComponent>(ent.Owner))
        {
            var all = organ.Comp2.Markings;
            if (organ.Comp1.Category is { } category
                && all.TryGetValue(HumanoidVisualLayers.Breasts, out var markings)
                && markings.Count > 0)
            {
                cleared[category] = new() { [HumanoidVisualLayers.Breasts] = new() };
            }
        }

        if (cleared.Count > 0)
            _visualBody.ApplyMarkings(ent, cleared);
    }
}
