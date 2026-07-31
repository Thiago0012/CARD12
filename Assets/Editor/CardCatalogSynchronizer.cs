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
