using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Match bootstrap: wires services, reads plan-phase clicks, and advances phases.
/// Spawn lives on <see cref="MatchSpawner"/>; plan marks on <see cref="PlanMarks"/>;
/// plan rules on <see cref="OrderPlanner"/>; battle on <see cref="BattleResolver"/>.
/// </summary>
public class MatchController : MonoBehaviour
{
    [SerializeField] PlaceholderBoard board;
    [SerializeField] Player player;

    /// <summary>Placeholder target so Attack has something to hit. Not from the player deck.</summary>
    [SerializeField] UnitStats dummyDefinition;
    [SerializeField] Position dummyStart = new Position(5, 3);
    [SerializeField] MatchPhaseHud phaseHud;
    [SerializeField] UnitActionMenu actionMenu;
    [SerializeField] SpawnHud spawnHud;

    readonly MatchRuntime _runtime = new MatchRuntime();
    readonly MatchStatus _status = new MatchStatus();
    OrderPlanner _planner;
    BattleResolver _resolver;
    MatchSpawner _spawner;
    PlanMarks _marks;

    void Start()
    {
        _planner = new OrderPlanner(_runtime, board);
        _resolver = new BattleResolver(_runtime, board);
        _spawner = new MatchSpawner(_runtime, board, transform, player);
        _marks = new PlanMarks(_runtime, board);
        actionMenu.OnMove = () => AfterPlanChange(() => _planner.ChooseAction(UnitPlanKind.Move));
        actionMenu.OnAttack = () => AfterPlanChange(() => _planner.ChooseAction(UnitPlanKind.Attack));
        actionMenu.OnUtility = () => AfterPlanChange(() => _planner.ChooseAction(UnitPlanKind.Utility));
        actionMenu.OnClear = () => AfterPlanChange(_planner.ClearSelectedPlan);
        actionMenu.OnUnspawn = () => AfterPlanChange(() => _spawner.TryUnspawn(_runtime.Selected));
        phaseHud.OnReady = ConfirmOrders;
        phaseHud.OnNextTurn = NextTurn;
        if (spawnHud == null)
            spawnHud = FindAnyObjectByType<SpawnHud>();
        if (spawnHud != null)
            spawnHud.OnSpawn = TrySpawnNextFromDeck;
        StartMatch();
    }

    void Update()
    {
        if (_runtime.Phase != MatchPhase.Plan)
            return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;
        if (PointerOnHud())
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector2 screen = Mouse.current.position.ReadValue();
        AfterPlanChange(() => _planner.HandleClick(cam.ScreenToWorldPoint(screen)));
    }

    void StartMatch()
    {
        _spawner.SetUnitSprite(PlaceholderSprite.WhiteSquare());
        _spawner.TrySpawnFromDeck(asOpening: true);
        if (dummyDefinition != null)
            _spawner.TrySpawn(dummyDefinition, false, dummyStart);
        _runtime.Turn = 1;
        _runtime.Phase = MatchPhase.Plan;
        AfterPlanChange(() => _planner.Select(null));
    }

    void TrySpawnNextFromDeck()
    {
        if (!_spawner.TrySpawnFromDeck())
            return;
        _marks.Refresh();
        RefreshHud();
    }

    void ConfirmOrders()
    {
        if (_runtime.Phase != MatchPhase.Plan)
            return;

        _runtime.Phase = MatchPhase.Battle;
        _runtime.Selected = null;
        _runtime.ChosenAction = UnitPlanKind.None;
        board.ClearMarks();
        _resolver.Execute();
        RefreshHud();
    }

    void NextTurn()
    {
        _runtime.Phase = MatchPhase.Plan;
        _runtime.Turn += 1;
        _runtime.SpawnEnergySpent = 0;
        AfterPlanChange(() => _planner.Select(null));
    }

    void AfterPlanChange(System.Action change)
    {
        change();
        _marks.Refresh();
        RefreshHud();
    }

    bool PointerOnHud()
    {
        Vector2 screen = Mouse.current.position.ReadValue();
        return phaseHud.ContainsScreenPoint(screen)
            || actionMenu.ContainsScreenPoint(screen)
            || (spawnHud != null && spawnHud.ContainsScreenPoint(screen));
    }

    void RefreshHud()
    {
        _status.Fill(_runtime, player != null ? player.username : null, _spawner.CanSpawnNow());
        phaseHud.Refresh(_status);
        actionMenu.Refresh(_status);
        if (spawnHud != null)
            spawnHud.Refresh(_status);
    }
}
