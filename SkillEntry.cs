// Adds "Surveying" to the journal's Skills list as a real Skill object on the game's skills manager.
// The game saves each vanilla skill in its own field, so this extra entry is never written to the save.
// Points are not earned yet (that is stage 2); the entry shows level 1 with the planned descriptions.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Surveying
{
    internal static class SkillEntry
    {
        internal const SkillType Type = (SkillType)100;
        private const string NameKey = "GAMEPLAY_SkillSurveying";
        private static bool dumped, iconPicked, textVerified, getHooked, failedOnce, panelLogged;
        private static string lastPanelState = "";

                private static int[] TierPoints => Perks.TierPoints;

        internal static readonly Dictionary<string, string> Text = BuildText();

        // Benefit lines follow the settings page (read once at launch). At most five lines per level, like vanilla.
        private static Dictionary<string, string> BuildText()
        {
            var s = Settings.Instance;
            var d = new Dictionary<string, string>
            {
                [NameKey] = "Surveying",
                ["GAMEPLAY_SurveyingDescriptionTier1"] = "You can scratch out a rough sketch of what's in front of you. North is that way. Probably.",
                ["GAMEPLAY_SurveyingDescriptionTier2"] = "You're getting a feel for the land. Your sketches come together more quickly, and you waste less Charcoal doing it.",
                ["GAMEPLAY_SurveyingDescriptionTier3"] = "You're a capable surveyor now. You've learned to read direction from the Sun and stars, and to make good use of high ground and failing light.",
                ["GAMEPLAY_SurveyingDescriptionTier4"] = "Mapping has become second nature. You take note of the ground you cover in any weather, and you usually have a fair idea of where you are.",
                ["GAMEPLAY_SurveyingDescriptionTier5"] = "Your skills are legendary. You carry Great Bear in your head. You are the map.",
            };
            int circle = Mathf.RoundToInt(s.circleLevel), bear = Mathf.RoundToInt(s.bearingsLevel);
            for (int lv = 1; lv <= 5; lv++)
            {
                var lines = new List<string>();
                if (lv == 1) lines.Add("Base Surveying Skill");
                float cut = lv >= 4 ? s.timeReductionHigh : s.timeReductionLow;
                if (s.fasterSurveys && lv >= 2 && cut > 0f) lines.Add($"Survey time reduced by {cut:0}%");
                if (s.keepCharcoal && lv >= 2 && s.keepCharcoalChance > 0f) lines.Add($"{s.keepCharcoalChance:0}% Chance to keep Charcoal after a Survey");
                float rad = (lv - 1) * s.radiusPerLevel, clear = lv >= 5 ? s.clearBonusL5 : lv == 4 ? s.clearBonusL4 : 0f;
                if (s.radiusBonus && rad > 0f) lines.Add(clear > 0f ? $"Survey radius increased by {rad:0.#}% ({rad + clear:0.#}% in clear daylight)" : $"Survey radius increased by {rad:0.#}%");
                if (s.altitudeBonus && lv >= 3) lines.Add($"Altitude bonus to Survey radius increased by {(lv >= 5 ? 100 : lv == 4 ? 50 : 25)}%");
                if (s.positionCircle && lv >= circle) lines.Add(lv >= 5 ? "Best position shown on the Region Map" : lv > circle ? "Closer position shown on the Region Map" : "Rough position shown on the Region Map");
                if (s.bearings && lv >= bear && lines.Count < 5) lines.Add(lv >= Mathf.RoundToInt(s.compassLightWeatherLevel) ? "Read direction from the Sun and stars, even in light weather" : "Read direction from the Sun and stars");
                if (s.duskSurvey && lv >= 3 && lines.Count < 5) lines.Add("Survey at dawn and dusk");
                if (s.trailNotes && lines.Count < 5) lines.Add(lv >= Mathf.RoundToInt(s.trailPoorMinLevel) ? "Trail notes added to each Survey, even in poor visibility" : "Trail notes added to each Survey");
                if (lines.Count > 5) lines.RemoveRange(5, lines.Count - 5);
                d[$"GAMEPLAY_SurveyingBenefitsTier{lv}"] = string.Join("\n", lines);
            }
            return d;
        }

        internal static string IconName = "ico_skills_cartography";   // small icon, confirmed present in the game's atlas
        internal static string BannerSprite = "ico_skills_cartography"; // level-up banner; a large version is used if the atlas has one

        private static void Msg(string s) { if (Mod.Detailed) Mod.Log.Msg("[skill] " + s); }   // detail, only with DetailedLog on
        private static void Info(string s) => Mod.Log.Msg("[skill] " + s);

        // Called once a second and whenever the journal's skills page refreshes. Cheap when nothing is to do.
        internal static void Ensure()
        {
            if (!Mod.JournalSkill.Value) return;
            try
            {
                SkillsManager mgr;
                try { mgr = GameManager.GetSkillsManager(); } catch { return; }
                if (mgr == null) return;
                var list = mgr.m_Skills;
                if (list == null || list.Count == 0) return;
                if (!EnsureText()) return;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && list[i].m_SkillType == Type) return;

                Skill template = list[0];
                if (!dumped)
                {
                    dumped = true;
                    for (int i = 0; i < list.Count; i++) Dump(list[i]);
                }

                var go = new GameObject("Skill_Surveying");
                go.transform.SetParent(mgr.transform, false);
                var sk = go.AddComponent<Skill>();
                sk.m_SkillType = Type;
                sk.m_LocalizedDisplayName = Loc(NameKey);
                int tiers = template.m_TierPoints != null && template.m_TierPoints.Length > 0 ? template.m_TierPoints.Length : 5;
                var pts = new int[tiers];
                var ben = new LocalizedString[tiers];
                var des = new LocalizedString[tiers];
                for (int t = 0; t < tiers; t++)
                {
                    int n = Math.Min(t, 4);
                    pts[t] = TierPoints[n];
                    ben[t] = Loc($"GAMEPLAY_SurveyingBenefitsTier{n + 1}");
                    des[t] = Loc($"GAMEPLAY_SurveyingDescriptionTier{n + 1}");
                }
                sk.m_TierPoints = new Il2CppStructArray<int>(pts);
                sk.m_TierLocalizedBenefits = new Il2CppReferenceArray<LocalizedString>(ben);
                sk.m_TierLocalizedDescriptions = new Il2CppReferenceArray<LocalizedString>(des);
                // Icons: start from a vanilla skill so nothing is ever blank; the large picture is swapped for the
                // journal's own survey picture, and the small icon is swapped once the journal's atlas can be read.
                sk.m_SkillIcon = template.m_SkillIcon;
                sk.m_SkillIconBackground = template.m_SkillIconBackground;
                string img = template.m_SkillImage ?? "";
                sk.m_SkillImage = Regex.IsMatch(img, @"ico_skill_large_\w+") ? Regex.Replace(img, @"ico_skill_large_\w+", "Collections_large_survey") : img;
                sk.m_CurrentPoints = (int)Perks.Points;
                iconPicked = false;
                list.Add(sk);
                Msg($"added to the skills list at index {list.Count - 1} (image '{sk.m_SkillImage}', icon '{sk.m_SkillIcon}')");
                Dump(sk);
            }
            catch (Exception e)
            {
                if (!failedOnce) { failedOnce = true; Mod.Log.Error($"[skill] Ensure: {e}"); }
            }
        }

        // The game's "every skill at maximum" check (an achievement) should only count its own skills:
        // Surveying is taken off the list for the length of that check and put straight back.
        private static Skill parked; private static int parkedAt;
        static void Pre_AllMax(SkillsManager __instance)
        {
            try
            {
                parked = null;
                var list = __instance.m_Skills;
                if (list == null) return;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && list[i].m_SkillType == Type) { parked = list[i]; parkedAt = i; list.RemoveAt(i); return; }
            }
            catch (Exception e) { Info("all-max check (before): " + e.Message); }
        }
        static void Post_AllMax(SkillsManager __instance)
        {
            try
            {
                if (parked == null) return;
                var list = __instance.m_Skills;
                if (list != null) { if (parkedAt <= list.Count) list.Insert(parkedAt, parked); else list.Add(parked); }
                parked = null;
            }
            catch (Exception e) { Info("all-max check (after): " + e.Message); }
        }

        // Keeps the journal entry in step with the stored points.
        internal static void SetPoints(float points)
        {
            try
            {
                var list = GameManager.GetSkillsManager()?.m_Skills;
                if (list == null) return;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && list[i].m_SkillType == Type && list[i].m_CurrentPoints != (int)points) list[i].m_CurrentPoints = (int)points;
            }
            catch { }
        }

        private static LocalizedString Loc(string key) => new LocalizedString { m_LocalizationID = key };

        private static void Dump(Skill s)
        {
            try
            {
                if (s == null) { Msg("skill: null"); return; }
                string tp = s.m_TierPoints == null ? "null" : string.Join(",", (int[])s.m_TierPoints);
                string b0 = s.m_TierLocalizedBenefits != null && s.m_TierLocalizedBenefits.Length > 0 ? s.m_TierLocalizedBenefits[0]?.m_LocalizationID : "none";
                Msg($"skill type={(int)s.m_SkillType} class={s.GetIl2CppType().Name} name='{s.m_DisplayName}' id='{s.m_LocalizedDisplayName?.m_LocalizationID}' tierPoints=[{tp}] points={s.m_CurrentPoints} tier={s.GetCurrentTierNumber()} icon='{s.m_SkillIcon}' iconBg='{s.m_SkillIconBackground}' image='{s.m_SkillImage}' benefits0='{b0}'");
            }
            catch (Exception e) { Info("dump failed: " + e.Message); }
        }

        // Puts our strings into the game's text table; if that does not take, falls back to answering lookups directly.
        private static bool EnsureText()
        {
            if (getHooked) return true;
            var table = Localization.s_CurrentLanguageStringTable;
            if (table == null) return false;
            try
            {
                if (!table.DoesKeyExist(NameKey))
                {
                    int langs = table.m_Languages != null ? table.m_Languages.Count : 1;
                    foreach (var kv in Text)
                    {
                        var e = table.DoesKeyExist(kv.Key) ? table.GetEntryFromKey(kv.Key) : table.AddEntryForKey(kv.Key);
                        if (e.m_Languages == null) e.m_Languages = new Il2CppSystem.Collections.Generic.List<string>();
                        for (int i = 0; i < langs; i++)
                        {
                            if (i < e.m_Languages.Count) e.m_Languages[i] = kv.Value; else e.m_Languages.Add(kv.Value);
                        }
                    }
                    textVerified = false;
                }
                if (!textVerified)
                {
                    string got = Localization.Get(NameKey);
                    if (got == Text[NameKey]) { textVerified = true; Msg("text added to the game's string table"); }
                    else { Info($"string table lookup returned '{got}'; answering lookups directly instead"); HookGet(); }
                }
            }
            catch (Exception e)
            {
                Info($"string table failed ({e.GetType().Name}: {e.Message}); answering lookups directly instead");
                HookGet();
            }
            return true;
        }

        private static void HookGet()
        {
            if (getHooked) return;
            getHooked = true;
            Mod.Instance.Hook(typeof(Localization), "Get", new[] { typeof(string) }, "Pre_LocGet", null, typeof(SkillEntry));
        }

        static bool Pre_LocGet(string key, ref string __result)
        {
            if (key != null && Text.TryGetValue(key, out var v)) { __result = v; return false; }
            return true;
        }

        // Journal hooks: make sure the entry exists before the page builds, then report what the page shows.
        static void Pre_PanelLog(Panel_Log __instance) { Ensure(); PickIcon(__instance); }

        static void Post_RefreshSkillsList(Panel_Log __instance)
        {
            try
            {
                var shown = __instance.m_SkillsDisplayList;
                int rows = shown == null ? -1 : shown.Count, ours = -1;
                for (int i = 0; i < rows; i++)
                    if (shown[i] != null && shown[i].m_Skill != null && shown[i].m_Skill.m_SkillType == Type) ours = i;
                var mgr = GameManager.GetSkillsManager();
                string state = $"journal skills page: {rows} rows, {(mgr?.m_Skills == null ? -1 : mgr.m_Skills.Count)} skills, Surveying row={ours}, offset={__instance.m_SkillListOffset}, selected={__instance.m_SkillListSelectedIndex}";
                if (state != lastPanelState) { lastPanelState = state; Msg(state); }
            }
            catch (Exception e) { if (!panelLogged) { panelLogged = true; Info("page report failed: " + e.Message); } }
        }

        // The small list icon comes from a sprite atlas. Prefer a survey/charcoal/map sprite if the atlas has one.
        private static void PickIcon(Panel_Log panel)
        {
            if (iconPicked) return;
            try
            {
                var mgr = GameManager.GetSkillsManager();
                var shown = panel.m_SkillsDisplayList;
                if (mgr?.m_Skills == null || shown == null || shown.Count == 0 || shown[0] == null || shown[0].m_SkillIcon == null) return;
                Skill ours = null;
                for (int i = 0; i < mgr.m_Skills.Count; i++) if (mgr.m_Skills[i] != null && mgr.m_Skills[i].m_SkillType == Type) ours = mgr.m_Skills[i];
                if (ours == null) return;
                iconPicked = true;
                var atlas = shown[0].m_SkillIcon.atlas;
                if (atlas == null || atlas.spriteList == null) { Info("icon atlas not readable; keeping the borrowed icon"); return; }
                string[] want = { "ico_skills_cartography", "cartograph", "survey", "charcoal", "map", "compass" };
                var found = new List<string>();
                for (int i = 0; i < atlas.spriteList.Count; i++)
                {
                    string n = atlas.spriteList[i]?.name;
                    if (n == null) continue;
                    string low = n.ToLowerInvariant();
                    foreach (var w in want) if (low.Contains(w)) { found.Add(n); break; }
                }
                Msg($"icon atlas '{atlas.name}' has {atlas.spriteList.Count} sprites; candidates: {(found.Count == 0 ? "none" : string.Join(", ", found))}");
                foreach (var big in new[] { "ico_skill_large_cartography", "ico_skill_large_surveying" })
                    if (atlas.GetSprite(big) != null) { BannerSprite = big; break; }
                Msg($"banner sprite '{BannerSprite}'; vanilla background sprite in this atlas: {(atlas.GetSprite(ours.m_SkillIconBackground) != null)}");
                foreach (var w in want)
                    foreach (var n in found)
                        if (n.ToLowerInvariant().Contains(w)) { ours.m_SkillIcon = n; IconName = n; Msg($"icon set to '{n}'"); return; }
            }
            catch (Exception e) { Info("icon pick failed: " + e.Message); }
        }
    }
}
