using System.Collections.Generic;
using UnityEngine;

namespace LethalDoors.Traps
{
    /// <summary>
    /// Recolours a hijacked trap so players can tell at a glance that it is on their side:
    /// the turret laser and the mine indicator turn green instead of red.
    ///
    /// Neither Turret nor Landmine exposes its laser/indicator as a field — they live in the
    /// prefab and are driven by the animator — so we discover them at runtime.
    ///
    /// Colours are written through a <see cref="MaterialPropertyBlock"/> rather than onto the
    /// material. That matters for modded traps: skin systems (DefendFacility's mini turret, for
    /// one) push their look through a property block, and a block overrides whatever the material
    /// says — so writing to the material had no visible effect at all. A block also avoids
    /// instantiating a material copy per renderer.
    /// </summary>
    internal static class TrapVisuals
    {
        // HDRP first, then the built-in names, so this works whichever shader the prefab uses.
        private static readonly string[] ColorProps =
        {
            "_EmissiveColor", "_EmissionColor", "_BaseColor", "_Color", "_UnlitColor"
        };

        private static readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        private sealed class Record
        {
            public readonly List<Light> Lights = new List<Light>();
            public readonly List<Color> LightColors = new List<Color>();

            public readonly List<LineRenderer> Lines = new List<LineRenderer>();
            public readonly List<Color> LineStart = new List<Color>();
            public readonly List<Color> LineEnd = new List<Color>();

            public readonly List<Renderer> Renderers = new List<Renderer>();
            public readonly List<int> Props = new List<int>();
            public readonly List<Color> Colors = new List<Color>();
        }

        private static readonly Dictionary<Component, Record> _records = new Dictionary<Component, Record>();

        public static void Clear() => _records.Clear();

        public static void ApplyAllied(Component trap)
        {
            if (trap == null) return;
            if (!Plugin.Config.AlliedTint.Value) return;
            if (_records.ContainsKey(trap)) return; // already tinted

            var rec = new Record();
            try
            {
                // Lights and line renderers hold their colour on the component itself, so a skin
                // system cannot hide these — this is what makes the laser turn green.
                foreach (var light in trap.GetComponentsInChildren<Light>(true))
                {
                    if (light == null) continue;
                    rec.Lights.Add(light);
                    rec.LightColors.Add(light.color);
                    light.color = ToGreen(light.color);
                }

                foreach (var lr in trap.GetComponentsInChildren<LineRenderer>(true))
                {
                    if (lr == null) continue;
                    rec.Lines.Add(lr);
                    rec.LineStart.Add(lr.startColor);
                    rec.LineEnd.Add(lr.endColor);
                    lr.startColor = ToGreen(lr.startColor);
                    lr.endColor = ToGreen(lr.endColor);
                    TintRenderer(lr, rec, requireRed: false);   // the beam material itself
                }

                foreach (var r in trap.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r is LineRenderer) continue;
                    TintRenderer(r, rec, requireRed: true);     // only the red indicator
                }
            }
            catch { /* cosmetic only — never let this break the trap */ }

            _records[trap] = rec;
            Plugin.Log.LogInfo(
                $"Allied tint on '{trap.gameObject.name}': {rec.Lights.Count} light(s), " +
                $"{rec.Lines.Count} beam(s), {rec.Renderers.Count} indicator material(s).");
        }

        public static void Restore(Component trap)
        {
            if (trap == null) return;
            if (!_records.TryGetValue(trap, out var rec)) return;
            _records.Remove(trap);

            try
            {
                for (int i = 0; i < rec.Lights.Count; i++)
                    if (rec.Lights[i] != null) rec.Lights[i].color = rec.LightColors[i];

                for (int i = 0; i < rec.Lines.Count; i++)
                {
                    if (rec.Lines[i] == null) continue;
                    rec.Lines[i].startColor = rec.LineStart[i];
                    rec.Lines[i].endColor = rec.LineEnd[i];
                }

                for (int i = 0; i < rec.Renderers.Count; i++)
                {
                    var r = rec.Renderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_block);
                    _block.SetColor(rec.Props[i], rec.Colors[i]);
                    r.SetPropertyBlock(_block);
                }
            }
            catch { /* ignore */ }
        }

        // ------------------------------------------------------------------ helpers
        private static void TintRenderer(Renderer r, Record rec, bool requireRed)
        {
            var mats = r.sharedMaterials;          // shared: never instantiate a copy
            if (mats == null) return;

            r.GetPropertyBlock(_block);            // keep whatever a skin system already set
            bool touched = false;

            for (int m = 0; m < mats.Length; m++)
            {
                var mat = mats[m];
                if (mat == null) continue;

                for (int p = 0; p < ColorProps.Length; p++)
                {
                    string prop = ColorProps[p];
                    if (!mat.HasProperty(prop)) continue;

                    Color c = mat.GetColor(prop);
                    if (requireRed && !IsDominantlyRed(c)) continue;
                    if (c.maxColorComponent <= 0.001f) continue; // black/unused slot

                    int id = Shader.PropertyToID(prop);
                    rec.Renderers.Add(r);
                    rec.Props.Add(id);
                    rec.Colors.Add(c);
                    _block.SetColor(id, ToGreen(c));
                    touched = true;
                }
            }

            if (touched) r.SetPropertyBlock(_block);
        }

        /// <summary>Red indicator/laser, as opposed to the grey chassis or a neutral light.</summary>
        private static bool IsDominantlyRed(Color c)
        {
            return c.r > 0.25f && c.r > c.g * 1.8f && c.r > c.b * 1.8f;
        }

        /// <summary>Swap hue to green while keeping the original brightness (HDR emissives included).</summary>
        private static Color ToGreen(Color c)
        {
            float intensity = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (intensity <= 0f) intensity = 1f;
            return new Color(0f, intensity, 0f, c.a);
        }
    }
}
