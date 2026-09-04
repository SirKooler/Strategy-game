using System.Collections.Generic;

/// <summary>
/// Snapshot the HUD reads each refresh. MatchController fills this; HUD does not query the match.
/// </summary>
public class MatchStatus
{
    public MatchPhase Phase;
    public int Turn;
    public IReadOnlyList<Unit> Units;
    public Unit Selected;
    public string PlayerName;
    public ICollection<Unit> PlayerUnits;
    public UnitPlanKind ChosenAction;
    public bool CanPlanSelected;
    public int EnergyRemaining;
    public bool CanAffordMove;
    public bool CanAffordAttack;
    public bool CanAffordUtility;
    public bool HasUtility;
    public int NextSpawnCost;
    public bool SpawnCostIsInfinite;
    public bool CanSpawn;
    public bool CanUnspawnSelected;

    public void Fill(MatchRuntime runtime, string playerName, bool canSpawn)
    {
        Phase = runtime.Phase;
        Turn = runtime.Turn;
        Units = runtime.Units;
        Selected = runtime.Selected;
        PlayerName = playerName;
        PlayerUnits = runtime.PlayerUnits;
        ChosenAction = runtime.ChosenAction;
        CanPlanSelected = runtime.CanIssueOrders(runtime.Selected);
        EnergyRemaining = runtime.EnergyRemaining;
        CanAffordMove = runtime.CanAfford(runtime.Selected, UnitPlanKind.Move);
        CanAffordAttack = runtime.CanAfford(runtime.Selected, UnitPlanKind.Attack);
        CanAffordUtility = runtime.CanAfford(runtime.Selected, UnitPlanKind.Utility);
        HasUtility = runtime.Selected != null
            && runtime.Selected.behaviour != null
            && runtime.Selected.behaviour.HasUtility;
        NextSpawnCost = runtime.NextSpawnCost;
        SpawnCostIsInfinite = runtime.SpawnCostIsInfinite;
        CanSpawn = canSpawn;
        CanUnspawnSelected = runtime.CanUnspawn(runtime.Selected);
    }
}
