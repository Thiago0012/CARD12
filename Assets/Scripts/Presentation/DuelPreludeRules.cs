namespace ArcaneArena.Presentation
{
    public enum DuelPreludeChoice : byte
    {
        None = 0,
        Rock = 1,
        Paper = 2,
        Scissors = 3
    }

    public enum DuelPreludeOutcome : byte
    {
        Tie = 0,
        PlayerOne = 1,
        PlayerTwo = 2
    }

    /// <summary>
    /// Pure pre-duel rules. Keeping this separate from UI and networking
    /// makes the same result usable by bot, local and online matches.
    /// </summary>
    public static class DuelPreludeRules
    {
        public static DuelPreludeOutcome Resolve(
            DuelPreludeChoice playerOne,
            DuelPreludeChoice playerTwo)
        {
            if (!IsPlayable(playerOne) || !IsPlayable(playerTwo))
                return DuelPreludeOutcome.Tie;
            if (playerOne == playerTwo)
                return DuelPreludeOutcome.Tie;

            bool playerOneWins =
                playerOne == DuelPreludeChoice.Rock &&
                playerTwo == DuelPreludeChoice.Scissors ||
                playerOne == DuelPreludeChoice.Paper &&
                playerTwo == DuelPreludeChoice.Rock ||
                playerOne == DuelPreludeChoice.Scissors &&
                playerTwo == DuelPreludeChoice.Paper;
            return playerOneWins
                ? DuelPreludeOutcome.PlayerOne
                : DuelPreludeOutcome.PlayerTwo;
        }

        public static bool IsPlayable(DuelPreludeChoice choice)
        {
            return choice >= DuelPreludeChoice.Rock &&
                   choice <= DuelPreludeChoice.Scissors;
        }

        public static string Label(DuelPreludeChoice choice)
        {
            return choice switch
            {
                DuelPreludeChoice.Rock => "PEDRA",
                DuelPreludeChoice.Paper => "PAPEL",
                DuelPreludeChoice.Scissors => "TESOURA",
                _ => "—"
            };
        }
    }
}
