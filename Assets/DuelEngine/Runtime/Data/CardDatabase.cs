using System;
using System.Collections.Generic;
using System.IO;
using ArcaneDuel.DuelEngine.Content;
using ArcaneDuel.DuelEngine.Interop;
using UnityEngine;

namespace ArcaneDuel.DuelEngine.Data
{
    public sealed class CardRecord
    {
        public uint Code { get; internal set; }
        public uint Alias { get; internal set; }
        public uint Type { get; internal set; }
        public int Level { get; internal set; }
        public uint Attribute { get; internal set; }
        public ulong Race { get; internal set; }
        public int Attack { get; internal set; }
        public int Defense { get; internal set; }
        public uint LeftScale { get; internal set; }
        public uint RightScale { get; internal set; }
        public uint LinkMarker { get; internal set; }
        public ushort[] Setcodes { get; internal set; }
        public string EnglishName { get; internal set; }
        public string Name { get; internal set; }
        public string Description { get; internal set; }
        public string[] Strings { get; internal set; }

        internal OcgCardData ToNative(IntPtr setcodes)
        {
            return new OcgCardData
            {
                Code = Code,
                Alias = Alias,
                Setcodes = setcodes,
                Type = Type,
                Level = unchecked((uint)Level),
                Attribute = Attribute,
                Race = Race,
                Attack = Attack,
                Defense = Defense,
                LeftScale = LeftScale,
                RightScale = RightScale,
                LinkMarker = LinkMarker
            };
        }
    }

    [Serializable]
    internal sealed class CardTextCollection
    {
        public int schemaVersion;
        public int count;
        public CardTextEntry[] cards;
    }

    [Serializable]
    internal sealed class CardTextEntry
    {
        public uint code;
        public string englishName;
        public string name;
        public string description;
        public string[] strings;
    }

    public sealed class CardDatabase
    {
        private readonly Dictionary<uint, CardRecord> records;

        private CardDatabase(Dictionary<uint, CardRecord> records)
        {
            this.records = records;
        }

        public int Count => records.Count;
        public IEnumerable<CardRecord> Cards => records.Values;

        public bool TryGet(uint code, out CardRecord record)
        {
            return records.TryGetValue(code, out record);
        }

        public CardRecord Get(uint code)
        {
            if (!TryGet(code, out CardRecord record))
            {
                throw new KeyNotFoundException($"Card {code:00000000} is not part of the compiled Core catalog.");
            }
            return record;
        }

        public static CardDatabase LoadDefault()
        {
            string root = YgoContentLocator.Resolve("Data");
            return Load(Path.Combine(root, "cards.bin"), Path.Combine(root, "card-texts.json"));
        }

        public static CardDatabase Load(string binaryPath, string textPath)
        {
            var result = new Dictionary<uint, CardRecord>();
            using (var stream = File.OpenRead(binaryPath))
            using (var reader = new BinaryReader(stream))
            {
                string magic = new string(reader.ReadChars(4));
                if (magic != "ADCB")
                {
                    throw new InvalidDataException($"Unexpected Arcane card database magic '{magic}'.");
                }
                uint version = reader.ReadUInt32();
                if (version != 1)
                {
                    throw new InvalidDataException($"Unsupported Arcane card database version {version}.");
                }
                uint count = reader.ReadUInt32();
                for (uint i = 0; i < count; i++)
                {
                    var card = new CardRecord
                    {
                        Code = reader.ReadUInt32(),
                        Alias = reader.ReadUInt32(),
                        Type = reader.ReadUInt32(),
                        Level = reader.ReadInt32(),
                        Attribute = reader.ReadUInt32(),
                        Race = reader.ReadUInt64(),
                        Attack = reader.ReadInt32(),
                        Defense = reader.ReadInt32(),
                        LeftScale = reader.ReadUInt32(),
                        RightScale = reader.ReadUInt32(),
                        LinkMarker = reader.ReadUInt32()
                    };
                    byte setcodeCount = reader.ReadByte();
                    card.Setcodes = new ushort[setcodeCount];
                    for (int setcodeIndex = 0; setcodeIndex < setcodeCount; setcodeIndex++)
                    {
                        card.Setcodes[setcodeIndex] = reader.ReadUInt16();
                    }
                    result.Add(card.Code, card);
                }
                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Arcane card database contains unexpected trailing bytes.");
                }
            }

            CardTextCollection texts = JsonUtility.FromJson<CardTextCollection>(File.ReadAllText(textPath));
            if (texts == null || texts.cards == null || texts.count != result.Count)
            {
                throw new InvalidDataException("Card text database does not match cards.bin.");
            }
            foreach (CardTextEntry text in texts.cards)
            {
                if (!result.TryGetValue(text.code, out CardRecord card))
                {
                    throw new InvalidDataException($"Text entry {text.code} has no binary card record.");
                }
                card.EnglishName = string.IsNullOrWhiteSpace(text.englishName)
                    ? text.name
                    : text.englishName;
                card.Name = text.name;
                card.Description = text.description;
                card.Strings = text.strings ?? new string[16];
            }
            return new CardDatabase(result);
        }
    }
}
