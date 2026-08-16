using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor
{
    public static class CardCatalogSynchronizer
    {
        private const string CatalogPath =
            "Assets/Cards/CardCatalog.asset";
        private const string YugiArtFolder =
            "Assets/Cards/Cards/Decks/YugiMutoBattleCity";
        private const string ToonArtFolder =
            "Assets/Cards/Cards/Decks/ToonTest";
        private const string ShiranuiArtFolder =
            "Assets/Cards/Cards/Decks/ShiranuiSupremacy";
        private const string MausoleumArtFolder =
            "Assets/Cards/Cards/Decks/MausoleumLockdownEdison";
        private const string BatchJuly2026ArtFolder =
            "Assets/Cards/Cards/Decks/BatchJuly2026";
        private const string BatchAugust2026ArtFolder =
            "Assets/Cards/Cards/Decks/BatchAugust2026";
        private const string StarterDeckArtFolder =
            "Assets/Cards/Cards/Decks/StarterDecks2026";

        [MenuItem("Arcane Arena/Content/Sync All Card Metadata and Rarities")]
        public static void SyncAllPublishedMetadata()
        {
            CardCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
                throw new FileNotFoundException(CatalogPath);
            CardDatabase database = CardDatabase.LoadDefault();
            int refreshed = RefreshPortugueseMetadata(catalog, database);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"ARCANE_CARD_RARITY_SYNC_OK refreshed={refreshed} " +
                $"catalog={catalog.Entries.Count}");
        }

        [MenuItem("Arcane Arena/Content/Sync Yugi Battle City")]
        public static void SyncYugiMutoBattleCity()
        {
            SyncDeck(
                "yugi-battle-city",
                YugiArtFolder,
                CuratedDeckLists.YugiMutoBattleCityMain,
                CuratedDeckLists.YugiMutoBattleCityExtra);
        }

        [MenuItem("Arcane Arena/Content/Sync Toon Deck and Portuguese Text")]
        public static void SyncToonDeckAndPortugueseText()
        {
            SyncDeck(
                "toon-test",
                ToonArtFolder,
                CuratedDeckLists.ToonTestMain,
                CuratedDeckLists.ToonTestExtra);
        }

        [MenuItem("Arcane Arena/Content/Sync Shiranui Deck and Portuguese Text")]
        public static void SyncShiranuiDeckAndPortugueseText()
        {
            SyncDeck(
                "shiranui-supremacy",
                ShiranuiArtFolder,
                CuratedDeckLists.ShiranuiSupremacyMain,
                CuratedDeckLists.ShiranuiSupremacyExtra);
        }

        [MenuItem("Arcane Arena/Content/Sync Mausoleum Edison and Portuguese Text")]
        public static void SyncMausoleumLockdownEdisonAndPortugueseText()
        {
            SyncDeck(
                "mausoleum-lockdown-edison",
                MausoleumArtFolder,
                CuratedDeckLists.MausoleumLockdownEdisonMain,
                CuratedDeckLists.MausoleumLockdownEdisonExtra);
        }

        [MenuItem("Arcane Arena/Content/Sync July 2026 Deck Batch")]
        public static void SyncBatchJuly2026()
        {
            SyncDeck(
                "july-2026-nine-deck-batch",
                BatchJuly2026ArtFolder,
                new[]
                {
                    CuratedDeckLists.AzaminaIllusionsMain,
                    CuratedDeckLists.PlantLinkMain,
                    CuratedDeckLists.NoobsGaiaMain,
                    CuratedDeckLists.SummonBansMain,
                    CuratedDeckLists.StarWarriorLevel5XyzMain,
                    CuratedDeckLists.AssaultModeGoodStuffMain,
                    CuratedDeckLists.Dragones2Main,
                    CuratedDeckLists.FemaleReptileMain,
                    CuratedDeckLists.ReturnToSenderMain
                }.SelectMany(cards => cards).ToArray(),
                new[]
                {
                    CuratedDeckLists.AzaminaIllusionsExtra,
                    CuratedDeckLists.PlantLinkExtra,
                    CuratedDeckLists.NoobsGaiaExtra,
                    CuratedDeckLists.SummonBansExtra,
                    CuratedDeckLists.StarWarriorLevel5XyzExtra,
                    CuratedDeckLists.AssaultModeGoodStuffExtra,
                    CuratedDeckLists.Dragones2Extra,
                    CuratedDeckLists.FemaleReptileExtra,
                    CuratedDeckLists.ReturnToSenderExtra
                }.SelectMany(cards => cards).ToArray());
        }

        [MenuItem("Arcane Arena/Content/Sync August 2026 Deck Batch")]
        public static void SyncBatchAugust2026()
        {
            SyncDeck(
                "august-2026-nine-deck-batch",
                BatchAugust2026ArtFolder,
                new[]
                {
                    CuratedDeckLists.CrimsonPowerforceMain,
                    CuratedDeckLists.DarkMagicalBlastMain,
                    CuratedDeckLists.HiddenArtsOfShadowsMain,
                    CuratedDeckLists.BlackwingsPrideMain,
                    CuratedDeckLists.DragonmaidToOrderX3Main,
                    CuratedDeckLists.CyberneticSuccessorMain,
                    CuratedDeckLists.RunickMain,
                    CuratedDeckLists.ExodiaMain,
                    CuratedDeckLists.BlueEyesMaxModifiedMain
                }.SelectMany(cards => cards).ToArray(),
                new[]
                {
                    CuratedDeckLists.CrimsonPowerforceExtra,
                    CuratedDeckLists.DarkMagicalBlastExtra,
                    CuratedDeckLists.HiddenArtsOfShadowsExtra,
                    CuratedDeckLists.BlackwingsPrideExtra,
                    CuratedDeckLists.DragonmaidToOrderX3Extra,
                    CuratedDeckLists.CyberneticSuccessorExtra,
                    CuratedDeckLists.RunickExtra,
                    CuratedDeckLists.ExodiaExtra,
                    CuratedDeckLists.BlueEyesMaxModifiedExtra
                }.SelectMany(cards => cards).ToArray());
        }

        [MenuItem("Arcane Arena/Content/Sync Starter Deck Cards")]
        public static void SyncStarterDeckCards()
        {
            TextAsset source = Resources.Load<TextAsset>(
                "StarterDecks/starter-deck-sources");
            if (source == null)
                throw new FileNotFoundException(
                    "Resources/StarterDecks/starter-deck-sources.json");
            StarterDeckSourceCatalogFile catalog =
                JsonUtility.FromJson<StarterDeckSourceCatalogFile>(source.text);
            if (catalog?.decks == null || catalog.decks.Count != 6)
                throw new InvalidDataException(
                    "O catálogo bruto de decks iniciais deve conter seis decks.");

            uint[] codes = catalog.decks
                .Where(deck => deck?.raw != null)
                .SelectMany(deck => deck.raw.mainDeck
                    .Concat(deck.raw.extraDeck)
                    .Concat(deck.raw.sideDeck))
                .Select(value => uint.Parse(value))
                .Distinct()
                .ToArray();
            SyncDeck(
                "starter-decks-2026-08",
                StarterDeckArtFolder,
                codes,
                System.Array.Empty<uint>());
        }

        private static void SyncDeck(
            string deckId,
            string artFolder,
            uint[] mainDeck,
            uint[] extraDeck)
        {
            CardCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
                throw new FileNotFoundException(CatalogPath);

            CardDatabase database = CardDatabase.LoadDefault();
            RefreshPortugueseMetadata(catalog, database);
            uint[] codes = mainDeck
                .Concat(extraDeck)
                .Distinct()
                .ToArray();
            int added = 0;
            foreach (uint code in codes)
            {
                string officialId = code.ToString("00000000");
                if (catalog.FindByOfficialId(officialId) != null ||
                    catalog.FindByOfficialId(code.ToString()) != null)
                {
                    continue;
                }

                string artPath = $"{artFolder}/{code}.jpg";
                ConfigureSprite(artPath);
                Sprite sprite =
                    AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
                if (sprite == null)
                    throw new FileNotFoundException(artPath);

                CardCatalogEntry entry = catalog.GetOrCreate(
                    AssetDatabase.AssetPathToGUID(artPath),
                    sprite);
                entry.ApplyCoreMetadata(database.Get(code));
                added++;
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"ARCANE_CARD_CATALOG_SYNC_OK deck={deckId} added={added}");
        }

        private static int RefreshPortugueseMetadata(
            CardCatalog catalog,
            CardDatabase database)
        {
            int refreshed = 0;
            var synchronized = new List<(CardCatalogEntry Entry, CardRecord Card)>();
            foreach (CardCatalogEntry entry in catalog.Entries)
            {
                if (entry == null ||
                    !uint.TryParse(entry.OfficialCardId, out uint code) ||
                    !database.TryGet(code, out CardRecord card))
                {
                    continue;
                }

                entry.ApplyCoreMetadata(card);
                synchronized.Add((entry, card));
                refreshed++;
            }
            foreach (IGrouping<string, (CardCatalogEntry Entry, CardRecord Card)> group in
                     synchronized.GroupBy(
                         item => item.Entry.EnglishName ?? string.Empty,
                         StringComparer.Ordinal))
            {
                uint[] alternateCodes = group
                    .Where(item => item.Card.Alias != 0)
                    .Select(item => item.Card.Code)
                    .Distinct()
                    .ToArray();
                var variantByCode = new Dictionary<uint, CardArtVariant>();
                if (alternateCodes.Length == 1)
                {
                    variantByCode[alternateCodes[0]] = CardArtVariant.Alt;
                }
                else if (alternateCodes.Length > 1)
                {
                    variantByCode[alternateCodes[0]] = CardArtVariant.Alt1;
                    variantByCode[alternateCodes[1]] = CardArtVariant.Alt2;
                    for (int index = 2; index < alternateCodes.Length; index++)
                        variantByCode[alternateCodes[index]] = CardArtVariant.Alt;
                }
                foreach ((CardCatalogEntry entry, CardRecord card) in group)
                {
                    entry.RefreshRarity(
                        variantByCode.TryGetValue(
                            card.Code,
                            out CardArtVariant inferred)
                            ? inferred
                            : CardArtVariant.Auto);
                }
            }
            return refreshed;
        }

        private static void ConfigureSprite(string assetPath)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new FileNotFoundException(assetPath);
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            changed |= CatalogTextureImportOptimizer.ConfigureImporter(
                importer);
            if (changed)
                importer.SaveAndReimport();
        }
    }
}
