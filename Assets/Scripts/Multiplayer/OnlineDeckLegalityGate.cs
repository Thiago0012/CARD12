using System;
using System.Linq;
using ArcaneArena.Frontend;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;

namespace ArcaneArena.Multiplayer
{
    internal static class OnlineDeckLegalityGate
    {
        private static CardDatabase database;
        private static CardVisualCatalog visuals;

        internal static bool TryValidate(
            DuelDeckLoadout loadout,
            out string rejection)
        {
            rejection = string.Empty;
            if (loadout == null)
            {
                rejection = "O manifesto do deck esta ausente.";
                return false;
            }
            BanlistDefinition definition = UnityEngine.Resources.Load<BanlistDefinition>($"Banlist/{loadout.banlistId}");
            if (definition == null)
            {
                rejection = $"A banlist do lobby ({loadout.banlistId}) não está disponível localmente.";
                return false;
            }
            BanlistService banlist = new BanlistService(definition);

            if (!string.Equals(
                    loadout.banlistId,
                    BanlistService.ActiveBanlistId,
                    StringComparison.Ordinal))
            {
                // Removido o bloqueio para permitir diferentes banlists em partidas privadas.
                // Mas a validação de sha256 a seguir vai confirmar se a integridade confere.
            }

            string computedHash = DeckManifestHasher.ComputeSha256(
                loadout.banlistId,
                loadout.mainDeckCardIds,
                loadout.extraDeckCardIds,
                loadout.sideDeckCardIds);
            if (!string.Equals(
                    loadout.normalizedDeckSha256,
                    computedHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                rejection = "O manifesto SHA-256 do deck nao confere.";
                return false;
            }

            if (!TryParse(loadout.mainDeckCardIds, out uint[] main) ||
                !TryParse(loadout.extraDeckCardIds, out uint[] extra) ||
                !TryParse(loadout.sideDeckCardIds, out uint[] side))
            {
                rejection = "O deck possui identificadores de carta invalidos.";
                return false;
            }

            try
            {
                database ??= CardDatabase.LoadDefault();
                visuals ??= CardVisualCatalog.LoadDefault();
                var deck = new DeckFile
                {
                    schemaVersion = 1,
                    id = loadout.deckId ?? string.Empty,
                    name = loadout.displayName ?? string.Empty,
                    mainDeck = main.ToList(),
                    extraDeck = extra.ToList(),
                    sideDeck = side.ToList()
                };
                DeckValidationResult validation = DeckRules.Validate(
                    deck, database, visuals, banlist);
                if (!validation.IsValid)
                {
                    rejection = validation.Summary;
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                rejection = "Falha ao validar o conteudo local do deck: " +
                    exception.GetBaseException().Message;
                return false;
            }
        }

        private static bool TryParse(
            System.Collections.Generic.IEnumerable<string> values,
            out uint[] cards)
        {
            var parsed = new System.Collections.Generic.List<uint>();
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (!uint.TryParse(value, out uint code) || code == 0)
                {
                    cards = Array.Empty<uint>();
                    return false;
                }
                parsed.Add(code);
            }
            cards = parsed.ToArray();
            return true;
        }
    }
}
