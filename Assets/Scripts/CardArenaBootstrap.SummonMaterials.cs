using System.Collections.Generic;
using ArcaneArena.Cards;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneArena
{
    public sealed partial class CardArenaBootstrap
    {
        private const uint MoveReasonMaterial = 0x00000008U;
        private const uint MoveReasonFusion = 0x00040000U;
        private const uint MoveReasonSynchro = 0x00080000U;
        private const uint MoveReasonXyz = 0x00200000U;
        private const uint MoveReasonLink = 0x10000000U;
        private const uint PendulumCardType = 0x01000000U;
        private const ulong SummonMaterialEventWindow = 36UL;

        private sealed class SummonMaterialPresentationRecord
        {
            public uint Code;
            public MonsterFrameKind Frame;
            public ulong EventSequence;
        }

        private readonly List<SummonMaterialPresentationRecord>
            recentSummonMaterials = new();
        private ulong summonMaterialEventSequence;

        private void TrackSummonMaterialPresentation(DuelEvent duelEvent)
        {
            summonMaterialEventSequence++;
            PurgeStaleSummonMaterials();
            if (duelEvent?.Message != CoreMessage.Move ||
                duelEvent.Code == 0 ||
                (duelEvent.Value & MoveReasonMaterial) == 0U)
            {
                return;
            }

            MonsterFrameKind frame = MaterialFrameForReason(duelEvent.Value);
            if (pendingSummonPresentation != null &&
                (frame == MonsterFrameKind.Unknown ||
                 pendingSummonPresentation.SummonFrame == frame))
            {
                pendingSummonPresentation.MaterialCodes.Add(duelEvent.Code);
                return;
            }

            recentSummonMaterials.Add(new SummonMaterialPresentationRecord
            {
                Code = duelEvent.Code,
                Frame = frame,
                EventSequence = summonMaterialEventSequence
            });
            if (recentSummonMaterials.Count > 24)
                recentSummonMaterials.RemoveAt(0);
        }

        private List<uint> ConsumeSummonMaterialCodes(
            MonsterFrameKind summonFrame,
            byte controller)
        {
            var result = new List<uint>();
            for (int index = recentSummonMaterials.Count - 1;
                 index >= 0;
                 index--)
            {
                SummonMaterialPresentationRecord record =
                    recentSummonMaterials[index];
                if (summonMaterialEventSequence - record.EventSequence >
                    SummonMaterialEventWindow)
                {
                    recentSummonMaterials.RemoveAt(index);
                    continue;
                }
                if (record.Frame != MonsterFrameKind.Unknown &&
                    record.Frame != summonFrame)
                {
                    continue;
                }
                result.Insert(0, record.Code);
                recentSummonMaterials.RemoveAt(index);
            }

            if (summonFrame == MonsterFrameKind.Pendulum &&
                result.Count == 0)
            {
                AddPendulumScaleCards(result, controller);
            }
            return result;
        }

        private void AddPendulumScaleCards(
            ICollection<uint> destination,
            byte controller)
        {
            if (destination == null || state?.Players == null ||
                controller >= state.Players.Length)
            {
                return;
            }
            uint[] zones = state.Players[controller].SpellTrapZones;
            if (zones == null)
                return;
            for (int index = 0; index < zones.Length; index++)
            {
                uint code = zones[index];
                if (code == 0 || database == null ||
                    !database.TryGet(code, out CardRecord card) ||
                    (card.Type & PendulumCardType) == 0U)
                {
                    continue;
                }
                destination.Add(code);
                if (destination.Count >= 2)
                    return;
            }
        }

        private void PurgeStaleSummonMaterials()
        {
            for (int index = recentSummonMaterials.Count - 1;
                 index >= 0;
                 index--)
            {
                if (summonMaterialEventSequence -
                    recentSummonMaterials[index].EventSequence >
                    SummonMaterialEventWindow)
                {
                    recentSummonMaterials.RemoveAt(index);
                }
            }
        }

        private void ResetSummonMaterialPresentation()
        {
            recentSummonMaterials.Clear();
            summonMaterialEventSequence = 0UL;
        }

        private static MonsterFrameKind MaterialFrameForReason(uint reason)
        {
            if ((reason & MoveReasonFusion) != 0U)
                return MonsterFrameKind.Fusion;
            if ((reason & MoveReasonSynchro) != 0U)
                return MonsterFrameKind.Synchro;
            if ((reason & MoveReasonXyz) != 0U)
                return MonsterFrameKind.Xyz;
            if ((reason & MoveReasonLink) != 0U)
                return MonsterFrameKind.Link;
            return MonsterFrameKind.Unknown;
        }
    }
}
