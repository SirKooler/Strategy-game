using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ready control. Energy lives on the scene <see cref="EnergyHud"/> object.
/// </summary>
public class MatchPhaseHud : MonoBehaviour
{
    public Action OnReady;
    public Action OnHome;

    static readonly Color ReadyColor = new Color(0.22f, 0.62f, 0.38f, 1f);
    static readonly Color WaitingColor = new Color(0.62f, 0.52f, 0.18f, 1f);

    [SerializeField] RectTransform panel;
    [SerializeField] EnergyHud energyHud;
    [SerializeField] Button readyButton;
    [SerializeField] Text resultLabel;
    [SerializeField] Button homeButton;

    Image _readyImage;
    Text _readyLabel;

    void Awake()
    {
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(() => OnReady?.Invoke());
            _readyImage = readyButton.GetComponent<Image>();
            _readyLabel = readyButton.GetComponentInChildren<Text>();
        }
        EnsureResultLabel();
        EnsureHomeButton();
    }

    void EnsureResultLabel()
    {
        if (resultLabel != null || panel == null)
            return;

        Transform existing = panel.Find("ResultLabel");
        if (existing != null)
        {
            resultLabel = existing.GetComponent<Text>();
            if (resultLabel != null)
                return;
        }

        var go = new GameObject("ResultLabel", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.layer = 5;
        go.transform.SetParent(panel, false);
        resultLabel = go.GetComponent<Text>();
        resultLabel.font = _readyLabel != null
            ? _readyLabel.font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        resultLabel.alignment = TextAnchor.MiddleCenter;
        resultLabel.color = Color.white;
        resultLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        resultLabel.resizeTextForBestFit = true;
        resultLabel.resizeTextMinSize = 10;
        resultLabel.resizeTextMaxSize = 24;
        go.GetComponent<LayoutElement>().preferredHeight = 36f;
        go.SetActive(false);
    }

    void EnsureHomeButton()
    {
        if (homeButton == null && panel != null)
        {
            Transform existing = panel.Find("HomeButton");
            if (existing != null)
                homeButton = existing.GetComponent<Button>();
        }

        if (homeButton == null && panel != null)
        {
            var go = new GameObject("HomeButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.layer = 5;
            go.transform.SetParent(panel, false);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.75f, 1f);
            homeButton = go.GetComponent<Button>();
            homeButton.targetGraphic = image;
            go.GetComponent<LayoutElement>().preferredHeight = 36f;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.layer = 5;
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textGo.GetComponent<Text>();
            text.font = _readyLabel != null
                ? _readyLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "Home";
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        if (homeButton == null)
            return;

        homeButton.onClick.AddListener(() => OnHome?.Invoke());
        homeButton.gameObject.SetActive(false);
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        bool showingResult = resultLabel != null && resultLabel.gameObject.activeInHierarchy
            || homeButton != null && homeButton.gameObject.activeInHierarchy;
        bool onPanel = showingResult && panel != null && HudHit.Contains(panel, screenPosition);
        bool onEnergy = energyHud != null && HudHit.Contains(energyHud.Rect, screenPosition);
        bool onReady = readyButton != null && HudHit.Contains(readyButton.transform as RectTransform, screenPosition);
        return onPanel || onEnergy || onReady;
    }

    public void Refresh(MatchStatus status)
    {
        bool plan = status.Phase == MatchPhase.Plan;
        bool won = status.Winner != MatchWinner.None;
        if (energyHud != null)
            energyHud.SetEnergy(status.EnergyRemaining);
        if (resultLabel != null)
        {
            resultLabel.gameObject.SetActive(won);
            if (won)
                resultLabel.text = status.Winner == MatchWinner.Local ? "You win" : "Opponent wins";
        }
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(plan && !won);
            if (_readyLabel != null)
            {
                _readyLabel.text = status.IsWaitingForOpponent ? "Waiting" : "Ready";
                _readyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            if (_readyImage != null)
                _readyImage.color = status.IsWaitingForOpponent ? WaitingColor : ReadyColor;
        }
        if (homeButton != null)
            homeButton.gameObject.SetActive(won);
        Image panelImage = panel != null ? panel.GetComponent<Image>() : null;
        if (panelImage != null)
        {
            panelImage.enabled = won;
            panelImage.raycastTarget = won;
        }
    }
}
