#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Flow;
using Debug = UnityEngine.Debug;

namespace Uninarok.Networking.Dev
{
    /// <summary>
    /// Measurement tool for a development player (SPEC-0001 T8.2, T9.6, T9.7). It does nothing unless the player is started
    /// with <c>-uninarok-probe &lt;mode&gt;</c>:
    /// <list type="bullet">
    /// <item><c>baseline</c>: no networking; samples the main thread's GC allocation per frame for a few seconds (the probe's own cost).</item>
    /// <item><c>network</c>: waits for the flow (driven by <see cref="DevAutoLogin"/>) to reach InGame, then samples per frame for
    /// 60 s; phases are "login to map" and "in game idle".</item>
    /// <item><c>threads</c>: 20 connect/dispose cycles against a local listener; reports process thread counts before and after.</item>
    /// </list>
    /// It writes <c>probe-report-&lt;mode&gt;.txt</c> under <c>Application.persistentDataPath</c> and quits. Absent from release builds.
    /// </summary>
    public sealed class NetworkProbe : MonoBehaviour
    {
        private struct Phase
        {
            public string Name;
            public int Frames;
            public long SumBytes;
            public long MaxBytes;
            public int FramesWithAlloc;
            public long SumCount;
            public double Seconds;
            public long HeapStart, HeapEnd;       // managed heap in use (all threads), bytes
            public int CollectionsStart, CollectionsEnd;
            public readonly List<string> AllocFrames;   // the first few frames that allocated: when and how much
            public Phase(string name) : this() { Name = name; AllocFrames = new List<string>(); }
        }

        private string _mode;
        private float _seconds = 60f;
        private ProfilerRecorder _gcBytes;
        private ProfilerRecorder _gcCount;
        private Phase _phase;
        private readonly List<Phase> _done = new List<Phase>();
        private readonly StringBuilder _notes = new StringBuilder();
        private float _phaseStart;
        private float _startTime;
        private bool _measuring;
        private bool _finished;
        private NetworkClient _client;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-uninarok-probe");
            if (at < 0 || at + 1 >= args.Length) return;

            var go = new GameObject("NetworkProbe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<NetworkProbe>();
            probe._mode = args[at + 1];
            int s = Array.IndexOf(args, "-uninarok-probe-seconds");
            if (s >= 0 && s + 1 < args.Length && float.TryParse(args[s + 1], out float seconds)) probe._seconds = seconds;
        }

        private void Start()
        {
            Application.runInBackground = true;
            _startTime = Time.realtimeSinceStartup;
            _gcBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcCount = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
            _notes.AppendLine("mode=" + _mode + " seconds=" + _seconds + " unity=" + Application.unityVersion + " platform=" + Application.platform +
                              " development=" + Debug.isDebugBuild + " backend=" + ScriptingBackend());
            _notes.AppendLine("recorders valid: bytes=" + _gcBytes.Valid + " count=" + _gcCount.Valid);

            if (_mode == "threads") { RunThreadCheck(); return; }
            if (_mode == "baseline") { BeginPhase("baseline idle"); return; }

            _client = NetworkClient.Instance != null ? NetworkClient.Instance : FindFirstObjectByType<NetworkClient>();
            if (_client == null) { Finish("no NetworkClient in the scene"); return; }
            BeginPhase("login to map");
        }

        private static string ScriptingBackend()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#elif ENABLE_MONO
            return "Mono";
#else
            return "CoreCLR";
#endif
        }

        private void Update()
        {
            if (_finished) return;
            Sample();

            if (_mode == "baseline")
            {
                if (Time.realtimeSinceStartup - _phaseStart >= _seconds) { EndPhase(); Finish("ok"); }
                return;
            }
            if (_mode != "network") return;

            FlowState state = _client.Flow.State;
            if (_phase.Name == "login to map")
            {
                if (state == FlowState.InGame) { EndPhase(); BeginPhase("in game idle"); }
                else if (state == FlowState.Failed || Time.realtimeSinceStartup - _startTime > 40f)
                {
                    _notes.AppendLine("flow did not reach InGame: state=" + state + " failure=" + _client.Flow.Failure);
                    EndPhase();
                    Finish("login to map failed");
                }
            }
            else if (_phase.Name == "in game idle")
            {
                if (state != FlowState.InGame) { _notes.AppendLine("left InGame: " + state); EndPhase(); Finish("session ended early"); }
                else if (Time.realtimeSinceStartup - _phaseStart >= _seconds) { EndPhase(); Finish("ok"); }
            }
        }

        private void BeginPhase(string name)
        {
            _phase = new Phase(name);
            _phase.HeapStart = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            _phase.CollectionsStart = GC.CollectionCount(0);
            _phaseStart = Time.realtimeSinceStartup;
            _measuring = true;
        }

        private void EndPhase()
        {
            if (!_measuring) return;
            _phase.Seconds = Time.realtimeSinceStartup - _phaseStart;
            _phase.HeapEnd = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            _phase.CollectionsEnd = GC.CollectionCount(0);
            _done.Add(_phase);
            _measuring = false;
        }

        // The recorder's last value is the allocation of the last completed frame on the main thread.
        private void Sample()
        {
            if (!_measuring || !_gcBytes.Valid) return;
            long bytes = _gcBytes.LastValue;
            _phase.Frames++;
            _phase.SumBytes += bytes;
            if (bytes > _phase.MaxBytes) _phase.MaxBytes = bytes;
            if (bytes > 0)
            {
                _phase.FramesWithAlloc++;
                if (_phase.AllocFrames.Count < 12)
                    _phase.AllocFrames.Add("+" + (Time.realtimeSinceStartup - _phaseStart).ToString("F2") + " s: " + bytes + " B");
            }
            if (_gcCount.Valid) _phase.SumCount += _gcCount.LastValue;
        }

        // ---- thread check (T9.7) ----

        private void RunThreadCheck()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            ushort port = (ushort)((IPEndPoint)listener.LocalEndpoint).Port;
            // NetworkClient is a single-instance component: reuse the scene's one (the flow is Idle and ignores these connections).
            NetworkClient client = NetworkClient.Instance != null ? NetworkClient.Instance : FindFirstObjectByType<NetworkClient>();
            if (client == null) { Finish("no NetworkClient in the scene"); return; }
            var accepted = new List<Socket>();

            NetProfiling.Install();
            int startedBefore = NetProfiling.SocketThreadsStarted, stoppedBefore = NetProfiling.SocketThreadsStopped;
            int connected = 0;
            Action<ConnectionRole> onConnected = r => connected++;
            client.Connected += onConnected;
            int maxRunning = 0;

            for (int i = 0; i < 20; i++)
            {
                int target = connected + 1;
                client.Connect("127.0.0.1", port, ConnectionRole.Map);
                accepted.Add(listener.AcceptSocket());
                var wait = Stopwatch.StartNew();
                while (connected < target && wait.ElapsedMilliseconds < 3000) { PumpClient(client); System.Threading.Thread.Sleep(2); }
                maxRunning = Math.Max(maxRunning, NetProfiling.SocketThreadsRunning);
                client.Disconnect();
            }
            int runningRightAfter = NetProfiling.SocketThreadsRunning;
            foreach (var s in accepted) s.Close();
            listener.Stop();

            // every closed connection's two threads must announce that they stop
            var settle = Stopwatch.StartNew();
            while (settle.ElapsedMilliseconds < 4000 && NetProfiling.SocketThreadsRunning > 0) System.Threading.Thread.Sleep(50);

            int started = NetProfiling.SocketThreadsStarted - startedBefore;
            int stopped = NetProfiling.SocketThreadsStopped - stoppedBefore;
            _notes.AppendLine("connect/dispose cycles: 20, connected events: " + connected);
            _notes.AppendLine("socket threads: started=" + started + " stopped=" + stopped + " running now=" + NetProfiling.SocketThreadsRunning +
                              " (right after the last Disconnect: " + runningRightAfter + ", peak while connected: " + maxRunning + ")");
            _notes.AppendLine("no leaked socket thread: " + (started == 40 && stopped == 40 && NetProfiling.SocketThreadsRunning == 0));
            Finish("ok");
        }

        private static void PumpClient(NetworkClient client)
        {
            // NetworkClient.Update pumps once per frame; here we are inside one frame, so drive it through its public contract:
            // the status flips when the connect task completes and the next Update raises Connected.
            // Yield to the player loop by waiting briefly; Update runs on the main thread between our checks only if we return,
            // so use the reflection-free path: call Update via SendMessage.
            client.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
        }

        // ---- report ----

        private void Finish(string outcome)
        {
            if (_finished) return;
            _finished = true;
            var sb = new StringBuilder();
            sb.Append(_notes);
            sb.AppendLine("outcome=" + outcome);
            foreach (Phase p in _done)
            {
                double perFrame = p.Frames == 0 ? 0 : (double)p.SumBytes / p.Frames;
                sb.AppendLine($"phase '{p.Name}': {p.Seconds:F1} s, {p.Frames} frames, GC alloc total={p.SumBytes} B, max per frame={p.MaxBytes} B, " +
                              $"frames with any alloc={p.FramesWithAlloc}, avg={perFrame:F2} B/frame, allocation calls={p.SumCount}");
                sb.AppendLine($"   all threads: managed heap {p.HeapStart} -> {p.HeapEnd} B (delta {p.HeapEnd - p.HeapStart}), GC collections {p.CollectionsStart} -> {p.CollectionsEnd}");
                if (p.AllocFrames != null && p.AllocFrames.Count > 0) sb.AppendLine("   allocating frames (first 12): " + string.Join("; ", p.AllocFrames));
            }
            string path = Path.Combine(Application.persistentDataPath, "probe-report-" + _mode + ".txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[Probe] " + sb);
            _gcBytes.Dispose();
            _gcCount.Dispose();
            Application.Quit();
        }
    }
}
#endif
