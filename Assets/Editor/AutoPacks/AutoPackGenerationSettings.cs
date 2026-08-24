using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneArena.Editor.AutoPacks
{
    [CreateAssetMenu(
        fileName = "AutoPackGenerationSettings",
        menuName = "Game/Shop/Auto Pack Generation Settings")]
    public sealed class AutoPackGenerationSettings : ScriptableObject
    {
        public const int RequiredMinimum = 40;
        public const int RequiredMaximum = 85;
        public const int RequiredPrice = 25;

        [SerializeField] private bool enabled = true;
        [SerializeField] private int minCardsPerPack = RequiredMinimum;
        [SerializeField] private int maxCardsPerPack = RequiredMaximum;
        [SerializeField] private int priceCoins = RequiredPrice;
        [SerializeField, Min(1)] private int generatorVersion = 1;
        [SerializeField] private string[] watchedFolders =
        {
            "Assets/Cards/CardCatalog.asset",
            "Assets/Cards/Cards",
            "Assets/StreamingAssets/Ygo/Data/cards.bin",
            "Assets/StreamingAssets/Ygo/Data/card-texts.json"
        };
        [SerializeField] private Sprite defaultPackSprite;
        [SerializeField] private bool includeForbiddenCards = true;
        [SerializeField] private bool blockOnMissingArtwork = true;
        [SerializeField] private string displayNamePattern =
            "Pacote de Expansao {0:000}";
        [SerializeField] private string[] excludedPathTokens =
        {
            "/Tokens/",
            "/Placeholders/",
            "/EditorOnly/"
        };

        public bool Enabled => enabled;
        public int MinCardsPerPack => minCardsPerPack;
        public int MaxCardsPerPack => maxCardsPerPack;
        public int PriceCoins => priceCoins;
        public int GeneratorVersion => generatorVersion;
        public IReadOnlyList<string> WatchedFolders => watchedFolders ??
            Array.Empty<string>();
        public Sprite DefaultPackSprite => defaultPackSprite;
        public bool IncludeForbiddenCards => includeForbiddenCards;
        public bool BlockOnMissingArtwork => blockOnMissingArtwork;
        public string DisplayNamePattern => string.IsNullOrWhiteSpace(
                displayNamePattern)
            ? "Pacote de Expansao {0:000}"
            : displayNamePattern;
        public IReadOnlyList<string> ExcludedPathTokens =>
            excludedPathTokens ?? Array.Empty<string>();

        internal void InitializeDefaultSprite(Sprite sprite)
        {
            if (defaultPackSprite == null)
                defaultPackSprite = sprite;
            NormalizeNormativeValues();
        }

        internal bool HasNormativeValues =>
            minCardsPerPack == RequiredMinimum &&
            maxCardsPerPack == RequiredMaximum &&
            priceCoins == RequiredPrice &&
            generatorVersion > 0;

        private void OnValidate()
        {
            NormalizeNormativeValues();
        }

        private void NormalizeNormativeValues()
        {
            minCardsPerPack = RequiredMinimum;
            maxCardsPerPack = RequiredMaximum;
            priceCoins = RequiredPrice;
            generatorVersion = Math.Max(1, generatorVersion);
            watchedFolders ??= Array.Empty<string>();
            excludedPathTokens ??= Array.Empty<string>();
        }
    }
}
