using Content.Shared._EternalWar.BodyZones;
using Content.Shared._EternalWar.Organs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server._EternalWar.Organs;

/// <summary>
/// The stomach passes what was eaten on: the poisons to the liver, which holds them back and breaks them down
/// (what doesn't fit goes into the blood), the rest to the intestines, which absorb it.
/// Now and then a bit of food ends up in the appendix; once it is full, it hurts and makes the body vomit.
/// </summary>
public sealed partial class OrganFunctionsSystem
{
    /// <summary>
    /// Whether the stomach sends this reagent to the liver.
    /// </summary>
    public bool IsPoison(OrganFunctionsComponent comp, string reagent)
    {
        return !comp.NotPoisons.Contains(reagent)
            && ProtoMan.TryIndex<ReagentPrototype>(reagent, out var proto)
            && comp.PoisonGroups.Contains(proto.Group);
    }

    private void UpdateDigestion(Entity<OrganFunctionsComponent> ent, Entity<BodyZonesComponent> body)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.LiverSolution, out var liverEnt, out var liver)
            || !_solutions.TryGetSolution(ent.Owner, ent.Comp.IntestinesSolution, out var intestinesEnt, out var intestines))
            return;

        var liverWorks = _zones.HasWorkingOrgan(body, ent.Comp.Liver);

        // The liver breaks down what it held back; without it the poison goes on into the blood.
        if (liver.Volume > FixedPoint2.Zero)
        {
            var held = _solutions.SplitSolution(liverEnt.Value, liverWorks ? ent.Comp.LiverBreakdownRate : ent.Comp.StomachTransferRate);
            if (!liverWorks)
                _bloodstream.TryAddToBloodstream(ent.Owner, held);
        }

        if (GetStomach(ent, body) is not { } stomachEnt || stomachEnt.Comp.Solution.Volume <= FixedPoint2.Zero)
            return;

        var portion = _solutions.SplitSolution(stomachEnt, ent.Comp.StomachTransferRate);
        var poison = new Solution();
        var food = new Solution();
        foreach (var (reagent, quantity) in portion.Contents)
        {
            (IsPoison(ent.Comp, reagent.Prototype) ? poison : food).AddReagent(reagent, quantity);
        }

        // Back into the stomach whatever has nowhere to go.
        var back = new Solution();

        if (poison.Volume > FixedPoint2.Zero)
        {
            if (liverWorks)
                _solutions.TryAddSolution(liverEnt.Value, poison.SplitSolution(liver.AvailableVolume));

            if (poison.Volume > FixedPoint2.Zero && !_bloodstream.TryAddToBloodstream(ent.Owner, poison))
                back.AddSolution(poison, ProtoMan);
        }

        if (food.Volume > FixedPoint2.Zero)
        {
            if (_zones.HasWorkingOrgan(body, ent.Comp.Intestines))
            {
                if (_zones.HasWorkingOrgan(body, ent.Comp.Appendix)
                    && _random.Prob(ent.Comp.AppendixChance)
                    && _solutions.TryGetSolution(ent.Owner, ent.Comp.AppendixSolution, out var appendixEnt, out var appendix))
                {
                    _solutions.TryAddSolution(appendixEnt.Value, food.SplitSolution(appendix.AvailableVolume));
                }

                _solutions.TryAddSolution(intestinesEnt.Value, food.SplitSolution(intestines.AvailableVolume));
            }

            // Without intestines nothing is absorbed: it stays in the stomach.
            back.AddSolution(food, ProtoMan);
        }

        if (back.Volume > FixedPoint2.Zero)
            _solutions.AddSolution(stomachEnt, back);
    }

    /// <summary>
    /// The solution of the body's working stomach.
    /// </summary>
    private Entity<SolutionComponent>? GetStomach(Entity<OrganFunctionsComponent> ent, Entity<BodyZonesComponent> body)
    {
        if (!_zones.HasWorkingOrgan(body, ent.Comp.Stomach))
            return null;

        foreach (var organ in _zones.GetOrgans(body, ent.Comp.Stomach))
        {
            if (HasComp<StomachComponent>(organ)
                && _solutions.TryGetSolution(organ, StomachSystem.DefaultSolutionName, out var solution))
                return solution;
        }

        return null;
    }

    #region Appendix

    private void UpdateAppendix(Entity<OrganFunctionsComponent> ent, Entity<BodyZonesComponent> body, TimeSpan now)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.AppendixSolution, out var appendixEnt, out var appendix))
            return;

        // Taken out, it took what was in it along.
        if (_zones.GetOrgans(body, ent.Comp.Appendix).Count == 0)
        {
            if (appendix.Volume > FixedPoint2.Zero)
                _solutions.RemoveAllSolution(appendixEnt.Value);

            SetAppendicitis(ent, false);
            return;
        }

        var fill = appendix.MaxVolume > FixedPoint2.Zero ? (float) (appendix.Volume / appendix.MaxVolume) : 0f;

        if (!ent.Comp.Appendicitis)
        {
            if (fill >= 1f)
            {
                SetAppendicitis(ent, true);
                ent.Comp.NextVomit = now + ent.Comp.AppendixFirstVomit;
            }

            return;
        }

        _solutions.SplitSolution(appendixEnt.Value, ent.Comp.AppendixDrainRate);
        if (fill <= ent.Comp.AppendixRecoveredAt)
        {
            SetAppendicitis(ent, false);
            return;
        }

        if (now < ent.Comp.NextVomit)
            return;

        ent.Comp.NextVomit = now + _random.Next(ent.Comp.AppendixVomitMin, ent.Comp.AppendixVomitMax);
        _popup.PopupEntity(Loc.GetString("organ-functions-appendicitis-cramp"), ent, ent, PopupType.MediumCaution);
        _vomit.Vomit(ent);
    }

    private void SetAppendicitis(Entity<OrganFunctionsComponent> ent, bool inflamed)
    {
        if (ent.Comp.Appendicitis == inflamed)
            return;

        ent.Comp.Appendicitis = inflamed;
        if (inflamed)
        {
            _alerts.ShowAlert(ent.Owner, ent.Comp.AppendixAlert);
            _popup.PopupEntity(Loc.GetString("organ-functions-appendicitis-start"), ent, ent, PopupType.LargeCaution);
            _adminLogger.Add(LogType.Damaged, LogImpact.Low, $"{ToPrettyString(ent):target} got appendicitis");
        }
        else
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.AppendixAlert);
            if (!TerminatingOrDeleted(ent))
                _popup.PopupEntity(Loc.GetString("organ-functions-appendicitis-end"), ent, ent, PopupType.Medium);
        }
    }

    #endregion
}
