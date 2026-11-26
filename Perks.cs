// Stage 2: Surveying points (saved per save slot through ModData), faster surveys, a chance to keep the
// charcoal, and a larger survey radius. Points are only earned for ground a survey newly covers.
using System;
using System.Collections.Generic;
using System.Globalization;
using Il2Cpp;
using ModSettings;
using UnityEngine;

namespace Surveying
{
    internal sealed class Settings : JsonModSettings
    {
        internal static readonly Settings Instance = new Settings();

        [Section("Skill")]
        [Name("Points per fresh Survey")]
        [Description("Points earned by a Survey that covers only new ground. Overlap with earlier Surveys earns proportionally less. (Default = 1)")]
        [Slider(0f, 5f, 51, NumberFormat = "{0:F1}")]
        public float pointsPerSurvey = 1f;

        [Name("Points per known location (existing save)")]
        [Description("One-time points for named locations already surveyed before the mod was added. Granted the first time the journal's survey list is opened. (Default = 1)")]
        [Slider(0f, 3f, 31, NumberFormat = "{0:F1}")]
        public float pointsPerKnownLocation = 1f;

        [Section("Survey benefits")]
        [Name("Faster Surveys")]
        [Description("Survey time reduced at level 2 and again at level 4. (Defaults = 25 and 50)")]
        public bool fasterSurveys = true;

        [Name("Survey time reduction, level 2-3 (%)")]
        [Slider(0f, 80f, 17)]
        public float timeReductionLow = 25f;

        [Name("Survey time reduction, level 4-5 (%)")]
        [Slider(0f, 80f, 17)]
        public float timeReductionHigh = 50f;

        [Name("Keep Charcoal")]
        [Description("From level 2, a chance that a Survey does not use up the Charcoal.")]
        public bool keepCharcoal = true;

        [Name("Chance to keep Charcoal (%)")]
        [Slider(0f, 100f, 21)]
        public float keepCharcoalChance = 25f;

        [Section("Trail notes")]
        [Name("Trail notes")]
        [Description("Ground you walk is noted and added in small circles the next time you Survey. Notes are lost when you leave the area.")]
        public bool trailNotes = true;

        [Name("Trail length, level 1 (m)")]
        [Slider(0f, 1000f, 41)]
        public float trailLengthLow = 150f;

        [Name("Trail length, level 5 (m)")]
        [Slider(0f, 2000f, 81)]
        public float trailLengthHigh = 800f;

        [Name("Trail note spacing (m)")]
        [Description("Distance walked between notes. Smaller gives a smoother revealed strip. (Default = 12)")]
        [Slider(5f, 50f, 46)]
        public float trailSpacing = 12f;

        [Name("Trail note radius (% of Survey radius)")]
        [Slider(5f, 50f, 10)]
        public float trailRadiusPercent = 20f;

        [Name("Trail notes in bad weather")]
        [Description("Whether ground walked in fog or a blizzard can be noted at all. When on, those notes use the reduced radius below.")]
        public bool trailInBadWeather = true;

        [Name("Trail notes at night")]
        [Description("Whether ground walked at night can be noted at all. When on, those notes use the reduced radius below.")]
        public bool trailAtNight = true;

        [Name("Level needed for poor-visibility notes")]
        [Description("Below this level nothing is noted in bad weather or at night. (Default = 4)")]
        [Slider(1f, 5f, 5)]
        public float trailPoorMinLevel = 4f;

        [Name("Poor-visibility note radius (% of normal note)")]
        [Description("Size of notes taken in bad weather or at night, compared with a normal note. (Default = 25)")]
        [Slider(5f, 100f, 20)]
        public float trailPoorRadiusPercent = 25f;

        [Name("Trail notes earn points")]
        [Description("Whether new ground added by trail notes counts toward the skill.")]
        public bool trailEarnsPoints = true;

        [Section("Familiar Ground (fully surveyed regions)")]
        [Name("Familiar Ground")]
        [Description("Rewards that apply only while you are inside a region whose named locations are all surveyed. Connector zones do not count.")]
        public bool familiarGround = true;

        [Name("Movement speed bonus (%)")]
        [Description("Walking, sprinting and crouching, outdoors only. Multiplies with other speed mods. (Default = 10)")]
        [Slider(0f, 30f, 31)]
        public float familiarSpeedPercent = 10f;

        [Name("Carry capacity bonus (KG)")]
        [Slider(0f, 10f, 21, NumberFormat = "{0:F1}")]
        public float familiarCarryKG = 2f;

        [Name("Sprain risk reduction (%)")]
        [Slider(0f, 50f, 11)]
        public float familiarSprainPercent = 10f;

        [Name("Survey radius per surveyed region (%)")]
        [Description("Applies everywhere, for each fully surveyed region. (Default = 2)")]
        [Slider(0f, 5f, 11, NumberFormat = "{0:F1}")]
        public float radiusPerRegion = 2f;

        [Name("Trail line on the map")]
        [Description("On Familiar Ground, the map shows the trail you have walked since your last Survey.")]
        public bool familiarTrailLine = true;

        [Name("TESTING: Surveying level override")]
        [Description("0 = off. Makes every benefit behave as if you had this level. Leave at 0 for normal play.")]
        [Slider(0f, 5f, 6)]
        public float testLevel = 0f;

        [Name("TESTING: show the level-up banner once")]
        [Description("Shows the Surveying level-up banner for your current level a moment after you confirm. Switches itself off.")]
        public bool testBanner = false;

        [Name("TESTING: treat every region as fully surveyed")]
        [Description("For trying the rewards out. Leave off for normal play.")]
        public bool familiarTest = false;

        [Section("Bearings")]
        [Name("Sun and star bearings")]
        [Description("Read direction from the Sun and stars. Needs a visible sky.")]
        public bool bearings = true;

        [Name("Compass on the HUD")]
        [Description("A compass strip at the top of the screen whenever direction can be read. North is taken from the sun: directly away from where it stands at midday. It settles on the nearest of 8 directions, 16 from level 4.")]
        public bool compass = true;

        [Name("Show / hide compass key")]
        public KeyCode bearingsKey = KeyCode.N;

        [Name("Level needed for bearings")]
        [Slider(1f, 5f, 5)]
        public float bearingsLevel = 3f;

        [Name("Level to read the compass in light weather")]
        [Description("Below this level the compass needs a clear or partly cloudy sky. From this level it can also be read under cloud, light snow and light fog. Heavy snow, blizzards and dense fog always blot it out.")]
        [Slider(1f, 5f, 5)]
        public float compassLightWeatherLevel = 4f;

        [Name("Compass in the corner of the map")]
        [Description("A small pointer in the bottom-right corner of the Region Map showing which way north is, since it is not always straight up.")]
        public bool mapNorthArrow = true;

        [Section("Position on the map")]
        [Name("Rough position circle")]
        [Description("Shows a circle on the Region Map that you are somewhere inside. The circle is never centred on you.")]
        public bool positionCircle = true;

        [Name("Sketched outline")]
        [Description("Draws a rough charcoal ring round the area, in the style of the map's own lines.")]
        public bool circleSketch = true;

        [Name("Level needed for the circle")]
        [Slider(1f, 5f, 5)]
        public float circleLevel = 4f;

        [Name("Circle radius at that level (m)")]
        [Slider(50f, 800f, 76)]
        public float circleRadiusLow = 250f;

        [Name("Circle radius at level 5 or on Familiar Ground (m)")]
        [Slider(50f, 500f, 46)]
        public float circleRadiusTight = 100f;

        [Name("Dead reckoning")]
        [Description("The circle grows the farther you walk from your last Survey, and shrinks back when you Survey again. Not on Familiar Ground.")]
        public bool deadReckoning = true;

        [Name("Circle growth per 100 m walked (m)")]
        [Slider(0f, 100f, 21)]
        public float deadReckoningGrowth = 20f;

        [Name("Largest the circle can grow (x its normal size)")]
        [Slider(1f, 4f, 13, NumberFormat = "{0:F2}")]
        public float deadReckoningCap = 3f;

        [Section("Altitude")]
        [Name("Altitude bonus")]
        [Description("Surveys made from high ground show more of the map: x1.25 at level 3, x1.5 at level 4, x2 at level 5.")]
        public bool altitudeBonus = true;

        [Section("Dawn and dusk")]
        [Name("Survey at dawn and dusk")]
        [Description("From level 3 you can Survey for a while longer either side of daylight, at a reduced radius.")]
        public bool duskSurvey = true;

        [Name("Extra time either side of daylight (hours)")]
        [Slider(0f, 3f, 13, NumberFormat = "{0:F2}")]
        public float duskExtraHours = 1f;

        [Name("Dawn and dusk Survey radius (% of normal)")]
        [Slider(20f, 100f, 17)]
        public float duskRadiusPercent = 60f;

        [Section("Survey radius")]
        [Name("Survey radius bonus")]
        [Description("Survey radius grows with each level above 1.")]
        public bool radiusBonus = true;

        [Name("Survey radius per level (%)")]
        [Slider(0f, 10f, 21, NumberFormat = "{0:F1}")]
        public float radiusPerLevel = 2f;

        [Name("Clear daylight bonus, level 4 (%)")]
        [Description("Extra Survey radius in clear weather during the day.")]
        [Slider(0f, 20f, 21)]
        public float clearBonusL4 = 5f;

        [Name("Clear daylight bonus, level 5 (%)")]
        [Slider(0f, 20f, 21)]
        public float clearBonusL5 = 10f;
    }

    internal static class Perks
    {
        internal static readonly HashSet<string> Familiar = new HashSet<string>(); // scene names of fully surveyed regions
        internal static float Walked;                                             // metres walked outdoors since the last survey
        internal static Vector2 Offset;                                           // position circle offset, fixed until the next survey
        private static readonly Queue<string> Announcements = new Queue<string>();
        internal static void Announce(string text) => Announcements.Enqueue(text);
        internal static void PersistNow() => Persist();
        internal static List<Vector4> TrailPoints => Trail;
        internal static string TrailScene => trailScene;

        // Points needed for levels 1-5. Vanilla skills use similar curves (Cooking is 0/25/75/150/350).
        internal static readonly int[] TierPoints = { 0, 20, 75, 175, 350 };
        private const float RadiusCap = 2f; // never more than double the game's own radius, whatever stacks

        private static readonly ModData.ModDataManager Store = new ModData.ModDataManager("Surveying", false);
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private static readonly Dictionary<IntPtr, Vector3> BaseTimes = new Dictionary<IntPtr, Vector3>();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Mirror of what is stored for the loaded save.
        private static string raw;                 // last text read from or written to the store
        internal static float Points;
        private static bool knownGranted;
        private static readonly Dictionary<string, List<Vector3>> Mine = new Dictionary<string, List<Vector3>>(); // scene -> (x, z, radius)

        // Trail notes: x, y, z, and w = 1 when laid in poor visibility.
        private static readonly List<Vector4> Trail = new List<Vector4>();
        private static string trailScene = "";
        private static float CrumbSpacing => Mathf.Clamp(Settings.Instance.trailSpacing, 5f, 50f);

        private static float surveyUntil;          // a survey was started and has not resolved yet
        private static readonly List<Vector3> pendingPrior = new List<Vector3>(); private static int pendingVanilla;
        private static Vector3 pendingMap; private static float mapScale = 1f;
        private static Vector3 pendingPos; private static float pendingRadius; private static string pendingScene; private static bool pending;

        private static void Msg(string s) { if (Mod.Detailed) Mod.Log.Msg("[perk] " + s); }   // detail, only with DetailedLog on
        private static void Info(string s) => Mod.Log.Msg("[perk] " + s);
        private static void Safe(string where, Action a)
        {
            try { a(); }
            catch (Exception e) { if (Failed.Add(where)) Mod.Log.Error($"[perk] {where}: {e}"); }
        }

        internal static int Level
        {
            get { int o = Mathf.RoundToInt(Settings.Instance.testLevel); if (o >= 1) return Mathf.Min(o, 5); int l = 1; for (int i = 1; i < TierPoints.Length; i++) if (Points >= TierPoints[i]) l = i + 1; return l; }
        }

        // ---- storage ----
        // Called once a second. The ModData cache is the single source of truth, so a save loaded at any
        // moment is picked up, and the main menu (no save loaded) reads as zero.
        private static string deferred; // shown once the journal is closed, so it is not hidden behind it
        internal static void Sync()
        {
            if (Settings.Instance.testBanner) { Settings.Instance.testBanner = false; ShowLevelUp(Level); }
            if (deferred != null) { Announcements.Enqueue(deferred); deferred = null; }
            if (Announcements.Count > 0)
                Safe("announce", () =>
                {
                    if (InterfaceManager.IsPanelEnabled<Panel_Log>() || InterfaceManager.IsPanelEnabled<Panel_Map>()) return;
                    HUDMessage.AddMessage(Announcements.Dequeue(), 5f, false, false);
                });
            Safe("Sync", () =>
            {
                string now = Store.Load();
                if (now == raw) return;
                raw = now;
                Points = 0; knownGranted = false; Mine.Clear(); Trail.Clear(); trailScene = ""; Familiar.Clear(); Offset = Vector2.zero; Walked = 0f; haveLast = false;
                if (!string.IsNullOrEmpty(now))
                {
                    foreach (var part in now.Split('|'))
                    {
                        int eq = part.IndexOf('=');
                        if (eq < 0) continue;
                        string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                        if (k == "points") float.TryParse(v, NumberStyles.Float, Inv, out Points);
                        else if (k == "known") knownGranted = v == "1";
                        else if (k == "fg") { foreach (var f in v.Split(',')) if (f.Length > 0) Familiar.Add(f); }
                        else if (k == "walk") float.TryParse(v, NumberStyles.Float, Inv, out Walked);
                        else if (k == "off")
                        {
                            var c = v.Split(',');
                            if (c.Length == 2 && float.TryParse(c[0], NumberStyles.Float, Inv, out float ox) && float.TryParse(c[1], NumberStyles.Float, Inv, out float oz)) Offset = new Vector2(ox, oz);
                        }
                        else if (k == "trail")
                        {
                            var seg = v.Split(';');
                            trailScene = seg[0];
                            for (int i = 1; i < seg.Length; i++)
                            {
                                var c = seg[i].Split(',');
                                if (c.Length == 4 && float.TryParse(c[0], NumberStyles.Float, Inv, out float x) && float.TryParse(c[1], NumberStyles.Float, Inv, out float y) && float.TryParse(c[2], NumberStyles.Float, Inv, out float z))
                                    Trail.Add(new Vector4(x, y, z, c[3] == "1" ? 1f : 0f));
                            }
                        }
                        else if (k.StartsWith("s:"))
                        {
                            var list = new List<Vector3>();
                            foreach (var p in v.Split(';'))
                            {
                                var c = p.Split(',');
                                if (c.Length == 3 && float.TryParse(c[0], NumberStyles.Float, Inv, out float x) && float.TryParse(c[1], NumberStyles.Float, Inv, out float z) && float.TryParse(c[2], NumberStyles.Float, Inv, out float r))
                                    list.Add(new Vector3(x, z, r));
                            }
                            Mine[k.Substring(2)] = list;
                        }
                    }
                }
                Info($"loaded: {(now == null ? "no stored data" : $"points={Points:0.##} level={Level} knownGranted={knownGranted} scenes={Mine.Count} familiar=[{string.Join(",", Familiar)}] trail={Trail.Count}")}");
            });
        }

        private static bool Persist()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("points=").Append(Points.ToString("0.###", Inv)).Append("|known=").Append(knownGranted ? "1" : "0");
            foreach (var kv in Mine)
            {
                sb.Append("|s:").Append(kv.Key).Append('=');
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    if (i > 0) sb.Append(';');
                    var p = kv.Value[i];
                    sb.Append(p.x.ToString("0", Inv)).Append(',').Append(p.y.ToString("0", Inv)).Append(',').Append(p.z.ToString("0", Inv));
                }
            }
            if (Familiar.Count > 0) sb.Append("|fg=").Append(string.Join(",", Familiar));
            if (Walked > 0f) sb.Append("|walk=").Append(Walked.ToString("0", Inv));
            if (Offset != Vector2.zero) sb.Append("|off=").Append(Offset.x.ToString("0.###", Inv)).Append(',').Append(Offset.y.ToString("0.###", Inv));
            if (Trail.Count > 0)
            {
                sb.Append("|trail=").Append(trailScene);
                foreach (var c in Trail)
                    sb.Append(';').Append(c.x.ToString("0.#", Inv)).Append(',').Append(c.y.ToString("0.#", Inv)).Append(',').Append(c.z.ToString("0.#", Inv)).Append(',').Append(c.w > 0.5f ? "1" : "0");
            }
            string text = sb.ToString();
            if (!Store.Save(text)) { if (Failed.Add("persist")) Info("could not store progress (no save slot loaded)"); return false; }
            raw = text;
            return true;
        }

        private static void Award(float amount, string why)
        {
            if (amount <= 0f) return;
            int before = Level;
            Points += amount;
            bool ok = Persist();
            Info($"+{amount:0.##} points ({why}); total {Points:0.##}, level {Level}{(ok ? "" : " (NOT stored)")}");
            SkillEntry.Ensure();
            SkillEntry.SetPoints(Points);
            if (Mathf.FloorToInt(Points) > Mathf.FloorToInt(Points - amount))
                Safe("pointicon", () => GameManager.GetSkillNotify().MaybeShowPointIncrease(SkillEntry.IconName));
            if (Level > before) ShowLevelUp(Level);
        }

        // The game's own skill level-up banner (and its sound), with a plain message if that is unavailable.
        internal static void ShowLevelUp(int level)
        {
            try
            {
                string tierName = "";
                Safe("tiername", () => tierName = GameManager.GetSkillsManager().GetTierName(level - 1));
                GameManager.GetSkillNotify().MaybeShowLevelUp(SkillEntry.BannerSprite, SkillEntry.Text["GAMEPLAY_SkillSurveying"], tierName, level);
                Info($"level-up banner: sprite '{SkillEntry.BannerSprite}', header 'Surveying', footer '{tierName}', number {level}");
            }
            catch (Exception e)
            {
                if (Failed.Add("banner")) Mod.Log.Error("[perk] banner: " + e);
                Safe("levelup", () => HUDMessage.AddMessage($"Surveying skill increased to {level}", 5f, true, false));
            }
        }

        // ---- trail notes ----
        private static Vector3 lastPos; private static string lastScene = ""; private static bool haveLast; private static float walkedUnsaved;
        private static float TrailMax => Mathf.Lerp(Settings.Instance.trailLengthLow, Settings.Instance.trailLengthHigh, (Level - 1) / 4f);

        // Called once a second: drops an invisible note every few metres walked outdoors.
        internal static void TrailTick()
        {
            try
            {
                string scene = GameManager.m_ActiveScene;
                if (string.IsNullOrEmpty(scene) || scene.StartsWith("MainMenu") || scene == "Boot" || scene == "Empty") return;
                var t = GameManager.GetPlayerTransform();
                var w = GameManager.GetWeatherComponent();
                var tod = GameManager.GetTimeOfDayComponent();
                if (t == null || w == null || tod == null) return;
                if (scene != trailScene)
                {
                    if (Trail.Count > 0) Msg($"trail notes dropped ({Trail.Count}): left {trailScene}");
                    Trail.Clear(); trailScene = scene;
                }
                if (w.IsIndoorEnvironment()) { haveLast = false; return; }
                Vector3 p = t.position;
                // Dead reckoning: total ground covered since the last survey (ignoring jumps such as a scene change).
                if (haveLast && lastScene == scene)
                {
                    float step = Mathf.Sqrt((p.x - lastPos.x) * (p.x - lastPos.x) + (p.z - lastPos.z) * (p.z - lastPos.z));
                    if (step > 0.5f && step < 30f) { Walked += step; walkedUnsaved += step; if (walkedUnsaved > 50f) { walkedUnsaved = 0f; Persist(); } }
                }
                lastPos = p; lastScene = scene; haveLast = true;
                if (!Settings.Instance.trailNotes) return;
                if (Trail.Count > 0)
                {
                    var l = Trail[Trail.Count - 1];
                    if ((p.x - l.x) * (p.x - l.x) + (p.z - l.z) * (p.z - l.z) < CrumbSpacing * CrumbSpacing) return;
                }
                var st = Settings.Instance;
                bool badWeather = w.IsDenseFog() || w.IsBlizzard() || w.GetWeatherStage().ToString().Contains("Fog");
                bool night = tod.IsNight();
                bool poor = badWeather || night;
                // Nothing is noted in poor visibility if that kind is switched off or the level is too low.
                if (poor && ((badWeather && !st.trailInBadWeather) || (night && !st.trailAtNight) || Level < Mathf.RoundToInt(st.trailPoorMinLevel))) return;
                Trail.Add(new Vector4(p.x, p.y, p.z, poor ? 1f : 0f));
                float max = TrailMax, len = 0f; int keepFrom = 0;
                for (int i = Trail.Count - 1; i > 0; i--)
                {
                    len += Mathf.Sqrt((Trail[i].x - Trail[i - 1].x) * (Trail[i].x - Trail[i - 1].x) + (Trail[i].z - Trail[i - 1].z) * (Trail[i].z - Trail[i - 1].z));
                    if (len > max) { keepFrom = i; break; }
                }
                if (keepFrom > 0) Trail.RemoveRange(0, keepFrom);
                Persist();
            }
            catch (Exception e) { if (Failed.Add("trail")) Mod.Log.Error("[perk] trail: " + e); }
        }

        // After a survey: reveal a small circle round each note that adds new ground, then forget the notes.
        private static float RevealTrail(Panel_Map map, List<Vector3> prior)
        {
            float pts = 0f;
            if (!Settings.Instance.trailNotes || Trail.Count == 0 || trailScene != pendingScene) return 0f;
            int used = 0, poorUsed = 0, before = -1, after = -1;
            Safe("trail.count", () => before = map.m_DetailSurveyPositions[pendingScene].Count);
            float full = pendingRadius * Settings.Instance.trailRadiusPercent / 100f;
            prior.Add(new Vector3(pendingMap.x, pendingMap.y, pendingRadius * mapScale));
            var notes = new List<Vector4>(Trail);
            Trail.Clear();
            foreach (var c in notes)
            {
                float r = c.w > 0.5f ? full * Mathf.Clamp(Settings.Instance.trailPoorRadiusPercent, 5f, 100f) / 100f : full;
                float d = Mathf.Sqrt((c.x - pendingPos.x) * (c.x - pendingPos.x) + (c.z - pendingPos.z) * (c.z - pendingPos.z));
                if (d + r <= pendingRadius) continue; // already inside the survey itself
                var world = new Vector3(c.x, c.y, c.z);
                var m = map.WorldPositionToMapPosition(pendingScene, world);
                float fresh = FreshFraction(m.x, m.y, r * mapScale, prior);
                if (fresh < 0.05f) continue;
                map.DoNearbyDetailsCheck(r, true, true, world, false);
                prior.Add(new Vector3(m.x, m.y, r * mapScale));
                if (Settings.Instance.trailEarnsPoints) pts += fresh * (r / 150f) * (r / 150f) * Settings.Instance.pointsPerSurvey;
                used++; if (c.w > 0.5f) poorUsed++;
            }
            Safe("trail.count2", () => after = map.m_DetailSurveyPositions[pendingScene].Count);
            Msg($"trail notes: {notes.Count} carried, {used} added new ground ({poorUsed} from poor visibility), radius {full:0.#} m; game circle count {before} -> {after}");
            if (used > 0) Safe("trail.msg", () => HUDMessage.AddMessage("Trail notes added to Survey", false, false));
            return pts;
        }

        // ---- survey hooks ----
        internal static void Pre_Start(CharcoalItem __instance) => Safe("Start", () =>
        {
            Sync();
            if (!BaseTimes.TryGetValue(__instance.Pointer, out var b))
            {
                b = new Vector3(__instance.m_SurveyGameMinutes, __instance.m_SurveyRealSeconds, __instance.m_SurveySkillExtendedHours);
                BaseTimes[__instance.Pointer] = b;
            }
            var s = Settings.Instance;
            int lv = Level;
            float cut = !s.fasterSurveys || lv < 2 ? 0f : (lv >= 4 ? s.timeReductionHigh : s.timeReductionLow) / 100f;
            __instance.m_SurveyGameMinutes = b.x * (1f - cut);
            __instance.m_SurveyRealSeconds = b.y * (1f - cut);
            // Dawn and dusk: the game already has a "skill extends survey hours" value; from level 3 it is widened.
            duskExtra = s.duskSurvey && lv >= 3 ? s.duskExtraHours : 0f;
            __instance.m_SurveySkillExtendedHours = b.z + duskExtra;
            duskSurveyNow = false;
            surveyUntil = Time.realtimeSinceStartup + 60f;
            if (cut > 0f) Msg($"survey time {b.x:0.#} -> {__instance.m_SurveyGameMinutes:0.#} game minutes (level {lv})");
        });

        // Runs when the game checks whether there is enough light. If it only passed because of the extra
        // hours, this survey counts as a dawn or dusk one and covers less ground.
        private static float duskExtra; private static bool duskSurveyNow, inVisibilityCheck;
        internal static void Post_Visibility(float extendedHoursBonus, bool __result)
        {
            if (inVisibilityCheck || !__result || duskExtra <= 0f) return;
            inVisibilityCheck = true;
            try { duskSurveyNow = !CharcoalItem.HasSurveyVisibility(extendedHoursBonus - duskExtra); }
            catch (Exception e) { if (Failed.Add("visibility")) Mod.Log.Error("[perk] visibility: " + e); }
            finally { inVisibilityCheck = false; }
        }

        internal static void Pre_Cancel() => surveyUntil = 0f;

        internal static bool Pre_Consume()
        {
            bool keep = false;
            Safe("Consume", () =>
            {
                var s = Settings.Instance;
                if (Time.realtimeSinceStartup > surveyUntil || !s.keepCharcoal || Level < 2) return;
                keep = UnityEngine.Random.value * 100f < s.keepCharcoalChance;
                if (keep) { Msg("charcoal kept"); HUDMessage.AddMessage("Charcoal not used up", false, false); }
            });
            return !keep;
        }

        internal static void Pre_Check(Panel_Map __instance, ref float radius, bool useOverridePosition, bool shouldAllowVistaReveals)
        {
            if (useOverridePosition || Time.realtimeSinceStartup > surveyUntil) return;
            float r = radius;
            Safe("Check", () =>
            {
                surveyUntil = 0f;
                var s = Settings.Instance;
                float mult = s.radiusBonus ? 1f + (Level - 1) * s.radiusPerLevel / 100f : 1f;
                if (s.familiarGround) mult += Familiar.Count * s.radiusPerRegion / 100f;
                Safe("clear", () =>
                {
                    var w = GameManager.GetWeatherComponent(); var tod = GameManager.GetTimeOfDayComponent();
                    if (Level >= 4 && w != null && tod != null && w.IsClear() && !tod.IsNight() && !duskSurveyNow)
                        mult += (Level >= 5 ? s.clearBonusL5 : s.clearBonusL4) / 100f;
                });
                mult = Mathf.Min(mult, RadiusCap);
                if (duskSurveyNow)
                {
                    mult *= Mathf.Clamp(s.duskRadiusPercent, 20f, 100f) / 100f;
                    Safe("duskmsg", () => HUDMessage.AddMessage("Visibility is poor. Your Survey covers less ground.", false, false));
                    duskSurveyNow = false;
                }
                r *= mult;
                var t = GameManager.GetPlayerTransform();
                pendingPos = t.position; pendingRadius = r; pendingScene = GameManager.m_ActiveScene ?? "?"; pending = true;
                // Read earlier circles now, before the game adds this survey's own circle to its list.
                // The game keeps its circles in map coordinates, so everything is compared there.
                pendingMap = __instance.WorldPositionToMapPosition(pendingScene, pendingPos);
                var east = __instance.WorldPositionToMapPosition(pendingScene, pendingPos + new Vector3(100f, 0f, 0f));
                mapScale = Mathf.Sqrt((east.x - pendingMap.x) * (east.x - pendingMap.x) + (east.y - pendingMap.y) * (east.y - pendingMap.y)) / 100f;
                if (mapScale < 0.01f) mapScale = 1f;
                pendingPrior.Clear();
                if (Mine.TryGetValue(pendingScene, out var mine))
                    foreach (var w in mine)
                    {
                        var m = __instance.WorldPositionToMapPosition(pendingScene, new Vector3(w.x, pendingPos.y, w.y));
                        pendingPrior.Add(new Vector3(m.x, m.y, w.z * mapScale));
                    }
                pendingVanilla = AddVanilla(__instance, pendingPrior);
                if (!Mathf.Approximately(mult, 1f)) Msg($"survey radius x{mult:0.00} -> {r:0.#} m");
            });
            radius = r;
        }

        internal static void Post_Check(Panel_Map __instance) => Safe("PostCheck", () =>
        {
            if (!pending) return;
            pending = false;
            var prior = pendingPrior; int vanilla = pendingVanilla;
            World.NewOffset();
            Walked = 0f; walkedUnsaved = 0f;
            World.RecheckRegions();
            MeasureListScale(__instance);
            float fresh = FreshFraction(pendingMap.x, pendingMap.y, pendingRadius * mapScale, prior);
            float scale = pendingRadius / 150f;
            Msg($"survey at ({pendingPos.x:0}, {pendingPos.z:0}) r={pendingRadius:0} in {pendingScene}: {fresh * 100f:0}% new ground ({prior.Count} earlier circles, {vanilla} from the game's own list)");
            if (!Mine.TryGetValue(pendingScene, out var mine)) Mine[pendingScene] = mine = new List<Vector3>();
            mine.Add(new Vector3(pendingPos.x, pendingPos.z, pendingRadius));
            float trailPts = 0f;
            Safe("RevealTrail", () => trailPts = RevealTrail(__instance, prior));
            float total = (fresh < 0.02f ? 0f : fresh * scale * scale * Settings.Instance.pointsPerSurvey) + trailPts;
            if (total <= 0f) { Persist(); return; }
            Award(total, trailPts > 0f ? "survey + trail notes" : "survey");
        });

        // The game stores its circles as map position times a constant (3 where measured). After each survey
        // the circle the game just added is compared with where the player is, to confirm or correct that.
        private static float listScale = 3f;
        private static void MeasureListScale(Panel_Map map)
        {
            try
            {
                var dict = map.m_DetailSurveyPositions;
                if (dict == null || !dict.ContainsKey(pendingScene)) return;
                var list = dict[pendingScene];
                if (list == null || list.Count == 0) return;
                var last = list[list.Count - 1];
                float ax = Mathf.Abs(pendingMap.x), ay = Mathf.Abs(pendingMap.y);
                if (Mathf.Max(ax, ay) < 20f) return; // too close to the map origin to measure
                float k = ax >= ay ? last.x / pendingMap.x : last.y / pendingMap.y;
                bool matches = Mathf.Abs(last.x - pendingMap.x * k) < 4f && Mathf.Abs(last.y - pendingMap.y * k) < 4f;
                if (!matches || k < 0.2f || k > 20f) { Msg($"list scale check: newest circle ({last.x}, {last.y}) is not this survey (player on map {pendingMap.x:0},{pendingMap.y:0}); keeping {listScale:0.##}"); return; }
                if (Mathf.Abs(k - listScale) > 0.02f) Msg($"list scale measured {k:0.###} (was {listScale:0.###})");
                listScale = k;
            }
            catch (Exception e) { if (Failed.Add("listscale")) Info("list scale check failed: " + e.Message); }
        }

        // The game's own survey circles for this scene.
        private static bool keysLogged;
        private static int AddVanilla(Panel_Map map, List<Vector3> into)
        {
            int n = 0;
            try
            {
                var dict = map.m_DetailSurveyPositions;
                if (dict == null) return 0;
                if (!keysLogged)
                {
                    keysLogged = true;
                    var sb = new System.Text.StringBuilder();
                    foreach (var kv in dict) sb.Append(kv.Key).Append('(').Append(kv.Value == null ? 0 : kv.Value.Count).Append(") ");
                    Msg($"game survey list keys: {sb} | scene={GameManager.m_ActiveScene} region={GameManager.TryGetCurrentRegionName()}");
                }
                Il2CppSystem.Collections.Generic.List<DetailSurveyPosition> list = null;
                foreach (var key in new[] { GameManager.m_ActiveScene, GameManager.TryGetCurrentRegionName() })
                    if (key != null && dict.ContainsKey(key)) { list = dict[key]; break; }
                if (list == null) return 0;
                float nearest = float.MaxValue;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p == null) continue;
                    float px = p.x / listScale, py = p.y / listScale;
                    float d = Mathf.Sqrt((px - pendingMap.x) * (px - pendingMap.x) + (py - pendingMap.y) * (py - pendingMap.y));
                    nearest = Mathf.Min(nearest, d);
                    into.Add(new Vector3(px, py, p.r * mapScale));
                    n++;
                }
                if (list.Count > 0) Msg($"game list for this scene: {list.Count} circles, nearest to player {nearest:0.#} map units (player on map {pendingMap.x:0},{pendingMap.y:0},{pendingMap.z:0}; scale {mapScale:0.###}, list scale {listScale:0.##}), last=({list[list.Count - 1].x}, {list[list.Count - 1].y}, h={list[list.Count - 1].h}, r={list[list.Count - 1].r})");
            }
            catch (Exception e) { if (Failed.Add("vanilla")) Info("could not read the game's survey list: " + e.Message); }
            return n;
        }

        // Share of the new circle not already inside an earlier circle, on a fixed grid of sample points.
        private static float FreshFraction(float cx, float cz, float r, List<Vector3> prior)
        {
            if (prior.Count == 0) return 1f;
            int total = 0, fresh = 0; const int N = 24;
            for (int ix = -N; ix <= N; ix++)
                for (int iz = -N; iz <= N; iz++)
                {
                    float dx = ix * r / N, dz = iz * r / N;
                    if (dx * dx + dz * dz > r * r) continue;
                    total++;
                    float x = cx + dx, z = cz + dz; bool covered = false;
                    for (int i = 0; i < prior.Count && !covered; i++)
                    {
                        var p = prior[i];
                        covered = (x - p.x) * (x - p.x) + (z - p.y) * (z - p.y) <= p.z * p.z;
                    }
                    if (!covered) fresh++;
                }
            return total == 0 ? 0f : (float)fresh / total;
        }

        // One-time credit for named locations surveyed before the mod existed.
        internal static void Post_RegionList(Panel_Log __instance) => Safe("RegionList", () =>
        {
            Sync();
            if (knownGranted || raw == null && !Persist()) return;
            var regions = __instance.m_SurveyRegionList;
            if (regions == null || regions.Count == 0) return;
            int done = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                var a = regions[i]?.m_Achievements;
                if (a == null) continue;
                for (int j = 0; j < a.Count; j++) if (a[j] != null && a[j].m_Completed) done++;
            }
            knownGranted = true;
            float pts = done * Settings.Instance.pointsPerKnownLocation;
            if (pts <= 0f) { Persist(); return; }
            Award(pts, $"{done} locations already surveyed");
            deferred = $"Surveying: {done} known locations credited";
        });
    }
}
