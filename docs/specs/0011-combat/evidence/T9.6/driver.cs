using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Uninarok.World;
using Uninarok.World.View;
using Uninarok.World.UI;
using Uninarok.Networking.Client;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Packets.In.Map;

public static class T96Driver
{
    const string Dir = "E:/Dev/Uninarok/docs/specs/0011-combat/evidence/T9.6/";
    const int KillsWanted = 20;

    class Target
    {
        public int N; public uint Id; public int Job; public uint StartAt; public int StartDist;
        public int TooFar; public int Blows; public long DamageSum; public int Misses;
        public int LastBarHp = -1, LastBarMax = -1, MaxHp = -1; public int Reclicks;
        public string End = ""; public uint EndAt; public int DeathX, DeathY;
        public List<int> HpSeen = new List<int>();
        public List<string> Loot = new List<string>();
    }

    static StreamWriter _log;
    static string _phase = "wait-ingame";
    static uint _phaseAt;
    static float _phaseReal;
    static WorldBootstrap _world;
    static WorldSession _session;
    static Target _cur;
    static readonly List<Target> _targets = new List<Target>();
    static readonly HashSet<uint> _tried = new HashSet<uint>();
    static readonly HashSet<uint> _knownItems = new HashSet<uint>();
    static readonly Queue<uint> _lootQueue = new Queue<uint>();
    static uint _lootItem; static float _lootReal; static int _lootTries;
    static float _spawnReal = -100f;
    static int _pickedUp, _pickupFailed;
    static readonly List<string> _messages = new List<string>();
    static PacketHandler<OWN_MESSAGE> _onMsg;
    static int _countChecks, _countMismatches;
    static string _clickPath = "";
    static bool _sessionHooked;
    public static bool Done;

    public static string Start()
    {
        Directory.CreateDirectory(Dir);
        _log = new StreamWriter(Dir + "run.log", false, new UTF8Encoding(false)) { AutoFlush = true };
        Log("START T9.6 driver (real time " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")");
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        _phaseReal = Time.realtimeSinceStartup;
        return "started";
    }

    public static void Stop()
    {
        EditorApplication.update -= Tick;
        try { if (_onMsg != null && NetworkClient.Instance != null) NetworkClient.Instance.Unsubscribe(_onMsg); } catch { }
        _log?.Flush(); _log?.Dispose(); _log = null;
    }

    static uint Now => _session != null ? _session.Clock.Now : 0;

    static void Log(string s)
    {
        string line = $"{Now,10} [{_phase}] {s}";
        _log?.WriteLine(line);
    }

    static void Phase(string p)
    {
        Log("PHASE -> " + p);
        _phase = p;
        _phaseAt = Now;
        _phaseReal = Time.realtimeSinceStartup;
    }

    static float InPhase => Time.realtimeSinceStartup - _phaseReal;

    static void Hook()
    {
        if (_sessionHooked) return;
        _sessionHooked = true;
        _session.TooFarAnswered += id =>
        {
            Log($"TOO_FAR target {id} range {_session.Status.AttackRange} {OwnCell()}");
            var t = _targets.FirstOrDefault(x => x.Id == id);
            if (t != null) t.TooFar++;
        };
        _session.BlowReceived += OnBlow;
        _session.PickupAnswered += (item, result) =>
        {
            Log($"PICKUP_ACK item {item} result {result}");
        };
        _session.ExpGained += (v, a) => Log($"EXP_GAIN var {v} +{a}");
        _onMsg = (in OWN_MESSAGE p) => { _messages.Add(p.Text); Log("SERVER_MSG \"" + p.Text + "\""); };
        NetworkClient.Instance.Subscribe(_onMsg);
    }

    static void OnBlow(in Blow b)
    {
        var model = _session.Model;
        uint own = model != null ? model.OwnId : 0;
        if (b.AttackerId == own || b.TargetId == own)
            Log($"BLOW {b.AttackerId} > {b.TargetId} dmg {b.Damage}+{b.LeftDamage} {b.Kind} hits {b.Hits} aMT {b.AttackMs}");
        if (_cur != null && b.AttackerId == own && b.TargetId == _cur.Id)
        {
            _cur.Blows++;
            _cur.DamageSum += b.Damage + b.LeftDamage;
            if (b.IsMiss) _cur.Misses++;
        }
    }

    static string OwnCell()
    {
        if (_session?.Model == null || !_session.Own.TryGet(out var m, out int s)) return "own ?";
        return $"own {m[s].CellX},{m[s].CellY}{(m[s].Walking ? " walking" : "")} {m[s].State}";
    }

    static string Stats()
    {
        var s = _session.Status;
        return $"HP {s.Hp}/{s.MaxHp} SP {s.Sp}/{s.MaxSp} base {s.BaseLevel} exp {s.BaseExp}/{s.NextBaseExp} job {s.JobLevel} exp {s.JobExp}/{s.NextJobExp}";
    }

    static void CheckCounts(string where)
    {
        var model = _session.Model; var p = _world.Presenter;
        if (model == null || p == null) return;
        int units = UnityEngine.Object.FindObjectsByType<Uninarok.Units.UnitView>(FindObjectsSortMode.None).Count(v => v.gameObject.activeInHierarchy);
        int items = UnityEngine.Object.FindObjectsByType<GroundItemView>(FindObjectsSortMode.None).Count(v => v.gameObject.activeInHierarchy);
        int modelItems = 0, modelUnits = 0;
        for (int i = 0; i < model.SlotLimit; i++) if (model.IsUsed(i)) { if (model[i].IsItem) modelItems++; else modelUnits++; }
        bool ok = model.Count == p.ShownCount + p.ItemCount && modelUnits == p.ShownCount && modelItems == p.ItemCount && units == p.ShownCount && items == p.ItemCount;
        _countChecks++;
        if (!ok) _countMismatches++;
        Log($"COUNT {where}: model {model.Count} (units {modelUnits}, items {modelItems}) shown {p.ShownCount} items {p.ItemCount} activeUnitViews {units} activeItemViews {items} => {(ok ? "OK" : "MISMATCH")}");
    }

    static bool Ready()
    {
        var client = NetworkClient.Instance;
        if (client == null || client.Flow == null || client.Flow.State != FlowState.InGame) return false;
        if (_world == null) _world = UnityEngine.Object.FindFirstObjectByType<WorldBootstrap>();
        if (_world == null || _world.Session == null) return false;
        _session = _world.Session;
        return _session.Model != null && _session.Own.TryGet(out _, out _) && _world.Map != null;
    }

    static void Tick()
    {
        try { Step(); }
        catch (Exception e) { Log("EXCEPTION " + e); Phase("failed"); Finish(); }
    }

    static void Step()
    {
        if (!EditorApplication.isPlaying) { if (_phase != "done") { Log("play mode ended"); Phase("aborted"); Finish(); } return; }
        if (_phase == "done" || _phase == "failed" || _phase == "aborted") return;
        bool ready = Ready();
        if (_session != null) Hook();
        switch (_phase)
        {
            case "wait-ingame":
                if (!ready) { if (InPhase > 90) { Log("never in game"); Phase("failed"); Finish(); } return; }
                Log("IN GAME map " + _world.MapName + " " + OwnCell() + " char " + NetworkClient.Instance.Flow.Context.CharacterId);
                if (_world.MapName != "prt_fild08") { Log("wrong map"); Phase("failed"); Finish(); return; }
                Phase("settle");
                return;
            case "settle":
                if (InPhase < 4) return;
                Log("START STATUS " + Stats());
                CheckCounts("start");
                MarkItems();
                Phase("hunt");
                return;
            case "hunt": Hunt(ready); return;
            case "fight": Fight(ready); return;
            case "loot-wait": LootWait(); return;
            case "loot": Loot(); return;
            case "pre-death-stats":
                if (InPhase < 0.1f) return;
                if (!_sentPre) { _sentPre = true; Log("PRE-DEATH STATUS " + Stats()); Command("@stats"); }
                if (InPhase < 3) return;
                CheckCounts("before death");
                Command("@monster 1039 1");
                Phase("dying");
                return;
            case "dying": Dying(ready); return;
            case "prompt": Prompt(); return;
            case "respawn":
                if (!ready || _world.MapName != "prontera") { if (InPhase > 60) { Log("no respawn"); Phase("failed"); Finish(); } return; }
                if (!_respawnLogged) { _respawnLogged = true; Log("RESPAWNED map " + _world.MapName + " " + OwnCell() + " prompt open " + _session.Restart.IsOpen); }
                if (InPhase < 5) return;
                _messages.Clear();
                Log("END STATUS (at @stats) " + Stats());
                _endStats = Stats();
                Command("@stats");
                Phase("end-exp");
                return;
            case "end-exp":
                if (InPhase < 2) return;
                Log("END STATUS (at @exp) " + Stats());
                Command("@exp");
                Phase("end-clean");
                return;
            case "end-clean":
                if (InPhase < 2) return;
                Command("@killmonster2 prt_fild08");
                Phase("end-counts");
                return;
            case "end-counts":
                if (InPhase < 1 + _endCounts) return;
                CheckCounts("end " + _endCounts);
                if (++_endCounts >= 6) { Log("FINAL STATUS " + Stats()); Phase("done"); Finish(); }
                return;
        }
    }
    static bool _sentPre, _respawnLogged; static int _endCounts; static string _endStats;

    static void Command(string line)
    {
        string sent = _session.Own.Command(line);
        Log($"COMMAND {line} -> {(sent ?? "null")}");
    }

    static void MarkItems()
    {
        var m = _session.Model;
        for (int i = 0; i < m.SlotLimit; i++) if (m.IsUsed(i) && m[i].IsItem) _knownItems.Add(m[i].Id);
    }

    static int Dist(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

    static void Hunt(bool ready)
    {
        if (!ready) return;
        if (_targets.Count(t => t.End == "killed") >= KillsWanted) { Phase("pre-death-stats"); return; }
        var m = _session.Model;
        if (!_session.Own.TryGet(out _, out int own)) return;
        if (m[own].State == EntityState.Dead) { Log("died while hunting"); Phase("failed"); Finish(); return; }
        if (_session.Combat.State == CombatState.Fetching) return;
        int best = -1, bestD = int.MaxValue;
        for (int i = 0; i < m.SlotLimit; i++)
        {
            if (!m.IsUsed(i)) continue;
            ref readonly var e = ref m[i];
            if (e.Kind != EntityKind.Monster || e.State == EntityState.Dead || _tried.Contains(e.Id)) continue;
            int d = Dist(m[own].CellX, m[own].CellY, e.CellX, e.CellY);
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best < 0 || bestD > 14)
        {
            if (Time.realtimeSinceStartup - _spawnReal > 12f) { _spawnReal = Time.realtimeSinceStartup; Log("no monster within 14 cells (nearest " + (best < 0 ? "none" : bestD.ToString()) + ")"); Command("@monster 1002 3"); }
            return;
        }
        var target = m[best];
        var r = _session.Combat.Attack(target.Id, true);
        _tried.Add(target.Id);
        _cur = new Target { N = _targets.Count + 1, Id = target.Id, Job = target.Look.Job, StartAt = Now, StartDist = bestD, MaxHp = target.MaxHp };
        _targets.Add(_cur);
        Log($"ATTACK #{_cur.N} id {target.Id} class {target.Look.Job} at {target.CellX},{target.CellY}{(target.Walking ? " walking" : "")} dist {bestD} hp {target.Hp}/{target.MaxHp} {OwnCell()} -> {r} state {_session.Combat.State}");
        if (r != AttackResult.Sent && r != AttackResult.Approaching) { _cur.End = "refused " + r; _cur = null; return; }
        Phase("fight");
    }

    static void Fight(bool ready)
    {
        if (!ready) return;
        var m = _session.Model;
        bool there = m.TryGetSlot(_cur.Id, out int slot);
        if (there)
        {
            ref readonly var e = ref m[slot];
            if (e.MaxHp > 0) _cur.MaxHp = e.MaxHp;
            if (e.Hp >= 0 && (_cur.HpSeen.Count == 0 || _cur.HpSeen[_cur.HpSeen.Count - 1] != e.Hp)) _cur.HpSeen.Add(e.Hp);
            // CombatOverlay.DrawBars draws a monster's bar from these values exactly under this condition.
            if (e.Kind == EntityKind.Monster && e.State != EntityState.Dead && e.Hp > 0 && e.MaxHp > 0 && e.Hp < e.MaxHp) { _cur.LastBarHp = e.Hp; _cur.LastBarMax = e.MaxHp; }
            if (e.State == EntityState.Dead)
            {
                _cur.End = "killed"; _cur.EndAt = Now; _cur.DeathX = e.CellX; _cur.DeathY = e.CellY;
                Log($"KILLED #{_cur.N} id {_cur.Id} at {e.CellX},{e.CellY} after {(Now - _cur.StartAt)} ms blows {_cur.Blows} dmg {_cur.DamageSum} lastBar {_cur.LastBarHp}/{_cur.LastBarMax} tooFar {_cur.TooFar} combat {_session.Combat.State}");
                Phase("loot-wait");
                return;
            }
        }
        else
        {
            _cur.End = "gone"; _cur.EndAt = Now;
            Log($"GONE #{_cur.N} id {_cur.Id} (left the model alive) blows {_cur.Blows}");
            Phase("hunt");
            return;
        }
        if (_session.Combat.State == CombatState.Idle || _session.Combat.TargetId != _cur.Id)
        {
            if (_cur.Reclicks >= 3 || InPhase > 40) { _cur.End = "abandoned"; Log($"ABANDON #{_cur.N} state {_session.Combat.State}"); _session.Combat.Cancel(); Phase("hunt"); return; }
            _cur.Reclicks++;
            var r = _session.Combat.Attack(_cur.Id, true);
            Log($"RECLICK #{_cur.N} (combat was {_session.Combat.State}) -> {r}");
        }
        if (InPhase > 60) { _cur.End = "timeout"; Log("TIMEOUT #" + _cur.N); _session.Combat.Cancel(); Phase("hunt"); }
    }

    static void LootWait()
    {
        if (InPhase < 2.5f) return;
        var m = _session.Model;
        for (int i = 0; i < m.SlotLimit; i++)
        {
            if (!m.IsUsed(i) || !m[i].IsItem || _knownItems.Contains(m[i].Id)) continue;
            _knownItems.Add(m[i].Id);
            int d = Dist(m[i].CellX, m[i].CellY, _cur.DeathX, _cur.DeathY);
            Log($"ITEM {m[i].Id} item {m[i].ItemId} x{m[i].ItemAmount} at {m[i].CellX},{m[i].CellY} ({d} from the death cell)");
            if (d <= 4) { _lootQueue.Enqueue(m[i].Id); _cur.Loot.Add(m[i].ItemId + "x" + m[i].ItemAmount); }
        }
        CheckCounts("after kill #" + _cur.N);
        Phase("loot");
    }

    static void Loot()
    {
        var m = _session.Model;
        if (_lootItem != 0)
        {
            if (!m.TryGetSlot(_lootItem, out _)) { Log($"PICKED item {_lootItem} (gone from the model)"); _pickedUp++; _lootItem = 0; }
            else if (Time.realtimeSinceStartup - _lootReal > 8f)
            {
                if (_lootTries < 3 && _session.Combat.State == CombatState.Idle) { _lootTries++; _lootReal = Time.realtimeSinceStartup; Log($"PICKUP again {_lootItem} -> {_session.Combat.PickUp(_lootItem)}"); }
                else if (_lootTries >= 3) { Log($"PICKUP FAILED item {_lootItem}"); _pickupFailed++; _lootItem = 0; }
            }
            return;
        }
        if (_lootQueue.Count == 0) { _cur = null; Phase("hunt"); return; }
        if (!_session.Own.TryGet(out _, out int own) || m[own].Walking || _session.Combat.State != CombatState.Idle) return;
        _lootItem = _lootQueue.Dequeue();
        if (!m.TryGetSlot(_lootItem, out _)) { Log($"item {_lootItem} already gone"); _lootItem = 0; return; }
        _lootTries = 0; _lootReal = Time.realtimeSinceStartup;
        bool ok = _session.Combat.PickUp(_lootItem);
        Log($"PICKUP {_lootItem} {OwnCell()} -> {ok} state {_session.Combat.State}");
    }

    static void Dying(bool ready)
    {
        if (!ready) return;
        if (!_session.Own.TryGet(out var m, out int own)) return;
        if (m[own].State == EntityState.Dead)
        {
            Log("OWN DEAD " + OwnCell() + " " + Stats());
            Phase("prompt");
            return;
        }
        if (InPhase > 150) { Log("not dead after 150 s"); Phase("failed"); Finish(); }
    }

    static bool _clicked;
    static void Prompt()
    {
        var view = UnityEngine.Object.FindFirstObjectByType<RestartPromptView>();
        if (!_clicked)
        {
            if (view == null || !view.Shown) { if (InPhase > 10) { Log("restart prompt not shown"); Phase("failed"); Finish(); } return; }
            if (InPhase < 2) return;
            Log("RESTART PROMPT shown (view.Shown true, prompt open " + _session.Restart.IsOpen + ")");
            var button = (Button)typeof(RestartPromptView).GetProperty("SavePoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(view);
            using (var ev = NavigationSubmitEvent.GetPooled()) { ev.target = button; button.SendEvent(ev); }
            _clicked = true;
            _clickPath = "NavigationSubmitEvent on the save-point button";
            Log("CLICK save-point button by " + _clickPath);
            return;
        }
        if (InPhase > 6 && NetworkClient.Instance.Flow.State == FlowState.InGame && _world.MapName == "prt_fild08" && _session.Own.TryGet(out var m, out int own) && m[own].State == EntityState.Dead)
        {
            Log("the button event did nothing after 4 s: calling RestartPrompt.ReturnToSavePoint (the button's handler)");
            _clickPath = "RestartPrompt.ReturnToSavePoint (the button handler) after the event failed";
            Log("ReturnToSavePoint -> " + _session.Restart.ReturnToSavePoint());
            _phaseReal = Time.realtimeSinceStartup;
            return;
        }
        if (NetworkClient.Instance.Flow.State != FlowState.InGame || _world.MapName == "prontera") Phase("respawn");
    }

    static void Finish()
    {
        var b = new StringBuilder();
        b.AppendLine("n\tid\tclass\tstartDist\ttooFar\tblows\tmisses\tdamage\tmaxHp\tlastBarHp\tlastBarMax\thpSeen\treclicks\tend\tms\tloot");
        foreach (var t in _targets)
            b.AppendLine($"{t.N}\t{t.Id}\t{t.Job}\t{t.StartDist}\t{t.TooFar}\t{t.Blows}\t{t.Misses}\t{t.DamageSum}\t{t.MaxHp}\t{t.LastBarHp}\t{t.LastBarMax}\t{string.Join(",", t.HpSeen)}\t{t.Reclicks}\t{t.End}\t{(t.EndAt != 0 ? t.EndAt - t.StartAt : 0)}\t{string.Join(" ", t.Loot)}");
        b.AppendLine($"picked {_pickedUp} pickupFailed {_pickupFailed} countChecks {_countChecks} mismatches {_countMismatches} click {_clickPath}");
        b.AppendLine("endStats " + _endStats);
        b.AppendLine("messages:"); foreach (var s in _messages) b.AppendLine("  " + s);
        File.WriteAllText(Dir + "targets.tsv", b.ToString());
        Log("FINISH phase " + _phase);
        Done = true;
        EditorApplication.update -= Tick;
        try { if (_onMsg != null && NetworkClient.Instance != null) NetworkClient.Instance.Unsubscribe(_onMsg); } catch { }
        _log?.Flush();
    }

    public static string Status() => $"{_phase} kills {_targets.Count(t => t.End == "killed")} targets {_targets.Count} picked {_pickedUp} done {Done}";
}
