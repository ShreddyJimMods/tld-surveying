// Stages 4-5: Familiar Ground (rewards inside a fully surveyed region), sun and star bearings,
// the rough position circle on the map, and the altitude bonus.
using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;
using UnityEngine;

namespace Surveying
{
    internal static class World
    {
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private static void Msg(string s) { if (Mod.Detailed) Mod.Log.Msg("[world] " + s); }   // detail, only with DetailedLog on
        private static void Info(string s) => Mod.Log.Msg("[world] " + s);
        private static void Safe(string where, Action a)
        {
            try { a(); }
            catch (Exception e) { if (Failed.Add(where)) Mod.Log.Error($"[world] {where}: {e}"); }
        }

        // ---- where the player is, refreshed once a second ----
        internal static string Region = "";
        internal static bool Outdoors;
        internal static string Weather = "";
        internal static bool InGame, SkyVisible;    // sky: clear enough to read direction from the Sun or stars
        internal static bool FamiliarHere;          // current region is fully surveyed
        internal static bool SpeedActive;           // read by the movement hook, so kept as a plain flag
        internal static float SpeedMult = 1f;

        internal static void Tick()
        {
            try
            {
                var s = Settings.Instance;
                string scene = GameManager.m_ActiveScene;
                bool inGame = !string.IsNullOrEmpty(scene) && !scene.StartsWith("MainMenu") && scene != "Boot" && scene != "Empty";
                var w = inGame ? GameManager.GetWeatherComponent() : null;
                InGame = w != null;
                if (w == null) { FamiliarHere = false; SpeedActive = false; SkyVisible = false; return; }
                Region = GameManager.TryGetCurrentRegionName() ?? "";
                Outdoors = !w.IsIndoorEnvironment();
                bool was = FamiliarHere;
                FamiliarHere = s.familiarGround && (Perks.Familiar.Contains(Region) || s.familiarTest);
                if (FamiliarHere != was) Info($"Familiar Ground {(FamiliarHere ? "active" : "inactive")} in '{Region}'{(s.familiarTest ? " (testing switch on)" : "")}");
                string stage = w.GetWeatherStage().ToString();
                Weather = stage;
                SkyVisible = Outdoors && (w.IsClear() || stage == "PartlyCloudy" || stage == "ClearAurora" || (FamiliarHere && stage == "LightFog")
                    || (Perks.Level >= Mathf.RoundToInt(s.compassLightWeatherLevel) && (stage == "Cloudy" || stage == "LightSnow" || stage == "LightFog")));
                SpeedMult = 1f + s.familiarSpeedPercent / 100f;
                SpeedActive = FamiliarHere && Outdoors && s.familiarSpeedPercent > 0f;
                ApplyCarry(FamiliarHere ? s.familiarCarryKG : 0f);
                LogCarry();
                if (Region != northRegion) { var pt = GameManager.GetPlayerTransform(); if (pt != null) Bearing(pt); } // make sure this region's map directions are known
                SunSample();
                UpdateSunNorth();
            }
            catch (Exception e) { if (Failed.Add("tick")) Mod.Log.Error("[world] tick: " + e); }
        }

        // ---- movement speed (same function the SonicMode mod uses) ----
        internal static void Post_Speed(ref float __result)
        {
            if (SpeedActive) __result *= SpeedMult;
        }

        // ---- carry capacity ----
        // Added to the game's own capacity buff (the green "+x" in the inventory), which is not saved.
        // The game rebuilds that buff now and then, so this runs whenever capacity is asked for and re-adds it.
        private static IntPtr carryPtr; private static long carryAdded, carryExpect;
        private static float carryWantKG;
        private static void ApplyCarry(float kg)
        {
            carryWantKG = kg;
            var enc = GameManager.GetEncumberComponent();
            if (enc != null) Pre_MaxCarry(enc);
        }
        private static string carryPath = "";
        internal static void Pre_MaxCarry(Encumber __instance)
        {
            long perf = Perf.Start();
            try
            {
                long want = (long)Math.Round(carryWantKG * 1e9);
                // The buff the inventory shows in green lives on the player manager; fall back to the
                // encumbrance component's copy, and last of all to the base capacity (works, but not shown in green).
                var pm = GameManager.GetPlayerManagerComponent();
                var bv = pm != null ? pm.m_CarryingCapacityBuffValues : null;
                string path = "player buff";
                if (bv == null) { bv = __instance.m_CarryingCapacityBuffValues; path = "encumber buff"; }
                IntPtr ptr; long cur;
                if (bv != null) { ptr = bv.Pointer; cur = bv.m_MaxCarryCapacityBuff.m_Units; }
                else { path = "base capacity"; ptr = __instance.Pointer; cur = __instance.m_MaxCarryCapacity.m_Units; }
                // A different target, or a value the game has rebuilt, means nothing of ours is in it any more.
                if (ptr != carryPtr || cur != carryExpect) carryAdded = 0;
                carryPtr = ptr;
                if (want != carryAdded)
                {
                    long d = want - carryAdded;
                    if (bv != null) bv.m_MaxCarryCapacityBuff = new ItemWeight(cur + d);
                    else
                    {
                        __instance.m_MaxCarryCapacity = new ItemWeight(cur + d);
                        __instance.m_MaxCarryCapacityWhenExhausted = new ItemWeight(__instance.m_MaxCarryCapacityWhenExhausted.m_Units + d);
                    }
                    if (Failed.Add("carrylog" + path + want)) Msg($"carry bonus via {path}: {cur / 1e9:0.##} -> {(cur + d) / 1e9:0.##} kg");
                    cur += d; carryAdded = want;
                }
                carryExpect = cur;
                if (path != carryPath) { carryPath = path; Msg($"carry bonus target: {path} (player buff {(pm?.m_CarryingCapacityBuffValues == null ? "missing" : "present")}, encumber buff {(__instance.m_CarryingCapacityBuffValues == null ? "missing" : "present")})"); }
            }
            catch (Exception e) { if (Failed.Add("carry")) Mod.Log.Error("[world] carry: " + e); }
            finally { Perf.StopHook(perf); }
        }

        // The same top-up, run before the other capacity questions the game asks (sprint limit, encumbrance
        // level, the HUD ring), so they all see the bonus and agree with each other.
        internal static void Pre_Capacity(Encumber __instance) => Pre_MaxCarry(__instance);

        private static string lastCarryState = "";
        private static void LogCarry()
        {
            try
            {
                if (!Mod.Detailed) return;
                var enc = GameManager.GetEncumberComponent();
                if (enc == null) return;
                string state = $"level {enc.GetEncumberLevel()}, encumbered {enc.IsEncumbered()}";
                if (state == lastCarryState) return;
                lastCarryState = state;
                var pm = GameManager.GetPlayerManagerComponent();
                long buff = pm != null && pm.m_CarryingCapacityBuffValues != null ? pm.m_CarryingCapacityBuffValues.m_MaxCarryCapacityBuff.m_Units : 0;
                Msg($"carry: {state}; weight {enc.m_GearWeightKG.m_Units / 1e9:0.##}, max {enc.GetMaxCarryCapacityKG().m_Units / 1e9:0.##}, effective {enc.GetEffectiveCarryCapacityKG().m_Units / 1e9:0.##}, no-sprint {enc.GetNoSprintCarryCapacityKG().m_Units / 1e9:0.##} (base {enc.m_NoSprintCarryCapacity.m_Units / 1e9:0.##}), no-walk base {enc.m_NoWalkCarryCapacity.m_Units / 1e9:0.##}, base max {enc.m_MaxCarryCapacity.m_Units / 1e9:0.##}, buff {buff / 1e9:0.##}, ours {carryAdded / 1e9:0.##}");
            }
            catch (Exception e) { if (Failed.Add("carrylog2")) Mod.Log.Error("[world] carry log: " + e); }
        }

        // ---- sprain risk ----
        private static IntPtr sprainPtr; private static float sprainBase, sprainSet;
        internal static void Pre_Sprain(Sprains __instance)
        {
            long perf = Perf.Start();
            try
            {
                float cur = __instance.m_BaseChanceWhenMovingOnSlope;
                if (__instance.Pointer != sprainPtr || !Mathf.Approximately(cur, sprainSet)) sprainBase = cur;
                sprainPtr = __instance.Pointer;
                float want = FamiliarHere ? sprainBase * (1f - Settings.Instance.familiarSprainPercent / 100f) : sprainBase;
                if (!Mathf.Approximately(cur, want))
                {
                    __instance.m_BaseChanceWhenMovingOnSlope = want;
                    Msg($"sprain base chance {cur:0.##} -> {want:0.##}");
                }
                sprainSet = want;
            }
            catch (Exception e) { if (Failed.Add("sprain")) Mod.Log.Error("[world] sprain: " + e); }
            finally { Perf.StopHook(perf); }
        }

        // ---- region completion ----
        // Reads the journal's region list whenever the game builds it.
        internal static void Post_RegionList(Panel_Log __instance) => Safe("RegionList", () =>
        {
            var regions = __instance.m_SurveyRegionList;
            if (regions == null) return;
            bool changed = false;
            for (int i = 0; i < regions.Count; i++) if (!string.IsNullOrEmpty(regions[i]?.m_SceneName)) Listed.Add(regions[i].m_SceneName);
            for (int i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                var a = r?.m_Achievements;
                if (a == null || a.Count == 0 || string.IsNullOrEmpty(r.m_SceneName)) continue;
                if (r.m_SceneName.Contains("TransitionZone")) continue; // connector zones do not count
                bool all = true;
                for (int j = 0; j < a.Count && all; j++) all = a[j] != null && a[j].m_Completed;
                if (all && Perks.Familiar.Add(r.m_SceneName))
                {
                    changed = true;
                    Info($"region complete: {r.m_RegionName} ({r.m_SceneName})");
                    Perks.Announce("Familiar Ground");
                    Perks.Announce($"All named locations surveyed in {r.m_RegionName}");
                }
            }
            if (changed) Perks.PersistNow();
        });

        // Regions the journal has no location list for (the Far Territory ones): judged from the named
        // locations in the loaded outdoor scene instead. Checked after each survey while standing in the region.
        private static readonly HashSet<string> Listed = new HashSet<string>();
        internal static void CheckUnlistedRegion() => Safe("Unlisted", () =>
        {
            if (!Outdoors || string.IsNullOrEmpty(Region) || Listed.Count == 0 || Listed.Contains(Region)) return;
            if (Region.Contains("Transition") || Region.Contains("TrackSection") || Perks.Familiar.Contains(Region)) return;
            var all = UnityEngine.Object.FindObjectsOfType<MapDetail>();
            int named = 0, done = 0; var types = new Dictionary<string, int>(); var missing = new List<string>();
            foreach (var md in all)
            {
                if (md == null) continue;
                string type = md.m_IconType.ToString();
                types.TryGetValue(type, out int c); types[type] = c + 1;
                // Named places only: not resources, containers or corpses.
                if (string.IsNullOrEmpty(md.m_LocID) || type.Contains("Detail") || type.Contains("Resource")) continue;
                named++;
                if (md.m_IsUnlocked) done++; else if (missing.Count < 12) missing.Add(md.m_LocID);
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in types) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            Info($"unlisted region {Region}: {done}/{named} named locations on the map (icon types: {sb}); missing: {string.Join(", ", missing)}");
            if (named >= 5 && done == named && Perks.Familiar.Add(Region))
            {
                Perks.Announce("Familiar Ground");
                Perks.Announce("All named locations surveyed in this Region");
                Perks.PersistNow();
            }
        });

        // After a survey, ask the journal to rebuild its list so a region completed just now is noticed.
        internal static void RecheckRegions() => Safe("Recheck", () =>
        {
            var log = InterfaceManager.GetPanel<Panel_Log>();
            if (log != null) log.BuildCartographyRegionList();
            CheckUnlistedRegion();
        });

        // ---- which way is north ----
        // North means "up on this region's map". Region maps are not all drawn the same way round relative
        // to the game world, so the world-to-map conversion is sampled once per region and reused.
        private static string northRegion = ""; private static Vector2 mapEast = new Vector2(1f, 0f), mapNorthward = new Vector2(0f, 1f);
        internal static float Bearing(Transform player)
        {
            float yaw = player.eulerAngles.y;
            try
            {
                if (Region != northRegion && !string.IsNullOrEmpty(Region))
                {
                    var map = InterfaceManager.GetPanel<Panel_Map>();
                    if (map != null)
                    {
                        Vector3 p = player.position;
                        Vector3 m0 = map.WorldPositionToMapPosition(Region, p), mx = map.WorldPositionToMapPosition(Region, p + new Vector3(100f, 0f, 0f)), mz = map.WorldPositionToMapPosition(Region, p + new Vector3(0f, 0f, 100f));
                        var ex = new Vector2(mx.x - m0.x, mx.y - m0.y); var ez = new Vector2(mz.x - m0.x, mz.y - m0.y);
                        if (ex.magnitude > 0.5f && ez.magnitude > 0.5f) { mapEast = ex.normalized; mapNorthward = ez.normalized; } // per unit of world x and world z
                        else { mapEast = new Vector2(1f, 0f); mapNorthward = new Vector2(0f, 1f); }
                        northRegion = Region;
                        float zBearing = Mathf.Atan2(mapNorthward.x, mapNorthward.y) * Mathf.Rad2Deg;
                        string sun = "";
                        Safe("sun", () =>
                        {
                            var uni = GameManager.GetUniStorm(); var tod = GameManager.GetTimeOfDayComponent();
                            if (uni == null || uni.m_SunLight == null) return;
                            Vector3 toSun = -uni.m_SunLight.transform.forward;
                            Vector2 sm = mapEast * toSun.x + mapNorthward * toSun.z;
                            float az = (Mathf.Atan2(sm.x, sm.y) * Mathf.Rad2Deg + 360f) % 360f;
                            sun = $"; sun now at map bearing {az:0} deg, {Mathf.Asin(Mathf.Clamp(toSun.y, -1f, 1f)) * Mathf.Rad2Deg:0} deg above the horizon, hour {tod?.GetHour()}, game sun angle setting {uni.m_SunAngle:0.#}";
                        });
                        Safe("orientation", () =>
                        {
                            var w = GameManager.GetWeatherComponent();
                            if (w == null) return;
                            sun += $"; game sun rotation for this region {w.GetRotationDegreesForRegion(Region):0.#}";
                            if (Failed.Add("orientation-table") && w.m_RegionOrientationValues != null)
                            {
                                var sb = new System.Text.StringBuilder();
                                for (int i = 0; i < w.m_RegionOrientationValues.Length; i++)
                                {
                                    var o = w.m_RegionOrientationValues[i];
                                    if (o != null) sb.Append('[').Append(i).Append("] ").Append(o.m_RegionName).Append('=').Append(o.m_RotationDegrees.ToString("0.#")).Append("  ");
                                }
                                Msg("game sun rotation table: " + sb);
                            }
                        });
                        Msg($"north for {Region}: world +z points to map bearing {(zBearing + 360f) % 360f:0} deg (0 = map up){sun}");
                    }
                }
            }
            catch (Exception e) { if (Failed.Add("north")) Mod.Log.Error("[world] north: " + e); }
            // North is where the sky says it is: directly away from the midday sun in this region.
            return ((yaw - SunNorthWorld) % 360f + 360f) % 360f;
        }

        // World bearing (0 = world +z, clockwise) of north as the sun shows it in the current region.
        // Worked out from the game's per-region sun rotation, and replaced by a direct measurement of the
        // sun's path once three sightings have been taken there.
        internal static float SunNorthWorld;
        private static readonly Dictionary<string, float> sunMeasuredMidday = new Dictionary<string, float>();
        private static void UpdateSunNorth()
        {
            try
            {
                var w = GameManager.GetWeatherComponent();
                if (w == null || string.IsNullOrEmpty(Region)) return;
                // Measured in Mystery Lake: midday sun at world bearing 258 with a region value of 78, and the value turns the sun clockwise.
                float midday = 180f + w.GetRotationDegreesForRegion(Region);
                if (sunMeasuredMidday.TryGetValue(Region, out float measured)) midday = measured;
                SunNorthWorld = ((midday + 180f) % 360f + 360f) % 360f;
            }
            catch (Exception e) { if (Failed.Add("sunnorth")) Mod.Log.Error("[world] sun north: " + e); }
        }

        // The same north as a direction on this region's map (for the arrow drawn there).
        internal static Vector2 SunNorthOnMap()
        {
            float r = SunNorthWorld * Mathf.Deg2Rad;
            Vector2 v = mapEast * Mathf.Sin(r) + mapNorthward * Mathf.Cos(r);
            return v.sqrMagnitude > 1e-6f ? v.normalized : new Vector2(0f, 1f);
        }

        // ---- where the sun's path really lies (measurement only, changes nothing) ----
        // Three sightings of the sun a minute apart fix the circle it travels on. The top of that circle is
        // where the sun stands at midday, which on a consistent map would be due south (bearing 180).
        private static readonly List<Vector3> sunDirs = new List<Vector3>();
        private static float nextSunSample; private static string sunRegion = ""; private static float lastSunLogged = -999f;
        private static void SunSample()
        {
            try
            {
                if (!Outdoors || Region != northRegion || Time.realtimeSinceStartup < nextSunSample) return;
                var uni = GameManager.GetUniStorm(); var tod = GameManager.GetTimeOfDayComponent();
                if (uni == null || uni.m_SunLight == null || tod == null || tod.IsNight()) return;
                Vector3 w = -uni.m_SunLight.transform.forward;
                if (w.y < 0.05f) return;
                nextSunSample = Time.realtimeSinceStartup + 60f;
                if (Region != sunRegion) { sunDirs.Clear(); sunRegion = Region; lastSunLogged = -999f; }
                sunDirs.Add(w.normalized);
                if (sunDirs.Count > 3) sunDirs.RemoveAt(0);
                if (sunDirs.Count < 3) return;
                Vector3 n = Vector3.Cross(sunDirs[1] - sunDirs[0], sunDirs[2] - sunDirs[1]);
                if (n.magnitude < 1e-5f) return;                         // the sun has not moved enough between sightings
                n.Normalize();
                Vector3 centre = n * Vector3.Dot(n, sunDirs[0]);
                Vector3 up = Vector3.up - n * n.y;
                if (up.magnitude < 1e-3f) return;
                Vector3 top = centre + up.normalized * (sunDirs[0] - centre).magnitude;
                float midday = (Mathf.Atan2(top.x, top.z) * Mathf.Rad2Deg + 360f) % 360f;
                float nowBearing = (Mathf.Atan2(sunDirs[2].x, sunDirs[2].z) * Mathf.Rad2Deg + 360f) % 360f;
                sunMeasuredMidday[Region] = midday;
                if (Mathf.Abs(Mathf.DeltaAngle(midday, lastSunLogged)) < 4f) return;
                lastSunLogged = midday;
                var wc = GameManager.GetWeatherComponent();
                float value = wc != null ? wc.GetRotationDegreesForRegion(Region) : float.NaN;
                Vector2 onMap = mapEast * Mathf.Sin(midday * Mathf.Deg2Rad) + mapNorthward * Mathf.Cos(midday * Mathf.Deg2Rad);
                Msg($"sun path in {Region}: midday sun at world bearing {midday:0} (map bearing {(Mathf.Atan2(onMap.x, onMap.y) * Mathf.Rad2Deg + 360f) % 360f:0}); compass north now set to world bearing {(midday + 180f) % 360f:0}; game value for region {value:0.#}; hour {tod.GetHour()}");
            }
            catch (Exception e) { if (Failed.Add("sunsample")) Mod.Log.Error("[world] sun sample: " + e); }
        }

        // ---- sun and star bearings ----
        private static readonly string[] Dir8 = { "North", "North-East", "East", "South-East", "South", "South-West", "West", "North-West" };
        private static readonly string[] Dir16 = { "North", "North-North-East", "North-East", "East-North-East", "East", "East-South-East", "South-East", "South-South-East", "South", "South-South-West", "South-West", "West-South-West", "West", "West-North-West", "North-West", "North-North-West" };

        // The game's own key check first (it respects menus); plain Unity input as a fallback.
        private static bool gameInputBroken, unityInputBroken;
        private static bool KeyDown(KeyCode key)
        {
            if (!gameInputBroken)
                try { if (InputManager.GetKeyDown(InputManager.m_CurrentContext, key)) return true; }
                catch (Exception e) { gameInputBroken = true; Info("game key check unavailable: " + e.Message); }
            if (!unityInputBroken)
                try { if (Input.GetKeyDown(key)) return true; }
                catch (Exception e) { unityInputBroken = true; Info("Unity key check unavailable: " + e.Message); }
            return false;
        }

        internal static void KeyTick()
        {
            try
            {
                var s = Settings.Instance;
                if (!s.bearings || s.bearingsKey == KeyCode.None) return;
                if (!KeyDown(s.bearingsKey)) return;
                // The key shows or hides the compass strip; no text. The choice is remembered between sessions.
                Mod.CompassVisible.Value = !Mod.CompassVisible.Value;
                MelonLoader.MelonPreferences.Save();
                Msg($"compass {(Mod.CompassVisible.Value ? "shown" : "hidden")} by key");
            }
            catch (Exception e) { if (Failed.Add("key")) Mod.Log.Error("[world] key: " + e); }
        }

        // ---- altitude bonus ----
        // The game widens a survey made from high ground when it draws the map. That widening is scaled here,
        // per region map, and put back when the level or setting no longer calls for it.
        private static readonly Dictionary<IntPtr, Vector2> FogBase = new Dictionary<IntPtr, Vector2>(); // x = base, y = last set
        internal static float AltitudeMult
        {
            get
            {
                if (!Settings.Instance.altitudeBonus) return 1f;
                int l = Perks.Level;
                return l >= 5 ? 2f : l == 4 ? 1.5f : l == 3 ? 1.25f : 1f;
            }
        }
        private static void ApplyAltitude(Panel_Map map) => Safe("Altitude", () =>
        {
            var dict = map.m_FogOfWar;
            if (dict == null) return;
            float mult = AltitudeMult;
            foreach (var kv in dict)
            {
                var fog = kv.Value;
                if (fog == null) continue;
                float cur = fog.m_RangeBoostMaxAmount;
                if (!FogBase.TryGetValue(fog.Pointer, out var b) || !Mathf.Approximately(cur, b.y)) b = new Vector2(cur, cur);
                float want = b.x * mult;
                if (!Mathf.Approximately(cur, want))
                {
                    fog.m_RangeBoostMaxAmount = want; fog.m_Redraw = true;
                    Msg($"altitude boost for {kv.Key}: {cur:0.##} -> {want:0.##} (heights {fog.m_RangeBoostMinMaxHeight.x:0}-{fog.m_RangeBoostMinMaxHeight.y:0})");
                }
                else if (Failed.Add("fog:" + kv.Key)) Msg($"altitude boost for {kv.Key}: {cur:0.##} unchanged (heights {fog.m_RangeBoostMinMaxHeight.x:0}-{fog.m_RangeBoostMinMaxHeight.y:0})");
                FogBase[fog.Pointer] = new Vector2(b.x, want);
            }
        });

        // ---- rough position circle on the map ----
        private static float uiScale = 1f;     // map position -> the map panel's own units, learned from the game's player icon
        private static bool dumped;

        internal static void NewOffset()
        {
            float a = UnityEngine.Random.value * Mathf.PI * 2f, d = 0.35f + UnityEngine.Random.value * 0.4f;
            Perks.Offset = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
        }

        internal static void Post_MapEnable(Panel_Map __instance, bool enable, bool cameFromDetailSurvey) => Safe("MapEnable", () =>
        {
            foreach (var g in made) if (g != null) UnityEngine.Object.Destroy(g);
            made.Clear();
            if (!enable) return;
            ApplyAltitude(__instance);
            var s = Settings.Instance;
            var t = GameManager.GetPlayerTransform();
            if (t == null || __instance.m_PlayerIcon == null) return;
            string scene = GameManager.TryGetCurrentRegionName() ?? GameManager.m_ActiveScene; // maps are per region, also when indoors
            var icon = __instance.m_PlayerIcon;
            Vector3 mp = __instance.WorldPositionToMapPosition(scene, t.position);
            Vector3 east = __instance.WorldPositionToMapPosition(scene, t.position + new Vector3(100f, 0f, 0f));
            float mapScale = Mathf.Max(0.01f, Mathf.Sqrt((east.x - mp.x) * (east.x - mp.x) + (east.y - mp.y) * (east.y - mp.y)) / 100f);
            // Right after a survey the game shows its own player icon at the true spot: use it to learn the panel's units.
            Vector3 ip = icon.localPosition;
            if (cameFromDetailSurvey && icon.gameObject.activeInHierarchy && Mathf.Max(Mathf.Abs(mp.x), Mathf.Abs(mp.y)) > 20f)
            {
                float k = Mathf.Abs(mp.x) >= Mathf.Abs(mp.y) ? ip.x / mp.x : ip.y / mp.y;
                if (k > 0.05f && k < 50f && Mathf.Abs(ip.x - mp.x * k) < 6f && Mathf.Abs(ip.y - mp.y * k) < 6f) uiScale = k;
            }
            Msg($"map opened: player on map ({mp.x:0},{mp.y:0}), game icon at ({ip.x:0},{ip.y:0}) active={icon.gameObject.activeInHierarchy} parent='{icon.parent?.name}', map scale {mapScale:0.###}, panel scale {uiScale:0.###}");
            if (!dumped) { dumped = true; Dump(__instance); }

            if (s.mapNorthArrow && s.bearings && Perks.Level >= Mathf.RoundToInt(s.bearingsLevel) && northRegion == scene)
                Safe("cornercompass", () => CornerCompass(__instance, icon));

            if (!s.positionCircle || !Outdoors) return;
            bool tight = FamiliarHere;
            if (Perks.Level < Mathf.RoundToInt(s.circleLevel) && !tight) return;
            float lv = Mathf.Clamp01((Perks.Level - s.circleLevel) / Mathf.Max(1f, 5f - s.circleLevel));
            float radius = tight ? s.circleRadiusTight : Mathf.Lerp(s.circleRadiusLow, s.circleRadiusTight, lv);
            if (s.deadReckoning && !tight)
            {
                float grown = radius + Perks.Walked / 100f * s.deadReckoningGrowth;
                radius = Mathf.Min(grown, radius * Mathf.Max(1f, s.deadReckoningCap));
            }
            if (Perks.Offset == Vector2.zero) NewOffset();
            Vector3 centre = t.position + new Vector3(Perks.Offset.x, 0f, Perks.Offset.y) * radius;
            Vector3 cm = __instance.WorldPositionToMapPosition(scene, centre);
            int layer = icon.gameObject.layer, depth = 1;
            Safe("depth", () =>
            {
                var pmi = __instance.m_AreaPrefab != null ? __instance.m_AreaPrefab.GetComponent<MapIcon>() : null;
                if (pmi != null && pmi.m_AreaZoneSprite != null) depth = pmi.m_AreaZoneSprite.depth;
            });
            // Preferred: a soft-edged smudge drawn from a generated texture. The player is inside the solid part.
            int soft = Mathf.RoundToInt(2f * radius * mapScale * uiScale * (s.circleSketch ? 1.04f : 1f / SolidPart));
            var tex = MakeSoft(icon.parent, new Vector3(cm.x * uiScale, cm.y * uiScale, ip.z), soft, new Color(1f, 1f, 1f, s.circleSketch ? 0.38f : 0.55f), depth, layer);
            if (tex != null) Msg($"position circle (soft): radius {radius:0} m -> {soft} panel units at ({cm.x * uiScale:0},{cm.y * uiScale:0}), offset ({Perks.Offset.x:0.00},{Perks.Offset.y:0.00}), depth {depth}, shader '{tex.shader?.name}'");
            else DrawSpriteRing(__instance, icon, cm, ip, radius, mapScale);
            if (tex != null && s.circleSketch)
                Safe("sketch", () => SketchRing(icon.parent, new Vector2(cm.x * uiScale, cm.y * uiScale), radius * mapScale * uiScale, ip.z, depth + 1, layer));

            // Familiar Ground: the trail you have walked since your last survey, as a dotted line.
            if (FamiliarHere && s.familiarTrailLine && tex != null && Perks.TrailScene == GameManager.m_ActiveScene && Perks.TrailPoints.Count > 0)
            {
                var pts = Perks.TrailPoints; int n = 0;
                bool ringShown = tex != null; Vector2 ringCentre = new Vector2(cm.x * uiScale, cm.y * uiScale); float ringRadius = radius * mapScale * uiScale;
                var ink = new Color(0.10f, 0.095f, 0.09f, 1f); // the dark warm grey of the map's own charcoal lines
                                // Notes in map space, split into runs wherever the notes were interrupted, then smoothed into a flowing curve.
                var runs = new List<List<Vector2>>(); List<Vector2> cur = null;
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 m3 = __instance.WorldPositionToMapPosition(scene, new Vector3(pts[i].x, pts[i].y, pts[i].z)) * uiScale;
                    var m = new Vector2(m3.x, m3.y);
                    if (cur == null || (m - cur[cur.Count - 1]).magnitude >= 80f) { cur = new List<Vector2>(); runs.Add(cur); }
                    cur.Add(m);
                }
                foreach (var run in runs)
                {
                    var path = Smooth(run);
                    // Same scribble every time the map opens: the randomness is seeded from where the run starts.
                    var rng = new System.Random((int)(run[0].x * 31f + run[0].y * 17f) ^ run.Count);
                    float Rand(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
                    float seed = Rand(0f, 100f);
                    float along = 0f, nextStart = 0f, dashLen = Rand(4.6f, 5.4f);
                    bool inDash = true; Vector2 start = path[0]; float wander0 = 0f;
                    for (int i = 1; i < path.Count; i++)
                    {
                        Vector2 p0 = path[i - 1], d = path[i] - p0; float len = d.magnitude, done = 0f;
                        if (len < 0.01f) continue;
                        Vector2 dir = d / len, side = new Vector2(-dir.y, dir.x);
                        while (done < len)
                        {
                            float target = nextStart + dashLen;               // where the current dash or gap ends
                            float part = Mathf.Min(len - done, target - along);
                            done += part; along += part;
                            if (along < target - 0.001f) continue;            // segment ran out first; carry on in the next one
                            Vector2 here = p0 + dir * done;
                            // The pencil drifts off the true line slowly (wander) and each stroke is a little off (jitter).
                            float wander = (Mathf.PerlinNoise(along * 0.035f, seed) - 0.5f) * 1.5f;
                            // The trail stops where the uncertain area begins: inside the ring nothing is exact.
                            bool insideRing = ringShown && ((start + here) * 0.5f - ringCentre).magnitude < ringRadius * 1.03f;
                            if (inDash && insideRing) { dashLen = Rand(2.0f, 2.5f); }
                            else if (inDash)
                            {
                                Vector2 a0 = start + side * (wander0 + Rand(-0.15f, 0.15f)), a1 = here + side * (wander + Rand(-0.15f, 0.15f));
                                // One charcoal stroke: soft-edged, each a little different in weight and darkness.
                                var stroke = new Color(ink.r, ink.g, ink.b, Rand(0.86f, 0.94f));
                                if (n < 600 && Line(icon.parent, new Vector3(a0.x, a0.y, 0f), new Vector3(a1.x, a1.y, 0f), ip.z, Rand(1.45f, 1.6f), stroke, depth + 1, layer, StrokeTexture())) n++;
                                dashLen = Rand(2.0f, 2.5f);                   // a short lift of the charcoal before the next stroke
                            }
                            else { start = here; wander0 = wander; dashLen = Rand(4.6f, 5.4f); } // the next stroke
                            inDash = !inDash; nextStart = along;
                        }
                    }
                }
                Msg($"trail line: {n} charcoal strokes from {pts.Count} notes");
            }
        });

        // A hand-sketched charcoal ring round the uncertain area, in the same ink as the map's own lines:
        // one loose, slightly wobbly pass, so it reads as "roughly here" rather than a precise boundary.
        private static void SketchRing(Transform parent, Vector2 centre, float radius, float z, int depth, int layer)
        {
            var rng = new System.Random((int)(Perks.Offset.x * 1000f) * 31 + (int)(Perks.Offset.y * 1000f));
            float Rand(float lo, float hi) => lo + (float)rng.NextDouble() * (hi - lo);
            var ink = new Color(0.10f, 0.095f, 0.09f, 1f);
            int strokes = 0;
            for (int pass = 0; pass < 1; pass++)
            {
                float seed = Rand(0f, 50f), start = Rand(0f, 360f), r0 = radius;
                float circumference = 2f * Mathf.PI * r0, stroke = 9f, gap = pass == 0 ? 4f : 7f;
                int count = Mathf.Clamp(Mathf.RoundToInt(circumference / (stroke + gap)), 12, 140);
                float stepDeg = 360f / count, strokeDeg = stepDeg * stroke / (stroke + gap);
                for (int i = 0; i < count; i++)
                {
                    float a0 = start + i * stepDeg, a1 = a0 + strokeDeg;
                    Vector2 P(float deg)
                    {
                        float rad = deg * Mathf.Deg2Rad;
                        // the hand wanders in and out a little as it goes round
                        float rr = r0 * (1f + (Mathf.PerlinNoise(seed + Mathf.Cos(rad) * 1.7f, seed + Mathf.Sin(rad) * 1.7f) - 0.5f) * 0.09f);
                        return centre + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * rr;
                    }
                    Vector2 p0 = P(a0), p1 = P(a1);
                    var c = new Color(ink.r, ink.g, ink.b, pass == 0 ? Rand(0.8f, 0.92f) : Rand(0.45f, 0.6f));
                    if (strokes < 300 && Line(parent, new Vector3(p0.x, p0.y, 0f), new Vector3(p1.x, p1.y, 0f), z, pass == 0 ? 1.7f : 1.3f, c, depth, layer, StrokeTexture())) strokes++;
                }
            }
            Msg($"position sketch: {strokes} strokes, radius {radius:0} panel units");
        }

        // A small north pointer in the bottom-right corner of the map screen. It is fixed to the screen, not
        // to the map, so it stays put while the map is dragged or zoomed.
        private static void CornerCompass(Panel_Map map, Transform icon)
        {
            var uiRoot = icon.GetComponentInParent<UIRoot>();
            if (uiRoot == null) return;
            float half = uiRoot.activeHeight / 2f, halfW = half * Screen.width / Mathf.Max(1f, Screen.height);
            var holder = new GameObject("Surveying_MapCompass");
            made.Add(holder);
            holder.layer = icon.gameObject.layer;
            holder.transform.SetParent(map.transform, false);
            holder.transform.position = uiRoot.transform.TransformPoint(new Vector3(halfW - 78f, -half + 118f, 0f));
            holder.transform.localScale = Vector3.one;
            Vector2 dir = SunNorthOnMap(), side = new Vector2(-dir.y, dir.x);
            var white = new Color(1f, 1f, 1f, 0.85f); var faint = new Color(1f, 1f, 1f, 0.45f);
            const int depth = 120;
            Vector2 tip = dir * 17f, tail = -dir * 17f;
            Line(holder.transform, new Vector3(tail.x, tail.y, 0f), new Vector3(tip.x, tip.y, 0f), 0f, 1.5f, white, depth, holder.layer, StrokeTexture(), false);
            Line(holder.transform, new Vector3(-side.x * 9f, -side.y * 9f, 0f), new Vector3(side.x * 9f, side.y * 9f, 0f), 0f, 1.2f, faint, depth, holder.layer, StrokeTexture(), false);
            foreach (float sgn in new[] { 1f, -1f })
            {
                Vector2 barb = tip - dir * 6f + side * (3.5f * sgn);
                Line(holder.transform, new Vector3(tip.x, tip.y, 0f), new Vector3(barb.x, barb.y, 0f), 0f, 1.4f, white, depth, holder.layer, StrokeTexture(), false);
            }
            Vector2 at = tip + dir * 9f;
            var nl = MakeLabel(holder.transform, new Vector3(at.x, at.y, 0f), "N", 12, white, depth + 1, holder.layer, false);
            Msg($"map compass: north at map bearing {(Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 360f) % 360f:0}, screen half-size {halfW:0}x{half:0}, label {(nl != null ? "made" : "not made")}");
        }

        // A text label in the game's own font (borrowed from a HUD label), cleared with the other map marks.
        private static UILabel MakeLabel(Transform parent, Vector3 localPos, string text, int size, Color color, int depth, int layer, bool track = true)
        {
            var hud = InterfaceManager.GetPanel<Panel_HUD>();
            var src = hud != null ? hud.m_SkillLevelUpHeader : null;
            if (src == null) return null;
            var go = UnityEngine.Object.Instantiate(src.gameObject, parent);
            if (track) made.Add(go);
            go.name = "Surveying_Label";
            go.layer = layer;
            foreach (var tw in go.GetComponents<UITweener>()) UnityEngine.Object.Destroy(tw);
            var loc = go.GetComponent<UILocalize>();
            if (loc != null) UnityEngine.Object.Destroy(loc);
            go.transform.localScale = Vector3.one; go.transform.localRotation = Quaternion.identity;
            go.transform.localPosition = localPos;
            go.SetActive(true);
            var l = go.GetComponent<UILabel>();
            l.enabled = true;
            l.overflowMethod = UILabel.Overflow.ResizeFreely;
            l.alignment = NGUIText.Alignment.Center; l.pivot = UIWidget.Pivot.Center;
            l.fontSize = size; l.text = text;
            l.color = color; l.alpha = color.a; l.depth = depth;
            return l;
        }

        private const float SolidPart = 0.7f; // share of the smudge's width that is solid before it fades out
        private static Texture2D softTex;
        private static readonly List<GameObject> made = new List<GameObject>();
        private static bool softBroken;

        private static Texture2D SoftTexture()
        {
            if (softTex != null) return softTex;
            const int N = 96;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // Uneven, charcoal-like edge: the fade starts and ends at slightly different distances round the rim.
                    float wob = (Mathf.PerlinNoise(x * 0.09f + 3.1f, y * 0.09f + 7.7f) - 0.5f) * 0.12f;
                    float k = Mathf.Clamp01((d + wob - 0.45f) / 0.5f);
                    float alpha = 1f - k * k * (3f - 2f * k);
                    if (d >= 0.98f) alpha = 0f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            softTex = t;
            return t;
        }

        // Evens out the wobble of individual notes (a light average), then rounds the corners (two passes of corner cutting).
        private static List<Vector2> Smooth(List<Vector2> p)
        {
            if (p.Count < 3) return p;
            var a = new List<Vector2>(p.Count) { p[0] };
            for (int i = 1; i < p.Count - 1; i++) a.Add((p[i - 1] + p[i] * 2f + p[i + 1]) * 0.25f);
            a.Add(p[p.Count - 1]);
            for (int pass = 0; pass < 2; pass++)
            {
                var b = new List<Vector2>(a.Count * 2) { a[0] };
                for (int i = 0; i < a.Count - 1; i++) { b.Add(a[i] * 0.75f + a[i + 1] * 0.25f); b.Add(a[i] * 0.25f + a[i + 1] * 0.75f); }
                b.Add(a[a.Count - 1]);
                a = b;
            }
            return a;
        }

        // 'thickness' is in panel units and may be a fraction: the widget itself cannot be thinner than
        // 2 units, so it is drawn 2 high and squashed.
        private static bool Line(Transform parent, Vector3 a, Vector3 b, float z, float thickness, Color color, int depth, int layer, Texture2D texture = null, bool track = true)
        {
            float dx = b.x - a.x, dy = b.y - a.y, len = Mathf.Sqrt(dx * dx + dy * dy);
            var seg = MakeTex(texture ?? SolidTexture(), parent, new Vector3((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f, z), 2, 2, color, depth, layer, track);
            if (seg == null) return false;
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            seg.transform.localScale = new Vector3(Mathf.Max(0.5f, len) / 2f, thickness / 2f, 1f);
            return true;
        }

        // A single charcoal stroke: densest along its middle, feathered at the sides, tapered at both ends, grainy.
        private static Texture2D strokeTex;
        private static Texture2D StrokeTexture()
        {
            if (strokeTex != null) return strokeTex;
            const int W = 64, H = 16;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = Mathf.Abs((y + 0.5f) / H * 2f - 1f);
                    float across = Mathf.Clamp01(1f - v * v);                         // soft sides
                    float ends = Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.12f);          // tapered ends
                    float grain = 0.55f + 0.45f * Mathf.PerlinNoise(x * 0.45f + 11f, y * 0.9f + 5f);
                    float press = 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.08f, 2.3f);  // pressure changing along the stroke
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, across * ends * grain * press));
                }
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            strokeTex = t;
            return t;
        }

        private static Texture2D solidTex;
        internal static Texture2D SolidTexture()
        {
            if (solidTex != null) return solidTex;
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) t.SetPixel(x, y, Color.white);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            solidTex = t;
            return t;
        }

        private static UITexture MakeSoft(Transform parent, Vector3 localPos, int size, Color color, int depth, int layer)
            => MakeTex(SoftTexture(), parent, localPos, size, size, color, depth, layer);

        internal static UITexture MakeTex(Texture2D texture, Transform parent, Vector3 localPos, int width, int height, Color color, int depth, int layer, bool track = true)
        {
            if (softBroken) return null;
            try
            {
                var go = new GameObject("Surveying_Soft");
                go.layer = layer;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPos;
                var ut = go.AddComponent<UITexture>();
                ut.mainTexture = texture;
                if (ut.shader == null) ut.shader = Shader.Find("Unlit/Transparent Colored");
                if (ut.shader == null) { UnityEngine.Object.Destroy(go); softBroken = true; Info("soft texture: no usable shader; falling back to the plain circle"); return null; }
                ut.color = color; ut.width = width; ut.height = height; ut.depth = depth;
                if (track) made.Add(go); // tracked objects are cleared whenever the map opens or closes
                return ut;
            }
            catch (Exception e) { softBroken = true; Info("soft texture failed (" + e.Message + "); falling back to the plain circle"); return null; }
        }

        // Fallback: the game's own area-zone sprite, hard-edged.
        private static void DrawSpriteRing(Panel_Map map, Transform icon, Vector3 cm, Vector3 ip, float radius, float mapScale)
        {
            if (map.m_AreaPrefab == null) { Info("no area prefab; cannot draw the position circle"); return; }
            var ring = UnityEngine.Object.Instantiate(map.m_AreaPrefab, icon.parent);
            made.Add(ring);
            ring.name = "Surveying_PositionCircle";
            ring.transform.localPosition = new Vector3(cm.x * uiScale, cm.y * uiScale, ip.z);
            ring.transform.localScale = Vector3.one;
            ring.SetActive(true);
            int size = Mathf.RoundToInt(2f * radius * mapScale * uiScale);
            var mi = ring.GetComponent<MapIcon>();
            UISprite area = mi != null ? mi.m_AreaZoneSprite : null;
            foreach (var wd in ring.GetComponentsInChildren<UIWidget>(true))
            {
                bool isArea = area != null ? wd.Pointer == area.Pointer : wd.TryCast<UISprite>() != null;
                if (area != null && !isArea) { wd.gameObject.SetActive(false); continue; }
                wd.gameObject.SetActive(true); wd.enabled = true;
                wd.width = size; wd.height = size; wd.alpha = 0.6f;
            }
            if (mi != null) mi.enabled = false;
            foreach (var c in ring.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            Msg($"position circle (plain): radius {radius:0} m -> {size} panel units");
        }

        private static void Dump(Panel_Map map)
        {
            Safe("dump", () =>
            {
                foreach (var pair in new[] { ("area", map.m_AreaPrefab), ("big", map.m_BigSpritePrefab) })
                {
                    if (pair.Item2 == null) { Msg($"prefab {pair.Item1}: null"); continue; }
                    foreach (var wd in pair.Item2.GetComponentsInChildren<UIWidget>(true))
                    {
                        var sp = wd.TryCast<UISprite>();
                        Msg($"prefab {pair.Item1}: '{wd.name}' {wd.GetIl2CppType().Name} {wd.width}x{wd.height} active={wd.gameObject.activeSelf} sprite={(sp != null ? sp.spriteName : "-")} type={(sp != null ? sp.type.ToString() : "-")}");
                    }
                }
            });
        }
    }
}
