using System;

[Serializable]
public class PositionField
{
    public int x;
    public int y;
    public int z;
}

[Serializable]
public class StatsField
{
    public int health;
    public int strength;
    public int intelligence;
}

[Serializable]
public class SoldierInitDataField
{
    public int ID;
    public string soldierType;
    public string camp;
    public PositionField position;
    public StatsField stats;
}

[Serializable]
public class TargetDamageField
{
    public int targetId;
    public int damage;
}

[Serializable]
public class ActionField
{
    public string actionType;
    public int soldierId;
    public PositionField[] path;
    public int remainingMovement;
    
    // Attack and Ability
    public TargetDamageField[] damageDealt;
    
    // Ability specific
    public string ability;
    public PositionField targetPosition;
    public int intelligenceCost;
}

[Serializable]
public class SoldierRoundStat
{
    public int soldierId;
    public string survived; // sometimes "true" or "false" string, or actual boolean if updated later
    public PositionField position;
    // We accommodate both capitalized and lowercase 'stats'
    public StatsField Stats;
    public StatsField stats;
}

[Serializable]
public class ScoreField
{
    public int redScore;
    public int blueScore;
}

[Serializable]
public class GameRoundField
{
    public int roundNumber;
    public ActionField[] actions;
    public SoldierRoundStat[] stats;
    public ScoreField score;
    public string end; // e.g. "false", "Red", "Blue"
}

[Serializable]
public class RowListField
{
    public int[] row;
}

[Serializable]
public class MapDataField
{
    public int mapWidth;
    public RowListField[] rows;
}

[Serializable]
public class PlayerDataField
{
    public string player1;
    public string player2;
}

[Serializable]
public class RootData
{
    public MapDataField mapdata;
    public PlayerDataField playerData;
    public SoldierInitDataField[] soldiersData;
    public GameRoundField[] gameRounds;
}
