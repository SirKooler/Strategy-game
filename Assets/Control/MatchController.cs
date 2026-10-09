using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Match bootstrap: wires services, reads plan-phase clicks, and advances phases.
/// Spawn lives on <see cref="MatchSpawner"/>; plan marks on <see cref="PlanMarks"/>;
/// plan rules on <see cref="OrderPlanner"/>; battle play on <see cref="BattlePlayback"/>.
/// </summary>
public partial class MatchController : MonoBehaviour
{
    [SerializeField] PlaceholderBoard board;
    [SerializeField] Player player;

    /// <summary>Placeholder target so Attack has something to hit. Not from the player deck.</summary>
    [SerializeField] UnitStats dummyDefinition;
    [SerializeField] Position dummyStart = new Position(5, 3);
    [SerializeField] MatchPhaseHud phaseHud;
    [SerializeField] UnitActionMenu actionMenu;
    [SerializeField] CharacterMenu characterMenu;
    [SerializeField] HealthBar healthBarPrefab;
    [SerializeField] bool useCloudMatch;
    [SerializeField] string joinMatchId;

    readonly MatchRuntime _runtime = new MatchRuntime();
    readonly MatchStatus _status = new MatchStatus();
    readonly MatchClient _cloud = new MatchClient();
    OrderPlanner _planner;
    BattleResolver _resolver;
    BattlePlayback _playback;
    MatchSpawner _spawner;
    PlanMarks _marks;
    CloudMatchSnapshot _snapshot;
    bool _cloudReadyBusy;
    bool _cancelReadyRequested;
    bool _holdReadyUntilOpponent;
    int _submittedTurn;

    void Start()
    {
        _spawner = new MatchSpawner(_runtime, board, transform, player);
        _spawner.SetHealthBar(healthBarPrefab);
        _planner = new OrderPlanner(_runtime, board, _spawner);
        _resolver = new BattleResolver(_runtime, board);
        _marks = new PlanMarks(_runtime, board);
        _playback = new BattlePlayback(_resolver, _marks, board, RefreshHud, SettleFlags, PresentFlagLandings, () => _runtime.Winner != MatchWinner.None);
        actionMenu.OnRememberPlan = _planner.RememberPlan;
        actionMenu.OnMove = () => AfterPlanChange(() => _planner.ChooseAction(UnitPlanKind.Move));
        actionMenu.OnAttack = () => AfterPlanChange(() => _planner.ChooseAction(UnitPlanKind.Attack));
        actionMenu.OnUtility = () => AfterPlanChange(() => _planner.ChooseAction(UnitPlanKind.Utility));
        actionMenu.OnClear = () => AfterPlanChange(_planner.ClearSelectedPlan);
        actionMenu.OnUnspawn = () => AfterPlanChange(() => _spawner.TryUnspawn(_runtime.Selected));
        actionMenu.OnRevertPlan = () => AfterPlanChange(_planner.RevertViewedAction);
        actionMenu.OnClose = () => AfterPlanChange(() => _planner.Select(null));
        phaseHud.OnReady = ConfirmOrders;
        phaseHud.OnHome = () => SceneManager.LoadScene(HomeScreen.SceneName);
        if (characterMenu == null)
            characterMenu = FindAnyObjectByType<CharacterMenu>(FindObjectsInactive.Include);

        if (characterMenu != null)
        {
            characterMenu.OnSpawn = definition => AfterPlanChange(() => _planner.ChoosePendingSpawn(definition));
            Canvas canvas = FindAnyObjectByType<Canvas>();
            Transform deck = canvas != null
                ? canvas.transform.Find("TopHud/" + CharacterMenu.ToggleButtonName)
                : null;
            if (deck != null)
                characterMenu.BindToggle(deck.GetComponent<UnityEngine.UI.Button>());
        }

        if (MatchSession.HasMatch || useCloudMatch)
            StartCoroutine(StartCloudMatch());
        else
            StartMatch();
    }

    void Update()
    {
        if (!TryReadClick(out Vector2 screen))
            return;

        if (characterMenu != null)
            characterMenu.HideIfClickedOutside(screen);

        if (_runtime.Phase != MatchPhase.Plan)
            return;
        if (PointerOnHud(screen))
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 screen3 = new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z));
        AfterPlanChange(() => _planner.HandleClick(cam.ScreenToWorldPoint(screen3)));
    }

    /// <summary>
    /// Device Simulator and phones send touch. Editor Game view sends mouse.
    /// Prefer touch so simulator coordinates match the camera pixel rect.
    /// </summary>
    static bool TryReadClick(out Vector2 screen)
    {
        screen = default;
        Touchscreen touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            screen = touch.primaryTouch.position.ReadValue();
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screen = mouse.position.ReadValue();
            return true;
        }

        return false;
    }

    void StartMatch()
    {
        board.ShowFarRowAtBottom(false);
        _runtime.LocalPlayerId = Flag.LocalSeat;
        _runtime.OpponentPlayerId = Flag.OpponentSeat;
        board.AssignFlagOwners(_runtime.LocalPlayerId, _runtime.OpponentPlayerId, _runtime.HomeRow, _runtime.LocalPlayerId);
        _spawner.TrySpawnFromDeck(asOpening: true);
        if (dummyDefinition != null)
            _spawner.TrySpawn(dummyDefinition, false, dummyStart);
        _runtime.Turn = 1;
        _runtime.Phase = MatchPhase.Plan;
        AfterPlanChange(() => _planner.Select(null));
    }

    void ConfirmOrders()
    {
        if (_runtime.Phase != MatchPhase.Plan || _runtime.Winner != MatchWinner.None)
            return;
        if (useCloudMatch)
        {
            if (_runtime.IsWaitingForOpponent)
                StartCoroutine(WithdrawCloudReady());
            else
                StartCoroutine(ConfirmCloudOrders());
            return;
        }

        _runtime.Phase = MatchPhase.Battle;
        _runtime.Selected = null;
        _runtime.ChosenAction = UnitPlanKind.None;
        _runtime.ClearPendingSpawn();
        _runtime.IsResolving = true;
        board.ClearMarks();
        RefreshHud();
        StartCoroutine(PlayBattle());
    }

    IEnumerator PlayBattle()
    {
        yield return _playback.Play();
        _runtime.IsResolving = false;
        RefreshHud();
        yield return new WaitForSeconds(1f);
        NextTurn();
    }

    void NextTurn()
    {
        if (_runtime.IsResolving || _runtime.Winner != MatchWinner.None)
            return;

        _runtime.Phase = MatchPhase.Plan;
        if (useCloudMatch && _snapshot != null)
        {
            _runtime.Turn = _snapshot.turn;
            _runtime.SpawnEnergySpent = 0;
            _runtime.PaidPlayerSpawnCount = _snapshot.local != null ? _snapshot.local.paidSpawnCount : 0;
        }
        else
        {
            _runtime.Turn += 1;
            _runtime.SpawnEnergySpent = 0;
        }

        AfterPlanChange(() => _planner.Select(null));
    }

    void AfterPlanChange(System.Action change)
    {
        _planner.BeginChange();
        string plansBefore = WaitingPlanFingerprint();
        change();
        _marks.Refresh();
        RefreshHud();
        if (_planner.TookAction && actionMenu != null)
            actionMenu.ReturnToDefault();
        if (plansBefore != null && plansBefore != WaitingPlanFingerprint() && !_cloudReadyBusy && !_holdReadyUntilOpponent)
            StartCoroutine(ReplaceSubmittedPlans());
    }

    bool PointerOnHud(Vector2 screen)
    {
        return phaseHud.ContainsScreenPoint(screen)
            || actionMenu.ContainsScreenPoint(screen)
            || (characterMenu != null && characterMenu.ContainsScreenPoint(screen));
    }

    void SettleFlags()
    {
        _runtime.CaptureFlags(board);
    }

    void PresentFlagLandings(BattleTick tick)
    {
        if (tick == null)
            return;
        for (int i = 0; i < tick.Moves.Count; i++)
        {
            BattleMove move = tick.Moves[i];
            if (move.Blocked || move.Unit == null || move.Destination == null)
                continue;
            Flag flag = board.GetFlag(move.Destination.Column, move.Destination.Row);
            if (flag == null)
                continue;
            string owner = _runtime.OwnerIdOf(move.Unit);
            flag.SetOwner(owner, Flag.ForPlayer(owner, _runtime.LocalPlayerId));
        }
    }

    void RefreshHud()
    {
        _status.Fill(_runtime, player, _spawner.CanSpawnNow());
        phaseHud.Refresh(_status);
        actionMenu.Refresh(_status);
        if (characterMenu != null)
            characterMenu.Refresh(_status);
    }
}
