using System;
using System.Collections.Generic;

[Serializable]
public class CloudCell
{
    public int column;
    public int row;
}

[Serializable]
public class CloudOrder
{
    public string unitId;
    public string characterId;
    public string type;
    public int column;
    public int row;
    public int abilityId;
}

[Serializable]
public class CloudSpawn
{
    public string characterId;
    public int column;
    public int row;
}

[Serializable]
public class CloudUnit
{
    public string unitId;
    public string ownerPlayerId;
    public string characterId;
    public int column;
    public int row;
    public int health;
    public int maxHealth;
    public int damage;
    public bool isOpeningSpawn;
    public bool hasEnteredBattle;
    public bool useSpeedsterMove;
}

[Serializable]
public class CloudSeat
{
    public string playerId;
    public List<string> deck = new List<string>();
    public List<string> spawned = new List<string>();
    public List<string> dead = new List<string>();
    public int paidSpawnCount;
    public int energyRemaining;
    public bool plansSubmitted;
}

[Serializable]
public class CloudBattleMove
{
    public string unitId;
    public int column;
    public int row;
    public bool blocked;
}

[Serializable]
public class CloudBattleAbility
{
    public string unitId;
    public string kind;
    public int damage;
    public List<CloudCell> targets = new List<CloudCell>();
}

[Serializable]
public class CloudBattleSpawn
{
    public string unitId;
    public string ownerPlayerId;
    public string characterId;
    public int column;
    public int row;
    public int health;
    public int maxHealth;
    public int damage;
}

[Serializable]
public class CloudBattleTick
{
    public int ring;
    public bool isEmpty;
    public bool isSpawn;
    public List<CloudBattleSpawn> spawns = new List<CloudBattleSpawn>();
    public List<CloudCell> ringCells = new List<CloudCell>();
    public List<CloudBattleMove> moves = new List<CloudBattleMove>();
    public List<CloudBattleAbility> abilities = new List<CloudBattleAbility>();
}

[Serializable]
public class CloudFlag
{
    public int column;
    public int row;
    public string ownerPlayerId;
}

[Serializable]
public class CloudMatchSnapshot
{
    public string matchId;
    public int turn;
    public string phase;
    public int columns;
    public int rows;
    public int homeRow;
    public string localPlayerId;
    public string opponentPlayerId;
    public CloudSeat local;
    public CloudSeat opponent;
    public List<CloudUnit> units = new List<CloudUnit>();
    public List<CloudFlag> flags = new List<CloudFlag>();
    public string winnerPlayerId;
    public List<CloudBattleTick> lastTicks = new List<CloudBattleTick>();
    public bool waitingForOpponent;
    public bool waitingForOpponentPlans;
}

[Serializable]
public class CloudSubmitPlansResult
{
    public bool accepted;
    public bool waitingForOpponent;
    public List<CloudBattleTick> ticks = new List<CloudBattleTick>();
    public CloudMatchSnapshot match;
}

[Serializable]
public class CloudQueueResult
{
    public string status;
    public int trophies;
    public CloudMatchSnapshot match;
}
