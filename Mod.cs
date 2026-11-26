// Surveying - stage 1: a logging-only build. It changes nothing in the game; it records which of the
// game's survey, map, weather, carry-weight and sprain functions actually run, and with what values,
// so the real perks can be attached to functions that are known to fire.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(Surveying.Mod), "Skilled Cartographer", "0.10.0", "ShreddyJim", "https://github.com/ShreddyJimMods/tld-skilled-cartographer/releases")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace Surveying
{
    public class Mod : MelonMod
    {
        internal static MelonLogger.Instance Log;
        internal static MelonPreferences_Entry<bool> StructHooks;
        internal static MelonPreferences_Entry<bool> Diagnostics;
        internal static MelonPreferences_Entry<bool> DetailedLog;
        internal static MelonPreferences_Entry<bool> PerfLog;
        internal static bool Detailed => DetailedLog != null && DetailedLog.Value;
        internal static MelonPreferences_Entry<bool> CompassVisible;
        internal static MelonPreferences_Entry<bool> JournalSkill;
        internal static Mod Instance;
        private float nextSummary, nextSkillCheck;

        public override void OnInitializeMelon()
        {
            Log = LoggerInstance;
            Instance = this;
            var cat = MelonPreferences.CreateCategory("Surveying");
            StructHooks = cat.CreateEntry("StructHooks", false, description:
                "Also hook the two game functions that pass a struct (map unlock parameters, altitude range boost). Off by default: this kind of hook can crash the game.");

            CompassVisible = cat.CreateEntry("CompassVisible", true, description: "Whether the HUD compass is currently switched on (toggled in game with the compass key).");
            DetailedLog = cat.CreateEntry("DetailedLog", false, description:
                "Write the detail of each survey, map opening, compass change and carry-weight change to the log. Off: only points, level-ups, region completions and problems are logged.");
            PerfLog = cat.CreateEntry("PerfLog", false, description:
                "Record frame times and how much of each slow frame was spent in this mod. Only useful when investigating stutter.");
            Diagnostics = cat.CreateEntry("Diagnostics", false, description:
                "Log every survey, map, weather, carry-weight and sprain call the mod watches. Only useful when investigating a problem.");
            if (Diagnostics.Value)
            {
                Hook(typeof(CharcoalItem), "StartDetailSurvey", null, "Pre_StartDetailSurvey");
                Hook(typeof(CharcoalItem), "CancelDetailSurvey", null, "Pre_CancelDetailSurvey");
                Hook(typeof(CharcoalItem), "DetailSurveyComplete", null, "Pre_DetailSurveyComplete");
                Hook(typeof(CharcoalItem), "ConsumeCharcoal", null, "Pre_ConsumeCharcoal");
                Hook(typeof(CharcoalItem), "HasSurveyVisibility", null, null, "Post_HasSurveyVisibility");
                Hook(typeof(CharcoalItem), "GetSurveyGameMinutes", null, null, "Post_GetSurveyGameMinutes");
                Hook(typeof(CharcoalItem), "GetSurveyRealSeconds", null, null, "Post_GetSurveyRealSeconds");
                Hook(typeof(Panel_Map), "DoDetailSurvey", null, "Pre_DoDetailSurvey", "Post_DoDetailSurvey");
                Hook(typeof(Panel_Map), "DoNearbyDetailsCheck", null, "Pre_DoNearbyDetailsCheck");
                Hook(typeof(Panel_Map), "ShouldAddSurveyPosition", null, null, "Post_ShouldAddSurveyPosition");
                Hook(typeof(Panel_Map), "Enable", new[] { typeof(bool), typeof(bool) }, "Pre_MapEnable");
                Hook(typeof(MapDetail), "Unlock", new[] { typeof(bool) }, "Pre_MapDetailUnlock");
                Hook(typeof(MapDetail), "Surveyed", null, "Pre_MapDetailSurveyed");
                Hook(typeof(MapDetailManager), "MaybeUnlockFaithfulCartographer", null, "Pre_FaithfulCartographer");
                Hook(typeof(Weather), "CanSurveyEnvironment", null, null, "Post_CanSurveyEnvironment");
                Hook(typeof(Encumber), "GetMaxCarryCapacityKG", null, "Pre_MaxCarry");
                Hook(typeof(Sprains), "MaybeSprainWhileMoving", null, "Pre_MaybeSprain");
                Hook(typeof(vp_FPSController), "GetSlopeMultiplier", null, null, "Post_SlopeMultiplier");
                Hook(typeof(Panel_Log), "BuildCartographyRegionList", null, null, "Post_BuildCartographyRegionList");
                if (StructHooks.Value)
                {
                    Hook(typeof(MapDetailManager), "UnlockMapDetailObjectsNearPosition", null, "Pre_UnlockNear", "Post_UnlockNear");
                    Hook(typeof(MapDetailUnlockParameters), "GetRangeBoostFromHeightDelta", null, null, "Post_RangeBoost");
                }
            }
            JournalSkill = cat.CreateEntry("JournalSkill", true, description:
                "Show Cartography in the journal's Skills list. Turn off if the Skills page misbehaves.");
            if (JournalSkill.Value)
            {
                Hook(typeof(Panel_Log), "Enable", new[] { typeof(bool) }, "Pre_PanelLog", null, typeof(SkillEntry));
                Hook(typeof(Panel_Log), "RefreshSkillsList", null, "Pre_PanelLog", "Post_RefreshSkillsList", typeof(SkillEntry));
                Hook(typeof(Panel_Log), "RefreshSelectedSkillDescriptionView", null, "Pre_PanelLog", null, typeof(SkillEntry));
            }

            Settings.Instance.AddToModSettings("Skilled Cartographer");
            Hook(typeof(CharcoalItem), "StartDetailSurvey", null, "Pre_Start", null, typeof(Perks));
            Hook(typeof(CharcoalItem), "CancelDetailSurvey", null, "Pre_Cancel", null, typeof(Perks));
            Hook(typeof(CharcoalItem), "ConsumeCharcoal", null, "Pre_Consume", null, typeof(Perks));
            Hook(typeof(Panel_Map), "DoNearbyDetailsCheck", null, "Pre_Check", "Post_Check", typeof(Perks));
            Hook(typeof(CharcoalItem), "HasSurveyVisibility", null, null, "Post_Visibility", typeof(Perks));
            Hook(typeof(Panel_Log), "BuildCartographyRegionList", null, null, "Post_RegionList", typeof(Perks));
            Hook(typeof(Panel_Log), "BuildCartographyRegionList", null, null, "Post_RegionList", typeof(World));
            Hook(typeof(SkillsManager), "AllSkillsAtMaxmiumLevel", null, "Pre_AllMax", "Post_AllMax", typeof(SkillEntry));
            Hook(typeof(vp_FPSController), "GetSlopeMultiplier", null, null, "Post_Speed", typeof(World));
            Hook(typeof(Sprains), "MaybeSprainWhileMoving", null, "Pre_Sprain", null, typeof(World));
            Hook(typeof(Encumber), "GetMaxCarryCapacityKG", null, "Pre_MaxCarry", null, typeof(World));
            foreach (var name in new[] { "GetEffectiveCarryCapacityKG", "GetNoSprintCarryCapacityKG", "GetEncumberLevel", "IsEncumbered", "Update" })
                Hook(typeof(Encumber), name, null, "Pre_Capacity", null, typeof(World));
            Hook(typeof(Panel_Map), "Enable", new[] { typeof(bool), typeof(bool) }, null, "Post_MapEnable", typeof(World));

            Log.Msg("loaded (logging, plus a Cartography entry in the journal's Skills list; perks: points, faster surveys, keep charcoal, radius, trail notes, Familiar Ground, bearings, position circle, altitude)");
        }

        internal void Hook(Type type, string name, Type[] args, string prefix, string postfix = null, Type holder = null)
        {
            try
            {
                MethodInfo m = args == null ? AccessTools.Method(type, name) : AccessTools.Method(type, name, args);
                if (m == null) { Log.Warning($"hook {type.Name}.{name}: not found"); return; }
                const BindingFlags f = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                HarmonyInstance.Patch(m,
                    prefix == null ? null : new HarmonyMethod((holder ?? typeof(Hooks)).GetMethod(prefix, f)),
                    postfix == null ? null : new HarmonyMethod((holder ?? typeof(Hooks)).GetMethod(postfix, f)));
                if (Detailed) Log.Msg($"hook {type.Name}.{name}: attached");
            }
            catch (Exception e) { Log.Error($"hook {type.Name}.{name}: {e.GetType().Name}: {e.Message}"); }
        }

        public override void OnUpdate() { Perf.FrameStart(); long t0 = Perf.Start(); World.KeyTick(); Compass.Frame(); Perf.StopFrame(t0); }

        public override void OnLateUpdate()
        {
            if (Time.realtimeSinceStartup >= nextSkillCheck)
            {
                nextSkillCheck = Time.realtimeSinceStartup + 1f;
                long t0 = Perf.Start();
                Perks.Sync();
                Perks.TrailTick();
                World.Tick();
                SkillEntry.Ensure();
                SkillEntry.SetPoints(Perks.Points);
                Perf.StopTick(t0);
            }
            if (!Diagnostics.Value || Time.realtimeSinceStartup < nextSummary) return;
            nextSummary = Time.realtimeSinceStartup + 60f;
            Hooks.Summary();
        }
    }

    internal static class Hooks
    {
        private static readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        private static readonly Dictionary<string, string> Last = new Dictionary<string, string>();
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private static float surveyWindowUntil;
        private static string lastSummary = "";

        private static void Msg(string s) => Mod.Log.Msg(s);

        private static void Safe(string where, Action a)
        {
            try { a(); }
            catch (Exception e) { if (Failed.Add(where)) Mod.Log.Error($"{where}: {e.GetType().Name}: {e.Message}"); }
        }

        private static void Count(string key) { Counts.TryGetValue(key, out int n); Counts[key] = n + 1; }

        // Logs only when the value differs from the last one logged under this key.
        private static void OnChange(string key, string value)
        {
            Count(key);
            if (Last.TryGetValue(key, out var old) && old == value) return;
            Last[key] = value;
            Msg($"{key} = {value}");
        }

        private static bool InSurveyWindow => Time.realtimeSinceStartup < surveyWindowUntil;
        private static void OpenSurveyWindow(float seconds) => surveyWindowUntil = Math.Max(surveyWindowUntil, Time.realtimeSinceStartup + seconds);

        internal static void Summary()
        {
            var sb = new StringBuilder();
            foreach (var kv in Counts) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("  ");
            string s = sb.ToString();
            if (s.Length == 0 || s == lastSummary) return;
            lastSummary = s;
            Msg("calls so far: " + s);
        }

        private static string Ctx()
        {
            var sb = new StringBuilder();
            Safe("ctx.region", () => sb.Append($"region={GameManager.TryGetCurrentRegionName()} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}"));
            Safe("ctx.player", () =>
            {
                var t = GameManager.GetPlayerTransform();
                if (t != null) sb.Append($" pos=({t.position.x:0.0}, {t.position.y:0.0}, {t.position.z:0.0}) heading={t.eulerAngles.y:0}");
            });
            Safe("ctx.weather", () =>
            {
                var w = GameManager.GetWeatherComponent();
                if (w != null) sb.Append($" weather={w.GetWeatherStage()} indoor={w.IsIndoorEnvironment()} denseFog={w.IsDenseFog()} blizzard={w.IsBlizzard()} clear={w.IsClear()} fullMoon={w.IsFullMoon()} canSurvey={w.CanSurveyEnvironment()}");
            });
            Safe("ctx.time", () =>
            {
                var tod = GameManager.GetTimeOfDayComponent();
                if (tod != null) sb.Append($" hour={tod.GetHour()} night={tod.IsNight()}");
            });
            return sb.ToString();
        }

        // ---- charcoal ----
        static void Pre_StartDetailSurvey(CharcoalItem __instance) => Safe("StartDetailSurvey", () =>
        {
            OpenSurveyWindow(120f);
            Msg($"CharcoalItem.StartDetailSurvey: minutes={__instance.m_SurveyGameMinutes} realSeconds={__instance.m_SurveyRealSeconds} skillExtendedHours={__instance.m_SurveySkillExtendedHours} | {Ctx()}");
        });
        static void Pre_CancelDetailSurvey() => Safe("CancelDetailSurvey", () => Msg("CharcoalItem.CancelDetailSurvey"));
        static void Pre_DetailSurveyComplete() => Safe("DetailSurveyComplete", () => { OpenSurveyWindow(10f); Msg("CharcoalItem.DetailSurveyComplete"); });
        static void Pre_ConsumeCharcoal(CharcoalItem __instance) => Safe("ConsumeCharcoal", () =>
        {
            var gi = __instance.m_GearItem;
            string units = "?";
            Safe("ConsumeCharcoal.units", () => { if (gi != null && gi.m_StackableItem != null) units = gi.m_StackableItem.m_Units.ToString(); });
            Msg($"CharcoalItem.ConsumeCharcoal: stack units before={units}");
        });
        static void Post_HasSurveyVisibility(float extendedHoursBonus, bool __result) => Safe("HasSurveyVisibility", () => OnChange("CharcoalItem.HasSurveyVisibility", $"{__result} (extendedHoursBonus={extendedHoursBonus})"));
        static void Post_GetSurveyGameMinutes(float __result) => Safe("GetSurveyGameMinutes", () => OnChange("CharcoalItem.GetSurveyGameMinutes", __result.ToString("0.##")));
        static void Post_GetSurveyRealSeconds(float __result) => Safe("GetSurveyRealSeconds", () => OnChange("CharcoalItem.GetSurveyRealSeconds", __result.ToString("0.##")));

        // ---- map panel ----
        static void Pre_DoDetailSurvey(Panel_Map __instance, SurveyType surveyType) => Safe("DoDetailSurvey", () =>
        {
            OpenSurveyWindow(10f);
            Msg($"Panel_Map.DoDetailSurvey: type={surveyType} radius={__instance.m_DetailSurveyRadiusMeters} rockCacheRadius={__instance.m_DetailSurveyRockCacheRadiusMeters} sprayRadius={__instance.m_DetailSurveySprayPaintRadiusMeters} polaroidRadius={__instance.m_DetailSurvayPolaroidRadiusMeters} iconRange={__instance.m_RangeToShowMapIcons} | {Ctx()}");
        });
        static void Post_DoDetailSurvey(Panel_Map __instance) => Safe("DoDetailSurvey.after", () =>
        {
            var d = __instance.m_DetailSurveyPositions;
            if (d == null) { Msg("Panel_Map.DoDetailSurvey done: no survey position table"); return; }
            var sb = new StringBuilder("Panel_Map.DoDetailSurvey done: survey positions per region:");
            foreach (var kv in d)
            {
                var list = kv.Value;
                int n = list == null ? 0 : list.Count;
                sb.Append($" {kv.Key}={n}");
                if (n > 0) { var p = list[n - 1]; sb.Append($"(last x={p.x} y={p.y} h={p.h} r={p.r})"); }
            }
            Msg(sb.ToString());
        });
        static void Pre_DoNearbyDetailsCheck(float radius, bool forceAddSurveyPosition, bool useOverridePosition, Vector3 overridePostion, bool shouldAllowVistaReveals) => Safe("DoNearbyDetailsCheck", () =>
        {
            Count("Panel_Map.DoNearbyDetailsCheck");
            string s = $"radius={radius} forceAdd={forceAddSurveyPosition} override={useOverridePosition} at=({overridePostion.x:0.0}, {overridePostion.y:0.0}, {overridePostion.z:0.0}) vistas={shouldAllowVistaReveals}";
            // Outside a survey this runs for other reasons (spray paint, rock caches, walking); log each distinct shape once.
            if (InSurveyWindow) Msg("Panel_Map.DoNearbyDetailsCheck (during survey): " + s);
            else OnChange("Panel_Map.DoNearbyDetailsCheck (other)", $"radius={radius} forceAdd={forceAddSurveyPosition} override={useOverridePosition} vistas={shouldAllowVistaReveals}");
        });
        static void Post_ShouldAddSurveyPosition(bool __result) => Safe("ShouldAddSurveyPosition", () => { Count("Panel_Map.ShouldAddSurveyPosition"); if (InSurveyWindow) Msg($"Panel_Map.ShouldAddSurveyPosition -> {__result}"); });
        static void Pre_MapEnable(bool enable, bool cameFromDetailSurvey) => Safe("MapEnable", () => { if (enable) Msg($"Panel_Map opened (cameFromDetailSurvey={cameFromDetailSurvey})"); });

        // ---- named locations ----
        static void Pre_MapDetailUnlock(MapDetail __instance, bool ignoreLogged) => Safe("MapDetail.Unlock", () =>
        {
            Count("MapDetail.Unlock");
            if (InSurveyWindow) Msg($"MapDetail.Unlock (during survey): {__instance.m_LocID} alreadyUnlocked={__instance.m_IsUnlocked} surveyRadius={__instance.m_SurveyRadius} requiresInteraction={__instance.m_RequiresInteraction} ignoreLogged={ignoreLogged}");
        });
        static void Pre_MapDetailSurveyed(MapDetail __instance) => Safe("MapDetail.Surveyed", () => { Count("MapDetail.Surveyed"); if (InSurveyWindow) Msg($"MapDetail.Surveyed: {__instance.m_LocID}"); });
        static void Pre_FaithfulCartographer() => Safe("FaithfulCartographer", () => { Count("MapDetailManager.MaybeUnlockFaithfulCartographer"); if (InSurveyWindow) Msg("MapDetailManager.MaybeUnlockFaithfulCartographer"); });

        // ---- journal: per-region survey progress (open Journal > Collections > Surveyed Locations to trigger) ----
        static void Post_BuildCartographyRegionList(Panel_Log __instance) => Safe("BuildCartographyRegionList", () =>
        {
            var list = __instance.m_SurveyRegionList;
            if (list == null) { Msg("Panel_Log.BuildCartographyRegionList: no list"); return; }
            Msg($"Panel_Log.BuildCartographyRegionList: {list.Count} regions");
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                int total = r.m_Achievements == null ? 0 : r.m_Achievements.Count, done = 0;
                for (int j = 0; j < total; j++) if (r.m_Achievements[j].m_Completed) done++;
                Msg($"  region {r.m_RegionName} scene={r.m_SceneName} unlocked={r.m_IsUnlocked} numComplete='{r.m_numComplete}' percent={r.m_CompletePercent:0.###} locations={done}/{total}");
            }
        });

        // ---- environment and gameplay numbers ----
        static void Post_CanSurveyEnvironment(bool __result) => Safe("CanSurveyEnvironment", () => OnChange("Weather.CanSurveyEnvironment", __result.ToString()));
        static void Pre_MaxCarry(Encumber __instance) => Safe("MaxCarry", () => Count("Encumber.GetMaxCarryCapacityKG"));
        static void Post_SlopeMultiplier(float __result) => Safe("SlopeMultiplier", () =>
        {
            Count("vp_FPSController.GetSlopeMultiplier");
        });
        static void Pre_MaybeSprain(Sprains __instance) => Safe("MaybeSprain", () =>
        {
            Count("Sprains.MaybeSprainWhileMoving");
            OnChange("Sprains fields", $"baseSlopeChance={__instance.m_BaseChanceWhenMovingOnSlope} perDegree={__instance.m_ChanceIncreaseEachDegreeAboveMinSlope} minSlope={__instance.m_MinSlopeDegreesForSprain} encumbered=+{__instance.m_ChanceIncreaseEncumbered} exhausted=+{__instance.m_ChanceIncreaseExhausted} sprinting=+{__instance.m_ChanceIncreaseSprinting}");
        });

        // ---- struct hooks (opt-in) ----
        static void Pre_UnlockNear(MapDetailUnlockParameters parameters) => Safe("UnlockNear", () =>
            Msg($"MapDetailManager.UnlockMapDetailObjectsNearPosition: at=({parameters.m_Position.x:0.0}, {parameters.m_Position.y:0.0}, {parameters.m_Position.z:0.0}) radius={parameters.m_Radius} lineOfSight={parameters.m_RequiresLineOfSight} ignoreHeight={parameters.m_IgnoreHeight} boostMinHeight={parameters.m_RangeBoostMinHeight} boostMaxHeight={parameters.m_RangeBoostMaxHeight} boostAmount={parameters.m_RangeBoostAmount}"));
        static void Post_UnlockNear(bool __result) => Safe("UnlockNear.after", () => Msg($"MapDetailManager.UnlockMapDetailObjectsNearPosition -> {__result}"));
        static void Post_RangeBoost(float heightDelta, float __result) => Safe("RangeBoost", () => Msg($"MapDetailUnlockParameters.GetRangeBoostFromHeightDelta({heightDelta:0.0}) -> {__result:0.###}"));
    }
}
