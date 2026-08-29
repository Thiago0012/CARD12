using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed class ProfileArtworkDefinition
    {
        public string ArtworkId { get; }
        public string DisplayName { get; }
        public string ResourcePath { get; }
        public int PriceCoins { get; }
        public bool IsPurchasable { get; }

        public ProfileArtworkDefinition(
            string artworkId,
            string displayName,
            string resourcePath,
            bool isPurchasable = true)
        {
            ArtworkId = artworkId;
            DisplayName = displayName;
            ResourcePath = resourcePath;
            IsPurchasable = isPurchasable;
            PriceCoins = isPurchasable
                ? ProfileArtworkCatalog.ArtworkPriceCoins
                : 0;
        }
    }

    /// <summary>
    /// Catálogo das artes cosméticas exibidas dentro da moldura direita da
    /// tela inicial. O item padrão mantém a moldura vazia até o jogador
    /// comprar e equipar uma arte.
    /// </summary>
    public static class ProfileArtworkCatalog
    {
        public const int CatalogVersion = 1;
        public const int ArtworkPriceCoins = 100;
        public const string DefaultArtworkId = "artwork-none";

        private const string Root = "Profile/Artworks/";

        private static readonly ProfileArtworkDefinition[] Items =
        {
            new(DefaultArtworkId, "Sem artwork", string.Empty, false),
            Art("artwork-001", "Aromalilith Rosemary",
                "aromalilith_rosemary___yugioh_master_duel_by_matteste_di5nnhw-pre"),
            Art("artwork-002", "Quimera, a Besta da Ilusão",
                "chimera_the_illusion_beast_full_render_master_duel_by_ygofaraday_dh7vjhq-pre"),
            Art("artwork-003", "Artwork Arcano 03",
                "df3ksgu-80a72fe8-e680-430c-96cb-08e592ed66fc"),
            Art("artwork-004", "Artwork Arcano 04",
                "df3yr5s-58164e67-d65f-455d-bde7-a80cf725807f"),
            Art("artwork-005", "Artwork Arcano 05",
                "dfp574h-8a12b164-a4e9-42b8-bab4-e573b3c5992e"),
            Art("artwork-006", "Artwork Arcano 06",
                "dg5c0xg-3b770f35-95cd-4df2-8479-150e1c97ab84"),
            Art("artwork-007", "Artwork Arcano 07",
                "dguobj0-dc3e0792-f910-42d6-be6a-25896d7908e4"),
            Art("artwork-008", "Diabellze, a Guardiã do Pecado",
                "diabellze_the_sinkeeper_master_duel_render_by_ygofaraday_dip9sdq-pre"),
            Art("artwork-009", "Artwork Arcano 09",
                "dipdmxc-f5c732b8-f274-46eb-9355-dac68050ba2d"),
            Art("artwork-010", "Artwork Arcano 10",
                "dipeatd-528d4e74-036c-46eb-a926-0bc31f1a2e2a"),
            Art("artwork-011", "Artwork Arcano 11",
                "djb57db-aaa343b5-2e9d-44d6-af52-205be03a9315"),
            Art("artwork-012", "Artwork Arcano 12",
                "djb66ho-fbb71163-c1b3-45b7-80dc-ef19c3137423"),
            Art("artwork-013", "Artwork Arcano 13",
                "dkiwuu8-04002ea4-f377-41be-b23d-b9d136aefbcf"),
            Art("artwork-014", "Artwork Arcano 14",
                "dksrstl-e1eddc79-48dc-4163-b28c-7862c3f54a12"),
            Art("artwork-015", "Artwork Arcano 15",
                "dkz135a-89a4f3e0-e182-4c5c-b971-fe48352c292c"),
            Art("artwork-016", "Artwork Arcano 16",
                "dl1gw6a-64f1864d-49eb-492d-b63d-e1eeb004dc6e"),
            Art("artwork-017", "Artwork Arcano 17",
                "dm0i6pp-4100f8fb-0062-44b0-b444-f889c0e3541c"),
            Art("artwork-018", "Artwork Arcano 18",
                "dm0izm7-de893753-688b-4686-bd2a-2407ee878096"),
            Art("artwork-019", "Artwork Arcano 19",
                "dmel3ov-1ac557e4-70f5-4559-b8eb-f5f4f8eeb99c"),
            Art("artwork-020", "Impulso Dominus",
                "dominus_impulse_full_render_by_ygofaraday_djqobxb-pre"),
            Art("artwork-021", "Evil★Twin Lil-la",
                "eviltwin_lil_la___yugioh_master_duel_by_matteste_diiga6h-414w-2x"),
            Art("artwork-022", "Labirinto do Castelo de Prata",
                "labrynth_of_the_silver_castle___yugioh_master_duel_by_matteste_dhc7cqd-414w-2x"),
            Art("artwork-023", "Lady Labrynth do Castelo de Prata",
                "lady_labrynth_of_the_silver_castle_alt_art_render_by_ygofaraday_dkz1p4v-pre"),
            Art("artwork-024", "Dragão Branco Lendário",
                "legendary_dragon_of_white___yugioh_master_duel_by_matteste_dg5c3ki-pre"),
            Art("artwork-025", "Dragão Senhor da Luz e das Trevas",
                "light_and_darkness_dragonlord___yugioh_master_duel_by_matteste_dixfut3-pre"),
            Art("artwork-026", "Obelisco, o Atormentador",
                "obelisk_the_tormentor___yugioh_master_duel_by_matteste_dgi0119-pre"),
            Art("artwork-027", "Dragão Pêndulo de Olhos Anômalos",
                "odd_eyes_pendulum_dragon___yugioh_master_duel_by_matteste_dguomq0-414w-2x"),
            Art("artwork-028", "Raidraptor – Falcão da Rebelião Ascendente",
                "raidraptor___rising_rebellion_falcon_render__1__by_d_evil6661_dhxl41q-pre"),
            Art("artwork-029", "Dragão Supernova Vermelho",
                "red_supernova_dragon___yugioh_master_duel_by_matteste_dju6zdb-pre"),
            Art("artwork-030", "Lágrima, a Rainha Rikka",
                "teardrop_the_rikka_queen_by_voquocviet1412_dfz8jvq-414w-2x"),
            Art("artwork-031", "Anjo Nobre Trickstar",
                "trickstar_noble_angel___yugioh_master_duel_by_matteste_dipgg3m-pre"),
            Art("artwork-032", "Relíquia Branca de Dogmatika",
                "white_relic_of_dogmatika_by_voquocviet1412_ditljxx-pre")
        };

        private static readonly Dictionary<string, Sprite> SpriteCache =
            new(StringComparer.Ordinal);

        public static IReadOnlyList<ProfileArtworkDefinition> All => Items;
        public static IEnumerable<ProfileArtworkDefinition> StoreVisible =>
            Items.Where(item => item.IsPurchasable);

        public static ProfileArtworkDefinition Resolve(string artworkId)
        {
            return Items.FirstOrDefault(item => string.Equals(
                       item.ArtworkId,
                       artworkId,
                       StringComparison.Ordinal)) ?? Items[0];
        }

        public static string ResolveId(string artworkId) =>
            Resolve(artworkId).ArtworkId;

        public static Sprite LoadSprite(string artworkId)
        {
            string resolved = ResolveId(artworkId);
            if (string.Equals(resolved, DefaultArtworkId,
                    StringComparison.Ordinal))
            {
                return null;
            }
            if (SpriteCache.TryGetValue(resolved, out Sprite cached))
                return cached;

            ProfileArtworkDefinition definition = Resolve(resolved);
            Texture2D texture = Resources.Load<Texture2D>(
                definition.ResourcePath);
            if (texture == null || texture.width <= 0 || texture.height <= 0)
                return null;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = definition.DisplayName;
            sprite.hideFlags = HideFlags.DontSave;
            SpriteCache[resolved] = sprite;
            return sprite;
        }

        private static ProfileArtworkDefinition Art(
            string id,
            string displayName,
            string fileName)
        {
            return new ProfileArtworkDefinition(
                id,
                displayName,
                Root + fileName);
        }
    }
}
