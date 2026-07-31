using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;

namespace ArcaneDuel.DuelEngine.Protocol
{
    internal static class AnnounceCardFilter
    {
        private const ulong Add = 0x4000000000000000;
        private const ulong Subtract = 0x4000000100000000;
        private const ulong Multiply = 0x4000000200000000;
        private const ulong Divide = 0x4000000300000000;
        private const ulong And = 0x4000000400000000;
        private const ulong Or = 0x4000000500000000;
        private const ulong Negate = 0x4000000600000000;
        private const ulong Not = 0x4000000700000000;
        private const ulong BitAnd = 0x4000000800000000;
        private const ulong BitOr = 0x4000000900000000;
        private const ulong BitNot = 0x4000001000000000;
        private const ulong BitXor = 0x4000001100000000;
        private const ulong ShiftLeft = 0x4000001200000000;
        private const ulong ShiftRight = 0x4000001300000000;
        private const ulong AllowAliases = 0x4000001400000000;
        private const ulong AllowTokens = 0x4000001500000000;
        private const ulong IsCode = 0x4000010000000000;
        private const ulong IsSetCard = 0x4000010100000000;
        private const ulong IsType = 0x4000010200000000;
        private const ulong IsRace = 0x4000010300000000;
        private const ulong IsAttribute = 0x4000010400000000;
        private const ulong GetCardCode = 0x4000010500000000;
        private const ulong GetCardType = 0x4000010700000000;
        private const ulong GetCardRace = 0x4000010800000000;
        private const ulong GetCardAttribute = 0x4000010900000000;
        private const uint Monster = 0x1;
        private const uint Token = 0x4000;

        internal static bool IsDeclarable(
            CardRecord card,
            IReadOnlyList<ulong> opcodes)
        {
            if (card == null || opcodes == null)
                return false;

            var stack = new Stack<long>();
            bool allowAliases = false;
            bool allowTokens = false;
            foreach (ulong opcode in opcodes)
            {
                switch (opcode)
                {
                    case Add: if (!Binary(stack, (left, right) => left + right)) return false; break;
                    case Subtract: if (!Binary(stack, (left, right) => left - right)) return false; break;
                    case Multiply: if (!Binary(stack, (left, right) => left * right)) return false; break;
                    case Divide:
                        if (!Binary(stack, (left, right) => right == 0 ? null : left / right)) return false;
                        break;
                    case And: if (!Binary(stack, (left, right) => Bool(left != 0 && right != 0))) return false; break;
                    case Or: if (!Binary(stack, (left, right) => Bool(left != 0 || right != 0))) return false; break;
                    case BitAnd: if (!Binary(stack, (left, right) => left & right)) return false; break;
                    case BitOr: if (!Binary(stack, (left, right) => left | right)) return false; break;
                    case BitXor: if (!Binary(stack, (left, right) => left ^ right)) return false; break;
                    case ShiftLeft: if (!Binary(stack, (left, right) => left << (int)right)) return false; break;
                    case ShiftRight: if (!Binary(stack, (left, right) => left >> (int)right)) return false; break;
                    case Negate: if (!Unary(stack, value => -value)) return false; break;
                    case Not: if (!Unary(stack, value => Bool(value == 0))) return false; break;
                    case BitNot: if (!Unary(stack, value => ~value)) return false; break;
                    case IsCode:
                        if (!Unary(stack, value => Bool(card.Code == unchecked((uint)value)))) return false;
                        break;
                    case IsSetCard:
                        if (!Unary(stack, value => Bool(HasSetCode(card, unchecked((int)value))))) return false;
                        break;
                    case IsType:
                        if (!Unary(stack, value => Bool((card.Type & unchecked((uint)value)) != 0))) return false;
                        break;
                    case IsRace:
                        if (!Unary(stack, value => Bool((card.Race & unchecked((ulong)value)) != 0))) return false;
                        break;
                    case IsAttribute:
                        if (!Unary(stack, value => Bool((card.Attribute & unchecked((uint)value)) != 0))) return false;
                        break;
                    case GetCardCode: stack.Push(card.Code); break;
                    case GetCardType: stack.Push(card.Type); break;
                    case GetCardRace: stack.Push(unchecked((long)card.Race)); break;
                    case GetCardAttribute: stack.Push(card.Attribute); break;
                    case AllowAliases: allowAliases = true; break;
                    case AllowTokens: allowTokens = true; break;
                    default: stack.Push(unchecked((long)opcode)); break;
                }
            }

            if (stack.Count != 1 || stack.Pop() == 0)
                return false;
            bool specialName = card.Code == 78734254 || card.Code == 13857930;
            bool validAlias = allowAliases || card.Alias == 0;
            bool validToken = allowTokens ||
                              (card.Type & (Monster | Token)) !=
                              (Monster | Token);
            return specialName || (validAlias && validToken);
        }

        internal static IEnumerable<uint> LiteralCardCodes(
            IReadOnlyList<ulong> opcodes)
        {
            if (opcodes == null)
                return Array.Empty<uint>();
            return opcodes
                .Select((value, index) => new { value, index })
                .Where(item => item.value == IsCode && item.index > 0)
                .Select(item => opcodes[item.index - 1])
                .Where(value => value <= uint.MaxValue)
                .Select(value => (uint)value)
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
        }

        private static bool Unary(
            Stack<long> stack,
            Func<long, long> operation)
        {
            if (stack.Count < 1)
                return false;
            stack.Push(operation(stack.Pop()));
            return true;
        }

        private static bool Binary(
            Stack<long> stack,
            Func<long, long, long?> operation)
        {
            if (stack.Count < 2)
                return false;
            long right = stack.Pop();
            long left = stack.Pop();
            long? result = operation(left, right);
            if (!result.HasValue)
                return false;
            stack.Push(result.Value);
            return true;
        }

        private static long Bool(bool value) => value ? 1L : 0L;

        private static bool HasSetCode(CardRecord card, int requested)
        {
            ushort type = (ushort)(requested & 0x0FFF);
            ushort subtype = (ushort)(requested & 0xF000);
            return card.Setcodes != null && card.Setcodes.Any(setcode =>
                (setcode & 0x0FFF) == type &&
                (setcode & 0xF000 & subtype) == subtype);
        }
    }
}
