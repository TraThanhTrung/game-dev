using System;
using System.Collections.Generic;

namespace GameResult
{
    /// <summary>
    /// Data model for match result containing GameSession info, enemies, and players.
    /// </summary>
    [Serializable]
    public class MatchResultData
    {
        public string sessionId;
        public string startTime;
        public string endTime;
        public int playerCount;
        public string status;
        public List<EnemyTypeInfo> enemies = new List<EnemyTypeInfo>();
        public List<PlayerInfo> players = new List<PlayerInfo>();
    }

    /// <summary>
    /// Information about an enemy type encountered in the match.
    /// </summary>
    [Serializable]
    public class EnemyTypeInfo
    {
        public string enemyTypeId;
        public string name;
        public string sectionName;
        public string checkpointName;
    }

    /// <summary>
    /// Information about a player in the match.
    /// </summary>
    [Serializable]
    public class PlayerInfo
    {
        public string playerId;
        public string avatarPath;
        public string name;
        public int level;
        public int gold;
    }
}

