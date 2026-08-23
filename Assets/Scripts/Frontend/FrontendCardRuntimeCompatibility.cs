using ArcaneArena.Cards;

namespace ArcaneArena.Frontend
{
    internal sealed class FrontendCardRuntimeProfile
    {
        public bool CanEnterDuel { get; set; }
        public string Note { get; set; } = string.Empty;
    }

    /// <summary>
    /// UI-only validation used by the migrated deck screens. It does not
    /// execute effects; the authoritative legality and effects remain in
    /// the new ygopro-core bridge.
    /// </summary>
    internal static class FrontendCardRuntimeCompatibility
    {
        public static FrontendCardRuntimeProfile ProfileFor(
            CardCatalogEntry entry)
        {
            bool available =
                entry != null &&
                entry.IsReadyForGameplay &&
                entry.HasArtwork;
            return new FrontendCardRuntimeProfile
            {
                CanEnterDuel = available,
                Note = available
                    ? "Validado pelo catálogo de apresentação; o core novo decide a legalidade."
                    : "A carta não está pronta no catálogo visual."
            };
        }

        public static bool CanEnterDuel(CardCatalogEntry entry)
        {
            return ProfileFor(entry).CanEnterDuel;
        }
    }
}
