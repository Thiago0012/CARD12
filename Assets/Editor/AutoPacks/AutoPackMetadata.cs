using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.Editor.AutoPacks
{
    public sealed class AutoPackMetadata : ScriptableObject
    {
        [SerializeField] private string packId;
        [SerializeField] private string generationBatchId;
        [SerializeField] private int generatorVersion;
        [SerializeField] private int priceCoins;
        [SerializeField] private List<string> cardIds = new();
        [SerializeField] private List<string> previewCardIds = new();
        [SerializeField] private Sprite packSprite;
        [SerializeField] private string contentHash;
        [SerializeField] private bool published;
        [SerializeField] private bool contentLockedAfterPublish;
        [SerializeField] private bool countsForAutoCoverage;
        [SerializeField] private bool manualVisualOverride;
        [SerializeField] private bool needsPreviewReview;

        public string PackId => packId ?? string.Empty;
        public string GenerationBatchId => generationBatchId ?? string.Empty;
        public int GeneratorVersion => generatorVersion;
        public int PriceCoins => priceCoins;
        public IReadOnlyList<string> CardIds => cardIds;
        public IReadOnlyList<string> PreviewCardIds => previewCardIds;
        public Sprite PackSprite => packSprite;
        public string ContentHash => contentHash ?? string.Empty;
        public bool Published => published;
        public bool ContentLockedAfterPublish => contentLockedAfterPublish;
        public bool CountsForAutoCoverage => countsForAutoCoverage;
        public bool ManualVisualOverride => manualVisualOverride;
        public bool NeedsPreviewReview => needsPreviewReview;

        internal void Initialize(
            string id,
            string batchId,
            int version,
            IEnumerable<string> cards,
            IEnumerable<string> previews,
            Sprite defaultSprite,
            string hash)
        {
            packId = id ?? string.Empty;
            generationBatchId = batchId ?? string.Empty;
            generatorVersion = Math.Max(1, version);
            priceCoins = AutoPackGenerationSettings.RequiredPrice;
            cardIds = (cards ?? Array.Empty<string>()).ToList();
            previewCardIds = (previews ?? Array.Empty<string>()).ToList();
            if (!manualVisualOverride || packSprite == null)
                packSprite = defaultSprite;
            contentHash = hash ?? string.Empty;
            published = true;
            contentLockedAfterPublish = true;
            countsForAutoCoverage = true;
            needsPreviewReview = false;
        }
    }
}
