using System;
using System.Collections.Generic;

namespace ArcaneDuel.DuelEngine.State
{
    /// <summary>
    /// Identifies one physical copy in the presentation mirror. The printed
    /// code identifies the shared definition; RuntimeId distinguishes copies.
    /// Controller/location/sequence always describe the latest Core address.
    /// </summary>
    public readonly struct CardInstanceKey : IEquatable<CardInstanceKey>
    {
        public CardInstanceKey(
            ulong runtimeId,
            uint definitionCode,
            byte owner,
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            RuntimeId = runtimeId;
            DefinitionCode = definitionCode;
            Owner = owner;
            Controller = controller;
            Location = location;
            Sequence = sequence;
            Position = position;
        }

        public ulong RuntimeId { get; }
        public uint DefinitionCode { get; }
        public byte Owner { get; }
        public byte Controller { get; }
        public byte Location { get; }
        public uint Sequence { get; }
        public uint Position { get; }
        public bool IsValid => RuntimeId != 0;

        public CardInstanceKey WithAddress(
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            return new CardInstanceKey(
                RuntimeId,
                DefinitionCode,
                Owner,
                controller,
                location,
                sequence,
                position);
        }

        public bool Equals(CardInstanceKey other)
        {
            return RuntimeId == other.RuntimeId;
        }

        public override bool Equals(object obj)
        {
            return obj is CardInstanceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return RuntimeId.GetHashCode();
        }

        public override string ToString()
        {
            return
                $"{DefinitionCode:00000000}#{RuntimeId} " +
                $"P{Controller} L{Location:X2} S{Sequence} P{Position:X2}";
        }

        public static bool operator ==(
            CardInstanceKey left,
            CardInstanceKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            CardInstanceKey left,
            CardInstanceKey right)
        {
            return !left.Equals(right);
        }
    }

    public sealed class CardInstanceState
    {
        private readonly Dictionary<ushort, uint> counters = new();
        private readonly HashSet<ulong> targetRuntimeIds = new();
        private readonly HashSet<ulong> relationRuntimeIds = new();
        private readonly Dictionary<byte, ulong> hints = new();

        internal CardInstanceState(
            ulong runtimeId,
            uint definitionCode,
            byte owner,
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            RuntimeId = runtimeId;
            DefinitionCode = definitionCode;
            Owner = owner;
            UpdateAddress(controller, location, sequence, position);
        }

        public ulong RuntimeId { get; }
        public uint DefinitionCode { get; internal set; }
        /// <summary>
        /// True when the Core deliberately hid which previously known card
        /// now occupies this facedown field address (MSG_SHUFFLE_SET_CARD).
        /// Authoritative repair must not undo that privacy boundary.
        /// </summary>
        public bool IdentityOpaque { get; internal set; }
        public byte Owner { get; }
        public byte Controller { get; private set; }
        public byte Location { get; private set; }
        public uint Sequence { get; private set; }
        public uint Position { get; private set; }
        public uint CoreStatus { get; internal set; }
        public bool IsPublic { get; internal set; }
        public uint LinkRating { get; internal set; }
        public uint LinkMarkers { get; internal set; }
        public ulong EquippedToRuntimeId { get; internal set; }
        public bool IsTemporaryTarget { get; internal set; }
        public IReadOnlyDictionary<ushort, uint> Counters => counters;
        public IReadOnlyCollection<ulong> TargetRuntimeIds =>
            targetRuntimeIds;
        public IReadOnlyCollection<ulong> RelationRuntimeIds =>
            relationRuntimeIds;
        public IReadOnlyDictionary<byte, ulong> Hints => hints;
        public CardInstanceKey Key => new CardInstanceKey(
            RuntimeId,
            DefinitionCode,
            Owner,
            Controller,
            Location,
            Sequence,
            Position);

        internal void UpdateAddress(
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            Controller = controller;
            Location = location;
            Sequence = sequence;
            Position = position;
        }

        internal void SetCounter(ushort type, uint amount)
        {
            if (amount == 0)
                counters.Remove(type);
            else
                counters[type] = amount;
        }

        internal void ReplaceCounters(
            IEnumerable<KeyValuePair<ushort, uint>> values)
        {
            counters.Clear();
            foreach (KeyValuePair<ushort, uint> item in
                     values ?? Array.Empty<KeyValuePair<ushort, uint>>())
            {
                SetCounter(item.Key, item.Value);
            }
        }

        internal void AddCounter(ushort type, uint amount)
        {
            if (amount == 0)
                return;
            counters.TryGetValue(type, out uint current);
            counters[type] = checked(current + amount);
        }

        internal void RemoveCounter(ushort type, uint amount)
        {
            if (!counters.TryGetValue(type, out uint current))
                return;
            SetCounter(type, amount >= current ? 0 : current - amount);
        }

        internal void AddTarget(ulong runtimeId)
        {
            if (runtimeId != 0)
                targetRuntimeIds.Add(runtimeId);
        }

        internal void RemoveTarget(ulong runtimeId)
        {
            if (runtimeId != 0)
                targetRuntimeIds.Remove(runtimeId);
        }

        internal void ReplaceTargets(IEnumerable<ulong> values)
        {
            targetRuntimeIds.Clear();
            foreach (ulong value in values ?? Array.Empty<ulong>())
                AddTarget(value);
        }

        internal void AddRelation(ulong runtimeId)
        {
            if (runtimeId != 0)
                relationRuntimeIds.Add(runtimeId);
        }

        internal void RemoveRelation(ulong runtimeId)
        {
            if (runtimeId != 0)
                relationRuntimeIds.Remove(runtimeId);
        }

        internal void SetHint(byte type, ulong value)
        {
            if (value == 0)
                hints.Remove(type);
            else
                hints[type] = value;
        }

        internal void RestorePresentationMetadata(
            IEnumerable<KeyValuePair<ushort, uint>> restoredCounters,
            ulong equippedToRuntimeId,
            IEnumerable<ulong> restoredTargets,
            IEnumerable<ulong> restoredRelations,
            IEnumerable<KeyValuePair<byte, ulong>> restoredHints,
            bool isTemporaryTarget)
        {
            ReplaceCounters(restoredCounters);
            EquippedToRuntimeId = equippedToRuntimeId;
            targetRuntimeIds.Clear();
            foreach (ulong target in restoredTargets ?? Array.Empty<ulong>())
                AddTarget(target);
            relationRuntimeIds.Clear();
            foreach (ulong relation in
                     restoredRelations ?? Array.Empty<ulong>())
            {
                AddRelation(relation);
            }
            hints.Clear();
            foreach (KeyValuePair<byte, ulong> item in
                     restoredHints ??
                     Array.Empty<KeyValuePair<byte, ulong>>())
            {
                SetHint(item.Key, item.Value);
            }
            IsTemporaryTarget = isTemporaryTarget;
        }
    }
}
