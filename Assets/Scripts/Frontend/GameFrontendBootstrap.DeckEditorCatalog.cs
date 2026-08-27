using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ArcaneArena.Cards;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private const int CatalogVirtualColumns = 7;
        private const int CatalogVirtualPoolSize = 98;
        private const float CatalogVirtualCellWidth = 55.5f;
        private const float CatalogVirtualCellHeight = 81f;
        private const float CatalogVirtualSpacingX = 4.5f;
        private const float CatalogVirtualSpacingY = 6f;
        private const float CatalogVirtualPadding = 12f;

        private sealed class CatalogVirtualCell
        {
            public RectTransform Root;
            public Image Artwork;
            public Image LockedOverlay;
            public Outline ArtworkOutline;
            public DeckEditorCardDrag Drag;
            public Image BanlistBadge;
            public CardRestrictionBadgeTooltip BanlistTooltip;
            public Image RarityBadge;
            public ArcaneRarityBadgeGraphic RarityMaterial;
            public Text RarityLabel;
            public Text OwnedCount;
            public DeckEditorNewBadgePulse NewBadge;
            public int DataIndex = -1;
        }

        private readonly List<CardCatalogEntry> _catalogFilteredEntries = new();
        private readonly List<CatalogVirtualCell> _catalogVirtualCells = new();
        private readonly HashSet<CardRarity> _catalogRarityFilters = new();
        private readonly HashSet<CardAttribute> _catalogAttributeFilters = new();
        private readonly HashSet<MonsterFrameKind> _catalogFrameFilters = new();
        private readonly Dictionary<string, int> _catalogOwnedCopies =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _catalogExplicitOwnedCopies =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _catalogNewCardIds =
            new(StringComparer.Ordinal);
        private HashSet<string> _catalogRelatedCardIds;
        private ScrollRect _catalogScroll;
        private Image _catalogAdvancedFilterButton;
        private Image _deckEditorRelatedCardsButton;
        private GameObject _catalogAdvancedFilterModal;
        private int _catalogOwnershipFilter;
        private bool _catalogCraftableOnly;
        private int _catalogFirstVirtualRow = -1;
        private bool _deckEditorNewMarkersWereShown;

        private void ConfigureVirtualCatalog()
        {
            if (_catalogContent == null || _catalogDropZone == null)
                return;

            GridLayoutGroup grid =
                _catalogContent.GetComponent<GridLayoutGroup>();
            if (grid != null)
                grid.enabled = false;
            ContentSizeFitter fitter =
                _catalogContent.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;

            _catalogContent.anchorMin = new Vector2(0f, 1f);
            _catalogContent.anchorMax = new Vector2(1f, 1f);
            _catalogContent.pivot = new Vector2(0.5f, 1f);
            _catalogScroll = _catalogDropZone.GetComponent<ScrollRect>();
            if (_catalogScroll != null)
            {
                _catalogScroll.onValueChanged.AddListener(
                    HandleVirtualCatalogScroll);
                _catalogScroll.scrollSensitivity = 105f;
                _catalogScroll.inertia = true;
                _catalogScroll.decelerationRate = 0.22f;
                _catalogScroll.movementType =
                    ScrollRect.MovementType.Elastic;
                _catalogScroll.elasticity = 0.08f;
            }
            EnsureVirtualCatalogPool();
        }

        private void EnsureVirtualCatalogPool()
        {
            if (_catalogContent == null)
                return;
            while (_catalogVirtualCells.Count < CatalogVirtualPoolSize)
                _catalogVirtualCells.Add(CreateVirtualCatalogCell());
        }

        private CatalogVirtualCell CreateVirtualCatalogCell()
        {
            Image slot = CreatePanel(
                _catalogContent,
                "Célula virtual do catálogo",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Color.clear);
            slot.raycastTarget = false;
            RectTransform root = slot.rectTransform;
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(
                CatalogVirtualCellWidth,
                CatalogVirtualCellHeight);

            Image artwork = CreateCardArtwork(
                slot.transform,
                null,
                Vector2.zero,
                Vector2.one,
                0f,
                false);
            Outline outline = artwork.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;

            Image locked = CreatePanel(
                artwork.transform,
                "Carta não adquirida",
                Vector2.zero,
                Vector2.one,
                new Color(0.005f, 0.015f, 0.025f, 0.62f));
            locked.raycastTarget = false;
            Outline lockedOutline = locked.gameObject.AddComponent<Outline>();
            lockedOutline.effectColor =
                new Color(Muted.r, Muted.g, Muted.b, 0.28f);
            lockedOutline.effectDistance = new Vector2(1f, -1f);
            lockedOutline.useGraphicAlpha = true;

            DeckEditorCardDrag drag =
                artwork.gameObject.AddComponent<DeckEditorCardDrag>();
            Text ownedCount = CreateText(
                artwork.transform,
                "0",
                67,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.68f, 0.01f),
                new Vector2(0.97f, 0.17f),
                TextAnchor.LowerRight);
            ownedCount.gameObject.name = "Quantidade possuída";
            RectTransform ownedCountRect = ownedCount.rectTransform;
            ownedCountRect.pivot = new Vector2(0.5f, 0.5f);
            ownedCountRect.offsetMin = new Vector2(1.20643f, -4.35295f);
            ownedCountRect.offsetMax = new Vector2(0.00003f, 7.95295f);
            ownedCountRect.localScale = Vector3.one;
            ownedCount.resizeTextForBestFit = true;
            ownedCount.resizeTextMinSize = 11;
            ownedCount.resizeTextMaxSize = 67;
            AddOutline(
                ownedCount.gameObject,
                new Color(0f, 0f, 0f, 0.96f),
                new Vector2(1.5f, -1.5f));

            Image banlistBadge = CreatePanel(
                artwork.transform,
                "Restrição da Banlist reciclável",
                new Vector2(0.015f, 0.76f),
                new Vector2(0.27f, 0.985f),
                Color.white);
            banlistBadge.preserveAspect = true;
            banlistBadge.raycastTarget = true;
            CardRestrictionBadgeTooltip banlistTooltip =
                banlistBadge.gameObject.AddComponent<
                    CardRestrictionBadgeTooltip>();
            banlistTooltip.Initialize(0);
            banlistBadge.gameObject.SetActive(false);

            Image rarityBadge = CreateRarityBadge(
                root,
                CardRarity.N,
                new Vector2(0.60f, 0.72f),
                new Vector2(0.99f, 0.995f),
                16);
            ApplyCapturedRectTransform(
                rarityBadge.rectTransform,
                new Vector2(0.60f, 0.72f),
                new Vector2(0.99f, 0.995f),
                -1.168f,
                0f,
                0f,
                0f);
            ArcaneRarityBadgeGraphic rarityMaterial =
                rarityBadge.GetComponentInChildren<
                    ArcaneRarityBadgeGraphic>(true);
            Text rarityLabel = rarityBadge.GetComponentInChildren<Text>(true);

            Image newBadgePanel = CreatePanel(
                artwork.transform,
                "Selo NEW",
                new Vector2(0.25f, 0.78f),
                new Vector2(0.75f, 0.985f),
                new Color(0.015f, 0.12f, 0.18f, 0.96f));
            newBadgePanel.raycastTarget = false;
            RectTransform newBadgeRect = newBadgePanel.rectTransform;
            newBadgeRect.pivot = new Vector2(0.5f, 0.5f);
            newBadgeRect.offsetMin = new Vector2(-13.042f, 0f);
            newBadgeRect.offsetMax = new Vector2(-13.042f, 0f);
            AddOutline(
                newBadgePanel.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.96f),
                new Vector2(1.5f, -1.5f));
            Text newLabel = CreateText(
                newBadgePanel.transform,
                "NEW",
                10,
                FontStyle.Bold,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);
            newLabel.raycastTarget = false;
            AddOutline(
                newLabel.gameObject,
                new Color(0f, 0f, 0f, 0.95f),
                new Vector2(1f, -1f));
            newBadgePanel.gameObject.AddComponent<CanvasGroup>();
            DeckEditorNewBadgePulse newBadge =
                newBadgePanel.gameObject.AddComponent<
                    DeckEditorNewBadgePulse>();
            newBadge.SetVisible(false);
            root.gameObject.SetActive(false);
            return new CatalogVirtualCell
            {
                Root = root,
                Artwork = artwork,
                LockedOverlay = locked,
                ArtworkOutline = outline,
                Drag = drag,
                BanlistBadge = banlistBadge,
                BanlistTooltip = banlistTooltip,
                RarityBadge = rarityBadge,
                RarityMaterial = rarityMaterial,
                RarityLabel = rarityLabel,
                OwnedCount = ownedCount,
                NewBadge = newBadge
            };
        }

        private void RebuildVirtualCatalog()
        {
            if (_catalogContent == null)
                return;

            _catalogFilteredEntries.Clear();
            _catalogOwnedCopies.Clear();
            _catalogNewCardIds.Clear();
            if (_repository?.State?.pendingDeckEditorNewCardIds != null)
            {
                foreach (string newCardId in
                         _repository.State.pendingDeckEditorNewCardIds)
                {
                    string normalized =
                        FrontendCardIdentity.NormalizeOfficialId(newCardId);
                    if (!string.IsNullOrWhiteSpace(normalized))
                        _catalogNewCardIds.Add(normalized);
                }
            }
            _deckEditorNewMarkersWereShown |= _catalogNewCardIds.Count > 0;
            BuildCatalogExplicitOwnershipIndex();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            List<CardCatalogEntry> entries = ReadyCatalogEntries();

            string search = _catalogSearch.Trim();
            foreach (CardCatalogEntry entry in entries)
            {
                if (entry == null ||
                    (_catalogFilter != CardCategory.Unknown &&
                     entry.Category != _catalogFilter))
                {
                    continue;
                }

                string cardId = DeckRepository.StableCardId(entry);
                if (!uniqueIds.Add(cardId) ||
                    !MatchesCatalogSearch(entry, cardId, search) ||
                    !MatchesAdvancedCatalogFilters(entry, cardId) ||
                    (_catalogRelatedCardIds != null &&
                     !_catalogRelatedCardIds.Contains(cardId)))
                {
                    continue;
                }
                _catalogFilteredEntries.Add(entry);
            }

            SortCatalogEntriesOwnedFirst();

            int rowCount = Mathf.CeilToInt(
                _catalogFilteredEntries.Count / (float)CatalogVirtualColumns);
            float pitchY = CatalogVirtualCellHeight + CatalogVirtualSpacingY;
            float contentHeight = CatalogVirtualPadding * 2f +
                Mathf.Max(0, rowCount) * pitchY -
                (rowCount > 0 ? CatalogVirtualSpacingY : 0f);
            float viewportHeight = _catalogDropZone != null
                ? _catalogDropZone.rect.height
                : 0f;
            _catalogContent.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(viewportHeight, contentHeight));
            _catalogContent.anchoredPosition = new Vector2(
                _catalogContent.anchoredPosition.x,
                0f);
            if (_catalogScroll != null)
            {
                _catalogScroll.StopMovement();
                _catalogScroll.verticalNormalizedPosition = 1f;
            }

            foreach (CatalogVirtualCell cell in _catalogVirtualCells)
            {
                cell.DataIndex = -1;
                cell.Root.gameObject.SetActive(false);
            }
            _catalogFirstVirtualRow = -1;
            EnsureVirtualCatalogPool();
            Canvas.ForceUpdateCanvases();
            RefreshVirtualCatalogWindow(true);
            UpdateAdvancedCatalogFilterButton();

            if (_catalogFilteredEntries.Count == 0)
            {
                SetEditorStatus(
                    "Nenhuma carta corresponde aos filtros selecionados.",
                    Gold);
            }
            else if (_editorStatus != null &&
                     _editorStatus.text.StartsWith(
                         "Nenhuma carta corresponde",
                         StringComparison.Ordinal))
            {
                _editorStatus.text = string.Empty;
            }
        }

        private static bool MatchesCatalogSearch(
            CardCatalogEntry entry,
            string cardId,
            string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;
            return ContainsInvariant(entry.DisplayName, search) ||
                   ContainsInvariant(entry.EnglishName, search) ||
                   ContainsInvariant(entry.TypeName, search) ||
                   ContainsInvariant(entry.RaceName, search) ||
                   ContainsInvariant(cardId, search);
        }

        private bool MatchesAdvancedCatalogFilters(
            CardCatalogEntry entry,
            string cardId)
        {
            if (_catalogRarityFilters.Count > 0 &&
                !_catalogRarityFilters.Contains(entry.Rarity))
            {
                return false;
            }
            if (_catalogAttributeFilters.Count > 0 &&
                !_catalogAttributeFilters.Contains(entry.Attribute))
            {
                return false;
            }
            if (_catalogFrameFilters.Count > 0 &&
                !_catalogFrameFilters.Contains(entry.MonsterFrame))
            {
                return false;
            }
            if (_catalogCraftableOnly && !entry.IsCraftable)
                return false;

            int owned = CatalogOwnedCopies(entry, cardId);
            return _catalogOwnershipFilter switch
            {
                1 => owned > 0,
                2 => owned == 0,
                _ => true
            };
        }

        private void HandleVirtualCatalogScroll(Vector2 _)
        {
            RefreshVirtualCatalogWindow(false);
        }

        private void RefreshVirtualCatalogWindow(bool force)
        {
            if (_catalogContent == null || _catalogVirtualCells.Count == 0)
                return;

            float pitchY = CatalogVirtualCellHeight + CatalogVirtualSpacingY;
            float scrollOffset = Mathf.Max(
                0f,
                _catalogContent.anchoredPosition.y);
            int firstVisibleRow = Mathf.Max(
                0,
                Mathf.FloorToInt(scrollOffset / pitchY));
            int firstRow = Mathf.Max(0, firstVisibleRow - 3);
            if (!force && firstRow == _catalogFirstVirtualRow)
                return;
            _catalogFirstVirtualRow = firstRow;

            int firstIndex = firstRow * CatalogVirtualColumns;
            int lastIndex = Mathf.Min(
                _catalogFilteredEntries.Count,
                firstIndex + CatalogVirtualPoolSize);
            foreach (CatalogVirtualCell cell in _catalogVirtualCells)
            {
                if (cell.DataIndex < firstIndex || cell.DataIndex >= lastIndex)
                {
                    cell.DataIndex = -1;
                    cell.Root.gameObject.SetActive(false);
                }
            }

            for (int dataIndex = firstIndex;
                 dataIndex < lastIndex;
                 dataIndex++)
            {
                CatalogVirtualCell cell = FindVirtualCell(dataIndex);
                if (cell == null)
                {
                    cell = FindFreeVirtualCell();
                    if (cell == null)
                        break;
                    BindVirtualCatalogCell(cell, dataIndex);
                }
                PositionVirtualCatalogCell(cell, dataIndex);
            }
        }

        private CatalogVirtualCell FindVirtualCell(int dataIndex)
        {
            foreach (CatalogVirtualCell cell in _catalogVirtualCells)
            {
                if (cell.DataIndex == dataIndex)
                    return cell;
            }
            return null;
        }

        private CatalogVirtualCell FindFreeVirtualCell()
        {
            foreach (CatalogVirtualCell cell in _catalogVirtualCells)
            {
                if (cell.DataIndex < 0)
                    return cell;
            }
            return null;
        }

        private void BindVirtualCatalogCell(
            CatalogVirtualCell cell,
            int dataIndex)
        {
            CardCatalogEntry entry = _catalogFilteredEntries[dataIndex];
            string cardId = DeckRepository.StableCardId(entry);
            Sprite artwork = entry.Artwork;
            int ownedCopies = CatalogOwnedCopies(entry, cardId);

            cell.DataIndex = dataIndex;
            cell.Root.gameObject.name = $"Célula da coleção {cardId}";
            cell.Root.gameObject.SetActive(true);
            Stretch(cell.Artwork.rectTransform);
            cell.Artwork.rectTransform.localScale = Vector3.one;
            cell.Artwork.rectTransform.localEulerAngles = Vector3.zero;
            Stretch(cell.LockedOverlay.rectTransform);
            cell.LockedOverlay.rectTransform.localScale = Vector3.one;
            cell.Artwork.sprite = artwork;
            cell.Artwork.color = artwork != null ? Color.white : Color.clear;
            cell.LockedOverlay.gameObject.SetActive(ownedCopies == 0);
            cell.OwnedCount.text = ownedCopies.ToString();
            cell.OwnedCount.color = ownedCopies > 0 ? Lime : Muted;
            cell.ArtworkOutline.effectColor = CatalogCardOutlineColor(entry);
            cell.Drag.Setup(this, cardId, artwork, ownedCopies > 0);
            cell.NewBadge?.SetVisible(_catalogNewCardIds.Contains(cardId));
            BindVirtualCatalogBanlist(cell, cardId);
            BindVirtualCatalogRarity(cell, entry);
        }

        private int CatalogOwnedCopies(
            CardCatalogEntry entry,
            string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
                return 0;
            if (_catalogOwnedCopies.TryGetValue(cardId, out int cached))
                return cached;
            _catalogExplicitOwnedCopies.TryGetValue(
                cardId,
                out int explicitQuantity);
            int owned = entry == null || _repository == null
                ? 0
                : !entry.HasAuthoredArtwork
                    ? explicitQuantity
                    : DeckShopCatalog.OwnedCopies(
                        _repository.State,
                        cardId,
                        explicitQuantity);
            _catalogOwnedCopies[cardId] = owned;
            return owned;
        }

        private void BuildCatalogExplicitOwnershipIndex()
        {
            _catalogExplicitOwnedCopies.Clear();
            if (_repository?.State?.cardQuantities == null)
                return;
            foreach (CardQuantityRecord record in
                     _repository.State.cardQuantities)
            {
                if (record == null)
                    continue;
                string cardId = FrontendCardIdentity.NormalizeOfficialId(
                    record.cardId);
                if (string.IsNullOrWhiteSpace(cardId))
                    continue;
                _catalogExplicitOwnedCopies.TryGetValue(
                    cardId,
                    out int current);
                _catalogExplicitOwnedCopies[cardId] =
                    Mathf.Max(current, Mathf.Max(0, record.quantity));
            }
        }

        private static void BindVirtualCatalogBanlist(
            CatalogVirtualCell cell,
            string cardId)
        {
            if (cell?.BanlistBadge == null)
                return;
            Sprite sprite = BanlistService.Active.BadgeFor(cardId);
            cell.BanlistBadge.sprite = sprite;
            cell.BanlistBadge.gameObject.SetActive(sprite != null);
            if (sprite != null && cell.BanlistTooltip != null)
            {
                cell.BanlistTooltip.Initialize(
                    BanlistService.Active.MaximumCopies(cardId));
            }
        }

        private static void BindVirtualCatalogRarity(
            CatalogVirtualCell cell,
            CardCatalogEntry entry)
        {
            if (cell?.RarityBadge == null)
                return;
            bool valid = entry != null &&
                         CardRarityCatalog.IsValid(entry.Rarity);
            cell.RarityBadge.gameObject.SetActive(valid);
            if (!valid)
                return;

            CardRarity rarity = entry.Rarity;
            cell.RarityBadge.gameObject.name = $"Raridade {rarity}";
            if (cell.RarityLabel != null)
                cell.RarityLabel.text = CardRarityCatalog.Label(rarity);
            if (cell.RarityMaterial == null)
                return;
            RarityPalette(
                rarity,
                out Color top,
                out Color bottom,
                out Color edge,
                out Color shine);
            cell.RarityMaterial.SetPalette(top, bottom, edge, shine);
        }

        private void SortCatalogEntriesOwnedFirst()
        {
            _catalogFilteredEntries.Sort((left, right) =>
            {
                string leftId = DeckRepository.StableCardId(left);
                string rightId = DeckRepository.StableCardId(right);
                int leftGroup = _catalogNewCardIds.Contains(leftId)
                    ? 0
                    : CatalogOwnedCopies(left, leftId) > 0
                        ? 1
                        : 2;
                int rightGroup = _catalogNewCardIds.Contains(rightId)
                    ? 0
                    : CatalogOwnedCopies(right, rightId) > 0
                        ? 1
                        : 2;
                int ownership = leftGroup.CompareTo(rightGroup);
                if (ownership != 0)
                    return ownership;

                int name = string.Compare(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.CurrentCultureIgnoreCase);
                if (name == 0)
                {
                    name = string.Compare(
                        leftId,
                        rightId,
                        StringComparison.Ordinal);
                }
                return _catalogSortDescending ? -name : name;
            });
        }

        private static Color CatalogCardOutlineColor(CardCatalogEntry entry)
        {
            return entry.Category == CardCategory.Spell
                ? new Color(0.1f, 0.86f, 0.74f, 0.8f)
                : entry.Category == CardCategory.Trap
                    ? new Color(0.9f, 0.24f, 0.64f, 0.8f)
                    : new Color(Gold.r, Gold.g, Gold.b, 0.75f);
        }

        private static void PositionVirtualCatalogCell(
            CatalogVirtualCell cell,
            int dataIndex)
        {
            int row = dataIndex / CatalogVirtualColumns;
            int column = dataIndex % CatalogVirtualColumns;
            cell.Root.anchorMin = new Vector2(0f, 1f);
            cell.Root.anchorMax = new Vector2(0f, 1f);
            cell.Root.pivot = new Vector2(0f, 1f);
            cell.Root.sizeDelta = new Vector2(
                CatalogVirtualCellWidth,
                CatalogVirtualCellHeight);
            cell.Root.localScale = Vector3.one;
            cell.Root.localEulerAngles = Vector3.zero;
            cell.Root.anchoredPosition = new Vector2(
                CatalogVirtualPadding +
                column * (CatalogVirtualCellWidth + CatalogVirtualSpacingX),
                -CatalogVirtualPadding -
                row * (CatalogVirtualCellHeight + CatalogVirtualSpacingY));
            cell.Root.gameObject.SetActive(true);
        }

        private void BuildDeckEditorRelatedCardsButton(Transform parent)
        {
            _deckEditorRelatedCardsButton = CreateCatalogControlButton(
                parent,
                "CARDS RELACIONADOS",
                new Vector2(0.605f, 0.49f),
                new Vector2(0.955f, 0.522f),
                Cyan,
                FilterSelectedCardRelations);
        }

        private void FinalizeDeckEditorRequestedLayout()
        {
            GridLayoutGroup mainGrid = _mainDeckContent != null
                ? _mainDeckContent.GetComponent<GridLayoutGroup>()
                : null;
            if (mainGrid != null)
                mainGrid.constraintCount = MainDeckGridColumns;

            if (_catalogContent != null)
            {
                _catalogContent.anchorMin = new Vector2(0f, 1f);
                _catalogContent.anchorMax = new Vector2(1f, 1f);
                _catalogContent.pivot = new Vector2(0.5f, 1f);
                _catalogContent.localScale = Vector3.one;
                _catalogContent.localEulerAngles = Vector3.zero;
                _catalogContent.offsetMin = new Vector2(
                    CatalogVirtualPadding,
                    _catalogContent.offsetMin.y);
                _catalogContent.offsetMax = new Vector2(
                    -30f,
                    _catalogContent.offsetMax.y);
            }

            if (_deckEditorDetailName != null)
            {
                ApplyCapturedRectTransform(
                    _deckEditorDetailName.rectTransform,
                    new Vector2(0.045f, 0.915f),
                    new Vector2(0.82f, 0.985f),
                    0f,
                    0f,
                    0f,
                    0f);
            }
            if (_deckEditorDetailType != null)
                ApplyDeckEditorTypeLayout(false);

            if (!string.IsNullOrWhiteSpace(_deckEditorSelectedCardId))
                ShowDeckEditorCardDetails(_deckEditorSelectedCardId);
            RebuildVirtualCatalog();
        }

        private void FilterSelectedCardRelations()
        {
            CardCatalogEntry selected = DeckRepository.ResolveCard(
                _catalog,
                _deckEditorSelectedCardId);
            if (selected == null)
                return;

            string selectedId = DeckRepository.StableCardId(selected);
            _catalogRelatedCardIds = BuildRelatedCardIds(selected);
            _catalogSearch = string.Empty;
            _catalogFilter = CardCategory.Unknown;
            _catalogSortDescending = false;
            _catalogRarityFilters.Clear();
            _catalogAttributeFilters.Clear();
            _catalogFrameFilters.Clear();
            _catalogOwnershipFilter = 0;
            _catalogCraftableOnly = false;
            if (_catalogSearchInput != null)
                _catalogSearchInput.SetTextWithoutNotify(string.Empty);
            UpdateCatalogFilterVisuals();
            RebuildCatalog();
            SetEditorStatus(
                $"{_catalogRelatedCardIds.Count} cards relacionados a " +
                $"{selected.DisplayName}.",
                Cyan);
        }

        private HashSet<string> BuildRelatedCardIds(CardCatalogEntry selected)
        {
            var related = new HashSet<string>(StringComparer.Ordinal);
            string selectedId = DeckRepository.StableCardId(selected);
            related.Add(selectedId);

            var keys = new HashSet<string>(StringComparer.Ordinal);
            AddRelationKey(keys, selected.DisplayName);
            AddRelationKey(keys, selected.EnglishName);
            AddQuotedRelationKeys(keys, selected.EffectText);
            string selectedEffect = NormalizeRelationText(selected.EffectText);

            foreach (CardCatalogEntry candidate in ReadyCatalogEntries())
            {
                string candidateId = DeckRepository.StableCardId(candidate);
                if (string.Equals(candidateId, selectedId, StringComparison.Ordinal))
                    continue;

                string candidateName = NormalizeRelationText(
                    candidate.DisplayName);
                string candidateEnglish = NormalizeRelationText(
                    candidate.EnglishName);
                string candidateEffect = NormalizeRelationText(
                    candidate.EffectText);
                bool matches = false;
                foreach (string key in keys)
                {
                    if (ContainsRelation(candidateName, key) ||
                        ContainsRelation(candidateEnglish, key) ||
                        ContainsRelation(candidateEffect, key))
                    {
                        matches = true;
                        break;
                    }
                }
                if (!matches && !string.IsNullOrWhiteSpace(selectedEffect))
                {
                    matches = ContainsRelation(
                                  selectedEffect,
                                  candidateName) ||
                              ContainsRelation(
                                  selectedEffect,
                                  candidateEnglish);
                }
                if (matches)
                    related.Add(candidateId);
            }
            return related;
        }

        private static void AddRelationKey(HashSet<string> keys, string value)
        {
            string normalized = NormalizeRelationText(value);
            if (normalized.Length >= 4)
                keys.Add(normalized);
        }

        private static void AddQuotedRelationKeys(
            HashSet<string> keys,
            string effect)
        {
            if (string.IsNullOrWhiteSpace(effect))
                return;
            int quoteStart = -1;
            for (int index = 0; index < effect.Length; index++)
            {
                char character = effect[index];
                bool quote = character == '"' || character == '“' ||
                             character == '”';
                if (!quote)
                    continue;
                if (quoteStart < 0)
                {
                    quoteStart = index + 1;
                    continue;
                }
                if (index > quoteStart)
                    AddRelationKey(keys, effect.Substring(quoteStart, index - quoteStart));
                quoteStart = -1;
            }
        }

        private static bool ContainsRelation(string source, string key)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   !string.IsNullOrWhiteSpace(key) &&
                   source.IndexOf(key, StringComparison.Ordinal) >= 0;
        }

        private static string NormalizeRelationText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            bool previousSpace = true;
            foreach (char character in decomposed)
            {
                UnicodeCategory category =
                    CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                    continue;
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    previousSpace = false;
                }
                else if (!previousSpace)
                {
                    builder.Append(' ');
                    previousSpace = true;
                }
            }
            return builder.ToString().Trim();
        }

        private static bool ContainsInvariant(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(
                       search,
                       StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private void ShowAdvancedCatalogFilters()
        {
            CloseAdvancedCatalogFilters();
            Image veil = CreatePanel(
                _screenRoot,
                "Filtros avançados do catálogo",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0.008f, 0.018f, 0.92f));
            _catalogAdvancedFilterModal = veil.gameObject;
            Image panel = CreatePanel(
                veil.transform,
                "Painel de filtros avançados",
                new Vector2(0.22f, 0.10f),
                new Vector2(0.78f, 0.90f),
                Color.clear);
            SkinDeckEditorSurface(panel, DeckEmerald, true, 0.98f);
            Image topEnergy = CreatePanel(
                panel.transform,
                "Energia superior dos filtros",
                new Vector2(0.05f, 0.887f),
                new Vector2(0.95f, 0.893f),
                new Color(DeckAmber.r, DeckAmber.g, DeckAmber.b, 0.92f));
            topEnergy.raycastTarget = false;
            Image lowerEnergy = CreatePanel(
                panel.transform,
                "Energia inferior dos filtros",
                new Vector2(0.18f, 0.012f),
                new Vector2(0.82f, 0.018f),
                new Color(DeckEmerald.r, DeckEmerald.g, DeckEmerald.b, 0.78f));
            lowerEnergy.raycastTarget = false;
            CreateText(
                panel.transform,
                "FILTROS AVANÇADOS",
                27,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.05f, 0.90f),
                new Vector2(0.78f, 0.98f),
                TextAnchor.MiddleLeft);
            CreateText(
                panel.transform,
                "OFICINA DE DECKS  •  FILTRAGEM TÁTICA",
                10,
                FontStyle.Bold,
                new Color(DeckMint.r, DeckMint.g, DeckMint.b, 0.82f),
                new Vector2(0.05f, 0.852f),
                new Vector2(0.60f, 0.882f),
                TextAnchor.MiddleLeft);
            CreateCatalogControlButton(
                panel.transform,
                "FECHAR",
                new Vector2(0.80f, 0.91f),
                new Vector2(0.96f, 0.975f),
                Danger,
                CloseAdvancedCatalogFilters);

            CreateAdvancedFilterSectionLabel(panel.transform, "RARIDADE", 0.82f);
            CardRarity[] rarities =
                { CardRarity.N, CardRarity.R, CardRarity.SR, CardRarity.UR };
            string[] rarityLabels = { "N", "R", "SR", "UR" };
            for (int index = 0; index < rarities.Length; index++)
            {
                CardRarity rarity = rarities[index];
                float x = 0.05f + index * 0.225f;
                CreateAdvancedFilterToggle(
                    panel.transform,
                    rarityLabels[index],
                    new Vector2(x, 0.73f),
                    new Vector2(x + 0.19f, 0.81f),
                    () => _catalogRarityFilters.Contains(rarity),
                    () => ToggleSetValue(_catalogRarityFilters, rarity));
            }

            CreateAdvancedFilterSectionLabel(panel.transform, "ATRIBUTO", 0.65f);
            CardAttribute[] attributes =
            {
                CardAttribute.Dark, CardAttribute.Light, CardAttribute.Earth,
                CardAttribute.Water, CardAttribute.Fire, CardAttribute.Wind,
                CardAttribute.Divine
            };
            string[] attributeLabels =
                { "TREVAS", "LUZ", "TERRA", "ÁGUA", "FOGO", "VENTO", "DIVINO" };
            for (int index = 0; index < attributes.Length; index++)
            {
                CardAttribute attribute = attributes[index];
                float width = 0.88f / attributes.Length;
                float x = 0.05f + index * width;
                CreateAdvancedFilterToggle(
                    panel.transform,
                    attributeLabels[index],
                    new Vector2(x, 0.56f),
                    new Vector2(x + width - 0.008f, 0.64f),
                    () => _catalogAttributeFilters.Contains(attribute),
                    () => ToggleSetValue(_catalogAttributeFilters, attribute));
            }

            CreateAdvancedFilterSectionLabel(panel.transform, "TIPO DE MONSTRO", 0.48f);
            MonsterFrameKind[] frames =
            {
                MonsterFrameKind.Normal, MonsterFrameKind.Effect,
                MonsterFrameKind.Ritual, MonsterFrameKind.Fusion,
                MonsterFrameKind.Synchro, MonsterFrameKind.Xyz,
                MonsterFrameKind.Link, MonsterFrameKind.Pendulum
            };
            string[] frameLabels =
                { "NORMAL", "EFEITO", "RITUAL", "FUSÃO", "SINCRO", "XYZ", "LINK", "PÊNDULO" };
            for (int index = 0; index < frames.Length; index++)
            {
                MonsterFrameKind frame = frames[index];
                int column = index % 4;
                int row = index / 4;
                float x = 0.05f + column * 0.225f;
                float yMin = row == 0 ? 0.39f : 0.30f;
                CreateAdvancedFilterToggle(
                    panel.transform,
                    frameLabels[index],
                    new Vector2(x, yMin),
                    new Vector2(x + 0.19f, yMin + 0.08f),
                    () => _catalogFrameFilters.Contains(frame),
                    () => ToggleSetValue(_catalogFrameFilters, frame));
            }

            CreateAdvancedFilterSectionLabel(panel.transform, "COLEÇÃO E CRAFT", 0.22f);
            CreateOwnershipFilterToggle(panel.transform, "TODAS", 0, 0.05f);
            CreateOwnershipFilterToggle(panel.transform, "OBTIDAS", 1, 0.275f);
            CreateOwnershipFilterToggle(panel.transform, "NÃO OBTIDAS", 2, 0.50f);
            CreateAdvancedFilterToggle(
                panel.transform,
                "GERÁVEIS",
                new Vector2(0.725f, 0.13f),
                new Vector2(0.93f, 0.21f),
                () => _catalogCraftableOnly,
                () => _catalogCraftableOnly = !_catalogCraftableOnly);

            CreateCatalogControlButton(
                panel.transform,
                "LIMPAR FILTROS",
                new Vector2(0.05f, 0.025f),
                new Vector2(0.46f, 0.105f),
                Gold,
                ResetAdvancedFiltersFromModal);
            CreateCatalogControlButton(
                panel.transform,
                "CONCLUIR",
                new Vector2(0.54f, 0.025f),
                new Vector2(0.95f, 0.105f),
                Lime,
                CloseAdvancedCatalogFilters);
            veil.transform.SetAsLastSibling();
        }

        private static void CreateAdvancedFilterSectionLabel(
            Transform parent,
            string label,
            float bottom)
        {
            Image marker = CreatePanel(
                parent,
                $"Marcador {label}",
                new Vector2(0.05f, bottom + 0.016f),
                new Vector2(0.058f, bottom + 0.054f),
                DeckAmber);
            marker.raycastTarget = false;
            Text section = CreateText(
                parent,
                label,
                15,
                FontStyle.Bold,
                new Color(DeckMint.r, DeckMint.g, DeckMint.b, 1f),
                new Vector2(0.07f, bottom),
                new Vector2(0.34f, bottom + 0.07f),
                TextAnchor.MiddleLeft);
            section.gameObject.name = $"Título {label}";
            Image line = CreatePanel(
                parent,
                $"Linha {label}",
                new Vector2(0.34f, bottom + 0.033f),
                new Vector2(0.95f, bottom + 0.037f),
                new Color(DeckEmerald.r, DeckEmerald.g, DeckEmerald.b, 0.44f));
            line.raycastTarget = false;
        }

        private void CreateOwnershipFilterToggle(
            Transform parent,
            string label,
            int filter,
            float x)
        {
            CreateAdvancedFilterToggle(
                parent,
                label,
                new Vector2(x, 0.13f),
                new Vector2(x + 0.205f, 0.21f),
                () => _catalogOwnershipFilter == filter,
                () => _catalogOwnershipFilter = filter);
        }

        private void CreateAdvancedFilterToggle(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Func<bool> selected,
            Action toggle)
        {
            Image button = null;
            button = CreateCatalogControlButton(
                parent,
                label,
                min,
                max,
                selected() ? Lime : Cyan,
                () =>
                {
                    toggle();
                    StyleAdvancedFilterToggle(button, selected());
                    RebuildCatalog();
                });
            StyleAdvancedFilterToggle(button, selected());
        }

        private static void StyleAdvancedFilterToggle(
            Image button,
            bool selected)
        {
            if (button == null)
                return;
            StyleCatalogControlButton(
                button,
                selected ? DeckAmber : DeckMint,
                selected);
        }

        private static void ToggleSetValue<T>(HashSet<T> set, T value)
        {
            if (!set.Add(value))
                set.Remove(value);
        }

        private void ResetAdvancedFiltersFromModal()
        {
            ClearAdvancedCatalogFilterState();
            RebuildCatalog();
            ShowAdvancedCatalogFilters();
        }

        private void ClearAdvancedCatalogFilterState()
        {
            _catalogRarityFilters.Clear();
            _catalogAttributeFilters.Clear();
            _catalogFrameFilters.Clear();
            _catalogOwnershipFilter = 0;
            _catalogCraftableOnly = false;
            _catalogRelatedCardIds = null;
            UpdateAdvancedCatalogFilterButton();
        }

        private void UpdateAdvancedCatalogFilterButton()
        {
            if (_catalogAdvancedFilterButton == null)
                return;
            int count = _catalogRarityFilters.Count +
                        _catalogAttributeFilters.Count +
                        _catalogFrameFilters.Count +
                        (_catalogOwnershipFilter == 0 ? 0 : 1) +
                        (_catalogCraftableOnly ? 1 : 0) +
                        (_catalogRelatedCardIds == null ? 0 : 1);
            Text label =
                _catalogAdvancedFilterButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = count > 0 ? $"FILTROS ({count})" : "FILTROS";
            StyleCatalogControlButton(
                _catalogAdvancedFilterButton,
                count > 0 ? DeckAmber : DeckMint,
                count > 0);
        }

        private void CloseAdvancedCatalogFilters()
        {
            if (_catalogAdvancedFilterModal != null)
                Destroy(_catalogAdvancedFilterModal);
            _catalogAdvancedFilterModal = null;
        }

        private void ReleaseVirtualCatalogView()
        {
            _catalogScroll = null;
            _catalogAdvancedFilterButton = null;
            _deckEditorRelatedCardsButton = null;
            _catalogAdvancedFilterModal = null;
            _catalogVirtualCells.Clear();
            _catalogFilteredEntries.Clear();
            _catalogOwnedCopies.Clear();
            _catalogExplicitOwnedCopies.Clear();
            _catalogFirstVirtualRow = -1;
        }
    }
}
