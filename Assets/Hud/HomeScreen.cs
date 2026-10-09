using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Placeholder home screen. Find match searches the queue, then loads the match scene.
/// </summary>
public class HomeScreen : MonoBehaviour
{
    public const string SceneName = "Home";
    public const string MatchSceneName = "SampleScene";

    [SerializeField] Player player;
    [SerializeField] string authProfile = "default";
    [SerializeField] Text trophyLabel;
    [SerializeField] Text statusLabel;
    [SerializeField] Button findButton;
    [SerializeField] Button cancelButton;

    readonly MatchClient _cloud = new MatchClient();
    bool _searching;
    bool _cancelRequested;

    void Awake()
    {
        if (findButton != null)
            findButton.onClick.AddListener(FindMatch);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelSearch);
        ShowIdle();
    }

    public void FindMatch()
    {
        if (_searching)
            return;
        StartCoroutine(Search());
    }

    public void CancelSearch()
    {
        if (!_searching)
            return;
        _cancelRequested = true;
    }

    IEnumerator Search()
    {
        _searching = true;
        _cancelRequested = false;
        ShowSearching();

        string profile = string.IsNullOrWhiteSpace(authProfile) ? "default" : authProfile.Trim();
        var signIn = _cloud.SignIn(profile);
        yield return Wait(signIn);
        if (signIn.IsFaulted || _cancelRequested)
        {
            yield return StopSearch();
            yield break;
        }

        var enqueue = _cloud.Enqueue(DeckIds());
        yield return Wait(enqueue);
        if (enqueue.IsFaulted)
        {
            ShowIdle();
            yield break;
        }

        if (EnterMatch(enqueue.Result))
            yield break;
        ShowTrophies(enqueue.Result);

        while (_searching && !_cancelRequested)
        {
            yield return new WaitForSeconds(1.5f);
            if (_cancelRequested)
                break;

            var poll = _cloud.PollQueue();
            yield return Wait(poll);
            if (poll.IsFaulted)
            {
                ShowIdle();
                yield break;
            }

            if (EnterMatch(poll.Result))
                yield break;
            ShowTrophies(poll.Result);
            if (poll.Result == null || poll.Result.status != "waiting")
            {
                ShowIdle();
                yield break;
            }
        }

        if (_cancelRequested)
            yield return StopSearch();
    }

    IEnumerator StopSearch()
    {
        var cancel = _cloud.CancelQueue();
        yield return Wait(cancel);
        if (!cancel.IsFaulted && EnterMatch(cancel.Result))
            yield break;
        ShowIdle();
    }

    bool EnterMatch(CloudQueueResult result)
    {
        if (result == null || result.status != "matched" || result.match == null)
            return false;

        _searching = false;
        _cancelRequested = false;
        MatchSession.HandOff(result.match);
        SceneManager.LoadScene(MatchSceneName);
        return true;
    }

    void ShowIdle()
    {
        _searching = false;
        _cancelRequested = false;
        if (trophyLabel != null)
            trophyLabel.text = "Trophies: 0";
        if (statusLabel != null)
            statusLabel.text = "";
        if (findButton != null)
            findButton.gameObject.SetActive(true);
        if (cancelButton != null)
            cancelButton.gameObject.SetActive(false);
    }

    void ShowSearching()
    {
        if (statusLabel != null)
            statusLabel.text = "Searching";
        if (findButton != null)
            findButton.gameObject.SetActive(false);
        if (cancelButton != null)
            cancelButton.gameObject.SetActive(true);
    }

    void ShowTrophies(CloudQueueResult result)
    {
        if (trophyLabel != null)
            trophyLabel.text = "Trophies: " + (result != null ? result.trophies : 0);
    }

    List<string> DeckIds()
    {
        var ids = new List<string>();
        if (player == null || player.deck == null)
            return ids;
        for (int i = 0; i < player.deck.Count; i++)
        {
            if (UnitConstants.TryGetPlayableId(player.deck[i], out string id))
                ids.Add(id);
        }

        return ids;
    }

    static IEnumerator Wait(System.Threading.Tasks.Task task)
    {
        while (!task.IsCompleted)
            yield return null;
        if (task.IsFaulted && task.Exception != null)
            Debug.LogError(task.Exception.GetBaseException().Message);
    }
}
