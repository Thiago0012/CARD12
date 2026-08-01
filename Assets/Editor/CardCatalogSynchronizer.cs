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

        private static void RefreshPortugueseMetadata(
            CardCatalog catalog,
            CardDatabase database)
        {
            foreach (CardCatalogEntry entry in catalog.Entries)
            {
                if (entry == null ||
                    !uint.TryParse(entry.OfficialCardId, out uint code) ||
                    !database.TryGet(code, out CardRecord card))
                {
                    continue;
                }

                entry.ApplyCoreMetadata(card);
            }
        }

        private static void ConfigureSprite(string assetPath)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new FileNotFoundException(assetPath);
            if (importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
