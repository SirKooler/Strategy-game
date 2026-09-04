using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Selected character actions.
/// Wire the scene UI in the Inspector. Utility and Unspawn are created at runtime if missing.
/// </summary>
public class UnitActionMenu : MonoBehaviour
{
    public Action OnMove;
    public Action OnAttack;
    public Action OnUtility;
    public Action OnClear;
    public Action OnUnspawn;

    [SerializeField] RectTransform panel;
    [SerializeField] Text selectedInfo;
    [SerializeField] Button moveButton;
    [SerializeField] Button attackButton;
    [SerializeField] Button utilityButton;
    [SerializeField] Button clearButton;
    [SerializeField] Button unspawnButton;

    void Awake()
    {
        EnsureUtilityButton();
        EnsureUnspawnButton();
        moveButton.onClick.AddListener(() => OnMove?.Invoke());
        attackButton.onClick.AddListener(() => OnAttack?.Invoke());
        if (utilityButton != null)
            utilityButton.onClick.AddListener(() => OnUtility?.Invoke());
        clearButton.onClick.AddListener(() => OnClear?.Invoke());
        if (unspawnButton != null)
            unspawnButton.onClick.AddListener(() => OnUnspawn?.Invoke());
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        if (panel == null || !panel.gameObject.activeInHierarchy)
            return false;
        return HudHit.Contains(panel, screenPosition);
    }

    public void Refresh(MatchStatus status)
    {
        bool show = status.Phase == MatchPhase.Plan && status.Selected != null && status.CanPlanSelected;
        panel.gameObject.SetActive(show);
        if (!show)
            return;

        string action = status.ChosenAction == UnitPlanKind.None
            ? (status.HasUtility ? "choose Move, Attack, or Utility" : "choose Move or Attack")
            : $"picking {status.ChosenAction}";
        selectedInfo.text = $"{status.Selected.behaviour.CurrentStats.displayName}  |  {action}";
        moveButton.interactable = status.CanAffordMove;
        attackButton.interactable = status.CanAffordAttack;
        if (utilityButton != null)
        {
            utilityButton.gameObject.SetActive(status.HasUtility);
            utilityButton.interactable = status.CanAffordUtility;
        }
        if (unspawnButton != null)
        {
            unspawnButton.gameObject.SetActive(status.CanUnspawnSelected);
            unspawnButton.interactable = status.CanUnspawnSelected;
        }
    }

    void EnsureUtilityButton()
    {
        if (utilityButton != null || panel == null)
            return;

        Transform row = panel.Find("PlanButtons");
        if (row == null)
            return;

        Transform existing = row.Find("UtilityButton");
        if (existing != null)
        {
            utilityButton = existing.GetComponent<Button>();
            return;
        }

        if (attackButton == null)
            return;

        GameObject go = Instantiate(attackButton.gameObject, row);
        go.name = "UtilityButton";
        go.transform.SetSiblingIndex(attackButton.transform.GetSiblingIndex() + 1);
        Text label = go.GetComponentInChildren<Text>();
        if (label != null)
            label.text = "Utility";
        Image image = go.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.28f, 0.72f, 0.48f, 1f);
        utilityButton = go.GetComponent<Button>();
        utilityButton.onClick.RemoveAllListeners();
    }

    void EnsureUnspawnButton()
    {
        if (unspawnButton != null || panel == null)
            return;

        Transform row = panel.Find("PlanButtons");
        if (row == null)
            return;

        Transform existing = row.Find("UnspawnButton");
        if (existing != null)
        {
            unspawnButton = existing.GetComponent<Button>();
            return;
        }

        if (clearButton == null)
            return;

        GameObject go = Instantiate(clearButton.gameObject, row);
        go.name = "UnspawnButton";
        Text label = go.GetComponentInChildren<Text>();
        if (label != null)
            label.text = "Unspawn";
        Image image = go.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.55f, 0.32f, 0.22f, 1f);
        unspawnButton = go.GetComponent<Button>();
        unspawnButton.onClick.RemoveAllListeners();
    }
}
