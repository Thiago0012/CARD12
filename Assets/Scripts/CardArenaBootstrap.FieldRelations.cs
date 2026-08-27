using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Multiplayer;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena
{
    /// <summary>
    /// Presents public Core card relationships as quiet tactical connections
    /// on the arena. The relationship data remains owned by DuelPresentationState;
    /// this class never creates rules or reveals a hidden card.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private enum FieldRelationKind : byte
        {
            Equipment,
            Target,
            Relation
        }

        private sealed class FieldRelationVisual
        {
            public string Key;
            public FieldRelationKind Kind;
            public DuelZone3D Source;
            public DuelZone3D Target;
            public LineRenderer Line;
        }

        private readonly Dictionary<string, FieldRelationVisual>
            fieldRelationVisuals = new(StringComparer.Ordinal);
        private Transform fieldRelationRoot;
        private Material fieldRelationMaterial;

        private void RefreshFieldRelationPresentation(bool rebuild)
        {
            // A cada quadro apenas atualizamos as posições das linhas. A
            // reconstrução das relações ocorre quando a assinatura do campo
            // mudou, evitando alocações contínuas em Android e PC.
            if (!rebuild)
                return;
            if (state == null)
            {
                ClearFieldRelationVisuals();
                return;
            }

            Dictionary<ulong, DuelZone3D> publicFieldZones =
                PublicFieldZonesByRuntimeId();
            var desired = new List<
                (string Key, FieldRelationKind Kind, DuelZone3D Source,
                    DuelZone3D Target)>();
            var knownKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach ((ulong runtimeId, DuelZone3D sourceZone) in
                     publicFieldZones.OrderBy(entry => entry.Key))
            {
                CardInstanceState source = InstanceAt(sourceZone);
                if (source == null)
                    continue;

                AddFieldRelation(
                    desired,
                    knownKeys,
                    publicFieldZones,
                    FieldRelationKind.Equipment,
                    runtimeId,
                    source.EquippedToRuntimeId,
                    false);
                foreach (ulong targetRuntimeId in source.TargetRuntimeIds)
                {
                    AddFieldRelation(
                        desired,
                        knownKeys,
                        publicFieldZones,
                        FieldRelationKind.Target,
                        runtimeId,
                        targetRuntimeId,
                        false);
                }
                foreach (ulong relatedRuntimeId in source.RelationRuntimeIds)
                {
                    AddFieldRelation(
                        desired,
                        knownKeys,
                        publicFieldZones,
                        FieldRelationKind.Relation,
                        runtimeId,
                        relatedRuntimeId,
                        true);
                }
            }

            if (desired.Count > 0)
                EnsureFieldRelationRoot();
            var obsolete = fieldRelationVisuals.Keys
                .Where(key => !knownKeys.Contains(key))
                .ToArray();
            foreach (string key in obsolete)
            {
                FieldRelationVisual visual = fieldRelationVisuals[key];
                if (visual?.Line != null)
                    Destroy(visual.Line.gameObject);
                fieldRelationVisuals.Remove(key);
            }

            foreach ((string key, FieldRelationKind kind, DuelZone3D source,
                      DuelZone3D target) in desired)
            {
                if (!fieldRelationVisuals.TryGetValue(key,
                        out FieldRelationVisual visual) ||
                    visual == null || visual.Line == null)
                {
                    visual = CreateFieldRelationVisual(key, kind);
                    fieldRelationVisuals[key] = visual;
                }
                visual.Kind = kind;
                visual.Source = source;
                visual.Target = target;
                visual.Line.gameObject.SetActive(true);
            }

            if (fieldRelationRoot != null)
                fieldRelationRoot.gameObject.SetActive(
                    fieldRelationVisuals.Count > 0);
            UpdateFieldRelationPresentation();
        }

        private Dictionary<ulong, DuelZone3D> PublicFieldZonesByRuntimeId()
        {
            var result = new Dictionary<ulong, DuelZone3D>();
            foreach (DuelZone3D zone in AllZones())
            {
                if (!CanPresentFieldRelationEndpoint(zone,
                        out CardInstanceState instance))
                {
                    continue;
                }
                result[instance.RuntimeId] = zone;
            }
            return result;
        }

        private bool CanPresentFieldRelationEndpoint(
            DuelZone3D zone,
            out CardInstanceState instance)
        {
            instance = null;
            if (zone == null || !zone.HasValidIdentity ||
                (zone.Kind != DuelZoneKind.Monster &&
                 zone.Kind != DuelZoneKind.SpellTrap &&
                 zone.Kind != DuelZoneKind.Field) ||
                !IsFaceUp(PositionAt(zone)))
            {
                return false;
            }
            instance = InstanceAt(zone);
            return instance != null && instance.RuntimeId != 0 &&
                   zone.FindPresentedCard() != null;
        }

        private static void AddFieldRelation(
            ICollection<(string Key, FieldRelationKind Kind, DuelZone3D Source,
                DuelZone3D Target)> desired,
            ISet<string> knownKeys,
            IReadOnlyDictionary<ulong, DuelZone3D> zones,
            FieldRelationKind kind,
            ulong sourceRuntimeId,
            ulong targetRuntimeId,
            bool symmetric)
        {
            if (sourceRuntimeId == 0 || targetRuntimeId == 0 ||
                sourceRuntimeId == targetRuntimeId ||
                !zones.TryGetValue(sourceRuntimeId, out DuelZone3D source) ||
                !zones.TryGetValue(targetRuntimeId, out DuelZone3D target))
            {
                return;
            }

            ulong first = symmetric
                ? Math.Min(sourceRuntimeId, targetRuntimeId)
                : sourceRuntimeId;
            ulong second = symmetric
                ? Math.Max(sourceRuntimeId, targetRuntimeId)
                : targetRuntimeId;
            string key = string.Concat(
                ((byte)kind).ToString(), ":", first.ToString(), ":",
                second.ToString());
            if (!knownKeys.Add(key))
                return;
            desired.Add((key, kind, source, target));
        }

        private void EnsureFieldRelationRoot()
        {
            if (fieldRelationRoot != null)
                return;
            var root = new GameObject("Conexões táticas do campo");
            root.transform.SetParent(transform, false);
            fieldRelationRoot = root.transform;
            Shader shader = Shader.Find("Sprites/Default") ??
                            Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                fieldRelationMaterial = new Material(shader)
                {
                    name = "Conexões táticas do campo"
                };
            }
        }

        private FieldRelationVisual CreateFieldRelationVisual(
            string key,
            FieldRelationKind kind)
        {
            EnsureFieldRelationRoot();
            var root = new GameObject("Ligação de carta");
            root.transform.SetParent(fieldRelationRoot, false);
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.sortingOrder = 1;
            line.sharedMaterial = fieldRelationMaterial;
            return new FieldRelationVisual
            {
                Key = key,
                Kind = kind,
                Line = line
            };
        }

        private void UpdateFieldRelationPresentation()
        {
            foreach (FieldRelationVisual visual in fieldRelationVisuals.Values)
            {
                if (visual?.Line == null || visual.Source == null ||
                    visual.Target == null)
                {
                    continue;
                }

                Vector3 source = visual.Source.CardPresentationAnchor.position +
                                 Vector3.up * 0.48f;
                Vector3 target = visual.Target.CardPresentationAnchor.position +
                                 Vector3.up * 0.48f;
                float distance = Vector3.Distance(source, target);
                Vector3 arc = Vector3.up * Mathf.Clamp(
                    distance * 0.12f,
                    0.18f,
                    0.52f);
                visual.Line.SetPosition(0, source);
                visual.Line.SetPosition(1, (source + target) * 0.5f + arc);
                visual.Line.SetPosition(2, target);

                Color color = FieldRelationColor(visual.Kind);
                float pulse = visual.Kind == FieldRelationKind.Target
                    ? Mathf.Lerp(
                        0.62f,
                        1f,
                        (Mathf.Sin(Time.unscaledTime * 5.4f) + 1f) * 0.5f)
                    : 0.84f;
                color.a *= pulse;
                visual.Line.startColor = color;
                visual.Line.endColor = new Color(
                    color.r,
                    color.g,
                    color.b,
                    color.a * 0.34f);
                visual.Line.startWidth = visual.Kind == FieldRelationKind.Target
                    ? 0.052f
                    : 0.036f;
                visual.Line.endWidth = 0.018f;
            }
        }

        private static Color FieldRelationColor(FieldRelationKind kind)
        {
            return kind switch
            {
                FieldRelationKind.Equipment => Gold,
                FieldRelationKind.Target => Red,
                _ => Cyan
            };
        }

        private void ClearFieldRelationVisuals()
        {
            foreach (FieldRelationVisual visual in fieldRelationVisuals.Values)
            {
                if (visual?.Line != null)
                    Destroy(visual.Line.gameObject);
            }
            fieldRelationVisuals.Clear();
            if (fieldRelationRoot != null)
                fieldRelationRoot.gameObject.SetActive(false);
        }

        private void DisposeFieldRelationPresentation()
        {
            ClearFieldRelationVisuals();
            if (fieldRelationMaterial != null)
                Destroy(fieldRelationMaterial);
            fieldRelationMaterial = null;
            if (fieldRelationRoot != null)
                Destroy(fieldRelationRoot.gameObject);
            fieldRelationRoot = null;
        }
    }
}
