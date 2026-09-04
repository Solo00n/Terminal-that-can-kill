using System.Collections.Generic;
using UnityEngine;

namespace LethalDoors.Traps
{
    /// <summary>
    /// Recolours a hijacked trap so players can tell at a glance that it is on their side:
    /// the turret laser and the mine indicator turn green instead of red.
    ///
    /// Neither Turret nor Landmine exposes its laser/indicator as a field — they live in the
    /// prefab and are driven by the animator — so we discover them at runtime: every Light and
    /// LineRenderer under the trap, plus any material whose colour is dominantly red (that is
    /// the indicator/laser; the grey chassis is left alone). Originals are cached so the trap
    /// goes back to normal when the hijack expires.
    /// </summary>
    internal static class TrapVisuals
    {
        // HDRP first, then the built-in names, so this works whichever shader the prefab uses.
        private static readonly string[] ColorProps =
        {
            "_EmissiveColor", "_EmissionColor", "_BaseColor", "_Color", "_UnlitColor"
        };

        private sealed class Record
        {
            public readonly List<Light> Lights = new List<Light>();
            public readonly List<Color> LightColors = new List<Color>();

            public readonly List<LineRenderer> Lines = new List<LineRenderer>();
            public readonly List<Color> LineStart = new List<Color>();
            public readonly List<Color> LineEnd = new List<Color>();

            public readonly List<Material> Mats = new List<Material>();
            public readonly List<int> MatProps = new List<int>();
            public readonly List<Color> MatColors = new List<Color>();
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
                    TintMaterials(lr.materials, rec, requireRed: false); // the beam material itself
                }

                foreach (var r in trap.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || r is LineRenderer) continue;
                    TintMaterials(r.materials, rec, requireRed: true);   // only the red indicator
                }
            }
            catch { /* cosmetic only — never let this break the trap */ }

            _records[trap] = rec;
        }

        public static void Restore(Component trap)
        {
            if (trap == null) { return; }
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

                for (int i = 0; i < rec.Mats.Count; i++)
                    if (rec.Mats[i] != null) rec.Mats[i].SetColor(rec.MatProps[i], rec.MatColors[i]);
            }
            catch { /* ignore */ }
        }

        // ------------------------------------------------------------------ helpers
        private static void TintMaterials(Material[] mats, Record rec, bool requireRed)
        {
            if (mats == null) return;
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
                    rec.Mats.Add(mat);
                    rec.MatProps.Add(id);
                    rec.MatColors.Add(c);
                    mat.SetColor(id, ToGreen(c));
                }
            }
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
