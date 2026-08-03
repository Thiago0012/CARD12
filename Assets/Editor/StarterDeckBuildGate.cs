using System.Linq;
using ArcaneDuel.Game;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArcaneArena.Editor
{
    public sealed class StarterDeckBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            BanlistDefinition banlist = Resources.Load<BanlistDefinition>(
                "Banlist/tcg_eu_2026_05_18");
            if (banlist == null || banlist.Entries.Count != 226 ||
                banlist.Entries.Count(entry => entry.maxCopies == 0) != 119 ||
                banlist.Entries.Count(entry => entry.maxCopies == 1) != 97 ||
                banlist.Entries.Count(entry => entry.maxCopies == 2) != 10)
            {
                throw new BuildFailedException(
                    "A banlist ativa nao possui a seed normativa 119/97/10.");
            }

            StarterDeckCatalog catalog = Resources.Load<StarterDeckCatalog>(
                "StarterDecks/StarterDeckCatalog");
            if (catalog == null || catalog.Decks.Count != 6)
            {
                throw new BuildFailedException(
                    "O catalogo inicial deve conter exatamente seis decks.");
            }
            if (catalog.ActiveBanlistId != BanlistService.ActiveBanlistId)
            {
                throw new BuildFailedException(
                    "O catalogo inicial usa outra versao de banlist.");
            }

            if (catalog.Decks.Any(deck => deck == null))
            {
                throw new BuildFailedException(
                    "O catalogo inicial contem uma referencia de deck ausente.");
            }

            StarterDeckDefinition blocked = catalog.Decks.FirstOrDefault(deck =>
                !deck.IsPublishable ||
                deck.PreviewCardIds.Count != 3 ||
                deck.Replacements.Any(entry => entry != null && !entry.approved));
            if (blocked != null)
            {
                string name = blocked != null
                    ? blocked.DisplayName
                    : "asset ausente";
                throw new BuildFailedException(
                    "Build bloqueada: o deck inicial '" + name +
                    "' ainda possui erro ou substituicao nao aprovada. " +
                    "Consulte StarterDeckImportReport.md.");
            }
        }
    }
}
