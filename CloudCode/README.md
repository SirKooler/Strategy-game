# Strategy match Cloud Code

Server-authoritative match module for the Unity client in `Strategy game`. Deploy this to the **same Unity Cloud project** as the game (`53b1b523-2eb6-4460-98c1-c02cd97d7d3d`).

This is not a Dedicated Server / Multiplay build. Clients call Cloud Code; match boards, decks, and status are stored as one Cloud Save Custom Item per match.

## Setup

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download) and the [UGS CLI](https://docs.unity.com/ugs/en-us/manual/devops/manual/ugs-cli).
2. In [cloud.unity.com](https://cloud.unity.com) create a service account with Cloud Code Editor and Cloud Save Editor on this project.
3. From this folder:

```bash
ugs login
ugs config set project-id 53b1b523-2eb6-4460-98c1-c02cd97d7d3d
ugs config set environment-name production
ugs deploy Match.Deploy.sln

# If that prints "No content deployed", publish a .ccm and deploy that instead:
#   dotnet publish Match/Match.csproj -c Release -r linux-x64 --no-self-contained -o publish/Match
#   Compress-Archive publish/Match/* publish/Match.zip
#   Move-Item publish/Match.zip publish/Match.ccm
#   ugs deploy publish/Match.ccm
```

## Functions

| Function | Role |
| --- | --- |
| `CreateMatch` | Validate a 9-unique playable deck, create the match, spawn the host opener. |
| `JoinMatch` | Validate the guest deck, spawn the opposite-row opener, start Plan. |
| `SubmitPlans` | Validate one seat's orders/spawns. When both seats are in, resolve rings and return ticks. |
| `GetMatch` | Return board, decks, and status for a member. Opponent pending plans are hidden. |

Identity always comes from `IExecutionContext.PlayerId`. Match ids must be GUIDs. Character ids are allowlisted (`Tank` … `Controller`). Dummy is rejected.
