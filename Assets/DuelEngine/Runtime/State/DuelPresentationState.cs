using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneDuel.DuelEngine.State
{
    public sealed class DuelistState
    {
        public DuelistState()
        {
            for (int index = 0; index < OverlayInstances.Length; index++)
                OverlayInstances[index] = new List<CardInstanceState>();
        }

        public int LifePoints { get; internal set; } = 8000;
        public int DeckCount { get; internal set; } = 40;
        public int ExtraDeckCount { get; internal set; } = 3;
        public List<uint> Hand { get; } = new List<uint>();
        public List<CardInstanceState> HandInstances { get; } =
            new List<CardInstanceState>();
        public uint[] MonsterZones { get; } = new uint[7];
        public uint[] MonsterPositions { get; } = new uint[7];
        public CardInstanceState[] MonsterInstances { get; } =
            new CardInstanceState[7];
        public List<CardInstanceState>[] OverlayInstances { get; } =
            new List<CardInstanceState>[7];
        public uint[] SpellTrapZones { get; } = new uint[8];
        public uint[] SpellTrapPositions { get; } = new uint[8];
        public CardInstanceState[] SpellTrapInstances { get; } =
            new CardInstanceState[8];
        public List<uint> Graveyard { get; } = new List<uint>();
        public List<CardInstanceState> GraveyardInstances { get; } =
            new List<CardInstanceState>();
        public List<uint> Banished { get; } = new List<uint>();
        public List<CardInstanceState> BanishedInstances { get; } =
            new List<CardInstanceState>();
    }

    public sealed class DuelistPresentationSnapshot
    {
        public int LifePoints { get; internal set; }
        public int DeckCount { get; internal set; }
        public int ExtraDeckCount { get; internal set; }
        public uint[] Hand { get; internal set; }
        public uint[] MonsterZones { get; internal set; }
        public uint[] MonsterPositions { get; internal set; }
        public uint[] SpellTrapZones { get; internal set; }
        public uint[] SpellTrapPositions { get; internal set; }
        public uint[] Graveyard { get; internal set; }
        public uint[] Banished { get; internal set; }
        public uint[][] OverlayMaterials { get; internal set; }
    }

    public sealed class DuelPresentationSnapshot
    {
        public DuelistPresentationSnapshot[] Players { get; internal set; }
        public int TurnNumber { get; internal set; }
        public byte TurnPlayer { get; internal set; }
        public uint Phase { get; internal set; }
        public byte? Winner { get; internal set; }
        public string[] Log { get; internal set; }
    }

    public sealed class DuelPresentationState
    {
        private readonly CardDatabase database;
        private ulong nextRuntimeId = 1;

        public DuelistState[] Players { get; } = { new DuelistState(), new DuelistState() };
        public int TurnNumber { get; private set; }
        public byte TurnPlayer { get; private set; }
        public uint Phase { get; private set; }
        public byte? Winner { get; private set; }
        public DuelPrompt Prompt { get; private set; }
        public List<string> Log { get; } = new List<string>();

        public DuelPresentationState(CardDatabase database)
        {
            this.database = database;
        }

        public void ConfigureDeckCounts(
            int playerMain,
            int playerExtra,
            int opponentMain,
            int opponentExtra)
        {
            Players[0].DeckCount = Math.Max(0, playerMain);
            Players[0].ExtraDeckCount = Math.Max(0, playerExtra);
            Players[1].DeckCount = Math.Max(0, opponentMain);
            Players[1].ExtraDeckCount = Math.Max(0, opponentExtra);
        }

        public DuelPresentationSnapshot CaptureSnapshot()
        {
            return new DuelPresentationSnapshot
            {
                Players = new[]
                {
                    Capture(Players[0]),
                    Capture(Players[1])
                },
                TurnNumber = TurnNumber,
                TurnPlayer = TurnPlayer,
                Phase = Phase,
                Winner = Winner,
                Log = Log.ToArray()
            };
        }

        public void RestoreSnapshot(DuelPresentationSnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.Players == null ||
                snapshot.Players.Length != 2)
            {
                throw new ArgumentException(
                    "A presentation snapshot must contain two duelists.",
                    nameof(snapshot));
            }
            Restore(Players[0], snapshot.Players[0], 0);
            Restore(Players[1], snapshot.Players[1], 1);
            TurnNumber = snapshot.TurnNumber;
            TurnPlayer = snapshot.TurnPlayer;
            Phase = snapshot.Phase;
            Winner = snapshot.Winner;
            Prompt = null;
            Log.Clear();
            if (snapshot.Log != null) Log.AddRange(snapshot.Log);
        }

        public void Apply(DuelEvent duelEvent)
        {
            if (duelEvent.Prompt != null) Prompt = duelEvent.Prompt;
            switch (duelEvent.Message)
            {
                case CoreMessage.Start:
                    Players[0].LifePoints = Players[1].LifePoints = (int)duelEvent.Value;
                    AddLog("O duelo começou. Regras Mestre 5 ativas.");
                    break;
                case CoreMessage.NewTurn:
                    TurnNumber++;
                    TurnPlayer = duelEvent.Player;
                    AddLog($"Turno {TurnNumber} · Duelista {TurnPlayer + 1}");
                    break;
                case CoreMessage.NewPhase:
                    Phase = duelEvent.Value;
                    AddLog(CoreMessageDecoder.PhaseName(Phase));
                    break;
                case CoreMessage.Draw:
                    ApplyDraw(duelEvent);
                    break;
                case CoreMessage.ShuffleHand:
                    ApplyShuffleHand(duelEvent);
                    break;
                case CoreMessage.Move:
                    ApplyMove(duelEvent);
                    break;
                case CoreMessage.PositionChange:
                    ApplyPositionChange(duelEvent);
                    break;
                case CoreMessage.Damage:
                    Players[duelEvent.Player].LifePoints -= (int)duelEvent.Value;
                    AddLog($"Duelista {duelEvent.Player + 1} sofreu {duelEvent.Value} de dano.");
                    break;
                case CoreMessage.Recover:
                    Players[duelEvent.Player].LifePoints += (int)duelEvent.Value;
                    AddLog($"Duelista {duelEvent.Player + 1} recuperou {duelEvent.Value} PV.");
                    break;
                case CoreMessage.LifePointsUpdate:
                    Players[duelEvent.Player].LifePoints = (int)duelEvent.Value;
                    break;
                case CoreMessage.Summoning:
                case CoreMessage.SpecialSummoning:
                case CoreMessage.FlipSummoning:
                    AddLog($"{Name(duelEvent.Code)} está sendo invocado.");
                    break;
                case CoreMessage.Chaining:
                    AddLog($"Corrente {duelEvent.Value}: {Name(duelEvent.Code)}.");
                    break;
                case CoreMessage.Attack:
                    AddLog(
                        duelEvent.DirectAttack
                            ? $"Duelista {duelEvent.Player + 1} declarou um ataque direto."
                            : $"Duelista {duelEvent.Player + 1} declarou um ataque.");
                    break;
                case CoreMessage.Battle:
                    AddLog(
                        duelEvent.DirectAttack
                            ? $"Ataque direto com {duelEvent.AttackerAttack} ATK."
                            : $"Batalha: {duelEvent.AttackerAttack} ATK contra " +
                              $"{Math.Max(duelEvent.TargetAttack, duelEvent.TargetDefense)}.");
                    break;
                case CoreMessage.Win:
                    Winner = duelEvent.Player;
                    Prompt = null;
                    AddLog($"Duelista {duelEvent.Player + 1} venceu o duelo.");
                    break;
                case CoreMessage.Retry:
                    AddLog("Resposta inválida; escolha novamente.");
                    break;
                default:
                    // Unknown packets remain explicit in the engine history and
                    // diagnostics, but are not exposed as technical noise in the
                    // player-facing duel timeline.
                    break;
            }
        }

        public void ClearPrompt()
        {
            Prompt = null;
        }

        private void ApplyDraw(DuelEvent duelEvent)
        {
            DuelistState player = Players[duelEvent.Player];
            foreach (uint code in duelEvent.Codes ?? Array.Empty<uint>())
            {
                uint sequence = (uint)player.Hand.Count;
                player.Hand.Add(code);
                player.HandInstances.Add(
                    CreateInstance(
                        code,
                        duelEvent.Player,
                        duelEvent.Player,
                        (byte)DuelLocation.Hand,
                        sequence,
                        0));
                player.DeckCount = Math.Max(0, player.DeckCount - 1);
            }
            AddLog($"Duelista {duelEvent.Player + 1} comprou {(duelEvent.Codes ?? Array.Empty<uint>()).Length} carta(s).");
        }

        private void ApplyShuffleHand(DuelEvent duelEvent)
        {
            if (duelEvent.Player >= Players.Length)
                return;
            DuelistState player = Players[duelEvent.Player];
            uint[] shuffledCodes = duelEvent.Codes ?? Array.Empty<uint>();
            if (shuffledCodes.Length != player.Hand.Count)
            {
                AddLog(
                    $"A reordenação da mão do Duelista " +
                    $"{duelEvent.Player + 1} foi ignorada por divergência " +
                    "de quantidade.");
                return;
            }

            var instancesByCode =
                new Dictionary<uint, Queue<CardInstanceState>>();
            for (int index = 0; index < player.Hand.Count; index++)
            {
                uint code = player.Hand[index];
                if (!instancesByCode.TryGetValue(
                        code,
                        out Queue<CardInstanceState> instances))
                {
                    instances = new Queue<CardInstanceState>();
                    instancesByCode.Add(code, instances);
                }
                CardInstanceState instance =
                    index < player.HandInstances.Count
                        ? player.HandInstances[index]
                        : null;
                instances.Enqueue(instance);
            }

            var reordered = new List<CardInstanceState>(
                shuffledCodes.Length);
            foreach (uint code in shuffledCodes)
            {
                if (!instancesByCode.TryGetValue(
                        code,
                        out Queue<CardInstanceState> instances) ||
                    instances.Count == 0)
                {
                    AddLog(
                        $"A reordenação da mão do Duelista " +
                        $"{duelEvent.Player + 1} foi ignorada porque o " +
                        $"Core informou uma cópia inesperada de {code:00000000}.");
                    return;
                }
                reordered.Add(
                    instances.Dequeue() ??
                    CreateInstance(
                        code,
                        duelEvent.Player,
                        duelEvent.Player,
                        (byte)DuelLocation.Hand,
                        (uint)reordered.Count,
                        0));
            }

            player.Hand.Clear();
            player.Hand.AddRange(shuffledCodes);
            player.HandInstances.Clear();
            player.HandInstances.AddRange(reordered);
            Reindex(
                player.HandInstances,
                duelEvent.Player,
                (byte)DuelLocation.Hand);
            AddLog(
                $"Mão do Duelista {duelEvent.Player + 1} reordenada pelo Core.");
        }

        private void ApplyMove(DuelEvent duelEvent)
        {
            CardInstanceState moving = null;
            if (duelEvent.Previous != null && duelEvent.Previous.Location != 0)
            {
                moving = Remove(duelEvent.Previous, duelEvent.Code);
            }
            if (duelEvent.Current != null && duelEvent.Current.Location != 0)
            {
                Add(duelEvent.Current, duelEvent.Code, moving);
            }
            if (duelEvent.Previous != null && duelEvent.Current != null &&
                duelEvent.Previous.Location != duelEvent.Current.Location)
            {
                AddLog($"{Name(duelEvent.Code)} → {LocationName(duelEvent.Current.Location)}");
            }
        }

        private CardInstanceState Remove(CardLocation location, uint code)
        {
            if (location.Controller >= Players.Length)
                return null;
            DuelistState player = Players[location.Controller];
            if ((location.Location & DuelLocation.Overlay) != 0)
            {
                return RemoveOverlay(
                    player,
                    code,
                    location.Sequence,
                    location.Position);
            }
            if ((location.Location & DuelLocation.Hand) != 0)
            {
                return RemoveListCard(
                    player.Hand,
                    player.HandInstances,
                    code,
                    location.Sequence,
                    location.Controller,
                    (byte)DuelLocation.Hand);
            }
            else if ((location.Location & DuelLocation.MonsterZone) != 0)
            {
                CardInstanceState instance =
                    TakeZone(player.MonsterInstances, location.Sequence);
                SetZone(player.MonsterZones, location.Sequence, 0);
                SetZone(player.MonsterPositions, location.Sequence, 0);
                return instance;
            }
            else if ((location.Location & DuelLocation.SpellTrapZone) != 0)
            {
                CardInstanceState instance =
                    TakeZone(player.SpellTrapInstances, location.Sequence);
                SetZone(player.SpellTrapZones, location.Sequence, 0);
                SetZone(player.SpellTrapPositions, location.Sequence, 0);
                return instance;
            }
            else if ((location.Location & DuelLocation.Graveyard) != 0)
            {
                return RemoveListCard(
                    player.Graveyard,
                    player.GraveyardInstances,
                    code,
                    location.Sequence,
                    location.Controller,
                    (byte)DuelLocation.Graveyard);
            }
            else if ((location.Location & DuelLocation.Banished) != 0)
            {
                return RemoveListCard(
                    player.Banished,
                    player.BanishedInstances,
                    code,
                    location.Sequence,
                    location.Controller,
                    (byte)DuelLocation.Banished);
            }
            return null;
        }

        private void Add(
            CardLocation location,
            uint code,
            CardInstanceState instance)
        {
            if (location.Controller >= Players.Length)
                return;
            DuelistState player = Players[location.Controller];
            instance ??= CreateInstance(
                code,
                location.Controller,
                location.Controller,
                location.Location,
                location.Sequence,
                location.Position);
            if (code != 0)
                instance.DefinitionCode = code;
            instance.UpdateAddress(
                location.Controller,
                location.Location,
                location.Sequence,
                location.Position);
            if ((location.Location & DuelLocation.Overlay) != 0)
            {
                AddOverlay(player, instance, location);
            }
            else if ((location.Location & DuelLocation.Hand) != 0)
            {
                int requestedIndex = location.Sequence > int.MaxValue
                    ? player.Hand.Count
                    : (int)location.Sequence;
                int index = Math.Min(
                    player.Hand.Count,
                    requestedIndex);
                player.Hand.Insert(index, code);
                player.HandInstances.Insert(index, instance);
                Reindex(
                    player.HandInstances,
                    location.Controller,
                    (byte)DuelLocation.Hand);
            }
            else if ((location.Location & DuelLocation.MonsterZone) != 0)
            {
                SetZone(player.MonsterZones, location.Sequence, code);
                SetZone(
                    player.MonsterPositions,
                    location.Sequence,
                    location.Position);
                SetZoneInstance(
                    player.MonsterInstances,
                    location.Sequence,
                    instance);
            }
            else if ((location.Location & DuelLocation.SpellTrapZone) != 0)
            {
                SetZone(player.SpellTrapZones, location.Sequence, code);
                SetZone(
                    player.SpellTrapPositions,
                    location.Sequence,
                    location.Position);
                SetZoneInstance(
                    player.SpellTrapInstances,
                    location.Sequence,
                    instance);
            }
            else if ((location.Location & DuelLocation.Graveyard) != 0)
            {
                player.Graveyard.Add(code);
                player.GraveyardInstances.Add(instance);
                Reindex(
                    player.GraveyardInstances,
                    location.Controller,
                    (byte)DuelLocation.Graveyard);
            }
            else if ((location.Location & DuelLocation.Banished) != 0)
            {
                player.Banished.Add(code);
                player.BanishedInstances.Add(instance);
                Reindex(
                    player.BanishedInstances,
                    location.Controller,
                    (byte)DuelLocation.Banished);
            }
        }

        private static void SetZone(uint[] zones, uint sequence, uint value)
        {
            if (sequence < zones.Length) zones[sequence] = value;
        }

        private void ApplyPositionChange(DuelEvent duelEvent)
        {
            CardLocation current = duelEvent.Current;
            if (current == null ||
                current.Controller >= Players.Length)
            {
                return;
            }
            DuelistState player = Players[current.Controller];
            if ((current.Location & DuelLocation.MonsterZone) != 0)
            {
                SetZone(
                    player.MonsterPositions,
                    current.Sequence,
                    current.Position);
                UpdateZonePosition(
                    player.MonsterInstances,
                    current.Sequence,
                    current.Position);
            }
            else if ((current.Location & DuelLocation.SpellTrapZone) != 0)
            {
                SetZone(
                    player.SpellTrapPositions,
                    current.Sequence,
                    current.Position);
                UpdateZonePosition(
                    player.SpellTrapInstances,
                    current.Sequence,
                    current.Position);
            }
        }

        public CardInstanceState InstanceAt(
            byte controller,
            byte location,
            uint sequence)
        {
            if (controller >= Players.Length)
                return null;
            DuelistState player = Players[controller];
            if ((location & DuelLocation.Overlay) != 0)
            {
                if (sequence >= player.OverlayInstances.Length)
                    return null;
                List<CardInstanceState> materials =
                    player.OverlayInstances[sequence];
                return materials.Count > 0
                    ? materials[materials.Count - 1]
                    : null;
            }
            if ((location & DuelLocation.Hand) != 0)
                return sequence < player.HandInstances.Count
                    ? player.HandInstances[(int)sequence]
                    : null;
            if ((location & DuelLocation.MonsterZone) != 0)
                return sequence < player.MonsterInstances.Length
                    ? player.MonsterInstances[sequence]
                    : null;
            if ((location & DuelLocation.SpellTrapZone) != 0)
                return sequence < player.SpellTrapInstances.Length
                    ? player.SpellTrapInstances[sequence]
                    : null;
            if ((location & DuelLocation.Graveyard) != 0)
                return sequence < player.GraveyardInstances.Count
                    ? player.GraveyardInstances[(int)sequence]
                    : null;
            if ((location & DuelLocation.Banished) != 0)
                return sequence < player.BanishedInstances.Count
                    ? player.BanishedInstances[(int)sequence]
                    : null;
            return null;
        }

        public string[] ValidateInstanceConsistency()
        {
            var problems = new List<string>();
            for (byte controller = 0; controller < Players.Length; controller++)
            {
                DuelistState player = Players[controller];
                ValidateList(
                    problems,
                    controller,
                    "hand",
                    player.Hand,
                    player.HandInstances);
                ValidateList(
                    problems,
                    controller,
                    "graveyard",
                    player.Graveyard,
                    player.GraveyardInstances);
                ValidateList(
                    problems,
                    controller,
                    "banished",
                    player.Banished,
                    player.BanishedInstances);
                ValidateZones(
                    problems,
                    controller,
                    "monster",
                    player.MonsterZones,
                    player.MonsterInstances);
                ValidateZones(
                    problems,
                    controller,
                    "spell/trap",
                    player.SpellTrapZones,
                    player.SpellTrapInstances);
                for (int zone = 0;
                     zone < player.OverlayInstances.Length;
                     zone++)
                {
                    foreach (CardInstanceState material in
                             player.OverlayInstances[zone])
                    {
                        if (material == null ||
                            (material.Location & DuelLocation.Overlay) == 0 ||
                            material.Sequence != zone)
                        {
                            problems.Add(
                                $"P{controller} overlay[{zone}] contains " +
                                "an invalid material binding.");
                        }
                    }
                }
            }
            return problems.ToArray();
        }

        private CardInstanceState CreateInstance(
            uint code,
            byte owner,
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            return new CardInstanceState(
                nextRuntimeId++,
                code,
                owner,
                controller,
                location,
                sequence,
                position);
        }

        private static CardInstanceState RemoveListCard(
            List<uint> cards,
            List<CardInstanceState> instances,
            uint code,
            uint sequence,
            byte controller,
            byte location)
        {
            int index = -1;
            if (sequence < cards.Count &&
                (code == 0 || cards[(int)sequence] == code))
            {
                index = (int)sequence;
            }
            if (index < 0 && code != 0)
                index = cards.IndexOf(code);
            if (index < 0 || index >= cards.Count)
                return null;

            cards.RemoveAt(index);
            CardInstanceState instance = index < instances.Count
                ? instances[index]
                : null;
            if (index < instances.Count)
                instances.RemoveAt(index);
            Reindex(instances, controller, location);
            return instance;
        }

        private static CardInstanceState TakeZone(
            CardInstanceState[] instances,
            uint sequence)
        {
            if (sequence >= instances.Length)
                return null;
            CardInstanceState result = instances[sequence];
            instances[sequence] = null;
            return result;
        }

        private static CardInstanceState RemoveOverlay(
            DuelistState player,
            uint code,
            uint hostSequence,
            uint overlaySequence)
        {
            if (hostSequence >= player.OverlayInstances.Length)
                return null;
            List<CardInstanceState> materials =
                player.OverlayInstances[hostSequence];
            int index = overlaySequence < materials.Count &&
                        (code == 0 ||
                         materials[(int)overlaySequence]
                             .DefinitionCode == code)
                ? (int)overlaySequence
                : materials.FindIndex(material =>
                    material != null &&
                    material.DefinitionCode == code);
            if (index < 0)
                return null;
            CardInstanceState result = materials[index];
            materials.RemoveAt(index);
            for (int materialIndex = 0;
                 materialIndex < materials.Count;
                 materialIndex++)
            {
                CardInstanceState material = materials[materialIndex];
                material?.UpdateAddress(
                    material.Controller,
                    (byte)DuelLocation.Overlay,
                    hostSequence,
                    (uint)materialIndex);
            }
            return result;
        }

        private static void AddOverlay(
            DuelistState player,
            CardInstanceState instance,
            CardLocation location)
        {
            if (location.Sequence >= player.OverlayInstances.Length)
                return;
            List<CardInstanceState> materials =
                player.OverlayInstances[location.Sequence];
            int index = location.Position <= materials.Count
                ? (int)location.Position
                : materials.Count;
            materials.Insert(index, instance);
            for (int materialIndex = 0;
                 materialIndex < materials.Count;
                 materialIndex++)
            {
                CardInstanceState material = materials[materialIndex];
                material?.UpdateAddress(
                    location.Controller,
                    (byte)DuelLocation.Overlay,
                    location.Sequence,
                    (uint)materialIndex);
            }
        }

        private static void SetZoneInstance(
            CardInstanceState[] instances,
            uint sequence,
            CardInstanceState instance)
        {
            if (sequence < instances.Length)
                instances[sequence] = instance;
        }

        private static void UpdateZonePosition(
            CardInstanceState[] instances,
            uint sequence,
            uint position)
        {
            if (sequence >= instances.Length ||
                instances[sequence] == null)
            {
                return;
            }
            CardInstanceState instance = instances[sequence];
            instance.UpdateAddress(
                instance.Controller,
                instance.Location,
                sequence,
                position);
        }

        private static void Reindex(
            List<CardInstanceState> instances,
            byte controller,
            byte location)
        {
            for (int index = 0; index < instances.Count; index++)
            {
                CardInstanceState instance = instances[index];
                instance?.UpdateAddress(
                    controller,
                    location,
                    (uint)index,
                    instance.Position);
            }
        }

        private static void ValidateList(
            ICollection<string> problems,
            byte controller,
            string label,
            IReadOnlyList<uint> codes,
            IReadOnlyList<CardInstanceState> instances)
        {
            if (codes.Count != instances.Count)
                problems.Add(
                    $"P{controller} {label}: {codes.Count} codes != " +
                    $"{instances.Count} instances.");
            int count = Math.Min(codes.Count, instances.Count);
            for (int index = 0; index < count; index++)
            {
                if (instances[index] == null ||
                    instances[index].DefinitionCode != codes[index])
                {
                    problems.Add(
                        $"P{controller} {label}[{index}] is not bound " +
                        $"to definition {codes[index]:00000000}.");
                }
            }
        }

        private static void ValidateZones(
            ICollection<string> problems,
            byte controller,
            string label,
            IReadOnlyList<uint> codes,
            IReadOnlyList<CardInstanceState> instances)
        {
            int count = Math.Min(codes.Count, instances.Count);
            for (int index = 0; index < count; index++)
            {
                bool occupied = codes[index] != 0;
                CardInstanceState instance = instances[index];
                if (occupied != (instance != null) ||
                    occupied && instance.DefinitionCode != codes[index])
                {
                    problems.Add(
                        $"P{controller} {label}[{index}] presentation " +
                        "instance does not match the authoritative code.");
                }
            }
        }

        private string Name(uint code)
        {
            return code != 0 && database.TryGet(code, out CardRecord card) ? card.Name : "Carta oculta";
        }

        private static string LocationName(byte location)
        {
            if ((location & DuelLocation.Hand) != 0) return "Mão";
            if ((location & DuelLocation.MonsterZone) != 0) return "Zona de Monstro";
            if ((location & DuelLocation.SpellTrapZone) != 0) return "Zona de Magia/Armadilha";
            if ((location & DuelLocation.Graveyard) != 0) return "Cemitério";
            if ((location & DuelLocation.Banished) != 0) return "Banimento";
            if ((location & DuelLocation.Deck) != 0) return "Deck";
            if ((location & DuelLocation.Extra) != 0) return "Deck Adicional";
            return "Fora do campo";
        }

        private void AddLog(string entry)
        {
            Log.Add(entry);
            while (Log.Count > 14) Log.RemoveAt(0);
        }

        private static DuelistPresentationSnapshot Capture(DuelistState player)
        {
            return new DuelistPresentationSnapshot
            {
                LifePoints = player.LifePoints,
                DeckCount = player.DeckCount,
                ExtraDeckCount = player.ExtraDeckCount,
                Hand = player.Hand.ToArray(),
                MonsterZones = (uint[])player.MonsterZones.Clone(),
                MonsterPositions =
                    (uint[])player.MonsterPositions.Clone(),
                SpellTrapZones = (uint[])player.SpellTrapZones.Clone(),
                SpellTrapPositions =
                    (uint[])player.SpellTrapPositions.Clone(),
                Graveyard = player.Graveyard.ToArray(),
                Banished = player.Banished.ToArray(),
                OverlayMaterials = player.OverlayInstances
                    .Select(materials => materials
                        .Where(material => material != null)
                        .Select(material => material.DefinitionCode)
                        .ToArray())
                    .ToArray()
            };
        }

        private void Restore(
            DuelistState destination,
            DuelistPresentationSnapshot source,
            byte controller)
        {
            if (source == null)
            {
                throw new ArgumentException(
                    "A presentation snapshot contains a null duelist.");
            }
            destination.LifePoints = source.LifePoints;
            destination.DeckCount = source.DeckCount;
            destination.ExtraDeckCount = source.ExtraDeckCount;
            Replace(destination.Hand, source.Hand);
            Replace(destination.Graveyard, source.Graveyard);
            Replace(destination.Banished, source.Banished);
            Array.Clear(destination.MonsterZones, 0, destination.MonsterZones.Length);
            Array.Clear(
                destination.MonsterPositions,
                0,
                destination.MonsterPositions.Length);
            Array.Clear(destination.SpellTrapZones, 0, destination.SpellTrapZones.Length);
            Array.Clear(
                destination.SpellTrapPositions,
                0,
                destination.SpellTrapPositions.Length);
            if (source.MonsterZones != null)
            {
                Array.Copy(
                    source.MonsterZones,
                    destination.MonsterZones,
                    Math.Min(source.MonsterZones.Length, destination.MonsterZones.Length));
            }
            if (source.SpellTrapZones != null)
            {
                Array.Copy(
                    source.SpellTrapZones,
                    destination.SpellTrapZones,
                    Math.Min(source.SpellTrapZones.Length, destination.SpellTrapZones.Length));
            }
            if (source.MonsterPositions != null)
            {
                Array.Copy(
                    source.MonsterPositions,
                    destination.MonsterPositions,
                    Math.Min(
                        source.MonsterPositions.Length,
                        destination.MonsterPositions.Length));
            }
            if (source.SpellTrapPositions != null)
            {
                Array.Copy(
                    source.SpellTrapPositions,
                    destination.SpellTrapPositions,
                    Math.Min(
                        source.SpellTrapPositions.Length,
                        destination.SpellTrapPositions.Length));
            }
            RebuildInstances(destination, controller);
            if (source.OverlayMaterials != null)
            {
                int zoneCount = Math.Min(
                    source.OverlayMaterials.Length,
                    destination.OverlayInstances.Length);
                for (int zone = 0; zone < zoneCount; zone++)
                {
                    uint[] materials =
                        source.OverlayMaterials[zone] ??
                        Array.Empty<uint>();
                    for (int index = 0;
                         index < materials.Length;
                         index++)
                    {
                        destination.OverlayInstances[zone].Add(
                            CreateInstance(
                                materials[index],
                                controller,
                                controller,
                                (byte)DuelLocation.Overlay,
                                (uint)zone,
                                (uint)index));
                    }
                }
            }
        }

        private void RebuildInstances(
            DuelistState player,
            byte controller)
        {
            player.HandInstances.Clear();
            for (int index = 0; index < player.Hand.Count; index++)
            {
                player.HandInstances.Add(
                    CreateInstance(
                        player.Hand[index],
                        controller,
                        controller,
                        (byte)DuelLocation.Hand,
                        (uint)index,
                        0));
            }
            player.GraveyardInstances.Clear();
            for (int index = 0; index < player.Graveyard.Count; index++)
            {
                player.GraveyardInstances.Add(
                    CreateInstance(
                        player.Graveyard[index],
                        controller,
                        controller,
                        (byte)DuelLocation.Graveyard,
                        (uint)index,
                        0));
            }
            player.BanishedInstances.Clear();
            for (int index = 0; index < player.Banished.Count; index++)
            {
                player.BanishedInstances.Add(
                    CreateInstance(
                        player.Banished[index],
                        controller,
                        controller,
                        (byte)DuelLocation.Banished,
                        (uint)index,
                        0));
            }
            Array.Clear(
                player.MonsterInstances,
                0,
                player.MonsterInstances.Length);
            foreach (List<CardInstanceState> materials in
                     player.OverlayInstances)
            {
                materials.Clear();
            }
            for (int index = 0; index < player.MonsterZones.Length; index++)
            {
                if (player.MonsterZones[index] == 0)
                    continue;
                player.MonsterInstances[index] =
                    CreateInstance(
                        player.MonsterZones[index],
                        controller,
                        controller,
                        (byte)DuelLocation.MonsterZone,
                        (uint)index,
                        player.MonsterPositions[index]);
            }
            Array.Clear(
                player.SpellTrapInstances,
                0,
                player.SpellTrapInstances.Length);
            for (int index = 0;
                 index < player.SpellTrapZones.Length;
                 index++)
            {
                if (player.SpellTrapZones[index] == 0)
                    continue;
                player.SpellTrapInstances[index] =
                    CreateInstance(
                        player.SpellTrapZones[index],
                        controller,
                        controller,
                        (byte)DuelLocation.SpellTrapZone,
                        (uint)index,
                        player.SpellTrapPositions[index]);
            }
        }

        private static void Replace(List<uint> destination, uint[] source)
        {
            destination.Clear();
            if (source != null) destination.AddRange(source);
        }
    }
}
