using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneArena.Multiplayer
{
    public enum DuelPlayerSide
    {
        PlayerOne = 0,
        PlayerTwo = 1
    }

    public enum DuelZoneKind
    {
        Monster = 0,
        SpellTrap = 1,
        MainDeck = 2,
        ExtraDeck = 3,
        Graveyard = 4,
        Banishment = 5,
        Field = 6
    }

    public enum DuelMonsterPosition
    {
        FaceUpAttack = 0,
        FaceUpDefense = 1,
        FaceDownDefense = 2
    }

    [Serializable]
    public struct DuelZoneAddress
    {
        [SerializeField] private string stableId;
        [SerializeField] private DuelPlayerSide owner;
        [SerializeField] private DuelZoneKind kind;
        [SerializeField] private int index;

        public string StableId => stableId;
        public DuelPlayerSide Owner => owner;
        public DuelZoneKind Kind => kind;
        public int Index => index;

        public DuelZoneAddress(DuelPlayerSide owner, DuelZoneKind kind, int index)
        {
            this.owner = owner;
            this.kind = kind;
            this.index = Mathf.Max(0, index);
            stableId = $"{SideToken(owner)}.{KindToken(kind)}.{this.index}";
        }

        private static string SideToken(DuelPlayerSide side)
        {
            return side == DuelPlayerSide.PlayerOne ? "p1" : "p2";
        }

        private static string KindToken(DuelZoneKind zoneKind)
        {
            switch (zoneKind)
            {
                case DuelZoneKind.Monster:
                    return "monster";
                case DuelZoneKind.SpellTrap:
                    return "spell_trap";
                case DuelZoneKind.MainDeck:
                    return "main_deck";
                case DuelZoneKind.ExtraDeck:
                    return "extra_deck";
                case DuelZoneKind.Graveyard:
                    return "graveyard";
                case DuelZoneKind.Banishment:
                    return "banishment";
                case DuelZoneKind.Field:
                    return "field";
                default:
                    return "unknown";
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class DuelFieldRegistry : MonoBehaviour
    {
        [SerializeField] private DuelZone3D[] zones = Array.Empty<DuelZone3D>();
        private readonly Dictionary<string, DuelZone3D> _zonesById =
            new Dictionary<string, DuelZone3D>(StringComparer.Ordinal);

        public IReadOnlyList<DuelZone3D> Zones => zones;

        private void Awake()
        {
            RebuildLookup();
        }

        public void RebuildIndex()
        {
            zones = GetComponentsInChildren<DuelZone3D>(true);
            foreach (var zone in zones)
            {
                if (zone != null)
                    zone.EnsureIdentityFromHierarchy(false);
            }
            Array.Sort(zones, CompareZones);
            RebuildLookup();
        }

        public bool TryGetZone(string stableId, out DuelZone3D zone)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                zone = null;
                return false;
            }

            return _zonesById.TryGetValue(stableId, out zone);
        }

        private void RebuildLookup()
        {
            _zonesById.Clear();
            if (zones == null)
                return;

            foreach (var zone in zones)
            {
                if (zone == null || string.IsNullOrWhiteSpace(zone.StableId))
                    continue;

                if (_zonesById.ContainsKey(zone.StableId))
                {
                    Debug.LogError($"ID de zona duplicado: {zone.StableId}", zone);
                    continue;
                }

                _zonesById.Add(zone.StableId, zone);
            }
        }

        private static int CompareZones(DuelZone3D left, DuelZone3D right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            return string.CompareOrdinal(left.StableId, right.StableId);
        }
    }
}
