using System;

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
    }
}
