using UnityEngine;

namespace ArcaneArena.StoryRoguelite
{
    [CreateAssetMenu(
        fileName = "StoryEncounterLpProfile",
        menuName = "Arcane Duel/Story Roguelite/Encounter LP Profile")]
    public sealed class StoryEncounterLpProfile : ScriptableObject
    {
        public const string ResourcePath =
            "StoryRoguelite/Generated/StoryEncounterLpProfile";

        [Min(1)] public int playerLifePoints = 6000;
        [Min(1)] public int normalEnemyLifePoints = 6000;
        [Min(1)] public int eliteEnemyLifePoints = 12000;
        [Min(1)] public int finalDuelEnemyLifePoints = 14000;
        [Min(1)] public int bossActOneLifePoints = 20000;
        [Min(1)] public int bossActTwoLifePoints = 35000;
        [Min(1)] public int bossActThreeLifePoints = 50000;

        public int ResolveEnemyLifePoints(RogueliteNodeType type, int act)
        {
            if (type == RogueliteNodeType.Boss)
            {
                return act <= 1
                    ? bossActOneLifePoints
                    : act == 2
                        ? bossActTwoLifePoints
                        : bossActThreeLifePoints;
            }
            if (type == RogueliteNodeType.FinalDuelArena)
                return finalDuelEnemyLifePoints;
            if (type == RogueliteNodeType.EliteDuel)
                return eliteEnemyLifePoints;
            return normalEnemyLifePoints;
        }

        public static int ResolveOfficialEnemyLifePoints(
            RogueliteNodeType type,
            int act)
        {
            if (type == RogueliteNodeType.Boss)
                return act <= 1 ? 20000 : act == 2 ? 35000 : 50000;
            if (type == RogueliteNodeType.FinalDuelArena) return 14000;
            if (type == RogueliteNodeType.EliteDuel) return 12000;
            return 6000;
        }
    }
}
