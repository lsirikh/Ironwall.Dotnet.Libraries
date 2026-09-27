// tests/unit/test_task_evidence.js
// 증거의 범위·STUCK 강제·우회의 흔적 — charter harness-v3 / N-05 (FR-01~FR-08 · D1~D7)
//
// 왜 이 파일이 존재하는가:
//   `task done` 의 증거 검사는 두 줄이었다 — 증거가 있는가, treeSig 가 같은가.
//   **어느 태스크를 위한 증거인지 묻지 않았다.** 그래서 `verify --filter foo` 로 파일
//   하나만 돌려 통과하면 그 증거로 모든 IMPL·TEST 태스크를 닫을 수 있었다.
//   그리고 `isBlocked` 는 verify 와 test 전이만 봤다 — 배너는 "STUCK 에서 task done 거부"
//   라고 말했지만 게이트 코드가 없었다.
//
//   거부가 과하면 정직한 작업이 막힌다. 아래 두 축은 같은 비중이다:
//   좁힌 증거를 거부하는 케이스가 벽을 지키고, 폴백·전체실행 케이스가 벽이 함정이
//   되지 않게 지킨다. **영향 그래프는 동적 import 를 못 잡는다** — 그래프가 태스크의
//   파일을 모르면 좁힌 증거를 억지로 인정하지 않고 전체 실행을 요구하는 것이 정답이다.

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const HOOKS = path.join(REPO, '.claude', 'hooks');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 200) : ''}`); }
}

let H = null, S = null;
try { H = require(path.join(HOOKS, 'advance-phase-harness.js')); } catch { /* 아래 */ }
try { S = require(path.join(HOOKS, '_signals.js')); } catch { /* 아래 */ }

console.log('\n═══ TE: 증거의 범위와 우회의 흔적 ═══');

console.log('\n[TE-00] should_expose_evidence_and_bypass_judges');
check('harness 가 evidenceCovers 를 export 한다', !!(H && typeof H.evidenceCovers === 'function'),
  H ? 'exports: ' + Object.keys(H).join(',') : 'harness 로드 실패');
check('harness 가 bypassTrigger 를 export 한다', !!(H && typeof H.bypassTrigger === 'function'));
check('_signals 가 isHumanDecision 를 export 한다', !!(S && typeof S.isHumanDecision === 'function'),
  S ? 'exports: ' + Object.keys(S).join(',') : '_signals 로드 실패');

if (!H || typeof H.evidenceCovers !== 'function' || typeof H.bypassTrigger !== 'function'
    || !S || typeof S.isHumanDecision !== 'function') {
  console.log('\n  판정 함수가 없어 나머지를 돌릴 수 없습니다 (Red).');
  console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
  process.exit(1);
}

// ── 픽스처 ───────────────────────────────────────────────────────────
// 그래프: a.js 를 고치면 test_a.js 가 돈다. b.js 를 고치면 test_a·test_b 둘 다 돈다.
const GRAPH = {
  files: {
    '.claude/hooks/a.js': {}, '.claude/hooks/b.js': {},
    'tests/unit/test_a.js': {}, 'tests/unit/test_b.js': {},
  },
};
const AFFECTED = {
  '.claude/hooks/a.js': ['tests/unit/test_a.js'],
  '.claude/hooks/b.js': ['tests/unit/test_a.js', 'tests/unit/test_b.js'],
  'tests/unit/test_a.js': ['tests/unit/test_a.js'],
};
const affectedTests = (_g, seed) => AFFECTED[seed] || [];
const covers = o => H.evidenceCovers({ graph: GRAPH, affectedTests, ...o });

const EV_FULL = { ref: 'r@t', scope: 'full', files: null };
const EV_NARROW = { ref: 'r@t', scope: 'filter', filter: 'test_a', files: ['tests/unit/test_a.js'] };
const EV_LEGACY = { ref: 'r@t', filter: null };   // v3.5 이전 — scope·files 없음

// ── TE-01: 전체 실행 증거는 모든 태스크를 닫는다 (D3) ─────────────────
// 이 케이스가 없으면 범위 검사가 정직한 작업을 막는 함정이 된다.
console.log('\n[TE-01] should_accept_any_task_when_evidence_is_full_run');
check('전체 실행 증거 → a.js 태스크 인정',
  covers({ evidence: EV_FULL, taskFiles: ['.claude/hooks/a.js'] }).ok);
check('전체 실행 증거 → b.js 태스크 인정',
  covers({ evidence: EV_FULL, taskFiles: ['.claude/hooks/b.js'] }).ok);
check('전체 실행 증거 → 파일을 안 적은 태스크도 인정',
  covers({ evidence: EV_FULL, taskFiles: [] }).ok);
check('그래프가 없어도 전체 실행 증거는 인정',
  H.evidenceCovers({ evidence: EV_FULL, taskFiles: ['.claude/hooks/a.js'], graph: null, affectedTests }).ok);

// ── TE-02: 좁힌 증거로 다른 태스크를 닫을 수 없다 (D2) ────────────────
console.log('\n[TE-02] should_refuse_other_task_when_evidence_is_narrowed');
{
  const own = covers({ evidence: EV_NARROW, taskFiles: ['.claude/hooks/a.js'] });
  check('좁힌 증거 → 그 증거가 덮는 태스크는 인정', own.ok, own.reason);
  const other = covers({ evidence: EV_NARROW, taskFiles: ['.claude/hooks/b.js'] });
  check('좁힌 증거 → 영향 테스트가 빠진 태스크는 거부', !other.ok, other.reason);
  check('거부 사유가 evidence-too-narrow', other.reason === 'evidence-too-narrow', other.reason);
  check('빠진 테스트를 알려준다', Array.isArray(other.missing) && other.missing.includes('tests/unit/test_b.js'),
    JSON.stringify(other.missing));
}

// ── TE-03: 그래프가 모르면 전체를 요구한다 — 폴백 (D4) ────────────────
// 동적 import 를 그래프가 못 잡는다. "모르면 인정" 이 아니라 "모르면 전체" 다.
console.log('\n[TE-03] should_require_full_run_when_graph_lacks_task_files');
{
  const noGraph = H.evidenceCovers({ evidence: EV_NARROW, taskFiles: ['.claude/hooks/a.js'], graph: null, affectedTests });
  check('그래프 없음 + 좁힌 증거 → 거부', !noGraph.ok, noGraph.reason);
  check('사유가 no-graph', noGraph.reason === 'no-graph', noGraph.reason);

  const unknown = covers({ evidence: EV_NARROW, taskFiles: ['.claude/hooks/zzz.js'] });
  check('그래프에 없는 파일 + 좁힌 증거 → 거부', !unknown.ok, unknown.reason);
  check('사유가 seed-not-in-graph', unknown.reason === 'seed-not-in-graph', unknown.reason);

  const noFiles = covers({ evidence: EV_NARROW, taskFiles: [] });
  check('태스크에 파일이 안 적혔고 좁힌 증거 → 거부', !noFiles.ok, noFiles.reason);
  check('사유가 no-task-files', noFiles.reason === 'no-task-files', noFiles.reason);
}

// ── TE-04: 증거가 아예 없거나 낡은 형식 (D1) ──────────────────────────
console.log('\n[TE-04] should_handle_missing_and_legacy_evidence');
{
  const none = covers({ evidence: null, taskFiles: ['.claude/hooks/a.js'] });
  check('증거 없음 → 거부', !none.ok && none.reason === 'no-evidence', none.reason);
  // 레거시 증거는 filter 가 없으므로 전체 실행으로 읽는다 — 옛 상태를 가진 사용자를 막지 않는다
  const legacy = covers({ evidence: EV_LEGACY, taskFiles: ['.claude/hooks/b.js'] });
  check('scope 없고 filter 없는 옛 증거는 전체로 읽는다', legacy.ok, legacy.reason);
  // 그러나 filter 가 있으면 좁힌 것이 분명하다
  const legacyNarrow = covers({ evidence: { ref: 'r@t', filter: 'test_a' }, taskFiles: ['.claude/hooks/b.js'] });
  check('scope 없고 filter 있는 옛 증거는 좁힌 것으로 읽는다', !legacyNarrow.ok, legacyNarrow.reason);
}

// ── TE-05: 적재 선별 (D7) ────────────────────────────────────────────
// 신규 설치는 test_command 가 비어 모든 태스크가 --no-test 로 닫힌다.
// 그것까지 적재하면 Loop D 가 다시 요금소가 된다.
console.log('\n[TE-05] should_record_only_meaningful_bypasses');
{
  const t = o => H.bypassTrigger(o);
  check('task --force 는 적재한다', t({ kind: 'task-force', task: 'IMPL-01' }) === 'task-force:IMPL-01',
    String(t({ kind: 'task-force', task: 'IMPL-01' })));
  check('verify --force 는 적재한다', /^verify-force/.test(String(t({ kind: 'verify-force' }))),
    String(t({ kind: 'verify-force' })));
  check('test_command 가 있는데 --no-test 는 적재한다',
    /^no-test/.test(String(t({ kind: 'no-test', task: 'IMPL-02', hasTestCommand: true }))),
    String(t({ kind: 'no-test', task: 'IMPL-02', hasTestCommand: true })));
  check('test_command 미설정의 --no-test 는 적재하지 않는다',
    t({ kind: 'no-test', task: 'IMPL-02', hasTestCommand: false }) === null,
    String(t({ kind: 'no-test', task: 'IMPL-02', hasTestCommand: false })));
  check('정상 통과는 적재하지 않는다', t({ kind: 'pass' }) === null, String(t({ kind: 'pass' })));
  check('skip 은 적재하지 않는다 (plan 에 사유가 남는다)', t({ kind: 'skip', task: 'IMPL-03' }) === null,
    String(t({ kind: 'skip', task: 'IMPL-03' })));
}

// ── TE-06: 원장 행의 출처 (D8/D9) ────────────────────────────────────
console.log('\n[TE-06] should_separate_harness_rows_from_human_decisions');
{
  const rows = [
    { id: 'D-1', source: 'migration' },
    { id: 'D-2', source: 'charter' },
    { id: 'D-3', source: 'loopV' },
    { id: 'D-4', source: 'user' },
    { id: 'D-5', source: 'prd' },
    { id: 'D-6' },                       // source 미기재 = 옛 행 → 사람으로 본다
  ];
  check('charter 자동 행은 사람 결정이 아니다', S.isHumanDecision(rows[1]) === false);
  check('loopV 자동 행은 사람 결정이 아니다', S.isHumanDecision(rows[2]) === false);
  check('migration 행은 사람 결정이 아니다', S.isHumanDecision(rows[0]) === false);
  check('user 행은 사람 결정이다', S.isHumanDecision(rows[3]) === true);
  check('prd 행은 사람 결정이다', S.isHumanDecision(rows[4]) === true);
  check('source 미기재 옛 행은 사람 결정으로 센다', S.isHumanDecision(rows[5]) === true);
  check('base 이후만 센다 — 봉투 자동 행 하나는 0', S.countHumanAdded(rows.slice(0, 2), 1) === 0,
    String(S.countHumanAdded(rows.slice(0, 2), 1)));
  check('base 이후 사람 행 2개', S.countHumanAdded(rows, 3) === 3, String(S.countHumanAdded(rows, 3)));
}

// ── TE-07: 원장 꼬리 되감기 (D10) ────────────────────────────────────
console.log('\n[TE-07] should_detect_ledger_rewind');
{
  const rows = [{ id: 'D-1' }, { id: 'D-2' }, { id: 'D-3' }];
  const tail = S.ledgerTail(rows);
  check('꼬리는 행수와 마지막 id 를 담는다', tail && tail.rows === 3 && tail.lastId === 'D-3', JSON.stringify(tail));
  check('행이 늘어난 것은 되감기가 아니다', S.tailRewound(tail, [...rows, { id: 'D-4' }]) === null);
  check('행이 줄면 되감기', !!S.tailRewound(tail, rows.slice(0, 2)));
  check('행수는 같은데 마지막 id 가 바뀌면 되감기',
    !!S.tailRewound(tail, [{ id: 'D-1' }, { id: 'D-2' }, { id: 'D-9' }]));
  check('꼬리가 없으면 판정하지 않는다', S.tailRewound(null, rows) === null);
}

// ── 샌드박스: CLI 게이트 (D5/D6) ─────────────────────────────────────
// 순수 함수가 맞아도 배선이 없으면 벽은 없다. 실제 CLI 를 돌린다.
const CLAUDE_MD = `# CLAUDE.md
\`\`\`yaml
project_name: "sbx"
language: "JavaScript"
version: "0.0.1"
test_command: ""
lint_command: ""
max_auto_fix: 3
\`\`\`
`;
const PLAN = `# 플랜

## Wave 1

- [~] **[IMPL-01]** 🟢 첫 태스크
  - 파일: \`src/a.js\`
  - 예상 공수: 1h
`;

function git(dir, a) {
  const r = spawnSync('git', a, { cwd: dir, encoding: 'utf8' });
  if (r.status !== 0) throw new Error(`git ${a.join(' ')}: ${r.stderr}`);
  return (r.stdout || '').trim();
}
function sandbox() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'taskev-'));
  fs.mkdirSync(path.join(dir, '.claude', 'hooks'), { recursive: true });
  for (const f of fs.readdirSync(HOOKS)) {
    if (f.endsWith('.js')) fs.copyFileSync(path.join(HOOKS, f), path.join(dir, '.claude', 'hooks', f));
  }
  for (const d of ['docs/memory', 'docs/prds', 'docs/plans', 'src']) fs.mkdirSync(path.join(dir, d), { recursive: true });
  fs.writeFileSync(path.join(dir, 'CLAUDE.md'), CLAUDE_MD);
  fs.writeFileSync(path.join(dir, 'docs', 'memory', 'audit-log.jsonl'), '');
  fs.writeFileSync(path.join(dir, 'docs', 'memory', 'session-context.md'),
    '# 세션 컨텍스트\n\n## 다음 할 일\n\n없음\n\n## 중요 기술 결정 사항\n\n| 날짜 | 결정 | 이유 |\n|------|------|------|\n');
  fs.writeFileSync(path.join(dir, 'docs', 'plans', 'p-plan.md'), PLAN);
  fs.writeFileSync(path.join(dir, 'src', 'a.js'), '// a\n');
  git(dir, ['init', '-q']); git(dir, ['config', 'user.email', 't@t']); git(dir, ['config', 'user.name', 't']);
  git(dir, ['config', 'core.autocrlf', 'false']);
  git(dir, ['add', '-A']); git(dir, ['commit', '-q', '-m', 'init']);
  git(dir, ['checkout', '-q', '-b', 'feat/te']);
  return dir;
}
function run(dir, args) {
  return spawnSync(process.execPath, [path.join(dir, '.claude', 'hooks', 'advance-phase.js'), ...args], {
    cwd: dir, encoding: 'utf8', timeout: 30000, env: { ...process.env, CLAUDE_PROJECT_DIR: dir },
  });
}
function stateFile(dir) {
  const base = path.join(dir, '.claude');
  const d = fs.readdirSync(base).find(x => x.startsWith('.branch-'));
  return path.join(base, d, 'pipeline-state.json');
}
function patchState(dir, fn) {
  const f = stateFile(dir);
  const s = JSON.parse(fs.readFileSync(f, 'utf8'));
  fs.writeFileSync(f, JSON.stringify(fn(s) || s, null, 2) + '\n');
}

console.log('\n[TE-08] should_block_task_done_when_loop_v_stuck');
{
  let dir = null;
  try {
    dir = sandbox();
    run(dir, ['status']);                       // 브랜치 상태 시딩
    patchState(dir, s => {
      s.phase = 'dev'; s.track = 'C'; s.activePlan = 'docs/plans/p-plan.md';
      s.loopV = { state: 'STUCK', attempts: 3, sigHistory: [], failingSet: ['src/a.js'], episodeStartedAt: null };
      s.lastEvidence = { ref: 'x@y', scope: 'full', files: null, treeSig: 'zzz', at: new Date().toISOString() };
      return s;
    });
    const r1 = run(dir, ['task', 'IMPL-01', 'done']);
    const out1 = (r1.stdout || '') + (r1.stderr || '');
    check('STUCK 에서 task done 이 거부된다', r1.status === 1 && /LOOP V|STUCK/.test(out1), out1);

    const r2 = run(dir, ['task', 'IMPL-01', 'done', '--force']);
    check('--force 만으로는 열리지 않는다 (사유 필수)', r2.status === 1,
      (r2.stdout || '') + (r2.stderr || ''));

    const r3 = run(dir, ['task', 'IMPL-01', 'done', '--force', '--reason', '접근을 바꿔 수동 확인함']);
    check('--force --reason 이 탈출로다', r3.status === 0, (r3.stdout || '') + (r3.stderr || ''));

    const st = JSON.parse(fs.readFileSync(stateFile(dir), 'utf8'));
    const nd = (st.cycle && st.cycle.needsDecision) || [];
    check('그 우회가 needsDecision 에 적재된다', nd.some(x => /task-force/.test(x)), JSON.stringify(nd));
  } catch (e) {
    check('TE-08 샌드박스', false, e.message);
  } finally {
    if (dir) try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ }
  }
}

console.log('\n[TE-09] should_require_reason_for_no_test');
{
  let dir = null;
  try {
    dir = sandbox();
    run(dir, ['status']);
    patchState(dir, s => {
      s.phase = 'dev'; s.track = 'C'; s.activePlan = 'docs/plans/p-plan.md';
      return s;
    });
    const r1 = run(dir, ['task', 'IMPL-01', 'done', '--no-test']);
    check('--no-test 가 사유 없이 통과하지 않는다', r1.status === 1, (r1.stdout || '') + (r1.stderr || ''));

    const r2 = run(dir, ['task', 'IMPL-01', 'done', '--no-test', '--reason', '이 태스크는 문서만 고친다']);
    check('--no-test --reason 은 통과한다', r2.status === 0, (r2.stdout || '') + (r2.stderr || ''));

    // test_command 미설정 샌드박스 → 적재하지 않는다 (신규 설치의 기본 경로)
    const st = JSON.parse(fs.readFileSync(stateFile(dir), 'utf8'));
    const nd = (st.cycle && st.cycle.needsDecision) || [];
    check('test_command 미설정이면 --no-test 를 적재하지 않는다', !nd.some(x => /no-test/.test(x)),
      JSON.stringify(nd));

    // audit 에는 언제나 표지가 남는다 — 적재하지 않는 것과 기록하지 않는 것은 다르다
    const audit = fs.readFileSync(path.join(dir, 'docs', 'memory', 'audit-log.jsonl'), 'utf8');
    check('audit 에 우회 표지가 남는다', /"noTest"\s*:\s*true/.test(audit), audit.slice(-400));
  } catch (e) {
    check('TE-09 샌드박스', false, e.message);
  } finally {
    if (dir) try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ }
  }
}


// ── TE-20: 반증 짝 판정 (charter harness-v4-quality / N-03 · FR-02) ───
//
// 왜: 하네스는 "테스트가 통과했다" 를 강제했고 "그 테스트가 실패할 수 있었는가" 는
//   묻지 않았다. 구현 태스크는 green 증거 하나로 닫혔다. 이 세션에서 결함 10건을
//   고치며 반증 확인을 **열 번 손으로** 했는데, 하네스가 시켜서 한 것이 아니다.
//
// 설계 긴장(실측): 엄격한 "태스크별 Red 선행" 은 실제 순서와 충돌한다 —
//   N-01 의 IMPL-01(도구 제작) 시점에는 그것을 겨냥한 실패 테스트가 존재할 수 없었다.
//   그래서 **파일이 겹치는 태스크의 Red** 도 인정하고, 없으면 `--no-red --reason` 으로
//   원장에 남긴다. 강제가 아니라 기울기다.
console.log('\n[TE-20] should_require_a_falsification_pair_for_impl_tasks');
{
  const H = require(path.join(REPO, '.claude', 'hooks', 'advance-phase-harness.js'));
  check('redPairOk 가 순수 함수로 노출된다', typeof H.redPairOk === 'function', typeof H.redPairOk);

  if (typeof H.redPairOk === 'function') {
    const NOW = 'sig-after';
    const BEFORE = 'sig-before';
    const PLAN = [
      { id: 'IMPL-01', file: 'src/a.js, src/b.js' },
      { id: 'IMPL-02', file: 'src/c.js' },
      { id: 'TEST-01', file: 'tests/unit/test_a.js, src/a.js' },
      { id: 'TEST-02', file: 'tests/unit/test_c.js' },
    ];
    const red = m => m;

    const CASES = [
      ['자기 태스크의 Red 가 있고 시점이 다르면 통과', {
        taskId: 'IMPL-01', planTasks: PLAN, nowSig: NOW,
        redMap: red({ 'IMPL-01': { task: 'IMPL-01', treeSig: BEFORE } }),
      }, true],
      ['파일이 겹치는 태스크의 Red 도 인정한다', {
        taskId: 'IMPL-01', planTasks: PLAN, nowSig: NOW,
        redMap: red({ 'TEST-01': { task: 'TEST-01', treeSig: BEFORE } }),
      }, true],
      ['파일이 겹치지 않는 Red 는 인정하지 않는다', {
        taskId: 'IMPL-02', planTasks: PLAN, nowSig: NOW,
        redMap: red({ 'TEST-01': { task: 'TEST-01', treeSig: BEFORE } }),
      }, false],
      ['Red 가 없으면 거부', {
        taskId: 'IMPL-01', planTasks: PLAN, nowSig: NOW, redMap: {},
      }, false],
      ['Red 의 treeSig 가 현재와 같으면 거부 — 고친 뒤에 찍은 Red 는 반증이 아니다', {
        taskId: 'IMPL-01', planTasks: PLAN, nowSig: NOW,
        redMap: red({ 'IMPL-01': { task: 'IMPL-01', treeSig: NOW } }),
      }, false],
      ['TEST 태스크는 이 검사의 대상이 아니다', {
        taskId: 'TEST-01', planTasks: PLAN, nowSig: NOW, redMap: {},
      }, true],
      ['plan 에 없는 태스크는 파일을 모르므로 자기 Red 만 본다', {
        taskId: 'IMPL-99', planTasks: PLAN, nowSig: NOW,
        redMap: red({ 'TEST-01': { task: 'TEST-01', treeSig: BEFORE } }),
      }, false],
    ];

    for (const [label, opts, want] of CASES) {
      let got = null;
      try { got = H.redPairOk(opts); } catch (e) { got = { ok: 'THREW:' + e.message }; }
      check(label, !!(got && got.ok) === want,
        `${got && got.ok ? 'ok' : 'reject(' + (got && got.reason) + ')'} · 기대=${want ? 'ok' : 'reject'}`);
    }

    // 거부에는 언제나 이유가 붙어야 한다 — 메시지 없는 벽은 우회를 부른다
    const r = H.redPairOk({ taskId: 'IMPL-01', planTasks: PLAN, nowSig: NOW, redMap: {} });
    check('거부에 이유가 붙는다', !!(r && r.reason), JSON.stringify(r));
  }
}

// ── TE-21: 하네스 운용 경로는 노드 범위 이탈이 아니다 (FR-04) ─────────
console.log('\n[TE-21] should_not_treat_harness_telemetry_as_scope_drift');
{
  const src = fs.readFileSync(path.join(REPO, '.claude', 'hooks', 'advance-phase.js'), 'utf8');
  check('HARNESS_OWNED 가 선언돼 있다', /const HARNESS_OWNED = \[/.test(src), '');
  check('tests/baseline/ 이 포함된다', /HARNESS_OWNED = \[[\s\S]{0,300}tests\/baseline\//.test(src), '');
  check('범위 검사가 그것을 면제한다', /HARNESS_OWNED\.some\(/.test(src), '');
}

// ── TE-22: 증거 요구는 접두사가 아니라 **태스크의 파일**로 판정한다 (v5/N-03) ──
//   게이트가 `/^(IMPL|TEST)-/` 로 "코드인가" 를 추론했다. 그런데 그 태스크에는
//   `- 파일: docs/specs/x.md` 라는 선언이 이미 있고 스케줄러가 그것을 파싱해 두고 있다.
//   판정에 쓸 실측이 바로 옆에 있는데 이름표를 봤다.
//
//   결과: 산출물이 문서뿐인 노드에서 `verify` 는 skip 하고(증거 미발급) 게이트는
//   영원히 열리지 않았다. 다운스트림은 태스크를 `DOC-` 로 개명해 우회했는데,
//   **그 우회가 통한다는 사실 자체가 접두사 판정이 임의적이라는 증거다.**
//
//   완화의 방향에 주의한다 — 모르는 것은 **코드로 본다**. 목록을 잊으면 게이트가
//   켜지지 꺼지지 않아야 한다.
console.log('\n[TE-22] should_decide_evidence_requirement_from_task_files');
{
  const H = require(path.join(REPO, '.claude', 'hooks', 'advance-phase-harness.js'));
  check('taskNeedsEvidence 가 순수 함수로 노출된다', typeof H.taskNeedsEvidence === 'function', typeof H.taskNeedsEvidence);

  if (typeof H.taskNeedsEvidence === 'function') {
    const CASES = [
      ['문서만 산출하면 증거를 요구하지 않는다', 'IMPL-01', 'docs/specs/protocol.md', false],
      ['문서 여러 개도 마찬가지', 'IMPL-01', '`docs/a.md`, `docs/b.md`', false],
      ['코드가 하나라도 섞이면 요구한다', 'IMPL-01', 'docs/a.md, src/a.js', true],
      ['코드만이면 당연히 요구한다', 'IMPL-01', 'src/a.js', true],
      ['docs/ 밖의 .md 도 문서다', 'IMPL-01', 'README.md', false],
      ['모르는 확장자는 코드로 본다 — 잊으면 게이트가 켜지는 쪽이어야 한다', 'IMPL-01', 'thing.weird', true],
      ['파일 선언이 없으면 접두사로 폴백한다 (IMPL)', 'IMPL-01', '', true],
      ['파일 선언이 없으면 접두사로 폴백한다 (TEST)', 'TEST-01', null, true],
      ['파일 선언이 없으면 접두사로 폴백한다 (DOC)', 'DOC-01', '', false],
      ['DOC 접두사여도 코드를 만지면 요구한다 — 개명으로 게이트를 끄지 못한다', 'DOC-01', 'src/a.js', true],
    ];
    for (const [label, id, file, want] of CASES) {
      let got = null;
      try { got = H.taskNeedsEvidence(id, file); } catch (e) { got = { needsEvidence: 'THREW:' + e.message }; }
      check(label, !!(got && got.needsEvidence) === want, `${JSON.stringify(got)} · 기대=${want}`);
    }
    const dv = H.taskNeedsEvidence('IMPL-01', 'docs/a.md');
    check('판정 사유가 붙는다', !!(dv && dv.why), JSON.stringify(dv));
  }

  // 반증 짝도 같은 판정을 써야 한다 — 두 게이트가 다른 기준을 쓰면 하나는 반드시 틀린다.
  if (typeof H.redPairOk === 'function') {
    const PLAN2 = [
      { id: 'IMPL-10', file: 'docs/specs/x.md' },
      { id: 'IMPL-11', file: 'src/z.js' },
    ];
    const rDoc = H.redPairOk({ taskId: 'IMPL-10', planTasks: PLAN2, nowSig: 'now', redMap: {} });
    check('문서 전용 IMPL 태스크는 반증 짝의 대상이 아니다', !!(rDoc && rDoc.ok), JSON.stringify(rDoc));
    const rCode = H.redPairOk({ taskId: 'IMPL-11', planTasks: PLAN2, nowSig: 'now', redMap: {} });
    check('코드 IMPL 태스크는 여전히 Red 없이 막힌다', !(rCode && rCode.ok), JSON.stringify(rCode));
  }
}

// ── TE-23: 플랜의 잘림이 **증거 게이트를 껐다** (v6/N-01) ────────────
//   v5/N-03 이 증거 요구를 태스크의 `파일:` 로 판정하게 만들었다. 그런데 플랜 파서도
//   같은 한 줄 정규식을 쓰고 있었다 — 첫 줄이 문서 경로면 잘린 목록이 docs-only 로
//   판정돼 **코드 태스크가 증거 없이 닫힌다.**
//
//   리포트는 charter 쪽만 봤고 "범위가 좁게 판정된다"(막는 방향)로 이해했다.
//   플랜 쪽은 **여는 방향**이다 — 판정의 근거를 실측으로 옮기면 그 실측을 읽는
//   파서가 새로운 급소가 된다.
console.log('\n[TE-23] should_not_disable_the_evidence_gate_by_truncation');
{
  const PG = require(path.join(REPO, '.claude', 'hooks', '_plan-graph.js'));
  const H2 = require(path.join(REPO, '.claude', 'hooks', 'advance-phase-harness.js'));

  const PLAN = [
    '# plan',
    '',
    '- [ ] **[IMPL-01]** 스펙과 구현',
    '  - 파일: `docs/specs/protocol.md`,',
    '    `src/a.js`, `src/b.js`',
    '  - 의존: TEST-01,',
    '    TEST-02',
    '  - 예상 공수: 1h',
    '',
    '- [ ] **[TEST-01]** 가',
    '  - 파일: `tests/unit/a.js`',
    '  - 예상 공수: 1h',
    '',
    '- [ ] **[TEST-02]** 나',
    '  - 파일: `tests/unit/b.js`',
    '  - 예상 공수: 1h',
  ].join('\n');

  const g = PG.parse(PLAN);
  const t = (g.tasks || {})['IMPL-01'] || {};

  check('플랜의 여러 줄 파일 목록이 전부 읽힌다',
    /src\/a\.js/.test(t.file || '') && /src\/b\.js/.test(t.file || ''), JSON.stringify(t.file));
  check('플랜의 여러 줄 의존이 전부 읽힌다',
    (t.deps || []).includes('TEST-01') && (t.deps || []).includes('TEST-02'), JSON.stringify(t.deps));
  check('뒤 태스크를 삼키지 않는다',
    !!(g.tasks || {})['TEST-01'] && !!(g.tasks || {})['TEST-02'], Object.keys(g.tasks || {}).join(', '));

  // 이 노드의 실제 목적 — 게이트가 꺼지지 않는다
  const ev = H2.taskNeedsEvidence('IMPL-01', t.file);
  check('코드가 섞인 태스크는 증거를 요구한다 (잘림으로 게이트가 꺼지지 않는다)',
    ev.needsEvidence === true, JSON.stringify(ev));

  // 반대 방향 — 진짜 문서 전용은 여전히 면제된다
  const docOnly = PG.parse([
    '# plan', '',
    '- [ ] **[IMPL-02]** 스펙만',
    '  - 파일: `docs/a.md`,',
    '    `docs/b.md`',
    '  - 예상 공수: 1h',
  ].join('\n'));
  const ev2 = H2.taskNeedsEvidence('IMPL-02', (docOnly.tasks['IMPL-02'] || {}).file);
  check('진짜 문서 전용 태스크는 여전히 면제된다', ev2.needsEvidence === false, JSON.stringify(ev2));
}

// ── TE-24~26: 의존 게이트는 원본 deps 로 판정한다 (v12/N-01) ─────────
//   done 게이트가 표시용 `sched.blockedBy[id]` 를 썼다. `frontier()` 는 대상이 [~]·[-] 면
//   계산을 건너뛰어, 선행이 [ ] 여도 start→done 과 skip→done 이 --force 없이 통과했다.
//   start 에는 의존 검사가 아예 없었다. 우회에는 흔적도 남지 않았다.
//
//   거부는 exit 코드가 아니라 **의존 문구**로 판정한다 — done 은 증거 게이트도 exit 1 이라
//   exit 만 보면 어느 게이트가 막았는지 모르고, 공허한 Red 가 된다.
const DEP_PLAN = (pre, tgt) => `# 플랜

## Wave 1

- [${pre}] **[IMPL-01]** 선행
  - 파일: \`src/a.js\`
  - 예상 공수: 1h

- [${tgt}] **[IMPL-02]** 후속
  - 파일: \`src/b.js\`
  - 의존: IMPL-01
  - 예상 공수: 1h
`;
const DEP_MSG = /IMPL-02 의 선행 태스크가/;
const outOf = r => (r.stdout || '') + (r.stderr || '');
function auditRows(dir) {
  return fs.readFileSync(path.join(dir, 'docs', 'memory', 'audit-log.jsonl'), 'utf8')
    .split('\n').filter(Boolean).map(l => { try { return JSON.parse(l); } catch { return {}; } });
}
function planStatus(dir, id) {
  const body = fs.readFileSync(path.join(dir, 'docs', 'plans', 'p-plan.md'), 'utf8');
  const m = body.match(new RegExp('^- \\[(.)\\] \\*\\*\\[' + id + '\\]\\*\\*', 'm'));
  return m ? m[1] : null;
}
function withDepSandbox(label, pre, tgt, fn) {
  let dir = null;
  try {
    dir = sandbox();
    fs.writeFileSync(path.join(dir, 'docs', 'plans', 'p-plan.md'), DEP_PLAN(pre, tgt));
    run(dir, ['status']);
    patchState(dir, s => { s.phase = 'dev'; s.track = 'C'; s.activePlan = 'docs/plans/p-plan.md'; return s; });
    fn(dir);
  } catch (e) {
    check(label + ' 샌드박스', false, e.message);
  } finally {
    if (dir) try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ }
  }
}

console.log('\n[TE-24] should_block_task_start_when_prerequisite_untouched');
withDepSandbox('TE-24 ①', ' ', ' ', dir => {
  const r = run(dir, ['task', 'IMPL-02', 'start']);
  const out = outOf(r);
  check('선행 [ ] → start 가 의존 문구로 거부된다',
    r.status !== 0 && /IMPL-02 의 선행 태스크가 아직 착수되지 않았습니다/.test(out), out);
  check('거부 문구가 선행과 그 상태를 보인다', /IMPL-01\(\[ \]\)/.test(out), out);
  check('거부되면 plan 의 IMPL-02 는 [ ] 그대로다', planStatus(dir, 'IMPL-02') === ' ', planStatus(dir, 'IMPL-02'));
  check('감사 task-start-blocked 가 남는다',
    auditRows(dir).some(x => x.event === 'task-start-blocked' && x.task === 'IMPL-02'),
    JSON.stringify(auditRows(dir).slice(-3)));
});
withDepSandbox('TE-24 ②', '~', ' ', dir => {
  const r = run(dir, ['task', 'IMPL-02', 'start']);
  check('(대조) 선행 [~] → start 가 통과한다', r.status === 0 && planStatus(dir, 'IMPL-02') === '~', outOf(r));
});
withDepSandbox('TE-24 ③', ' ', ' ', dir => {
  const r = run(dir, ['task', 'IMPL-02', 'start', '--force', '--reason', '선행은 다른 브랜치에서 끝냈다']);
  check('--force --reason 이 start 의 탈출로다', r.status === 0 && planStatus(dir, 'IMPL-02') === '~', outOf(r));
  const st = JSON.parse(fs.readFileSync(stateFile(dir), 'utf8'));
  const nd = (st.cycle && st.cycle.needsDecision) || [];
  check('그 우회가 needsDecision 에 task-force:IMPL-02 로 적재된다', nd.includes('task-force:IMPL-02'), JSON.stringify(nd));
  check('감사 task-start-forced 가 넘긴 선행을 담는다',
    auditRows(dir).some(x => x.event === 'task-start-forced' && x.task === 'IMPL-02'
      && JSON.stringify(x.missing || []).includes('IMPL-01')),
    JSON.stringify(auditRows(dir).slice(-3)));
});

console.log('\n[TE-25] should_block_task_done_when_prerequisite_unfinished');
withDepSandbox('TE-25 ①', '~', '~', dir => {
  const r = run(dir, ['task', 'IMPL-02', 'done']);
  const out = outOf(r);
  check('선행 [~] · 대상 [~] → done 이 의존 문구로 거부된다', r.status !== 0 && DEP_MSG.test(out), out);
  check('의존 게이트가 증거 게이트보다 먼저 막는다', !/검증 증거 없음/.test(out), out);
  check('거부되면 IMPL-02 는 [~] 그대로다', planStatus(dir, 'IMPL-02') === '~', planStatus(dir, 'IMPL-02'));
});
withDepSandbox('TE-25 ②', ' ', ' ', dir => {
  const r1 = run(dir, ['task', 'IMPL-02', 'skip', '--reason', '이번 사이클 밖으로 뺐다']);
  check('선행 [ ] 에서 대상 skip 은 지금처럼 통과한다', r1.status === 0 && planStatus(dir, 'IMPL-02') === '-', outOf(r1));
  const r2 = run(dir, ['task', 'IMPL-02', 'done']);
  check('skip 해 둔 대상의 done 도 의존 문구로 거부된다', r2.status !== 0 && DEP_MSG.test(outOf(r2)), outOf(r2));
});
withDepSandbox('TE-25 ③', '-', '~', dir => {
  const r = run(dir, ['task', 'IMPL-02', 'done']);
  const out = outOf(r);
  check('(대조) 선행 [-] → 의존 게이트에 막히지 않는다', !DEP_MSG.test(out), out);
  check('(대조) 의존 게이트를 지나 증거 게이트까지 간다', /검증 증거 없음/.test(out), out);
});

console.log('\n[TE-26] should_wire_both_gates_to_original_deps');
{
  const SRC = fs.readFileSync(path.join(HOOKS, 'advance-phase.js'), 'utf8');
  const START_CALL = /depGate\(\s*sched\s*,\s*id\s*,\s*'start'\s*\)/;
  const DONE_CALL = /depGate\(\s*sched\s*,\s*id\s*,\s*'done'\s*\)/;
  // 판정: task 분기 안에서 두 게이트가 depGate 를 부르고, 거부를 표시용 blockedBy 로 하지 않는다
  const wired = src => {
    const i = src.indexOf("if (targetPhase === 'task')");
    const j = src.indexOf("if (targetPhase === 'touch')", i);
    if (i < 0 || j < 0) return { ok: false, why: 'task 분기를 찾지 못함' };
    const body = src.slice(i, j);
    const start = START_CALL.test(body), done = DONE_CALL.test(body);
    const legacy = /sched\.blockedBy\[\s*id\s*\]/.test(body);
    return { ok: start && done && !legacy, start, done, legacy };
  };
  const now = wired(SRC);
  check('start·done 게이트가 depGate 를 부르고 blockedBy[id] 로 거부하지 않는다', now.ok, JSON.stringify(now));
  // 틀린 사본 — 판정이 실제로 무언가를 재는지
  const toLegacy = SRC.replace(DONE_CALL, '{ ok: !(sched.blockedBy[id] || []).length, missing: [] }');
  check('(반증) done 게이트를 blockedBy 로 되돌린 사본은 거부된다', toLegacy !== SRC && !wired(toLegacy).ok);
  const noStart = SRC.replace(START_CALL, '{ ok: true, missing: [] }');
  check('(반증) start 게이트를 뺀 사본은 거부된다', noStart !== SRC && !wired(noStart).ok);
}

// ── TE-27~29: Loop A 지적 — 흔적 없는 우회 · 보이지 않는 근거 (v12/N-01) ─
//   ① `task reconcile` 은 plan 에 손으로 적은 [x] 를 그대로 흡수한다. 의존 게이트를 거치지 않은
//      완료가 감사에도 needsDecision 에도 남지 않았다 — F-01 의 "흔적 없는 우회" 가 이 경로에 남았다.
//      흡수 자체는 막지 않는다(손 편집 plan 을 따라잡는 명령이다). 흔적만 남긴다.
//   ② depGate 는 plan 에 없는 선행을 무시하고, 그 근거는 "parseErrors 가 보고한다" 였다.
//      그런데 parseErrors 를 보여 주는 곳은 reconcile 뿐이었다 — 오타 선행이 조용히 사라졌다.
//   ③ start 게이트가 생기면서 순환 의존 plan 은 --force 없이 아무것도 시작할 수 없다.
//      거부 문구가 원인을 말하지 않으면 사람은 "선행을 먼저 start" 하려다 같은 벽에 다시 선다.
function withPlanSandbox(label, planText, fn) {
  let dir = null;
  try {
    dir = sandbox();
    fs.writeFileSync(path.join(dir, 'docs', 'plans', 'p-plan.md'), planText);
    run(dir, ['status']);
    patchState(dir, s => { s.phase = 'dev'; s.track = 'C'; s.activePlan = 'docs/plans/p-plan.md'; return s; });
    fn(dir);
  } catch (e) {
    check(label + ' 샌드박스', false, e.message);
  } finally {
    if (dir) try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ }
  }
}
const planOf = rows => '# 플랜\n\n## Wave 1\n\n' + rows.map(([st, id, deps]) =>
  `- [${st}] **[${id}]** ${id}\n  - 파일: \`src/${id.toLowerCase()}.js\`\n` + (deps ? `  - 의존: ${deps}\n` : '')).join('\n');

console.log('\n[TE-27] should_leave_a_trace_when_reconcile_absorbs_an_order_violation');
withDepSandbox('TE-27 ①', ' ', 'x', dir => {
  const r = run(dir, ['task', 'reconcile']);
  const out = outOf(r);
  check('reconcile 자체는 막지 않는다 (손 편집 plan 을 따라잡는 명령)', r.status === 0, out);
  const ev = auditRows(dir).find(x => x.event === 'task-reconcile-violation');
  check('의존 게이트를 거치지 않은 완료가 감사에 남는다',
    !!ev && JSON.stringify(ev.tasks || []).includes('IMPL-02'), JSON.stringify(auditRows(dir).slice(-3)));
  const st = JSON.parse(fs.readFileSync(stateFile(dir), 'utf8'));
  const nd = (st.cycle && st.cycle.needsDecision) || [];
  check('그 우회가 needsDecision 에 reconcile-violation:IMPL-02 로 적재된다', nd.includes('reconcile-violation:IMPL-02'), JSON.stringify(nd));
  check('화면에도 우회라고 말한다', /의존 게이트를 거치지 않은 완료/.test(out), out);
});
withDepSandbox('TE-27 ②', ' ', '-', dir => {
  run(dir, ['task', 'reconcile']);
  check('(대조) 대상 skip 은 위반으로 적재하지 않는다 — skip 은 선행과 무관하게 허용된다',
    !auditRows(dir).some(x => x.event === 'task-reconcile-violation'));
});
withDepSandbox('TE-27 ③', 'x', 'x', dir => {
  run(dir, ['task', 'reconcile']);
  check('(대조) 선행이 끝난 완료는 위반이 아니다', !auditRows(dir).some(x => x.event === 'task-reconcile-violation'));
});

console.log('\n[TE-28] should_show_plan_parse_errors_on_start_and_done');
withPlanSandbox('TE-28', planOf([[' ', 'IMPL-01'], [' ', 'IMPL-02', 'IMLP-01']]), dir => {
  const r1 = run(dir, ['task', 'IMPL-02', 'start']);
  const out1 = outOf(r1);
  check('오타 선행은 판정에서 빠지지만 start 가 파싱 경고를 보인다',
    /존재하지 않는/.test(out1) && /IMLP-01/.test(out1), out1);
  const r2 = run(dir, ['task', 'IMPL-02', 'done']);
  check('done 도 같은 경고를 보인다', /존재하지 않는/.test(outOf(r2)), outOf(r2));
});

console.log('\n[TE-29] should_name_the_cycle_when_start_is_blocked_by_it');
withPlanSandbox('TE-29', planOf([[' ', 'IMPL-01', 'IMPL-02'], [' ', 'IMPL-02', 'IMPL-01']]), dir => {
  const r = run(dir, ['task', 'IMPL-01', 'start']);
  const out = outOf(r);
  check('순환 의존에서 start 는 거부되고 원인이 순환이라고 말한다', r.status !== 0 && /순환 의존/.test(out), out);
  check('거부 문구가 순환 경로를 보인다', /IMPL-0[12] → IMPL-0[12] → IMPL-0[12]/.test(out), out);
});



console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
