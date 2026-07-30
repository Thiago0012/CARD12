namespace ArcaneDuel.Game
{
    public static class ProjectIdentity
    {
        public const string ProductName = "Arcane Duel";
        public const string ProjectVersion = "1.0.0";
        public const string UnityVersion = "6000.5.0f1";
        public const string CoreApiVersion = "11.0";
        public const string CoreCommit = "0764db0c75b3d1d574880d365aa3695ab1f13b43";
        public const string CardScriptsCommit = "55607ee511d9697b6eac5dbb689deaa5be712826";
        public const string BabelCdbCommit = "8d60901db521eb4183ca72560c01a70a6386c98c";
        public const string MainMenuScene = "MainMenu";
        // Compatibility alias used by the core-only presentation.  All exits
        // now return to the authored classic frontend; the newer portal is
        // intentionally retired from the player flow.
        public const string BootstrapScene = MainMenuScene;
        public const string DuelScene = "Duel";
        public const string CardLabScene = "CardLab";
    }
}
