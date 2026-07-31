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
        private const string ArtFolder =
            "Assets/Cards/Cards/Decks/YugiMutoBattleCity";

        [MenuItem("Arcane Arena/Content/Sync Yugi Battle City")]
        public static void SyncYugiMutoBattleCity()
        {
            CardCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
                throw new FileNotFoundException(CatalogPath);

            CardDatabase database = CardDatabase.LoadDefault();
            uint[] codes = CuratedDeckLists.YugiMutoBattleCityMain
                .Concat(CuratedDeckLists.YugiMutoBattleCityExtra)
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

                string artPath = $"{ArtFolder}/{code}.jpg";
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
                $"ARCANE_CARD_CATALOG_SYNC_OK deck=yugi-battle-city added={added}");
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
