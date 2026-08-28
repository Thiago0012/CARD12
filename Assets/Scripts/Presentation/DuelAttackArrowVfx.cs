using System.Collections.Generic;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena.Presentation
{
    /// <summary>
    /// Curved, quality-scaled attack trajectory shared by mouse and touch.
    /// The authoritative attack still comes exclusively from the Core; this
    /// component only renders the currently selected source and destination.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelAttackArrowVfx : MonoBehaviour
    {
        private static Material sharedLineMaterial;

        private readonly List<LineRenderer> pulses = new();
        private LineRenderer body;
        private LineRenderer glow;
        private LineRenderer head;
        private Vector3 start;
        private Vector3 end;
        private Vector3 control;
        private Color validColor;
        private Color directColor;
        private Color invalidColor;
        private bool visible;
        private bool validTarget;
        private bool directTarget;
        private int segmentCount = 18;
        private int activePulseCount = 2;

        public bool IsVisible => visible;
        public int SegmentCount => segmentCount;
        public int ActivePulseCount => activePulseCount;

        public void Configure(
            LineRenderer existingBody,
            Color valid,
            Color direct,
            Color invalid)
        {
            body = existingBody != null
                ? existingBody
                : gameObject.AddComponent<LineRenderer>();
            validColor = valid;
            directColor = direct;
            invalidColor = invalid;

            ConfigureLine(body, "Corpo da Seta de Ataque", 22);
            body.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.42f),
                new Keyframe(0.58f, 1f),
                new Keyframe(1f, 0.16f));

            glow = CreateLine("Halo da Seta de Ataque", 20);
            glow.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.54f),
                new Keyframe(0.58f, 1f),
                new Keyframe(1f, 0.20f));

            head = CreateLine("Ponta da Seta de Ataque", 23);
            head.positionCount = 3;
            head.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

            for (int index = 0; index < 3; index++)
            {
                LineRenderer pulse = CreateLine(
                    $"Pulso da Seta de Ataque {index + 1}",
                    24 + index);
                pulse.positionCount = 2;
                pulse.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.24f);
                pulses.Add(pulse);
            }

            ApplyQuality();
            SetVisible(false);
        }

        public void SetEndpoints(
            Vector3 origin,
            Vector3 destination,
            bool isValidTarget,
            bool isDirectTarget = false)
        {
            start = origin;
            end = destination;
            validTarget = isValidTarget;
            directTarget = isDirectTarget;

            Vector3 delta = end - start;
            float distance = Mathf.Max(0.01f, delta.magnitude);
            Vector3 lateral = Vector3.Cross(Vector3.up, delta.normalized);
            float side = Mathf.Sin(Time.unscaledTime * 1.45f) *
                         Mathf.Min(0.16f, distance * 0.018f);
            control = Vector3.Lerp(start, end, 0.54f) +
                      Vector3.up * Mathf.Clamp(
                          0.42f + distance * 0.085f,
                          0.48f,
                          1.18f) +
                      lateral * side;

            RebuildCurve();
            SetVisible(true);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (body != null) body.enabled = value;
            if (glow != null) glow.enabled = value && glow.gameObject.activeSelf;
            if (head != null) head.enabled = value;
            for (int index = 0; index < pulses.Count; index++)
            {
                pulses[index].enabled = value && index < activePulseCount;
            }
        }

        private void OnEnable()
        {
            ArcaneGraphicsPreferences.QualityChanged += ApplyQuality;
        }

        private void OnDisable()
        {
            ArcaneGraphicsPreferences.QualityChanged -= ApplyQuality;
        }

        private void Update()
        {
            if (!visible || body == null)
                return;

            Color targetColor = directTarget
                ? directColor
                : validTarget
                    ? validColor
                    : invalidColor;
            float heartbeat = 0.5f +
                              0.5f * Mathf.Sin(Time.unscaledTime * 8.5f);
            body.startColor = WithAlpha(
                Color.Lerp(targetColor, Color.white, heartbeat * 0.16f),
                0.94f);
            body.endColor = WithAlpha(Color.white, 1f);
            glow.startColor = WithAlpha(targetColor, 0.15f + heartbeat * 0.10f);
            glow.endColor = WithAlpha(targetColor, 0.32f + heartbeat * 0.18f);
            head.startColor = WithAlpha(targetColor, 0.96f);
            head.endColor = WithAlpha(Color.white, 1f);

            for (int index = 0; index < pulses.Count; index++)
            {
                LineRenderer pulse = pulses[index];
                if (!pulse.enabled)
                    continue;
                float t = Mathf.Repeat(
                    Time.unscaledTime * (0.72f + index * 0.06f) +
                    index / Mathf.Max(1f, activePulseCount),
                    1f);
                float tail = Mathf.Max(0f, t - 0.075f);
                pulse.SetPosition(0, Bezier(tail));
                pulse.SetPosition(1, Bezier(t));
                pulse.startColor = WithAlpha(targetColor, 0.12f);
                pulse.endColor = WithAlpha(Color.white, 0.94f);
            }
        }

        private void ApplyQuality()
        {
            ArcaneGraphicsQuality quality = ArcaneGraphicsPreferences.Quality;
            segmentCount = quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 8,
                ArcaneGraphicsQuality.Low => 11,
                ArcaneGraphicsQuality.Medium => 15,
                ArcaneGraphicsQuality.High => 20,
                _ => 26
            };
            activePulseCount = quality switch
            {
                ArcaneGraphicsQuality.VeryLow => 1,
                ArcaneGraphicsQuality.Low => 1,
                ArcaneGraphicsQuality.Medium => 2,
                _ => 3
            };
            if (glow != null)
            {
                bool glowEnabled = quality > ArcaneGraphicsQuality.VeryLow;
                glow.gameObject.SetActive(glowEnabled);
                glow.enabled = visible && glowEnabled;
            }
            for (int index = 0; index < pulses.Count; index++)
                pulses[index].enabled = visible && index < activePulseCount;
            if (visible)
                RebuildCurve();
        }

        private void RebuildCurve()
        {
            if (body == null || glow == null || head == null)
                return;
            body.positionCount = segmentCount;
            glow.positionCount = segmentCount;
            for (int index = 0; index < segmentCount; index++)
            {
                float t = index / Mathf.Max(1f, segmentCount - 1f);
                Vector3 point = Bezier(t);
                body.SetPosition(index, point);
                glow.SetPosition(index, point);
            }

            Vector3 beforeEnd = Bezier(0.92f);
            Vector3 tangent = (end - beforeEnd).normalized;
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
            float headLength = Mathf.Clamp(
                Vector3.Distance(start, end) * 0.075f,
                0.28f,
                0.52f);
            float headWidth = headLength * 0.62f;
            Vector3 basePoint = end - tangent * headLength;
            head.SetPosition(0, basePoint + side * headWidth);
            head.SetPosition(1, end);
            head.SetPosition(2, basePoint - side * headWidth);

            float distance = Vector3.Distance(start, end);
            body.widthMultiplier = Mathf.Clamp(distance * 0.024f, 0.10f, 0.18f);
            glow.widthMultiplier = body.widthMultiplier * 2.35f;
            head.widthMultiplier = body.widthMultiplier * 0.72f;
            for (int index = 0; index < pulses.Count; index++)
                pulses[index].widthMultiplier = body.widthMultiplier * 0.72f;
        }

        private Vector3 Bezier(float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return inverse * inverse * start +
                   2f * inverse * t * control +
                   t * t * end;
        }

        private LineRenderer CreateLine(string objectName, int sortingOrder)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            ConfigureLine(line, objectName, sortingOrder);
            return line;
        }

        private static void ConfigureLine(
            LineRenderer line,
            string objectName,
            int sortingOrder)
        {
            line.gameObject.name = objectName;
            line.useWorldSpace = true;
            line.loop = false;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sharedMaterial = SharedLineMaterial();
            line.sortingOrder = sortingOrder;
            line.enabled = false;
        }

        private static Material SharedLineMaterial()
        {
            if (sharedLineMaterial != null)
                return sharedLineMaterial;
            Shader shader =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;
            sharedLineMaterial = new Material(shader)
            {
                name = "Material compartilhado da seta de ataque",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3200
            };
            if (sharedLineMaterial.HasProperty("_BaseColor"))
                sharedLineMaterial.SetColor("_BaseColor", Color.white);
            if (sharedLineMaterial.HasProperty("_Color"))
                sharedLineMaterial.SetColor("_Color", Color.white);
            return sharedLineMaterial;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
