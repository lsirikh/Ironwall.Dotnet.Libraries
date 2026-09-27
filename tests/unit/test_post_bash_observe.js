// tests/unit/test_post_bash_observe.js
// 명령 도구 쓰기 관측 — charter harness-v3 / N-03 (FR-02·FR-03·FR-05·FR-15·FR-17 · D1·D3)
//
// 왜 이 파일이 존재하는가:
//   하네스가 Bash 로 쓴 파일을 보지 못해 네 사이클 연속으로 complete 가 관측 공백에 걸렸다.
//   관측 단위를 도구에서 **도구 호출 전후의 작업 트리 차이**로 바꾼 것이 이 노드다.
//
//   두 훅을 실제 페이로드로 구동해 전 과정을 확인한다: pre 가 스냅샷을 남기고,
//   그 사이에 파일이 바뀌고, post 가 차이를 touched 로 흡수한다.
//
//   가장 조심할 것은 **자기발화**다. 모델은 advance-phase 를 Bash 로 부른다. 상태 변경을
//   명령 문자열로 판정하면 모든 정상 전이가 위조로 찍혀 complete 가 영구 거부된다.
//   그래서 CLI 가 기준선을 함께 갱신하고, 관측자는 기준선과 디스크만 비교한다.

'use strict';

const { spawnSync } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

const REPO = path.resolve(__dirname, '..', '..');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 220) : ''}`); }
}

function git(d, a) { return spawnSync('git', a, { cwd: d, encoding: 'utf8' }).stdout || ''; }

function sandbox() {
  const d = fs.mkdtempSync(path.join(os.tmpdir(), 'obs-'));
  fs.mkdirSync(path.join(d, '.claude', 'hooks'), { recursive: true });
  for (const f of fs.readdirSync(path.join(REPO, '.claude', 'hooks'))) {
    const src = path.join(REPO, '.claude', 'hooks', f);
    if (fs.statSync(src).isFile()) fs.copyFileSync(src, path.join(d, '.claude', 'hooks', f));
  }
  for (const sub of ['prds', 'plans', 'memory', 'reports', 'tests', 'charters', 'analyses']) {
    fs.mkdirSync(path.join(d, 'docs', sub), { recursive: true });
  }
  fs.mkdirSync(path.join(d, 'src'), { recursive: true });
  fs.writeFileSync(path.join(d, 'src', 'a.js'), 'a\n');
  fs.writeFileSync(path.join(d, 'src', 'del.js'), 'gone soon\n');
  fs.writeFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'), '');
  fs.writeFileSync(path.join(d, 'docs', 'memory', 'decisions.jsonl'), '');
  fs.writeFileSync(path.join(d, '.gitignore'), '.claude/.branch-*/\n');
  fs.writeFileSync(path.join(d, 'CLAUDE.md'), '# T\n\n```yaml\nproject_name: "t"\ntest_command: ""\n```\n');
  git(d, ['init', '-q']); git(d, ['config', 'user.email', 't@t']); git(d, ['config', 'user.name', 't']);
  git(d, ['add', '-A']); git(d, ['commit', '-q', '-m', 'seed']);
  run(d, 'advance-phase.js', ['status']);   // 브랜치 상태 디렉터리 생성
  return d;
}
function cleanup(d) { try { fs.rmSync(d, { recursive: true, force: true }); } catch {} }

function run(d, script, args, input) {
  const r = spawnSync(process.execPath, [path.join(d, '.claude', 'hooks', script), ...(args || [])], {
    cwd: d, encoding: 'utf8', timeout: 30000, input: input || '',
    env: { ...process.env, CLAUDE_PROJECT_DIR: d },
  });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || '') };
}
function branchDir(d) {
  const cd = path.join(d, '.claude');
  const b = fs.readdirSync(cd).find(x => x.startsWith('.branch-'));
  return b ? path.join(cd, b) : null;
}
function readState(d) { try { return JSON.parse(fs.readFileSync(path.join(branchDir(d), 'pipeline-state.json'), 'utf8')); } catch { return {}; } }
function auditRows(d) {
  try {
    return fs.readFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'), 'utf8')
      .split('\n').filter(Boolean).map(l => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean);
  } catch { return []; }
}
// 도구 호출 한 번을 흉내낸다 — pre 가 스냅샷, 사이에 mutate, post 가 관측.
function toolCall(d, tid, mutate, toolName) {
  const tool = toolName || 'Bash';
  run(d, 'pre-tool-gate.js', [], JSON.stringify({
    hook_event_name: 'PreToolUse', session_id: 's1', tool_use_id: tid,
    tool_name: tool, tool_input: { command: 'echo probe' },
  }));
  mutate();
  return run(d, 'post-bash-observe.js', [], JSON.stringify({
    hook_event_name: 'PostToolUse', session_id: 's1', tool_use_id: tid,
    tool_name: tool, tool_input: { command: 'echo probe' }, tool_response: {},
  }));
}

console.log('\n═══ OB: 명령 도구 쓰기 관측 ═══');

// ── OB-01: 신규·수정·삭제 세 가지가 모두 관측된다 (D1) ────────────
console.log('\n[OB-01] should_observe_create_modify_delete_from_command_tool');
{
  const d = sandbox();
  const r = toolCall(d, 'tu1', () => {
    fs.writeFileSync(path.join(d, 'src', 'new.js'), 'new\n');
    fs.writeFileSync(path.join(d, 'src', 'a.js'), 'a modified\n');
    fs.unlinkSync(path.join(d, 'src', 'del.js'));
  });
  check('훅이 exit 0', r.code === 0, r.out);
  const t = require(path.join(d, '.claude', 'hooks', '_git.js')).touchedPaths((readState(d).cycle || {}).touched);
  check('신규 파일이 touched 에', t.includes('src/new.js'), JSON.stringify(t));
  check('수정 파일이 touched 에', t.includes('src/a.js'), JSON.stringify(t));
  check('삭제 파일이 touched 에', t.includes('src/del.js'), JSON.stringify(t));
  const tw = auditRows(d).filter(x => x.event === 'tool-write');
  check('감사는 도구 호출당 한 행', tw.length === 1, 'rows=' + tw.length);
  check('한 행에 파일 목록과 총수가 있다', !!(tw[0] && Array.isArray(tw[0].files) && tw[0].count === 3), JSON.stringify(tw[0]));
  check('스냅샷 파일이 정리된다', !fs.readdirSync(branchDir(d)).some(f => f.startsWith('pre-')), fs.readdirSync(branchDir(d)).join(','));
  cleanup(d);
}

// ── OB-02: 문서는 touchedDocs 로 분리된다 (FR-11) ──────────────────
console.log('\n[OB-02] should_separate_docs_into_touched_docs');
{
  const d = sandbox();
  toolCall(d, 'tu2', () => {
    fs.writeFileSync(path.join(d, 'docs', 'reports', 'r.md'), '# r\n');
    fs.writeFileSync(path.join(d, 'src', 'b.js'), 'b\n');
  });
  const c = readState(d).cycle || {};
  check('코드는 touched 에', require(path.join(d, '.claude', 'hooks', '_git.js')).touchedPaths(c.touched).includes('src/b.js'), JSON.stringify(c.touched));
  check('문서는 touchedDocs 에', (c.touchedDocs || []).includes('docs/reports/r.md'), JSON.stringify(c.touchedDocs));
  check('문서가 touched 를 오염시키지 않는다', !require(path.join(d, '.claude', 'hooks', '_git.js')).touchedPaths(c.touched).includes('docs/reports/r.md'));
  cleanup(d);
}

// ── OB-03: 하네스 런타임 산물은 관측하지 않는다 (FR-12 · D7) ──────
//   이 목록이 한 항목이라도 빠지면 Track A 가 죽고 봉투가 자기잠금된다.
console.log('\n[OB-03] should_not_observe_harness_runtime_artifacts');
{
  const d = sandbox();
  toolCall(d, 'tu3', () => {
    fs.writeFileSync(path.join(branchDir(d), 'scratch.json'), '{}');
    fs.mkdirSync(path.join(d, '.claude', 'install-logs'), { recursive: true });
    fs.writeFileSync(path.join(d, '.claude', 'install-logs', 'x.log'), 'log');
    fs.writeFileSync(path.join(d, '.claude', 'settings.local.json'), '{}');
  });
  const c = readState(d).cycle || {};
  const t = require(path.join(d, '.claude', 'hooks', '_git.js')).touchedPaths(c.touched);
  check('브랜치 상태 디렉터리는 관측 제외', !t.some(p => p.includes('.branch-')), JSON.stringify(t));
  check('설치 로그는 관측 제외', !t.some(p => p.includes('install-logs')), JSON.stringify(t));
  check('로컬 설정은 관측 제외', !t.some(p => p.includes('settings.local')), JSON.stringify(t));
  cleanup(d);
}

// ── OB-04: 자기발화 — CLI 를 거친 상태 변경은 침묵한다 (D3) ────────
//   이것이 이 노드에서 가장 위험한 오탐이다. 서면 하네스가 자기를 영구히 잠근다.
console.log('\n[OB-04] should_stay_silent_when_state_changed_through_cli');
{
  const d = sandbox();
  run(d, 'pre-tool-gate.js', [], JSON.stringify({
    hook_event_name: 'PreToolUse', session_id: 's1', tool_use_id: 'tu4',
    tool_name: 'Bash', tool_input: { command: 'node .claude/hooks/advance-phase.js set-track B' },
  }));
  run(d, 'advance-phase.js', ['set-track', 'B']);   // CLI 가 상태를 바꾸고 기준선도 갱신한다
  run(d, 'post-bash-observe.js', [], JSON.stringify({
    hook_event_name: 'PostToolUse', session_id: 's1', tool_use_id: 'tu4',
    tool_name: 'Bash', tool_input: { command: 'node .claude/hooks/advance-phase.js set-track B' }, tool_response: {},
  }));
  const rows = auditRows(d).filter(x => x.event === 'state-changed-outside-cli');
  check('CLI 를 거친 상태 변경은 위조로 찍히지 않는다', rows.length === 0, JSON.stringify(rows));
  check('cycle.stateOutsideCli 가 서지 않는다', !(readState(d).cycle || {}).stateOutsideCli);
  cleanup(d);
}

// ── OB-05: CLI 밖 상태 변경은 잡는다 (D3 반대 방향) ───────────────
console.log('\n[OB-05] should_flag_state_changed_outside_cli');
{
  const d = sandbox();
  run(d, 'pre-tool-gate.js', [], JSON.stringify({
    hook_event_name: 'PreToolUse', session_id: 's1', tool_use_id: 'tu5',
    tool_name: 'Bash', tool_input: { command: 'echo x' },
  }));
  // CLI 를 거치지 않고 상태 파일을 직접 고친다 — 기준선은 그대로다
  const sf = path.join(branchDir(d), 'pipeline-state.json');
  const s = JSON.parse(fs.readFileSync(sf, 'utf8'));
  s.track = 'A'; s.forged = true;
  fs.writeFileSync(sf, JSON.stringify(s, null, 2));
  run(d, 'post-bash-observe.js', [], JSON.stringify({
    hook_event_name: 'PostToolUse', session_id: 's1', tool_use_id: 'tu5',
    tool_name: 'Bash', tool_input: { command: 'echo x' }, tool_response: {},
  }));
  const rows = auditRows(d).filter(x => x.event === 'state-changed-outside-cli');
  check('CLI 밖 상태 변경이 감사에 남는다', rows.length >= 1, JSON.stringify(auditRows(d).map(r => r.event)));
  check('cycle.stateOutsideCli 가 선다', (readState(d).cycle || {}).stateOutsideCli === true, JSON.stringify(readState(d).cycle));
  cleanup(d);
}

// ── OB-06: 고아 스냅샷을 흡수한다 (FR-15) ─────────────────────────
//   PostToolUse 는 **성공한 도구 뒤에만** 발화한다. 실패한 명령이 절반 쓰고 죽으면
//   그 스냅샷은 고아가 되고, 흡수하지 않으면 그 쓰기가 영영 안 보인다.
console.log('\n[OB-06] should_absorb_orphan_snapshots');
{
  const d = sandbox();
  const bd = branchDir(d);
  // 실패한 도구 흉내 — pre 만 돌고 post 가 오지 않았다
  run(d, 'pre-tool-gate.js', [], JSON.stringify({
    hook_event_name: 'PreToolUse', session_id: 's1', tool_use_id: 'orphan1',
    tool_name: 'Bash', tool_input: { command: 'node half-write.js' },
  }));
  fs.writeFileSync(path.join(d, 'src', 'half.js'), 'half written\n');
  // 고아를 2분 넘은 것으로 만든다
  const of = path.join(bd, 'pre-s1.orphan1.json');
  const old = Date.now() - 5 * 60 * 1000;
  const p0 = JSON.parse(fs.readFileSync(of, 'utf8')); p0.at = old;
  fs.writeFileSync(of, JSON.stringify(p0));
  fs.utimesSync(of, new Date(old), new Date(old));

  toolCall(d, 'tu6', () => { fs.writeFileSync(path.join(d, 'src', 'later.js'), 'later\n'); });
  const t = require(path.join(d, '.claude', 'hooks', '_git.js')).touchedPaths((readState(d).cycle || {}).touched);
  check('고아가 남긴 쓰기도 관측된다', t.includes('src/half.js'), JSON.stringify(t));
  check('자기 호출의 쓰기도 관측된다', t.includes('src/later.js'), JSON.stringify(t));
  check('고아 흡수가 감사에 남는다', auditRows(d).some(r => r.event === 'observe-orphan-absorbed'),
    JSON.stringify(auditRows(d).map(r => r.event)));
  check('고아 스냅샷이 정리된다', !fs.readdirSync(bd).some(f => f.startsWith('pre-')), fs.readdirSync(bd).join(','));
  cleanup(d);
}

// ── OB-07: 남의 세션 고아는 건드리지 않는다 ───────────────────────
console.log('\n[OB-07] should_not_steal_other_session_snapshots');
{
  const d = sandbox();
  const bd = branchDir(d);
  fs.writeFileSync(path.join(bd, 'pre-other.xyz.json'), JSON.stringify({ at: Date.now() - 600000, tool: 'Bash', snap: { entries: {}, watch: {} } }));
  toolCall(d, 'tu7', () => { fs.writeFileSync(path.join(d, 'src', 'mine.js'), 'mine\n'); });
  check('남의 세션 스냅샷은 남는다', fs.existsSync(path.join(bd, 'pre-other.xyz.json')), fs.readdirSync(bd).join(','));
  cleanup(d);
}

// ── OB-08: 스냅샷이 없어도 죽지 않는다 ────────────────────────────
console.log('\n[OB-08] should_survive_missing_snapshot');
{
  const d = sandbox();
  const r = run(d, 'post-bash-observe.js', [], JSON.stringify({
    hook_event_name: 'PostToolUse', session_id: 's1', tool_use_id: 'never-seen',
    tool_name: 'Bash', tool_input: {}, tool_response: {},
  }));
  check('스냅샷 없이도 exit 0', r.code === 0, r.out);
  check('차단을 내지 않는다', !r.out.includes('"decision"'), r.out);
  const r2 = run(d, 'post-bash-observe.js', [], JSON.stringify({ hook_event_name: 'SomeOther', session_id: 's1' }));
  check('남의 이벤트에는 무출력 exit 0', r2.code === 0 && r2.out.trim() === '', r2.out);
  cleanup(d);
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
