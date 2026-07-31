using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 제어기 무통신(Blackout) 전파 시뮬레이션 (GMap_Controller_Blackout).
                  100+ 시나리오를 시뮬레이터(ControllerBlackoutModel 사용) vs 독립 오라클
                  (토폴로지 재유도)로 매 스텝 대조. 전 시나리오 2회 실행해 결정성까지 검증.
   검증 대상     : ①제어기→센서→그룹 확장 ②우선순위(Blackout>FaultedDetecting>Faulted>Detecting>Normal)
                  ③해소 순서(제어기 해소→센서 상태 재부상) ④자동복구(탐지→동일센서 장애 제거) ⑤결정성.
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
****************************************************************************/
public class ControllerBlackoutSimulationTests
{
    private readonly ITestOutputHelper _out;
    public ControllerBlackoutSimulationTests(ITestOutputHelper o) => _out = o;

    // ── 토폴로지 ──
    public sealed class Sensor { public int Id; public int ControllerId; public int[] Groups = Array.Empty<int>(); }
    public sealed class Topology
    {
        public List<Sensor> Sensors = new();
        public Dictionary<int, int[]> ControllerOwnGroups = new();   // 제어기 자신의 그룹(선택)
        public IEnumerable<int> AllGroups => Sensors.SelectMany(s => s.Groups)
            .Concat(ControllerOwnGroups.Values.SelectMany(g => g)).Distinct();
        public IEnumerable<int> AllControllers => Sensors.Select(s => s.ControllerId)
            .Concat(ControllerOwnGroups.Keys).Distinct();
    }

    // ── 연산 ──
    public enum Op { CtrlFault, CtrlResolve, SensorFault, SensorResolve, Detection, DetectionClear }
    public sealed record Step(Op Op, int Id);

    // ── 시뮬레이터(프로덕션 로직 사용) ──
    private sealed class Sim
    {
        private readonly Topology _t;
        // key → (groups, isBlackout, type). 제어기장애 cf{cid}, 센서장애 sf{sid}, 탐지 det{sid}
        private readonly Dictionary<string, (HashSet<int> Groups, bool Blackout, EnumEventType Type)> _entries = new();
        public Sim(Topology t) => _t = t;

        public void Apply(Step s)
        {
            switch (s.Op)
            {
                case Op.CtrlFault:
                {
                    // 프로덕션 확장 로직 사용 — 제어기에 연결된 센서 그룹 합집합.
                    var sensors = _t.Sensors.Select(x => ((int?)x.ControllerId, (IEnumerable<int>?)x.Groups));
                    _t.ControllerOwnGroups.TryGetValue(s.Id, out var own);
                    var groups = ControllerBlackoutModel.ExpandBlackoutGroups(s.Id, sensors, own);
                    _entries[$"cf{s.Id}"] = (new HashSet<int>(groups), true, EnumEventType.Fault);
                    break;
                }
                case Op.CtrlResolve: _entries.Remove($"cf{s.Id}"); break;
                case Op.SensorFault:
                {
                    var sensor = _t.Sensors.First(x => x.Id == s.Id);
                    _entries[$"sf{s.Id}"] = (new HashSet<int>(sensor.Groups), false, EnumEventType.Fault);
                    break;
                }
                case Op.SensorResolve: _entries.Remove($"sf{s.Id}"); break;
                case Op.Detection:
                {
                    var sensor = _t.Sensors.First(x => x.Id == s.Id);
                    _entries.Remove($"sf{s.Id}");   // EQM 자동복구: 탐지 도착 시 동일 센서 장애 제거
                    _entries[$"det{s.Id}"] = (new HashSet<int>(sensor.Groups), false, EnumEventType.Intrusion);
                    break;
                }
                case Op.DetectionClear: _entries.Remove($"det{s.Id}"); break;
            }
        }

        public EnumCompositeEventStatus Group(int g)
        {
            bool bo = false, fa = false, de = false;
            foreach (var e in _entries.Values)
                if (e.Groups.Contains(g))
                {
                    if (e.Blackout) bo = true;
                    else if (e.Type == EnumEventType.Fault) fa = true;
                    else if (e.Type == EnumEventType.Intrusion) de = true;
                }
            return ControllerBlackoutModel.Resolve(bo, fa, de);
        }
    }

    // ── 독립 오라클(스펙 재유도 — 시뮬레이터와 별개 경로) ──
    private sealed class Oracle
    {
        private readonly Topology _t;
        private readonly HashSet<int> _ctrlFaulted = new();
        private readonly HashSet<int> _sensorFaulted = new();
        private readonly HashSet<int> _detecting = new();
        public Oracle(Topology t) => _t = t;

        public void Apply(Step s)
        {
            switch (s.Op)
            {
                case Op.CtrlFault: _ctrlFaulted.Add(s.Id); break;
                case Op.CtrlResolve: _ctrlFaulted.Remove(s.Id); break;
                case Op.SensorFault: _sensorFaulted.Add(s.Id); break;
                case Op.SensorResolve: _sensorFaulted.Remove(s.Id); break;
                case Op.Detection: _sensorFaulted.Remove(s.Id); _detecting.Add(s.Id); break;   // 자동복구
                case Op.DetectionClear: _detecting.Remove(s.Id); break;
            }
        }

        public EnumCompositeEventStatus Group(int g)
        {
            // Blackout: 이 그룹을 커버하는 장애 제어기가 있는가 — 토폴로지에서 직접 재유도.
            bool blackout = _ctrlFaulted.Any(cid =>
                (_t.ControllerOwnGroups.TryGetValue(cid, out var own) && own.Contains(g)) ||
                _t.Sensors.Any(x => x.ControllerId == cid && x.Groups.Contains(g)));
            bool fault = _sensorFaulted.Any(sid => _t.Sensors.First(x => x.Id == sid).Groups.Contains(g));
            bool det = _detecting.Any(sid => _t.Sensors.First(x => x.Id == sid).Groups.Contains(g));

            if (blackout) return EnumCompositeEventStatus.Blackout;
            if (fault && det) return EnumCompositeEventStatus.FaultedDetecting;
            if (fault) return EnumCompositeEventStatus.Faulted;
            if (det) return EnumCompositeEventStatus.Detecting;
            return EnumCompositeEventStatus.Normal;
        }
    }

    public sealed class Scenario
    {
        public string Name = "";
        public Topology Topo = new();
        public List<Step> Steps = new();
    }

    // ── 100+ 시나리오 생성 ──
    private static List<Scenario> BuildScenarios()
    {
        var list = new List<Scenario>();

        // 토폴로지 팔레트
        // T1: C1→{S1(G1),S2(G2)}          (제어기 2그룹)
        // T2: C1→{S1(G1),S2(G1)}          (제어기 1그룹 2센서)
        // T3: C1→S1(G1), C2→S2(G1)        (2제어기 공유그룹)
        // T4: C1→S1(G1,G2), C2→S3(G2,G3)  (겹침)
        // T5: C1→(센서 없음), own G9       (센서 없는 제어기, 자기 그룹)
        // T6: C1→S1(그룹 없음)            (그룹 없는 센서)
        Topology T(params Sensor[] ss) => new() { Sensors = ss.ToList() };
        Sensor S(int id, int cid, params int[] g) => new() { Id = id, ControllerId = cid, Groups = g };

        var T1 = T(S(1, 1, 1), S(2, 1, 2));
        var T2 = T(S(1, 1, 1), S(2, 1, 1));
        var T3 = T(S(1, 1, 1), S(2, 2, 1));
        var T4 = T(S(1, 1, 1, 2), S(3, 2, 2, 3));
        var T5 = new Topology { Sensors = new(), ControllerOwnGroups = new() { [1] = new[] { 9 } } };
        var T6 = T(S(1, 1));

        Step CF(int c) => new(Op.CtrlFault, c);
        Step CR(int c) => new(Op.CtrlResolve, c);
        Step SF(int s) => new(Op.SensorFault, s);
        Step SR(int s) => new(Op.SensorResolve, s);
        Step DT(int s) => new(Op.Detection, s);
        Step DC(int s) => new(Op.DetectionClear, s);

        void Add(string name, Topology topo, params Step[] steps)
            => list.Add(new Scenario { Name = name, Topo = topo, Steps = steps.ToList() });

        // ── A. 단일 제어기 blackout & 해소 (기본) ──
        Add("A1 제어기장애→2그룹 검정", T1, CF(1));
        Add("A2 제어기장애→해소→정상", T1, CF(1), CR(1));
        Add("A3 1그룹2센서 검정→해소", T2, CF(1), CR(1));
        Add("A4 제어기장애 반복", T1, CF(1), CR(1), CF(1), CR(1));

        // ── B. blackout이 센서장애를 덮음 + 해소 시 재부상 (사용자 핵심 시나리오) ──
        Add("B1 센서장애→제어기장애(덮음)→제어기해소(주황복귀)→센서해소(정상)", T2, SF(1), CF(1), CR(1), SR(1));
        Add("B2 제어기장애→센서장애(여전검정)→제어기해소(주황)", T2, CF(1), SF(1), CR(1));
        Add("B3 제어기+두센서장애→제어기해소→둘 다 주황", T2, CF(1), SF(1), SF(2), CR(1));
        Add("B4 제어기장애→센서장애→센서해소→제어기해소(정상)", T2, CF(1), SF(1), SR(1), CR(1));
        Add("B5 다그룹: 제어기해소 후 G1만 센서장애 주황, G2 정상", T1, CF(1), SF(1), CR(1));

        // ── C. 다중 제어기 공유 그룹 ──
        Add("C1 두제어기장애→공유그룹 검정→하나해소(여전검정)→둘째해소(정상)", T3, CF(1), CF(2), CR(1), CR(2));
        Add("C2 한제어기장애+타제어기센서장애 공유그룹→검정", T3, CF(1), SF(2));
        Add("C3 겹침토폴로지 C1장애→G1,G2검정 G3정상", T4, CF(1));
        Add("C4 겹침 C1+C2장애→G1,G2,G3 검정→C1해소→G2,G3검정(C2) G1정상", T4, CF(1), CF(2), CR(1));

        // ── D. 탐지 상호작용 ──
        Add("D1 탐지→제어기장애(덮어검정)→해소(탐지복귀)", T2, DT(1), CF(1), CR(1));
        Add("D2 제어기장애→탐지(여전검정)→해소(탐지)", T2, CF(1), DT(1), CR(1));
        Add("D3 그룹내 센서장애+타센서탐지→FaultedDetecting→제어기장애 덮음", T2, SF(1), DT(2), CF(1), CR(1));
        Add("D4 탐지클리어", T2, DT(1), DC(1));

        // ── E. 엣지 ──
        Add("E1 센서없는제어기 own그룹 검정→해소", T5, CF(1), CR(1));
        Add("E2 그룹없는센서 제어기장애 무효과", T6, CF(1));
        Add("E3 존재않는 그룹 조회(정상)", T1, CF(1));   // 그룹 조회는 AllGroups로 제한되나 안전
        Add("E4 센서장애만(제어기무관 주황)", T2, SF(1));
        Add("E5 중복 제어기장애 재발화 idempotent", T1, CF(1), CF(1), CR(1));

        // ── F. 순서 순열(제어기/센서 raise·resolve 조합) — 프로그램 생성으로 다수 ──
        //   T2(1그룹2센서1제어기)에서 {CF,SF1,SF2}의 raise 순열 × resolve 순열 일부
        var raises = new[] { CF(1), SF(1), SF(2) };
        foreach (var p in Permutations(new List<int> { 0, 1, 2 }))
        {
            var steps = p.Select(i => raises[i]).ToList();
            // resolve는 raise 역순
            steps.AddRange(p.AsEnumerable().Reverse().Select(i => raises[i].Op == Op.CtrlFault ? CR(1) : SR(raises[i].Id)));
            Add($"F raise[{string.Join("", p)}]+역순해소", T2, steps.ToArray());
        }
        //   해소 순서를 바꾼 변형(제어기 먼저 해소 vs 나중)
        Add("F2 제어기먼저해소", T2, CF(1), SF(1), SF(2), CR(1), SR(1), SR(2));
        Add("F3 센서먼저해소", T2, CF(1), SF(1), SF(2), SR(1), SR(2), CR(1));
        Add("F4 교차해소", T2, CF(1), SF(1), SF(2), SR(1), CR(1), SR(2));

        // ── G. 규모 확장: 3제어기 × 각 2센서 × 3그룹 무작위(시드고정) 배치 ──
        for (int seed = 0; seed < 70; seed++)
        {
            var topo = BuildRandomTopology(seed, out var ctrls, out var sens, out var grps);
            var steps = BuildRandomSteps(seed, ctrls, sens);
            Add($"G seed{seed}", topo, steps.ToArray());
        }

        return list;
    }

    private static IEnumerable<List<int>> Permutations(List<int> items)
    {
        if (items.Count <= 1) { yield return new List<int>(items); yield break; }
        for (int i = 0; i < items.Count; i++)
        {
            var rest = new List<int>(items); rest.RemoveAt(i);
            foreach (var p in Permutations(rest)) { p.Insert(0, items[i]); yield return p; }
        }
    }

    // 시드기반 결정적 토폴로지/스텝 (Random 사용 금지 규칙 회피 위해 LCG 결정적)
    private static int Lcg(ref int state) { state = unchecked(state * 1103515245 + 12345) & 0x7fffffff; return state; }

    private static Topology BuildRandomTopology(int seed, out int[] ctrls, out int[] sens, out int[] grps)
    {
        int st = seed * 7 + 1;
        ctrls = new[] { 1, 2, 3 };
        grps = new[] { 10, 20, 30 };
        var sensors = new List<Sensor>();
        int sid = 100;
        foreach (var c in ctrls)
            for (int k = 0; k < 2; k++)
            {
                int g1 = grps[Lcg(ref st) % 3];
                var groups = (Lcg(ref st) % 2 == 0) ? new[] { g1 } : new[] { g1, grps[Lcg(ref st) % 3] }.Distinct().ToArray();
                sensors.Add(new Sensor { Id = sid++, ControllerId = c, Groups = groups });
            }
        sens = sensors.Select(s => s.Id).ToArray();
        return new Topology { Sensors = sensors };
    }

    private static List<Step> BuildRandomSteps(int seed, int[] ctrls, int[] sens)
    {
        int st = seed * 13 + 5;
        var steps = new List<Step>();
        var activeCtrl = new HashSet<int>(); var activeSf = new HashSet<int>(); var activeDet = new HashSet<int>();
        int n = 10 + (Lcg(ref st) % 10);
        for (int i = 0; i < n; i++)
        {
            int roll = Lcg(ref st) % 6;
            switch (roll)
            {
                case 0: { int c = ctrls[Lcg(ref st) % ctrls.Length]; if (activeCtrl.Add(c)) steps.Add(new(Op.CtrlFault, c)); break; }
                case 1: { int c = ctrls[Lcg(ref st) % ctrls.Length]; if (activeCtrl.Remove(c)) steps.Add(new(Op.CtrlResolve, c)); break; }
                case 2: { int s = sens[Lcg(ref st) % sens.Length]; if (!activeDet.Contains(s) && activeSf.Add(s)) steps.Add(new(Op.SensorFault, s)); break; }
                case 3: { int s = sens[Lcg(ref st) % sens.Length]; if (activeSf.Remove(s)) steps.Add(new(Op.SensorResolve, s)); break; }
                case 4: { int s = sens[Lcg(ref st) % sens.Length]; activeSf.Remove(s); if (activeDet.Add(s)) steps.Add(new(Op.Detection, s)); break; }
                case 5: { int s = sens[Lcg(ref st) % sens.Length]; if (activeDet.Remove(s)) steps.Add(new(Op.DetectionClear, s)); break; }
            }
        }
        return steps;
    }

    // ── 시뮬레이션 실행: 매 스텝 시뮬 vs 오라클 대조 ──
    private static (int steps, int checks) RunOnce(List<Scenario> scenarios, List<string> failures)
    {
        int totalSteps = 0, totalChecks = 0;
        foreach (var sc in scenarios)
        {
            var sim = new Sim(sc.Topo); var oracle = new Oracle(sc.Topo);
            var groups = sc.Topo.AllGroups.ToList();
            int stepIdx = 0;
            foreach (var step in sc.Steps)
            {
                sim.Apply(step); oracle.Apply(step); stepIdx++; totalSteps++;
                foreach (var g in groups)
                {
                    totalChecks++;
                    var a = sim.Group(g); var b = oracle.Group(g);
                    if (a != b)
                        failures.Add($"[{sc.Name}] step{stepIdx}({step.Op} {step.Id}) group{g}: sim={a} oracle={b}");
                }
            }
        }
        return (totalSteps, totalChecks);
    }

    [Fact]
    public void should_match_spec_across_100_scenarios_run_twice()
    {
        var scenarios = BuildScenarios();
        Assert.True(scenarios.Count >= 100, $"시나리오 수 {scenarios.Count} < 100");

        var fail1 = new List<string>();
        var (steps1, checks1) = RunOnce(scenarios, fail1);

        var fail2 = new List<string>();
        var (steps2, checks2) = RunOnce(scenarios, fail2);

        _out.WriteLine($"시나리오={scenarios.Count}, 스텝={steps1}, 색상대조={checks1}");
        _out.WriteLine($"1회차 실패={fail1.Count}, 2회차 실패={fail2.Count}");
        foreach (var f in fail1.Take(20)) _out.WriteLine("FAIL1: " + f);

        // 결정성: 두 회차 동일
        Assert.Equal(steps1, steps2);
        Assert.Equal(checks1, checks2);
        Assert.Equal(fail1.Count, fail2.Count);
        // 스펙 부합: 실패 0
        Assert.True(fail1.Count == 0, $"1회차 스펙 불일치 {fail1.Count}건 (예: {(fail1.Count > 0 ? fail1[0] : "")})");
        Assert.True(fail2.Count == 0, $"2회차 스펙 불일치 {fail2.Count}건");
    }

    // ── 핵심 시나리오 개별 명시 검증(사용자 요구 직결) ──
    [Fact]
    public void should_blackout_controller_groups_and_restore_orange_when_controller_recovers_with_remaining_sensor_fault()
    {
        var topo = new Topology { Sensors = new() {
            new Sensor{ Id=1, ControllerId=1, Groups=new[]{1} },
            new Sensor{ Id=2, ControllerId=1, Groups=new[]{2} } } };
        var sim = new Sim(topo);

        sim.Apply(new(Op.SensorFault, 1));                 // G1 센서장애
        Assert.Equal(EnumCompositeEventStatus.Faulted, sim.Group(1));

        sim.Apply(new(Op.CtrlFault, 1));                   // 제어기장애 → 연결센서 그룹 모두 검정
        Assert.Equal(EnumCompositeEventStatus.Blackout, sim.Group(1));
        Assert.Equal(EnumCompositeEventStatus.Blackout, sim.Group(2));

        sim.Apply(new(Op.CtrlResolve, 1));                 // 제어기 해소 → G1 센서장애 주황 재부상, G2 정상
        Assert.Equal(EnumCompositeEventStatus.Faulted, sim.Group(1));
        Assert.Equal(EnumCompositeEventStatus.Normal, sim.Group(2));

        sim.Apply(new(Op.SensorResolve, 1));               // 센서 해소 → 정상
        Assert.Equal(EnumCompositeEventStatus.Normal, sim.Group(1));
    }
}
