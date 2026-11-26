// A compass strip at the top of the HUD, shown only while direction can be read from the Sun or stars.
// It is as coarse as the skill: it settles on the nearest of 8 directions, or 16 from level 4.
using System;
using Il2Cpp;
using UnityEngine;

namespace Surveying
{
    internal static class Compass
    {
        private static readonly string[] Names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        private const float PixelsPerDegree = 2.4f, FadeAtDegrees = 75f;
        private const float FromTop = 30f;          // distance of the strip below the top edge of the screen
        private const float ShelterDrop = 36f;      // how far the wind-shelter icon is moved down to clear the strip
        private static GameObject root;
        private static UILabel[] labels;
        private static UITexture tick;
        private static IntPtr hudPtr;
        private static float shown = -1f, alpha, letters;
        private static string lastState = "";
        private static bool failed, built;

        internal static void Frame()
        {
            if (failed) return;
            try
            {
                var s = Settings.Instance;
                // "on" = the player has the compass switched on and the skill for it. With no readable sky it
                // stays on screen as a dim tick with no letters, so it is clear the compass is there but blind.
                bool on = s.compass && s.bearings && Mod.CompassVisible.Value && World.InGame && World.Outdoors && Perks.Level >= Mathf.RoundToInt(s.bearingsLevel);
                bool readable = on && World.SkyVisible;
                bool want = on;
                string state = !on ? "off" : readable ? "reading" : "blind (" + World.Weather + ")";
                if (state != lastState) { lastState = state; if (Mod.Detailed) Mod.Log.Msg("[compass] " + state); }
                if (!want && alpha <= 0f) { if (root != null && root.activeSelf) root.SetActive(false); return; }
                var hud = InterfaceManager.GetPanel<Panel_HUD>();
                var t = GameManager.GetPlayerTransform();
                if (hud == null || t == null) return;
                if (want && (InterfaceManager.IsOverlayActiveImmediate() || HUDManager.DoNotRenderHUD())) want = false;
                if (root == null || hud.Pointer != hudPtr) { Build(hud); if (root == null) return; }
                MoveShelterIcon(hud, s.compass && s.bearings);

                alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, Time.unscaledDeltaTime * 2f);
                letters = Mathf.MoveTowards(letters, readable ? 1f : 0f, Time.unscaledDeltaTime * 2f);
                if (alpha <= 0f) { root.SetActive(false); return; }
                if (!root.activeSelf) root.SetActive(true);

                float step = Perks.Level >= 4 ? 22.5f : 45f;
                float settled = Mathf.Round(World.Bearing(t) / step) * step;
                shown = shown < 0f ? settled : Mathf.MoveTowardsAngle(shown, settled, Time.unscaledDeltaTime * 140f);
                for (int i = 0; i < labels.Length; i++)
                {
                    // Reading: letters slide with your heading. Blind: the letters fade out behind a white smudge.
                    float delta = Mathf.DeltaAngle(shown, i * 45f);
                    float live = Mathf.Clamp01(1f - Mathf.Abs(delta) / FadeAtDegrees) * (i % 2 == 0 ? 0.95f : 0.7f);
                    labels[i].alpha = alpha * live * letters;
                    labels[i].transform.localPosition = new Vector3(delta * PixelsPerDegree, 0f, 0f);
                }
                if (tick != null) tick.alpha = alpha * 0.8f * letters;
                if (smudge != null) smudge.alpha = alpha * 0.6f * (1f - letters);
            }
            catch (Exception e) { failed = true; Mod.Log.Error("[compass] " + e); }
        }

        // The game's wind-shelter (shield) icon sits where the strip is; it is nudged down while the compass
        // feature is on and put back when it is off. Checked twice a second in case the game repositions it.
        private static IntPtr shelterPtr; private static Vector3 shelterHome; private static float nextShelterCheck; private static bool shelterLogged, shelterMoved;
        private static void MoveShelterIcon(Panel_HUD hud, bool down)
        {
            if (Time.unscaledTime < nextShelterCheck) return;
            nextShelterCheck = Time.unscaledTime + 0.5f;
            var sp = hud.m_Sprite_Sheltered;
            if (sp == null) return;
            var tr = sp.transform;
            if (sp.Pointer != shelterPtr) { shelterPtr = sp.Pointer; shelterHome = tr.localPosition; shelterMoved = false; }
            else if (!shelterMoved) shelterHome = tr.localPosition;    // follow the game's own placement while untouched
            Vector3 want = down ? shelterHome + new Vector3(0f, -ShelterDrop, 0f) : shelterHome;
            if ((tr.localPosition - want).sqrMagnitude > 0.01f) tr.localPosition = want;
            shelterMoved = down;
            if (!shelterLogged)
            {
                shelterLogged = true;
                var uiRoot = sp.GetComponentInParent<UIRoot>();
                Vector3 at = uiRoot != null ? uiRoot.transform.InverseTransformPoint(tr.position) : tr.position;
                if (Mod.Detailed) Mod.Log.Msg($"[compass] wind-shelter icon home ({shelterHome.x:0},{shelterHome.y:0}) under '{tr.parent?.name}', now at screen ({at.x:0},{at.y:0}), size {sp.width}x{sp.height}");
            }
        }

        private static UITexture smudge;
        private static Texture2D smudgeTex;
        private static Texture2D SmudgeTexture()
        {
            if (smudgeTex != null) return smudgeTex;
            const int W = 160, H = 24;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    // Blotches at two sizes, thinned out so there are clear gaps between them.
                    float n = 0.65f * Mathf.PerlinNoise(x * 0.07f + 3.3f, y * 0.19f + 8.1f) + 0.35f * Mathf.PerlinNoise(x * 0.23f + 17f, y * 0.45f + 2f);
                    float blotch = Mathf.Clamp01((n - 0.38f) / 0.3f);
                    float ends = Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.22f);        // fades out towards both ends
                    float sides = Mathf.Clamp01(Mathf.Min(v, 1f - v) / 0.3f);        // and at top and bottom
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, blotch * blotch * (3f - 2f * blotch) * ends * sides));
                }
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            smudgeTex = t;
            return t;
        }

        private static void Build(Panel_HUD hud)
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null; hudPtr = hud.Pointer; shown = -1f; alpha = 0f;
            var src = hud.m_SkillLevelUpHeader;          // borrowed for its font and layer
            var holder = hud.m_SkillLevelUpWidget;
            if (src == null || holder == null) { if (!built) Mod.Log.Msg("[compass] HUD label not found; compass unavailable"); built = true; return; }
            root = new GameObject("Surveying_Compass");
            root.layer = src.gameObject.layer;
            root.transform.SetParent(holder.transform.parent, false);
            root.transform.localScale = Vector3.one;
            var uiRoot = src.GetComponentInParent<UIRoot>();
            float half = uiRoot != null ? uiRoot.activeHeight / 2f : 540f;
            if (uiRoot != null) root.transform.position = uiRoot.transform.TransformPoint(new Vector3(0f, half - FromTop, 0f));
            else root.transform.localPosition = new Vector3(0f, half - FromTop, 0f);

            labels = new UILabel[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                var go = UnityEngine.Object.Instantiate(src.gameObject, root.transform);
                go.name = "Surveying_Compass_" + Names[i];
                foreach (var tw in go.GetComponents<UITweener>()) UnityEngine.Object.Destroy(tw);
                var loc = go.GetComponent<UILocalize>();
                if (loc != null) UnityEngine.Object.Destroy(loc);
                go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                go.SetActive(true);
                var l = go.GetComponent<UILabel>();
                l.enabled = true;
                l.overflowMethod = UILabel.Overflow.ResizeFreely;
                l.alignment = NGUIText.Alignment.Center;
                l.pivot = UIWidget.Pivot.Center;
                l.fontSize = i % 2 == 0 ? 18 : 13;
                l.text = Names[i];
                l.color = Color.white;
                labels[i] = l;
            }
            tick = World.MakeTex(World.SolidTexture(), root.transform, new Vector3(0f, -14f, 0f), 2, 6, Color.white, src.depth, root.layer, false);
            // Shown instead of the letters when the sky cannot be read: a patchy white smear, like snow on a lens.
            smudge = World.MakeTex(SmudgeTexture(), root.transform, new Vector3(0f, -2f, 0f), 190, 26, Color.white, src.depth, root.layer, false);
            if (smudge != null) smudge.alpha = 0f;
            root.SetActive(false);
            if (!built && Mod.Detailed) Mod.Log.Msg($"[compass] built under '{holder.transform.parent.name}' (UI height {(uiRoot != null ? uiRoot.activeHeight : -1)}, font size {src.fontSize}, depth {src.depth})");
            built = true;
        }
    }
}
