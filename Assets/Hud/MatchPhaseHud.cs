using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Match phase, turn, and ready/next-turn controls.
/// Energy lives on the scene <see cref="EnergyHud"/> object.
/// </summary>
public class MatchPhaseHud : MonoBehaviour
{
    public Action OnReady;
    public Action OnNextTurn;

    [SerializeField] RectTransform panel;
    [SerializeField] Image banner;
    [SerializeField] Text phaseTitle;
    [SerializeField] Text phaseSubtitle;
    [SerializeField] Text matchInfo;
    [SerializeField] Text turnHelp;
    [SerializeField] Text unitList;
    [SerializeField] EnergyHud energyHud;
    [SerializeField] Button readyButton;
    [SerializeField] Button nextTurnButton;

    void Awake()
    {
        readyButton.onClick.AddListener(() => OnReady?.Invoke());
        nextTurnButton.onClick.AddListener(() => OnNextTurn?.Invoke());
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        if (panel == null || !panel.gameObject.activeInHierarchy)
            return energyHud != null && HudHit.Contains(energyHud.Rect, screenPosition);
        return HudHit.Contains(panel, screenPosition)
            || (energyHud != null && HudHit.Contains(energyHud.Rect, screenPosition));
    }

    public void Refresh(MatchStatus status)
    {
        bool plan = status.Phase == MatchPhase.Plan;

        banner.color = plan
            ? new Color(0.55f, 0.7f, 1f, 0.7f)
            : new Color(1f, 0.55f, 0.5f, 0.7f);

        phaseTitle.text = plan ? "PLAN PHASE" : "BATTLE PHASE";
        phaseSubtitle.text = plan ? "Set orders. Units do not move yet." : "Orders have been resolved.";
        matchInfo.text = "MATCH  |  1 player  |  no win conditions  |  trophies: n/a";
        turnHelp.text = $"Turn {status.Turn}  |  {HelpText(status.Phase)}";
        if (energyHud != null)
            energyHud.SetEnergy(status.EnergyRemaining);
        unitList.text = BuildUnitList(status);
        readyButton.gameObject.SetActive(plan);
        nextTurnButton.gameObject.SetActive(!plan);
    }

    static string HelpText(MatchPhase phase)
    {
        return phase == MatchPhase.Battle
            ? "Review the result, then Next Turn"
            : "Click a character, then choose an action. Press Ready to start battle";
    }

    static string BuildUnitList(MatchStatus status)
    {
        string localName = string.IsNullOrWhiteSpace(status.PlayerName) ? "You" : status.PlayerName;
        var lines = new List<string>();
        for (int i = 0; i < status.Units.Count; i++)
        {
            Unit unit = status.Units[i];
            UnitStats stats = unit.behaviour.CurrentStats;
            bool playerOwned = status.PlayerUnits.Contains(unit);
            string owner = playerOwned ? localName : "Dummy";
            string order = playerOwned ? PlanLabel(status, unit) : "no turn";
            string marker = unit == status.Selected ? "> " : "  ";
            lines.Add($"{marker}{stats.displayName} ({owner})  HP {stats.health}/{stats.maxHealth}  {order}");
        }

        return string.Join("\n", lines);
    }

    static string PlanLabel(MatchStatus status, Unit unit)
    {
        PlannedAction plan = unit.behaviour.Plan;
        if (plan == null)
            return "no order";
        switch (plan.Type)
        {
            case PlannedActionType.Move:
                return plan.TargetPositions != null && plan.TargetPositions.Count == 1
                    ? $"MOVE to {plan.GetSoleTarget()}"
                    : "MOVE";
            case PlannedActionType.Attack:
                return AttackLabel(status, plan);
            case PlannedActionType.Utility:
                return UtilityLabel(status, plan);
            default:
                return "no order";
        }
    }

    static string AttackLabel(MatchStatus status, PlannedAction plan)
    {
        if (plan.TargetPositions == null || plan.TargetPositions.Count == 0)
            return "ATTACK";

        var names = new List<string>();
        foreach (Position cell in plan.TargetPositions)
        {
            Unit target = UnitAt(status.Units, cell);
            if (target == null)
                continue;
            string name = target.behaviour.CurrentStats.displayName;
            if (!names.Contains(name))
                names.Add(name);
        }

        if (names.Count > 0)
            return "ATTACK " + string.Join(", ", names);
        return $"ATTACK {plan.TargetPositions.Count} tiles";
    }

    static string UtilityLabel(MatchStatus status, PlannedAction plan)
    {
        if (plan.TargetPositions == null || plan.TargetPositions.Count == 0)
            return "HEAL";

        var names = new List<string>();
        foreach (Position cell in plan.TargetPositions)
        {
            Unit target = UnitAt(status.Units, cell);
            if (target == null || status.PlayerUnits == null || !status.PlayerUnits.Contains(target))
                continue;
            string name = target.behaviour.CurrentStats.displayName;
            if (!names.Contains(name))
                names.Add(name);
        }

        if (names.Count > 0)
            return "HEAL " + string.Join(", ", names);
        return $"HEAL {plan.TargetPositions.Count} tiles";
    }

    static Unit UnitAt(IReadOnlyList<Unit> units, Position position)
    {
        for (int i = 0; i < units.Count; i++)
        {
            Unit unit = units[i];
            if (unit.behaviour.Position.SameCell(position))
                return unit;
        }

        return null;
    }
}
