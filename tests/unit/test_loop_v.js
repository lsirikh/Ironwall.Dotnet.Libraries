// tests/unit/test_loop_v.js
// Loop V 상태기계 + Wave 2 배선 (v3.1 / TEST-04, TEST-05)
//
// 기존 test_loop_hardening.js 의 LH-07 은 stuckCount 산술만 검사했고, LH-01 은
// 승격 로직을 테스트 안에 **복제해서 그 복제본을 검증**했다 — 프로덕션 코드를 한 번도
// 호출하지 않아, Wave 1 에서 실제 승격 사다리를 삭제했는데도 계속 통과했다.
// 이 파일은 실제 모듈과 실제 훅만 구동한다.

'use strict';

const assert = require('assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const HOOKS = path.join(REPO, '.claude', 'hooks');
const H = require(path.join(HOOKS, 'advance-phase-harness.js'));
const G = require(path.join(HOOKS, '_plan-graph.js'));

let pass = 0, fail = 0;
function test(name, fn) {
  try { fn(); pass++; console.log(`  ✅ ${name}`); }
  catch (e) { fail++; console.log(`  ❌ ${name}: ${e.message}`); }
}

// 실패 결과 팩토리
const F = files => ({ pass: false, failingSet: files, message: 'boom', cmd: 't', treeSig: 'sig1', durationMs: 5 });
const P = () => ({ pass: true, cmd: 't', treeSig: 'sig1', durationMs: 5, output: 'ok' });

console.log('\n═══ LV: Loop V 상태기계 ═══\n');

test('should_enter_failing_on_first_failure', () => {
  const s = {};
  const r = H.applyTestResult(s, F(['a.js', 'b.js']));
  assert.strictEqual(r.action, 'failing');
  assert.strictEqual(s.loopV.state, 'FAILING');
  assert.strictEqual(s.loopV.attempts, 1);
});

test('should_stuck_immediately_when_failingSet_repeats', () => {
  const s = {};
  H.applyTestResult(s, F(['a.js', 'b.js']));
  const r = H.applyTestResult(s, F(['a.js', 'b.js']));
  assert.strictEqual(r.action, 'stuck', '동일 failingSet 재실패는 즉시 STUCK');
  assert.strictEqual(s.loopV.state, 'STUCK');
});

test('should_stuck_when_failingSet_grows', () => {
  const s = {};
  H.applyTestResult(s, F(['a.js']));
  const r = H.applyTestResult(s, F(['a.js', 'b.js']));
  assert.strictEqual(r.action, 'stuck', '실패가 늘면 진척이 아니다');
});

test('should_progress_when_failingSet_shrinks', () => {
  const s = {};
  H.applyTestResult(s, F(['a.js', 'b.js', 'c.js']));
  const r = H.applyTestResult(s, F(['a.js']));
  assert.strictEqual(r.action, 'progress');
  assert.strictEqual(s.loopV.attempts, 2);
  assert.strictEqual(s.loopV.state, 'FAILING');
});

test('should_exhaust_after_max_auto_fix_without_reset', () => {
  const s = {};
  const max = H.maxAutoFix();
  H.applyTestResult(s, F(['a.js', 'b.js', 'c.js', 'd.js']));      // FAILING(1)
  let last = null;
  const sets = [['a.js', 'b.js', 'c.js'], ['a.js', 'b.js'], ['a.js'], []];
  for (const set of sets) last = H.applyTestResult(s, F(set));
  // max=3 → attempts 1,2,3 후 다음은 EXHAUSTED (리셋 없음)
  assert.ok(['exhausted', 'progress'].includes(last.action), `action=${last.action}`);
  assert.ok(s.loopV.attempts >= max, `attempts=${s.loopV.attempts} (>= ${max} 여야, 리셋되면 안 됨)`);
  assert.notStrictEqual(s.loopV.state, 'IDLE', 'IDLE 로 리셋되면 안 됨');
});

test('should_block_verify_when_stuck', () => {
  const s = {};
  H.applyTestResult(s, F(['a.js']));
  H.applyTestResult(s, F(['a.js']));
  assert.strictEqual(H.isBlocked(s), true);
  assert.ok(/escalate/.test(H.blockedMessage(s)), '차단 메시지가 escalate 를 안내해야');
});

test('should_require_two_hypotheses_on_escalate', () => {
  const s = { loopV: { state: 'STUCK', attempts: 2, failingSet: ['a.js'], sigHistory: [] } };
  assert.strictEqual(H.escalate(s, '이것 하나').ok, false, '가설 1개는 거부');
  const r = H.escalate(s, '락 문제일 것; 아니면 경로 정규화');
  assert.strictEqual(r.ok, true);
  assert.strictEqual(s.loopV.state, 'ESCALATED');
  assert.strictEqual(r.hypotheses.length, 2);
});

test('should_resume_from_escalated_to_failing_1', () => {
  const s = { loopV: { state: 'ESCALATED', attempts: 3, failingSet: ['a.js'], sigHistory: ['a.js'] } };
  const r = H.applyTestResult(s, F(['a.js']));
  assert.strictEqual(r.action, 'failing');
  assert.strictEqual(s.loopV.attempts, 1, '에피소드가 새로 시작');
});

test('should_issue_evidenceRef_on_pass', () => {
  const s = {};
  H.applyTestResult(s, F(['a.js']));
  const r = H.applyTestResult(s, P());
  assert.strictEqual(r.action, 'pass');
  assert.ok(r.evidenceRef && r.evidenceRef.startsWith('sig1@'), `ref=${r.evidenceRef}`);
  assert.strictEqual(s.loopV.state, 'IDLE');
  assert.strictEqual(s.lastFailure, null);
  assert.strictEqual(s.lastEvidence.treeSig, 'sig1');
});

test('should_classify_infra_without_counter_change', () => {
  const s = {};
  H.applyTestResult(s, F(['a.js', 'b.js']));
  const before = s.loopV.attempts;
  const r = H.applyTestResult(s, { infra: true, reason: 'ETIMEDOUT', cmd: 't', message: 'timeout', durationMs: 1 });
  assert.strictEqual(r.action, 'infra');
  assert.strictEqual(s.loopV.attempts, before, 'INFRA 는 예산을 태우지 않는다');
  assert.strictEqual(s.loopV.state, 'FAILING');
});

test('should_record_expectedFail_without_transition', () => {
  const s = {};
  const r = H.applyTestResult(s, { ...F(['a.js']), expectedFail: true });
  assert.strictEqual(r.action, 'expect-fail');
  assert.strictEqual((s.loopV || {}).state, 'IDLE', 'Red 는 phase/루프 전이가 아니다');
  assert.strictEqual(s.lastVerify.expectedFail, true);
});

test('should_extract_failingSet_from_runner_output', () => {
  const out = [
    '  ❌ tests/unit/test_a.js   120ms',
    '  ✅ tests/unit/test_ok.js   10ms',
    '  실패 상세:',
    '    ❌ tests/unit/test_b.js',
    '       AssertionError at src/mod.js:41',
  ].join('\n');
  const set = H.extractFailingSet(out);
  assert.ok(set.includes('tests/unit/test_a.js'), JSON.stringify(set));
  assert.ok(set.includes('tests/unit/test_b.js'));
  assert.ok(set.includes('src/mod.js:41'));
  assert.ok(!set.includes('tests/unit/test_ok.js'), '통과한 파일은 포함되면 안 됨');
});

// ══ TEST-05: Wave 2 훅 배선 ══
console.log('\n═══ W2: 훅 배선 (실제 훅 stdin 구동) ═══\n');

// [v3.6/N-01/IMPL-14] 페이로드에 실제 이벤트명을 넣는다. 훅은 이제 hook_event_name 을
//   검증하고 남의 이벤트면 조용히 종료한다 — 이름이 없으면 옛 페이로드로 보고 통과시키지만,
//   테스트가 실제 계약과 다른 모양으로 훅을 구동하면 그 자체가 거짓 초록이 된다.
function gate(payload) {
  try {
    return execFileSync(process.execPath, [path.join(HOOKS, 'pre-tool-gate.js')], {
      input: JSON.stringify(Object.assign({ hook_event_name: 'PreToolUse' }, payload)),
      encoding: 'utf8', cwd: REPO, timeout: 20000,
    }) || '';
  } catch (e) { return '__ERR__' + (e.message || ''); }
}
const blocked = out => out.startsWith('{') && out.includes('"decision":"block"');

// [v3.6/N-08/IMPL-02] **저장소 상태에 의존하지 않는다** (FR-03).
//   예전에는 실 저장소의 `activePlan` 을 읽고, 없으면 `return` 했다 — phase 가 analysis 인
//   동안 세 케이스가 조용히 건너뛰어지면서 `17 PASS / 0 FAIL` 로 보고됐다. 그중 둘은
//   스킵 안내조차 없었다. plan 체크박스 직접 편집 차단 게이트의 살아 있는 커버리지가 0인데
//   초록이었다는 뜻이다. 이제 샌드박스에 plan 을 심고 **항상 실행**한다.
const SB = require('./_sandbox.js');
const SANDBOX_PLAN = `# 샌드박스 플랜

## Wave 1

- [ ] **[IMPL-01]** 🟢 첫 태스크
  - 파일: \`src/a.js\`
  - 예상 총 공수: 1h
`;
let _sbx = null;
function sandboxWithPlan() {
  if (_sbx) return _sbx;
  const dir = SB.makeSandbox({ prefix: 'loopv', files: { 'docs/plans/p-prd-plan.md': SANDBOX_PLAN } });
  SB.cli(dir, ['status']);
  SB.patchState(dir, s => { s.phase = 'dev'; s.track = 'C'; s.activePlan = 'docs/plans/p-prd-plan.md'; return s; });
  _sbx = dir;
  return dir;
}
function sandboxGate(dir, payload) {
  return SB.hook(dir, 'pre-tool-gate.js', Object.assign({ hook_event_name: 'PreToolUse' }, payload)).stdout || '';
}

test('should_block_task_status_edit_on_active_plan', () => {
  const dir = sandboxWithPlan();
  const out = sandboxGate(dir, {
    tool_name: 'Edit',
    tool_input: {
      file_path: 'docs/plans/p-prd-plan.md',
      old_string: '- [ ] **[IMPL-01]**',
      new_string: '- [x] **[IMPL-01]**',
    },
  });
  assert.ok(blocked(out), `상태문자 편집이 차단되어야: ${out.slice(0, 160)}`);
  assert.ok(out.includes('task'), '차단 메시지가 task CLI 를 안내해야');
});

test('should_allow_non_status_edit_on_active_plan', () => {
  const dir = sandboxWithPlan();
  const out = sandboxGate(dir, {
    tool_name: 'Edit',
    tool_input: { file_path: 'docs/plans/p-prd-plan.md', old_string: '예상 총 공수', new_string: '예상 총공수' },
  });
  assert.ok(!blocked(out), `설명 필드 편집은 통과해야: ${out.slice(0, 160)}`);
});

test('should_block_bash_write_to_state_files', () => {
  const sedCmd = 'sed' + ' -i s/a/b/ docs/memory/decisions.jsonl';
  assert.ok(blocked(gate({ tool_name: 'Bash', tool_input: { command: sedCmd } })), 'sed -i 차단');
  const redir = 'printf x > docs/memory/decisions.jsonl';
  assert.ok(blocked(gate({ tool_name: 'Bash', tool_input: { command: redir } })), '리다이렉트 차단');
});

test('should_not_false_positive_on_read_only_or_interpreter', () => {
  const cases = [
    'grep -rn pipeline-state.json docs/',
    'cat docs/memory/decisions.jsonl',
    'node -e "console.log(1)"',
    'npm test',
  ];
  for (const c of cases) {
    assert.ok(!blocked(gate({ tool_name: 'Bash', tool_input: { command: c } })), `오탐: ${c}`);
  }
});

test('should_compute_frontier_from_active_plan', () => {
  const dir = sandboxWithPlan();
  const s = G.schedule(fs.readFileSync(path.join(dir, 'docs', 'plans', 'p-prd-plan.md'), 'utf8'));
  assert.ok(s.progress.total > 0, '태스크가 파싱되어야');
  assert.strictEqual(s.parseErrors.length, 0, `파싱 오류: ${s.parseErrors.join(' / ')}`);
  assert.strictEqual(s.cycles.length, 0, '의존 순환 없어야');
  assert.ok(Array.isArray(s.unblocked), 'unblocked 계산');
  assert.ok(s.unblocked.includes('IMPL-01'), `프런티어에 IMPL-01: ${JSON.stringify(s.unblocked)}`);
});

// 샌드박스 정리 — 러너의 임시 디렉터리가 쌓이지 않게
process.on('exit', () => { if (_sbx) SB.cleanup(_sbx); });


console.log('\n═══ W3: test phase 전이 (실제 CLI 구동) ═══\n');

// [v4/후속] `advance-phase.js test` 는 Loop V 의 전수 검증 진입점인데
//   **어떤 테스트도 이 경로를 구동하지 않았다.** 그 결과 v4/N-05 가 커버리지 저장 한 줄을
//   엉뚱한 블록에 떨어뜨렸을 때(선언 없는 `if (coverage)`) 스위트는 46/46 초록이었고
//   `test` 전이는 100% ReferenceError 로 죽어 있었다 — 다운스트림 설치본이 보고해서 알았다.
//
//   `report` 가 `test` 를 건너뛰므로(`[NOTE] 건너뜀: test`) 사이클은 계속 흘러갔고,
//   출력을 버리고 호출하면 크래시가 보이지 않았다. **초록이 거짓이었던 전형이다.**
//
//   여기서 지키는 것은 "커버리지가 저장된다" 가 아니라 **"이 경로가 살아 있다"** 이다.
{
  const dir = SB.makeSandbox({ prefix: 'lvw3' });
  try {
    SB.cli(dir, ['status']);
    SB.patchState(dir, s => { s.track = 'B'; s.phase = 'dev'; return s; });

    const r = SB.cli(dir, ['test']);
    test('test 전이가 크래시하지 않는다', () => {
      assert(!/ReferenceError|is not defined/.test(r.out),
        (r.out.match(/(ReferenceError|\w+ is not defined)[^\n]*/) || [r.out.slice(-160)])[0]);
    });
    test('test 전이가 성공 종료한다', () => {
      assert(r.code === 0, `exit=${r.code} · ${r.out.slice(-200)}`);
    });
    test('phase 가 test 로 넘어간다', () => {
      const st = SB.readState(dir);
      assert(st && st.phase === 'test', `phase=${st && st.phase}`);
    });
    test('Loop V 결과가 출력된다', () => {
      assert(/Loop V/.test(r.out), r.out.slice(-200));
    });
  } finally { SB.cleanup(dir); }
}

// ── [v12/N-08] 실패 원문 로그 · 첫 실패 줄 ─────────────────────────────
//   실패 출력은 2000/500자로 잘려 상태에만 남았다. 로그는 루트의 .claude/verify-logs/ 에 쓰고 최신 cap 개만 둔다.
//   임시 루트만 쓴다 — 실제 저장소에 쓰는 테스트가 원장 근거를 오염시킨 적이 있다(DC-18).
console.log('\n═══ LV: 실패 원문 로그 (v12/N-08) ═══\n');
{
  const tmpRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'lv-log-'));
  try {
    test('should_export_saveRunLog', () => assert.strictEqual(typeof H.saveRunLog, 'function'));
    test('should_keep_only_newest_cap_logs', () => {
      const made = [];
      for (let i = 0; i < 5; i++) {
        made.push(H.saveRunLog({ root: tmpRoot, kind: 'fail', cmd: 't', output: 'out ' + i, meta: {}, now: Date.parse(`2026-09-15T00:00:0${i}Z`), cap: 3 }));
      }
      const dir = path.join(tmpRoot, '.claude', 'verify-logs');
      const files = fs.readdirSync(dir).sort();
      assert.strictEqual(files.length, 3, files.join(','));
      assert.ok(made[4] && files.includes(path.basename(made[4])), `최신이 남아야 한다: ${made[4]} · ${files.join(',')}`);
      assert.ok(made[0] && !files.includes(path.basename(made[0])), `가장 오래된 것은 지워져야 한다: ${made[0]}`);
      assert.ok(fs.readFileSync(path.join(dir, path.basename(made[4])), 'utf8').includes('out 4'));
    });
    test('should_return_null_without_throwing_when_root_unwritable', () => {
      const fileAsRoot = path.join(tmpRoot, 'not-a-dir');
      fs.writeFileSync(fileAsRoot, 'x');
      const r = H.saveRunLog({ root: fileAsRoot, kind: 'fail', cmd: 't', output: 'o', meta: {}, now: Date.now(), cap: 3 });
      assert.strictEqual(r, null);
    });
    test('should_return_first_fail_line', () => {
      assert.strictEqual(H.firstFailLine('a\n  ❌ x.js 실패\n❌ y\nb'), '❌ x.js 실패');
      assert.strictEqual(H.firstFailLine('모두 통과'), null);
    });
    // Loop A(v12/N-08, MEDIUM): cap 이 종류를 섞어 정리하면, 반복 실패 로그에 밀려 Red 증거가 가리키는 원문이 지워진다.
    test('should_apply_cap_within_same_kind_only', () => {
      const root2 = fs.mkdtempSync(path.join(os.tmpdir(), 'lv-kind-'));
      try {
        const red = H.saveRunLog({ root: root2, kind: 'red', cmd: 't', output: 'red', meta: {}, now: Date.parse('2026-09-15T01:00:00Z'), cap: 3 });
        for (let i = 1; i <= 4; i++) {
          H.saveRunLog({ root: root2, kind: 'fail', cmd: 't', output: 'f' + i, meta: {}, now: Date.parse(`2026-09-15T01:00:0${i}Z`), cap: 3 });
        }
        const files = fs.readdirSync(path.join(root2, '.claude', 'verify-logs'));
        assert.ok(red && files.includes(path.basename(red)), `red 로그가 fail 로그에 밀려 지워지면 안 된다: ${files.join(',')}`);
        assert.strictEqual(files.filter(f => /-fail\.log$/.test(f)).length, 3, files.join(','));
      } finally { fs.rmSync(root2, { recursive: true, force: true }); }
    });
  } finally { fs.rmSync(tmpRoot, { recursive: true, force: true }); }
}

// ── [v12/N-08 Loop A] test 전이의 잠금 거부 INFRA 도 원문 경로를 남긴다 ──
//   verify 경로는 INFRA 에 logFile 을 남기는데 test 전이 경로는 빠져, 잠금 거부를 맞아도 원문 위치를 알 수 없었다.
console.log('\n═══ W4: test 전이의 INFRA 원문 경로 (v12/N-08) ═══\n');
{
  const dir = SB.makeSandbox({ prefix: 'lvw4' });
  try {
    fs.writeFileSync(path.join(dir, 'CLAUDE.md'),
      '# CLAUDE.md\n```yaml\nproject_name: "sbx"\nversion: "0.0.1"\ntest_command: "node lockfail.js"\nmax_auto_fix: 3\n```\n');
    fs.writeFileSync(path.join(dir, 'lockfail.js'),
      "console.log('  ❌ [검증 잠금] 다른 실행(run x, pid 1)이 검증 중입니다 — 끝난 뒤 다시 실행하세요');\nprocess.exit(1);\n");
    SB.cli(dir, ['status']);
    SB.patchState(dir, s => { s.track = 'B'; s.phase = 'dev'; return s; });
    const r = SB.cli(dir, ['test'], { _HARNESS_TEST_RUN: '' });
    test('test 전이도 러너의 잠금 거부를 INFRA 로 본다 (대조)', () => assert(/INFRA/.test(r.out), r.out.slice(-300)));
    test('test 전이의 INFRA 감사에 logFile 이 남는다', () => {
      const rows = SB.auditRows(dir).filter(x => x.event === 'loopv-infra');
      assert(rows.length >= 1 && rows.some(x => x.logFile), JSON.stringify(rows).slice(0, 300));
    });
    test('test 전이의 INFRA 출력에 원문 경로가 보인다', () => assert(/실패 원문/.test(r.out), r.out.slice(-300)));
  } finally { SB.cleanup(dir); }
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
