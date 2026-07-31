using System;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Canonical numeric lists used by the runtime, frontend and Core audits.
    /// Keeping these outside the presentation layer prevents a shop-only copy
    /// from drifting away from the cards that are actually validated.
    /// </summary>
    public static class CuratedDeckLists
    {
        public static readonly uint[] DarkMagicianMain =
        {
            46986414, 46986414, 46986414, 72989439,
            72989439, 3078380, 60948488, 30603688,
            38033121, 7198399, 7084129, 7084129,
            7084129, 56132807, 14558127, 14558127,
            59438930, 59438930, 59438930, 34318086,
            20747792, 97268402, 97268402, 97268402,
            97631303, 47963370, 12266229, 47222536,
            47222536, 95477924, 68462976, 68462976,
            2314238, 83764719, 96729612, 73628505,
            63391643, 75190122, 59514116, 59514116,
            23020408, 23020408, 48680970, 48680970,
            82732705, 14315573, 9287078, 7922915,
            44095762, 44095762
        };

        public static readonly uint[] DarkMagicianExtra =
        {
            41721210, 98502113, 50237654, 43892408,
            88177324, 96471335, 96471335, 84013237,
            86331741, 38342335, 2857636, 80088625,
            34755994, 65741786, 94259633
        };

        public static readonly uint[] YugiMutoBattleCityMain =
        {
            10000020, 46986414, 99785935, 39256679,
            11549357, 90876561, 25652259, 70781052,
            5818798, 91152256, 41392891, 90357090,
            32452818, 13039848, 38033121, 75347539,
            64788463, 77207191, 65240384, 78193831,
            34124316, 50287060, 40640057, 34710660,
            87910978, 79571449, 24094653, 83764719,
            55144522, 5556668, 81510157, 24096228,
            72892473, 44095762, 49587034, 77414722,
            37383714, 3819470, 18807109, 60082869
        };

        public static readonly uint[] YugiMutoBattleCityExtra =
        {
            98502113, 4796100
        };

        public static string[] AsCardIds(uint[] cards)
        {
            if (cards == null)
                return Array.Empty<string>();
            var result = new string[cards.Length];
            for (int index = 0; index < cards.Length; index++)
                result[index] = cards[index].ToString();
            return result;
        }
    }
}
