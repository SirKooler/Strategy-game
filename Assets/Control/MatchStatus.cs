using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One deck entry as the character menu should draw it.
/// </summary>
public enum DeckSlotState
{
    Available,
    OnBoard,
    Dead
}

public class DeckSlotStatus
{
    public UnitStats Definition;
    public string DisplayName;
    public Sprite DeckSprite;
    public DeckSlotState State;
    public bool CanSpawnThis;
    public bool IsPending;
}

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
    public bool IsResolving;
    public bool IsWaitingForOpponent;
    public MatchWinner Winner;
    public readonly List<DeckSlotStatus> DeckSlots = new List<DeckSlotStatus>();

    public void Fill(MatchRuntime runtime, Player player, bool canSpawn)
    {
        Phase = runtime.Phase;
        Turn = runtime.Turn;
        Units = runtime.Units;
        Selected = runtime.Selected;
        PlayerName = player != null ? player.username : null;
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
        IsResolving = runtime.IsResolving;
        IsWaitingForOpponent = runtime.IsWaitingForOpponent;
        Winner = runtime.Winner;
        FillDeckSlots(runtime, player, canSpawn);
    }

    void FillDeckSlots(MatchRuntime runtime, Player player, bool canSpawn)
    {
        DeckSlots.Clear();
        if (player == null || player.deck == null)
            return;

        for (int i = 0; i < player.deck.Count; i++)
        {
            UnitStats definition = player.deck[i];
            if (definition == null)
                continue;

            ResolveIdentity(definition, out string displayName);
            DeckSlotState state = DeckSlotState.Available;
            if (runtime.DeadDefinitions.Contains(definition))
                state = DeckSlotState.Dead;
            else if (runtime.IsDefinitionOnBoard(definition))
                state = DeckSlotState.OnBoard;

            DeckSlots.Add(new DeckSlotStatus
            {
                Definition = definition,
                DisplayName = displayName,
                DeckSprite = CharacterAppearance.Deck(definition),
                State = state,
                CanSpawnThis = canSpawn && state == DeckSlotState.Available,
                IsPending = runtime.PendingSpawn == definition
            });
        }
    }

    static void ResolveIdentity(UnitStats definition, out string displayName)
    {
        displayName = definition.displayName;
        for (int i = 0; i < UnitConstants.All.Length; i++)
        {
            if (!UnitConstants.All[i].Matches(definition))
                continue;
            displayName = UnitConstants.All[i].DisplayName;
            return;
        }
    }
}
