using System;
using System.Diagnostics;
using UnityEngine;

namespace Surveying
{
    // Frame-time recorder, only active with the PerfLog preference on. It answers one question: when a frame
    // takes far longer than its neighbours, how much of that frame was spent in this mod's own code?
    internal static class Perf
    {
        internal static bool On => Mod.PerfLog != null && Mod.PerfLog.Value;
        private static long acc, accTick, accHooks;           // stopwatch ticks spent in this mod since the last frame start
        private static int hookCalls;
        private static bool tickedThisFrame;
        private static float avg = -1f, nextSummary, lastSpikeLog;
        private static int frames, over33, over50, over100, spikes, spikesOnTick;
        private static double sumMs, sumOurs, maxOurs, maxMs, maxOursInSpike;
        private static int gc0, gc1, gc2, gcIl;
        private static readonly double ToMs = 1000.0 / Stopwatch.Frequency;

        internal static long Start() => On ? Stopwatch.GetTimestamp() : 0;
        internal static void StopFrame(long t0) { if (t0 != 0) acc += Stopwatch.GetTimestamp() - t0; }
        internal static void StopTick(long t0) { if (t0 != 0) { long d = Stopwatch.GetTimestamp() - t0; acc += d; accTick += d; tickedThisFrame = true; } }
        internal static void StopHook(long t0) { if (t0 != 0) { long d = Stopwatch.GetTimestamp() - t0; acc += d; accHooks += d; hookCalls++; } }

        private static int IlGc() { try { return Il2CppSystem.GC.CollectionCount(0); } catch { return -1; } }

        // Called first thing each frame: the frame time Unity reports now belongs to the frame that just ended,
        // and everything this mod did since the previous call belongs to that same frame.
        internal static void FrameStart()
        {
            if (!On) return;
            try
            {
                float dt = Time.unscaledDeltaTime * 1000f;
                double ours = acc * ToMs, tick = accTick * ToMs, hooks = accHooks * ToMs;
                bool ticked = tickedThisFrame; int calls = hookCalls;
                acc = accTick = accHooks = 0; hookCalls = 0; tickedThisFrame = false;
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2), gi = IlGc();
                int d0 = g0 - gc0, d1 = g1 - gc1, d2 = g2 - gc2, di = gi - gcIl;
                gc0 = g0; gc1 = g1; gc2 = g2; gcIl = gi;
                if (!World.InGame || dt <= 0f || dt > 5000f) { avg = -1f; return; }   // menus and loading screens are not judged
                if (avg < 0f) { avg = dt; return; }

                frames++; sumMs += dt; sumOurs += ours; if (ours > maxOurs) maxOurs = ours; if (dt > maxMs) maxMs = dt;
                if (dt > 33.4f) over33++; if (dt > 50f) over50++; if (dt > 100f) over100++;
                bool spike = dt > 25f && dt > avg * 2.5f;
                if (spike)
                {
                    spikes++; if (ticked) spikesOnTick++; if (ours > maxOursInSpike) maxOursInSpike = ours;
                    if (Time.realtimeSinceStartup - lastSpikeLog > 0.25f)
                    {
                        lastSpikeLog = Time.realtimeSinceStartup;
                        Mod.Log.Msg($"[perf] slow frame {dt:0.0} ms (recent average {avg:0.0}); Surveying used {ours:0.00} ms of it ({(ticked ? $"once-a-second work {tick:0.00}, " : "")}hooks {hooks:0.00} over {calls} calls); garbage collections: managed {d0}/{d1}/{d2}, game {di}; {(World.Outdoors ? "outdoors" : "indoors")} in {World.Region}");
                    }
                }
                else avg += (dt - avg) * 0.05f;      // slow frames are kept out of the average they are judged against

                if (Time.realtimeSinceStartup >= nextSummary)
                {
                    if (nextSummary > 0f && frames > 0)
                        Mod.Log.Msg($"[perf] last 30 s: {frames} frames, average {sumMs / frames:0.0} ms, worst {maxMs:0.0} ms; over 33 ms: {over33}, over 50: {over50}, over 100: {over100}; slow frames {spikes} ({spikesOnTick} on a once-a-second frame); Surveying average {sumOurs / frames:0.000} ms per frame, worst {maxOurs:0.00} ms, worst inside a slow frame {maxOursInSpike:0.00} ms");
                    nextSummary = Time.realtimeSinceStartup + 30f;
                    frames = over33 = over50 = over100 = spikes = spikesOnTick = 0; sumMs = sumOurs = maxOurs = maxMs = maxOursInSpike = 0;
                }
            }
            catch (Exception e) { Mod.Log.Error("[perf] " + e); Mod.PerfLog.Value = false; }
        }
    }
}
