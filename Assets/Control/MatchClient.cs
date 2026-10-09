using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// Thin Cloud Code client for the Match module. Identity comes from Unity Authentication.
/// </summary>
public class MatchClient
{
    public const string ModuleName = "Match";

    public string PlayerId => AuthenticationService.Instance.IsSignedIn
        ? AuthenticationService.Instance.PlayerId
        : null;

    public async Task SignIn(string profile)
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        var auth = AuthenticationService.Instance;
        if (auth.IsSignedIn && auth.Profile != profile)
            auth.SignOut();
        if (auth.Profile != profile)
            auth.SwitchProfile(profile);
        if (!auth.IsSignedIn)
            await auth.SignInAnonymouslyAsync();
    }

    public Task<CloudMatchSnapshot> CreateMatch(IReadOnlyList<string> deck)
    {
        return CloudCodeService.Instance.CallModuleEndpointAsync<CloudMatchSnapshot>(
            ModuleName,
            "CreateMatch",
            new Dictionary<string, object> { { "deck", deck } });
    }

    public Task<CloudMatchSnapshot> JoinMatch(string matchId, IReadOnlyList<string> deck)
    {
        return CloudCodeService.Instance.CallModuleEndpointAsync<CloudMatchSnapshot>(
            ModuleName,
            "JoinMatch",
            new Dictionary<string, object>
            {
                { "matchId", matchId },
                { "deck", deck }
            });
    }

    public Task<CloudSubmitPlansResult> SubmitPlans(
        string matchId,
        int turn,
        List<CloudOrder> orders,
        List<CloudSpawn> spawns,
        List<string> unspawnUnitIds)
    {
        return CloudCodeService.Instance.CallModuleEndpointAsync<CloudSubmitPlansResult>(
            ModuleName,
            "SubmitPlans",
            new Dictionary<string, object>
            {
                { "matchId", matchId },
                { "turn", turn },
                { "orders", orders },
                { "spawns", spawns },
                { "unspawnUnitIds", unspawnUnitIds }
            });
    }

    public Task<CloudMatchSnapshot> WithdrawPlans(string matchId)
    {
        return CloudCodeService.Instance.CallModuleEndpointAsync<CloudMatchSnapshot>(
            ModuleName,
            "WithdrawPlans",
            new Dictionary<string, object> { { "matchId", matchId } });
    }

    public Task<CloudMatchSnapshot> GetMatch(string matchId)
    {
        return CloudCodeService.Instance.CallModuleEndpointAsync<CloudMatchSnapshot>(
            ModuleName,
            "GetMatch",
            new Dictionary<string, object> { { "matchId", matchId } });
    }

    public Task<CloudQueueResult> Enqueue(IReadOnlyList<string> deck)
    {
        return CloudCodeService.Instance.CallModuleEndpointAsync<CloudQueueResult>(
            ModuleName,
            "Enqueue",
            new Dictionary<string, object> { { "deck", deck } });
    }

    public Task<CloudQueueResult> PollQueue()
    {
        return CloudCodeService.Instance.CallModuleEndpointAsync<CloudQueueResult>(
            ModuleName,
            "PollQueue",
            new Dictionary<string, object>());
    }

    public Task<CloudQueueResult> CancelQueue()
    {
        return CloudCodeService.Instance.CallModuleEndpointAsync<CloudQueueResult>(
            ModuleName,
            "CancelQueue",
            new Dictionary<string, object>());
    }
}
