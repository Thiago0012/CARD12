using System;
using System.Collections.Generic;
using System.IO;
using ArcaneDuel.DuelEngine.Protocol;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public sealed class CardViewRegistry : IDisposable
    {
        private readonly CardVisualCatalog catalog;
        private readonly Dictionary<uint, Texture2D> textures =
            new Dictionary<uint, Texture2D>();

        public CardViewRegistry(CardVisualCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int LoadedCount => textures.Count;

        public bool TryGetTexture(uint code, out Texture2D texture)
        {
            if (textures.TryGetValue(code, out texture))
            {
                return texture != null;
            }
            if (!catalog.TryGet(code, out CardVisualData visual))
            {
                texture = null;
                return false;
            }

            string path = Path.Combine(
                Application.streamingAssetsPath,
                "Ygo",
                "Art",
                visual.artFile);
            if (!File.Exists(path))
            {
                texture = null;
                return false;
            }

            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"CardArt_{code}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
                return false;
            }
            textures.Add(code, texture);
            return true;
        }

        public void Dispose()
        {
            foreach (Texture2D texture in textures.Values)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            textures.Clear();
        }
    }

    public sealed class PlayerChoicePresenter
    {
        private readonly HashSet<uint> candidateCodes = new HashSet<uint>();

        public void Rebuild(DuelPrompt prompt)
        {
            candidateCodes.Clear();
            if (prompt == null) return;
            foreach (DuelChoice choice in prompt.Choices)
            {
                if (choice.CardCode != 0)
                {
                    candidateCodes.Add(choice.CardCode);
                }
            }
        }

        public bool IsCandidate(uint code)
        {
            return candidateCodes.Contains(code);
        }
    }

    public sealed class DuelVisualCue
    {
        public string Text { get; internal set; }
        public Color Color { get; internal set; }
        public float Duration { get; internal set; }
        public uint CardCode { get; internal set; }
    }

    public sealed class DuelAnimationQueue
    {
        private readonly Queue<DuelVisualCue> pending =
            new Queue<DuelVisualCue>();
        private float remaining;

        public DuelVisualCue Current { get; private set; }
        public float Progress => Current == null || Current.Duration <= 0f
            ? 0f
            : 1f - Mathf.Clamp01(remaining / Current.Duration);

        public void Enqueue(DuelEvent duelEvent, float speed = 1f)
        {
            DuelVisualCue cue = CreateCue(duelEvent);
            if (cue != null)
            {
                cue.Duration /= Mathf.Max(0.01f, speed);
                pending.Enqueue(cue);
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (Current != null)
            {
                remaining -= Mathf.Max(0f, unscaledDeltaTime);
                if (remaining <= 0f) Current = null;
            }
            if (Current == null && pending.Count > 0)
            {
                Current = pending.Dequeue();
                remaining = Current.Duration;
            }
        }

        private static DuelVisualCue CreateCue(DuelEvent duelEvent)
        {
            switch (duelEvent.Message)
            {
                case CoreMessage.NewTurn:
                    return Cue(
                        $"TURNO DO DUELISTA {duelEvent.Player + 1}",
                        new Color(0.16f, 0.88f, 1f),
                        0.9f);
                case CoreMessage.Summoning:
                case CoreMessage.SpecialSummoning:
                case CoreMessage.FlipSummoning:
                    return Cue(
                        "INVOCAÇÃO",
                        new Color(0.95f, 0.66f, 0.23f),
                        0.75f,
                        duelEvent.Code);
                case CoreMessage.Chaining:
                    return Cue(
                        $"CORRENTE {duelEvent.Value}",
                        new Color(0.74f, 0.42f, 1f),
                        0.72f,
                        duelEvent.Code);
                case CoreMessage.Damage:
                    return Cue(
                        $"-{duelEvent.Value} PV",
                        new Color(1f, 0.22f, 0.34f),
                        0.7f);
                case CoreMessage.Recover:
                    return Cue(
                        $"+{duelEvent.Value} PV",
                        new Color(0.2f, 1f, 0.58f),
                        0.7f);
                case CoreMessage.Win:
                    return Cue(
                        duelEvent.Player == 0 ? "VITÓRIA" : "DUELO ENCERRADO",
                        new Color(1f, 0.78f, 0.22f),
                        1.8f);
                default:
                    return null;
            }
        }

        private static DuelVisualCue Cue(
            string text,
            Color color,
            float duration,
            uint cardCode = 0)
        {
            return new DuelVisualCue
            {
                Text = text,
                Color = color,
                Duration = duration,
                CardCode = cardCode
            };
        }
    }
}
