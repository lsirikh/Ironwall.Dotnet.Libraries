// tests/unit/test_loop_ac.js
// Loop A 기록·리셋 · Loop C 마커 소유권 — charter harness-v3 / N-06 (FR-01~FR-11 · D1~D9)
//
// 왜 이 파일이 존재하는가:
//   Loop A 는 v3.2 도입 이후 프로덕션에서 **한 번도 발화한 적이 없다.** 전 이력의
//   `loop-a-review` 는 1건이고 그마저 훅을 막 작성하고 손으로 찔러본 합성 행이다.
//   그런데 `agent_type:'code-reviewer'` 로 훅을 구동하는 테스트가 0건이었다 — 그래서
//   "안 돌렸다" 와 "못 잡았다" 를 구분할 수 없었다. 이 파일이 그 구분을 만든다.
//
//   Loop C 마커는 전역 단일 파일이었다. 위반 없이 끝난 세션이 무조건 unlink 로 **남의
//   위반을 지웠고**, 위반과 함께 끝난 세션은 notified_sessions 를 비워 남의 사이클
//   내용을 남에게 다시 보여줬다. 게이트로 올리기 전에 그 바닥을 먼저 깐다.
//
//   **불변식**: subagent-stop 은 어떤 경로에서도 exit 1 하지 않는다 (charter, v3.2 D4).

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const HOOKS_SRC = path.join(REPO, '.claude', 'hooks');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 200) : ''}`); }
}

// ── 샌드박스 ─────────────────────────────────────────────────────────
const CLAUDE_MD = `# CLAUDE.md
\`\`\`yaml
project_name: "sbx"
language: "JavaScript"
version: "0.0.1"
test_command: ""
max_auto_fix: 3
\`\`\`
`;
function git(dir, a) {
  const r = spawnSync('git', a, { cwd: dir, encoding: 'utf8' });
  if (r.status !== 0) throw new Error(`git ${a.join(' ')}: ${r.stderr}`);
  return (r.stdout || '').trim();
}
function sandbox() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'loopac-'));
  fs.mkdirSync(path.join(dir, '.claude', 'hooks'), { recursive: true });
  for (const f of fs.readdirSync(HOOKS_SRC)) {
    if (f.endsWith('.js')) fs.copyFileSync(path.join(HOOKS_SRC, f), path.join(dir, '.claude', 'hooks', f));
  }
  for (const d of ['docs/memory', 'docs/prds', 'docs/plans', 'src']) fs.mkdirSync(path.join(dir, d), { recursive: true });
  fs.writeFileSync(path.join(dir, 'CLAUDE.md'), CLAUDE_MD);
  fs.writeFileSync(path.join(dir, 'docs', 'memory', 'audit-log.jsonl'), '');
  fs.writeFileSync(path.join(dir, 'docs', 'memory', 'session-context.md'),
    '# 세션 컨텍스트\n\n## 다음 할 일\n\n없음\n\n## 중요 기술 결정 사항\n\n| 날짜 | 결정 | 이유 |\n|------|------|------|\n');
  fs.writeFileSync(path.join(dir, 'src', 'a.js'), '// a\n');
  git(dir, ['init', '-q']); git(dir, ['config', 'user.email', 't@t']); git(dir, ['config', 'user.name', 't']);
  git(dir, ['config', 'core.autocrlf', 'false']);
  git(dir, ['add', '-A']); git(dir, ['commit', '-q', '-m', 'init']);
  git(dir, ['checkout', '-q', '-b', 'feat/ac']);
  return dir;
}
function cli(dir, args) {
  return spawnSync(process.execPath, [path.join(dir, '.claude', 'hooks', 'advance-phase.js'), ...args], {
    cwd: dir, encoding: 'utf8', timeout: 30000, env: { ...process.env, CLAUDE_PROJECT_DIR: dir },
  });
}
function hook(dir, name, payload) {
  const r = spawnSync(process.execPath, [path.join(dir, '.claude', 'hooks', name)], {
    cwd: dir, encoding: 'utf8', timeout: 30000, input: JSON.stringify(payload),
    env: { ...process.env, CLAUDE_PROJECT_DIR: dir },
  });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || '') };
}
function stateFile(dir) {
  const base = path.join(dir, '.claude');
  const d = fs.readdirSync(base).find(x => x.startsWith('.branch-'));
  return d ? path.join(base, d, 'pipeline-state.json') : null;
}
function readState(dir) {
  const f = stateFile(dir);
  return f && fs.existsSync(f) ? JSON.parse(fs.readFileSync(f, 'utf8')) : null;
}
function audit(dir) {
  try { return fs.readFileSync(path.join(dir, 'docs', 'memory', 'audit-log.jsonl'), 'utf8'); } catch { return ''; }
}
function rm(dir) { try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ } }

console.log('\n═══ AC: Loop A 기록·리셋 · Loop C 마커 소유권 ═══');

// ── AC-01: 리뷰어 페이로드가 lastReview 를 남긴다 (D2) ────────────────
console.log('\n[AC-01] should_record_review_when_agent_is_reviewer');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const tp = path.join(d, 'transcript.jsonl');
    fs.writeFileSync(tp, '{"role":"assistant","content":"리뷰 결과: 문제 없음"}\n');
    const r = hook(d, 'subagent-stop.js', {
      hook_event_name: 'SubagentStop', agent_type: 'code-reviewer',
      agent_id: 'rev1', session_id: 'sA', agent_transcript_path: tp,
    });
    check('훅이 exit 0 이다 (불변식)', r.code === 0, r.out);
    const st = readState(d);
    const lr = st && st.lastReview;
    check('lastReview 가 기록된다', !!lr, JSON.stringify(st && Object.keys(st)));
    check('agentType 이 담긴다', !!lr && lr.agentType === 'code-reviewer', JSON.stringify(lr));
    check('treeSig 가 담긴다', !!lr && typeof lr.treeSig === 'string' && lr.treeSig.length > 0, JSON.stringify(lr));
    check('감사에 loop-a-review 가 남는다', /"event":"loop-a-review"/.test(audit(d)), audit(d).slice(-300));

    // counts 는 폐기됐다 — 전사본 전체의 단어 빈도는 수렴 근거가 될 수 없다
    check('counts 를 더 이상 기록하지 않는다', !!lr && lr.counts === undefined, JSON.stringify(lr));
  } catch (e) { check('AC-01', false, e.message); } finally { rm(d); }
}

// ── AC-02: 비리뷰어는 감사만 남기고 lastReview 를 건드리지 않는다 (D3) ──
// N-04 의 D8 이 테스트 없이 ✅ 로 마감됐던 항목이다. 여기서 실제로 고정한다.
console.log('\n[AC-02] should_not_touch_review_when_agent_is_not_reviewer');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const tp = path.join(d, 'transcript.jsonl');
    fs.writeFileSync(tp, '{"role":"assistant","content":"조사 결과"}\n');
    hook(d, 'subagent-stop.js', {
      hook_event_name: 'SubagentStop', agent_type: 'code-reviewer',
      agent_id: 'rev1', session_id: 'sA', agent_transcript_path: tp,
    });
    const before = JSON.stringify((readState(d) || {}).lastReview);

    const r = hook(d, 'subagent-stop.js', {
      hook_event_name: 'SubagentStop', agent_type: 'general-purpose',
      agent_id: 'gp1', session_id: 'sA', agent_transcript_path: tp,
    });
    check('비리뷰어도 exit 0 이다', r.code === 0, r.out);
    check('lastReview 가 바뀌지 않는다', JSON.stringify((readState(d) || {}).lastReview) === before,
      before + ' → ' + JSON.stringify((readState(d) || {}).lastReview));
    check('감사에 subagent-stop 이 남는다', /"event":"subagent-stop"/.test(audit(d)), audit(d).slice(-300));
    check('미매칭 사유가 감사에 남는다 — "안 돌렸다"와 "못 잡았다"를 구분한다',
      /"reviewerMatch":false/.test(audit(d)), audit(d).slice(-400));
  } catch (e) { check('AC-02', false, e.message); } finally { rm(d); }
}

// ── AC-03: agent_type 이 null 인 실제 경로 (D3) ──────────────────────
// 프로덕션 감사에 실존하는 값이다. 이 경로로 리뷰어를 띄우면 Loop A 는 영원히 침묵한다.
console.log('\n[AC-03] should_flag_null_agent_type');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const r = hook(d, 'subagent-stop.js', {
      hook_event_name: 'SubagentStop', agent_type: null, agent_id: 'x1', session_id: 'sA',
    });
    check('exit 0 이다', r.code === 0, r.out);
    const a = audit(d);
    check('agent_type null 이 구분되어 기록된다', /"agentTypeMissing":true/.test(a), a.slice(-400));
    check('lastReview 는 생기지 않는다', !(readState(d) || {}).lastReview);
  } catch (e) { check('AC-03', false, e.message); } finally { rm(d); }
}

// ── AC-04: 새 사이클이 lastReview 를 지운다 (D1) ─────────────────────
console.log('\n[AC-04] should_reset_review_on_new_cycle');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const tp = path.join(d, 'transcript.jsonl');
    fs.writeFileSync(tp, '{"role":"assistant","content":"리뷰"}\n');
    hook(d, 'subagent-stop.js', {
      hook_event_name: 'SubagentStop', agent_type: 'code-reviewer',
      agent_id: 'rev1', session_id: 'sA', agent_transcript_path: tp,
    });
    check('사전 조건: lastReview 가 있다', !!(readState(d) || {}).lastReview);

    // new-cycle 은 complete 에서만 가능하다
    const f = stateFile(d);
    const s = JSON.parse(fs.readFileSync(f, 'utf8'));
    s.phase = 'complete';
    fs.writeFileSync(f, JSON.stringify(s, null, 2) + '\n');

    const r = cli(d, ['new-cycle']);
    check('new-cycle 이 성공한다', r.status === 0, (r.stdout || '') + (r.stderr || ''));
    check('새 사이클에서 lastReview 가 지워진다', !(readState(d) || {}).lastReview,
      JSON.stringify((readState(d) || {}).lastReview));
  } catch (e) { check('AC-04', false, e.message); } finally { rm(d); }
}

// ── AC-05: 마커는 소유자별 파일이고 남의 것을 지우지 않는다 (D5) ──────
console.log('\n[AC-05] should_not_destroy_other_sessions_marker');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const dir = path.join(d, '.claude', '.pending-signal-violations');
    fs.mkdirSync(dir, { recursive: true });
    // 남의 세션이 남긴 위반 마커
    const theirs = path.join(dir, 'other-branch-sB.json');
    fs.writeFileSync(theirs, JSON.stringify({
      evaluated_at: new Date().toISOString(), session_id: 'sB', notified_sessions: [],
      violations: [{ rowId: 'D-x', spec: 'grep:src/a.js /zzz/ >=1', detail: '0건' }],
      insufficient: [], parseErrors: 0, ledgerSilent: false,
    }) + '\n');

    // 내 세션은 위반이 없다 → 예전에는 무조건 unlink 로 남의 것까지 지웠다
    const r = hook(d, 'stop-observation.js', { hook_event_name: 'Stop', session_id: 'sA' });
    check('Stop 훅이 exit 0 이다 (불변식 — Stop 을 막지 않는다)', r.code === 0, r.out);
    check('남의 마커가 살아 있다', fs.existsSync(theirs),
      'dir=' + (fs.existsSync(dir) ? fs.readdirSync(dir).join(',') : '(없음)'));
    check('전역 단일 마커 파일을 더 이상 쓰지 않는다',
      !fs.existsSync(path.join(d, '.claude', '.pending-signal-violations.json')));
  } catch (e) { check('AC-05', false, e.message); } finally { rm(d); }
}

// ── AC-06: 러너 스냅샷이 마커 디렉터리를 되감지 않는다 (D6) ───────────
console.log('\n[AC-06] should_snapshot_marker_directory_not_file');
{
  try {
    const runner = fs.readFileSync(path.join(REPO, 'tests', 'run.js'), 'utf8');
    check('러너가 마커를 파일 하나로 스냅샷하지 않는다',
      !/'\.claude\/\.pending-signal-violations\.json'/.test(runner),
      (runner.match(/pending-signal[^\n]*/g) || []).join(' | '));
    check('러너가 마커 디렉터리를 인식한다',
      /pending-signal-violations['"/]/.test(runner),
      (runner.match(/pending-signal[^\n]*/g) || []).join(' | '));
  } catch (e) { check('AC-06', false, e.message); }
}

// ── AC-07: insufficient 가 진단 가능하다 (D7) ────────────────────────
// 건수 하나로는 무엇이 왜 막는지 알 수 없다. 게이트를 세우기 전의 선결 조건이다.
console.log('\n[AC-07] should_carry_diagnosable_insufficient');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    // 대상 파일이 없는 스펙 → insufficient
    fs.writeFileSync(path.join(d, 'docs', 'memory', 'decisions.jsonl'), JSON.stringify({
      id: 'D-2026-01-01-aaaaaa', date: '2026-01-01', decision: '무언가', reason: '이유',
      rejected: ['다른 것'], invalidation: { text: 't', specs: ['grep:src/gone.js /x/ >=1'] },
      files: [], topics: ['gate'], source: 'user',
    }) + '\n');
    // 코드 변경이 있어야 마커 조건이 선다
    const f = stateFile(d);
    const s = JSON.parse(fs.readFileSync(f, 'utf8'));
    // 사이클 시작 후 사람 결정이 0행 → 원장 침묵 → 마커가 선다
    s.cycle = { startedAt: new Date().toISOString(), touched: ['src/a.js'], decisionsAtStart: 1, needsDecision: [] };
    fs.writeFileSync(f, JSON.stringify(s, null, 2) + '\n');

    hook(d, 'stop-observation.js', { hook_event_name: 'Stop', session_id: 'sA' });
    const dir = path.join(d, '.claude', '.pending-signal-violations');
    const files = fs.existsSync(dir) ? fs.readdirSync(dir).filter(x => x.endsWith('.json')) : [];
    check('마커가 생긴다 (원장 침묵)', files.length > 0, JSON.stringify(files));
    if (files.length) {
      const m = JSON.parse(fs.readFileSync(path.join(dir, files[0]), 'utf8'));
      check('insufficient 가 배열이다 (건수가 아니라)', Array.isArray(m.insufficient), JSON.stringify(m.insufficient));
      if (Array.isArray(m.insufficient) && m.insufficient.length) {
        const i0 = m.insufficient[0];
        check('rowId·spec·detail 이 실린다', !!(i0.rowId && i0.spec && i0.detail), JSON.stringify(i0));
      } else {
        check('rowId·spec·detail 이 실린다', false, 'insufficient 가 비었다: ' + JSON.stringify(m.insufficient));
      }
    }
  } catch (e) { check('AC-07', false, e.message); } finally { rm(d); }
}

// ── AC-08: decide 가 supersedes 대상을 검증한다 (D10) ─────────────────
// 왜: 실측 체인 18d331 ← 9aa4b8 ← 5fc264 에서, 중간 행의 유일한 스펙은 LOOPS.md 가
//   "Loop C 첫 발화" 사례로 인용하는 불변식이었다. 그것을 supersede 한 행의 스펙은
//   완전히 다른 주제다 — 살아 있는 불변식이 무관한 행으로 조용히 꺼졌다.
//   위반이 complete 를 막게 되면 "고치는 것보다 뒤집는 게 싸다"가 성립하므로,
//   게이트와 이 검증은 반드시 같이 가야 한다.
console.log('\n[AC-08] should_validate_supersedes_target');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const led = path.join(d, 'docs', 'memory', 'decisions.jsonl');
    fs.writeFileSync(led, JSON.stringify({
      id: 'D-2026-01-01-aaaaaa', date: '2026-01-01', decision: '원래 결정', reason: '이유',
      rejected: ['다른 것'], invalidation: { text: 't', specs: ['grep:src/a.js /a/ >=1'] },
      files: ['src/a.js'], topics: ['gate'], source: 'user',
    }) + '\n');

    const ghost = cli(d, ['decide', '--json', JSON.stringify({
      decision: '없는 행을 뒤집는다', reason: '이유', rejected: ['그대로 두기'],
      invalidation: { text: '증상' }, supersedes: 'D-2026-01-01-zzzzzz',
    })]);
    check('존재하지 않는 supersedes 대상은 거부된다', ghost.status !== 0,
      (ghost.stdout || '') + (ghost.stderr || ''));

    const real = cli(d, ['decide', '--json', JSON.stringify({
      decision: '원래 결정을 뒤집는다', reason: '이유', rejected: ['그대로 두기'],
      invalidation: { text: '증상' }, supersedes: 'D-2026-01-01-aaaaaa',
    })]);
    const out = (real.stdout || '') + (real.stderr || '');
    check('존재하는 대상은 통과한다', real.status === 0, out);
    check('스펙이 승계되지 않으면 경고한다', /스펙|승계|불변식/.test(out), out);
  } catch (e) { check('AC-08', false, e.message); } finally { rm(d); }
}

// ── AC-09: decide settle 이 새 행을 append 한다 (D11) ─────────────────
console.log('\n[AC-09] should_settle_by_appending_new_row');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const led = path.join(d, 'docs', 'memory', 'decisions.jsonl');
    fs.writeFileSync(led, JSON.stringify({
      id: 'D-2026-01-01-aaaaaa', date: '2026-01-01', decision: '정착시킬 결정', reason: '이유',
      rejected: ['다른 것'], invalidation: { text: 't', specs: ['file:exists src/a.js'] },
      files: ['src/a.js'], topics: ['gate'], source: 'user',
    }) + '\n');

    const raw = cli(d, ['decide', '--json', JSON.stringify({
      decision: '몰래 정착시킨다', reason: '이유', rejected: ['정직하게 settle 쓰기'],
      invalidation: { text: '증상' }, settled: true,
    })]);
    check('원시 settled:true 는 거부된다 — 검증 없는 비공식 쓰기 경로였다', raw.status !== 0,
      (raw.stdout || '') + (raw.stderr || ''));

    const noReason = cli(d, ['decide', 'settle', 'D-2026-01-01-aaaaaa']);
    check('settle 은 --reason 이 필수다', noReason.status !== 0,
      (noReason.stdout || '') + (noReason.stderr || ''));

    const ok = cli(d, ['decide', 'settle', 'D-2026-01-01-aaaaaa', '--reason', '더 이상 논쟁 대상이 아니다']);
    check('settle 이 성공한다', ok.status === 0, (ok.stdout || '') + (ok.stderr || ''));
    const lines = fs.readFileSync(led, 'utf8').split('\n').filter(l => l.trim().startsWith('{'));
    check('원장이 2행이 된다 (기존 행을 고치지 않는다)', lines.length === 2, String(lines.length));
    check('원래 행이 그대로 남는다', /"decision":"정착시킬 결정"/.test(lines[0]), lines[0].slice(0, 120));
    check('새 행이 settles 로 대상을 가리킨다', /"settles":"D-2026-01-01-aaaaaa"/.test(lines[1]), lines[1].slice(0, 200));
  } catch (e) { check('AC-09', false, e.message); } finally { rm(d); }
}

// ── AC-10: decide 가 기록 시점에 스펙을 평가한다 (D12) ────────────────
// N-04 가 부호를 뒤집어 적은 스펙을 기록했고, 다음 세션의 Loop C 가 그것을 위반으로
// 보고했으며, append-only 때문에 supersedes 행을 하나 더 써야 했다. 그 왕복을 없앤다.
console.log('\n[AC-10] should_evaluate_specs_at_write_time');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const bad = cli(d, ['decide', '--json', JSON.stringify({
      decision: '무언가를 택함', reason: '이유', rejected: ['다른 것'],
      invalidation: { specs: ['grep:src/a.js /존재하지않는패턴/ >=1'] }, files: ['src/a.js'],
    })]);
    const out = (bad.stdout || '') + (bad.stderr || '');
    check('위반 스펙은 기록 시점에 거부된다', bad.status !== 0, out);
    check('무엇이 어긋났는지 보여준다', /존재하지않는패턴|위반|violated/.test(out), out);

    const allowed = cli(d, ['decide', '--allow-violated', '--json', JSON.stringify({
      decision: '아직 고치지 않은 결함을 겨냥한다', reason: '이유', rejected: ['스펙 없이 기록'],
      invalidation: { specs: ['grep:src/a.js /존재하지않는패턴/ >=1'] }, files: ['src/a.js'],
    })]);
    check('--allow-violated 로 의도된 위반은 통과한다', allowed.status === 0,
      (allowed.stdout || '') + (allowed.stderr || ''));
    const led = fs.readFileSync(path.join(d, 'docs', 'memory', 'decisions.jsonl'), 'utf8');
    check('그 사실이 행에 남는다', /"allowedViolated":true/.test(led), led.slice(-300));

    const ok = cli(d, ['decide', '--json', JSON.stringify({
      decision: '성립하는 스펙', reason: '이유', rejected: ['다른 것'],
      invalidation: { specs: ['file:exists src/a.js'] }, files: ['src/a.js'],
    })]);
    check('성립하는 스펙은 그냥 통과한다', ok.status === 0, (ok.stdout || '') + (ok.stderr || ''));
  } catch (e) { check('AC-10', false, e.message); } finally { rm(d); }
}

// ── AC-11: 감사 아카이브가 스펙 평가에 포함된다 (D9) ──────────────────
// GC 는 1000행마다 앞 절반을 아카이브로 옮긴다. 살아 있는 파일만 읽으면
// audit:·perf: 스펙이 표본의 절반을 조용히 잃는다.
console.log('\n[AC-11] should_include_archived_audit_rows');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const arc = path.join(d, 'docs', 'memory', 'audit-log-archive');
    fs.mkdirSync(arc, { recursive: true });
    fs.writeFileSync(path.join(arc, '2026-01-01.jsonl'),
      JSON.stringify({ timestamp: '2026-01-01T00:00:00.000Z', event: 'ac11-marker' }) + '\n');

    fs.writeFileSync(path.join(d, 'docs', 'memory', 'decisions.jsonl'), JSON.stringify({
      id: 'D-2026-01-01-aaaaaa', date: '2026-01-01', decision: '감사 스펙', reason: '이유',
      rejected: ['다른 것'], invalidation: { specs: ['audit:ac11-marker >= 1/36500d'] },
      files: [], topics: ['gate'], source: 'user',
    }) + '\n');

    const f = stateFile(d);
    const s = JSON.parse(fs.readFileSync(f, 'utf8'));
    s.cycle = { startedAt: new Date().toISOString(), touched: ['src/a.js'], decisionsAtStart: 1, needsDecision: [] };
    fs.writeFileSync(f, JSON.stringify(s, null, 2) + '\n');

    hook(d, 'stop-observation.js', { hook_event_name: 'Stop', session_id: 'sA' });
    const dir = path.join(d, '.claude', '.pending-signal-violations');
    const files = fs.existsSync(dir) ? fs.readdirSync(dir).filter(x => x.endsWith('.json')) : [];
    const m = files.length ? JSON.parse(fs.readFileSync(path.join(dir, files[0]), 'utf8')) : null;
    // 아카이브를 읽으면 ok → 위반도 insufficient 도 아니다
    const viol = m ? (m.violations || []) : [];
    const insuf = m ? (m.insufficient || []) : [];
    check('아카이브 행 덕분에 audit: 스펙이 위반이 아니다',
      !viol.some(v => /ac11-marker/.test(v.spec || '')), JSON.stringify(viol));
    check('불충분도 아니다', !insuf.some(v => /ac11-marker/.test(v.spec || '')), JSON.stringify(insuf));
  } catch (e) { check('AC-11', false, e.message); } finally { rm(d); }
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
