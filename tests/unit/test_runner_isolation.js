// tests/unit/test_runner_isolation.js
// 러너 격리와 스테일 락 — charter harness-v3 / N-07 (FR-01~FR-12 · D1~D13)
//
// 왜 이 파일이 존재하는가:
//   두 가지가 조용히 기록을 지우고 있었다.
//
//   ① **테스트 러너가 90초 동안 저장소의 모든 쓰기를 되감는다.** restoreSnapshot 은
//      내용 전체 동일성만 보고 다르면 스냅샷 버퍼로 덮어쓴다 — 자기 것과 남의 것을 가르는
//      조건이 코드에 하나도 없다. 실측: 한 세션이 90초에 감사 33~116행을 만들고,
//      decide 로 적은 원장이 최대 20행 날아간다. 그러면 그 사이클은 원장 없이 complete 할
//      수 없고 모델은 이미 내린 판단을 재구성할 방법이 없다.
//
//   ② **audit.lock 이 스테일이 되면 영구히 남는다.** logAudit 은 EEXIST 에 조용히
//      return 하고 PID·나이 검사가 없다. 한 세션이 한 번 죽으면 그 뒤 모든 세션의 감사
//      기록이 오류 하나 없이 멈춘다 — 그리고 N-02 의 승인 근거 대조가 그 장부에 의존하므로
//      정당한 승인이 위조로 판정된다.
//
//   격리는 CLAUDE_PROJECT_DIR 이 아니라 **훅 파일 복사**에서 나온다(훅 20개 전부
//   path.resolve(__dirname,'..','..') 를 쓴다). 그래서 경로 기반 락은 샌드박스에 안 보이고,
//   env 마커는 샌드박스까지 전파된다 — 둘을 섞으면 반드시 샌드박스를 오염시킨다.

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
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'runiso-'));
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
  git(dir, ['checkout', '-q', '-b', 'feat/iso']);
  return dir;
}
function cli(dir, args, env) {
  return spawnSync(process.execPath, [path.join(dir, '.claude', 'hooks', 'advance-phase.js'), ...args], {
    cwd: dir, encoding: 'utf8', timeout: 30000, env: { ...process.env, CLAUDE_PROJECT_DIR: dir, ...(env || {}) },
  });
}
function hook(dir, name, payload, env) {
  const r = spawnSync(process.execPath, [path.join(dir, '.claude', 'hooks', name)], {
    cwd: dir, encoding: 'utf8', timeout: 30000, input: JSON.stringify(payload),
    env: { ...process.env, CLAUDE_PROJECT_DIR: dir, ...(env || {}) },
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

// 확실히 죽은 PID — 이 값이 살아 있을 확률은 무시할 수준이고, 살아 있어도
//   process.kill(pid, 0) 이 참을 돌려주면 RI-01 이 실패해서 알려준다(조용히 통과하지 않는다).
const DEAD_PID = 999999;

console.log('\n═══ RI: 러너 격리와 스테일 락 ═══');

// ── RI-01: 죽은 PID 의 audit.lock 이 치워진다 (D7) ───────────────────
console.log('\n[RI-01] should_reclaim_stale_audit_lock');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const lock = path.join(d, 'docs', 'memory', 'audit.lock');
    fs.writeFileSync(lock, String(DEAD_PID));
    const before = audit(d).length;

    const r = hook(d, 'stop-observation.js', { hook_event_name: 'Stop', session_id: 'sA' });
    check('훅이 정상 종료한다', r.code === 0, r.out);
    check('죽은 PID 의 락이 치워진다', !fs.existsSync(lock),
      fs.existsSync(lock) ? fs.readFileSync(lock, 'utf8') : '');
    check('그 뒤 감사 기록이 재개된다', audit(d).length > before,
      `before=${before} after=${audit(d).length}`);
  } catch (e) { check('RI-01', false, e.message); } finally { rm(d); }
}

// ── RI-02: 살아 있는 PID 의 락은 건드리지 않는다 (D8) ────────────────
// 이 케이스가 벽이 함정이 되지 않게 지킨다 — 남의 살아 있는 작업을 뺏으면 안 된다.
console.log('\n[RI-02] should_keep_live_audit_lock');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const lock = path.join(d, 'docs', 'memory', 'audit.lock');
    fs.writeFileSync(lock, String(process.pid));   // 지금 살아 있는 PID
    hook(d, 'stop-observation.js', { hook_event_name: 'Stop', session_id: 'sA' });
    check('살아 있는 PID 의 락은 남는다', fs.existsSync(lock));
  } catch (e) { check('RI-02', false, e.message); } finally { rm(d); }
}

// ── RI-03: 산출물 문서가 touchedDocs 에 남는다 (D13) ─────────────────
// N-05·N-06 이 둘 다 이 결함으로 산출물 3건을 별도 커밋해야 했다.
console.log('\n[RI-03] should_record_doc_writes_in_touched_docs');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const f = stateFile(d);
    const s = JSON.parse(fs.readFileSync(f, 'utf8'));
    s.cycle = { startedAt: new Date().toISOString(), touched: [], touchedDocs: [], decisionsAtStart: 0, needsDecision: [] };
    fs.writeFileSync(f, JSON.stringify(s, null, 2) + '\n');

    const target = path.join(d, 'docs', 'reports', 'x-report.md');
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, '# 보고\n');
    hook(d, 'post-write-sync.js', {
      hook_event_name: 'PostToolUse', tool_name: 'Write', session_id: 'sA',
      tool_input: { file_path: target, content: '# 보고\n' },
    });
    const st = readState(d);
    const td = (st && st.cycle && st.cycle.touchedDocs) || [];
    check('docs/ 쓰기가 touchedDocs 에 남는다', td.some(x => String(x).includes('x-report.md')),
      JSON.stringify(td));
    const t = (st && st.cycle && st.cycle.touched) || [];
    check('touched(코드) 에는 들어가지 않는다', !t.some(x => String((x && x.path) || x).includes('x-report.md')),
      JSON.stringify(t));
  } catch (e) { check('RI-03', false, e.message); } finally { rm(d); }
}

// ── RI-04: 러너 복원이 남의 append 를 살린다 (D1/D2/D3) ──────────────
// 러너를 직접 구동하지 않고, 러너가 export 해야 할 순수 판정을 검사한다.
console.log('\n[RI-04] should_remove_only_own_appended_lines');
// run.js 는 스크립트다. `require.main === module` 가드가 서기 전에 require 하면
//   스위트가 재귀 실행된다 — 그래서 소스에서 가드를 먼저 확인한 뒤에만 로드한다.
let R = null;
try {
  const src0 = fs.readFileSync(path.join(REPO, 'tests', 'run.js'), 'utf8');
  if (/require\.main\s*===\s*module/.test(src0)) R = require(path.join(REPO, 'tests', 'run.js'));
} catch { /* 아래에서 Red 로 드러난다 */ }
{
  const ok = R && typeof R.restoreAppendOnly === 'function';
  check('run.js 가 restoreAppendOnly 를 export 한다', ok,
    R ? 'exports: ' + Object.keys(R).join(',') : 'run.js 로드 실패');
  if (ok) {
    const RUN = 'run-abc123';
    const before = '{"a":1}\n{"a":2}\n';
    const mine = `{"a":3,"run":"${RUN}"}\n`;
    const theirs = '{"a":4}\n';

    // 내 행과 남의 행이 섞여 있다 — 남의 것은 살아야 한다
    const mixed = before + mine + theirs;
    const r1 = R.restoreAppendOnly(mixed, before, RUN);
    check('남의 append 가 살아남는다', r1.next === before + theirs, JSON.stringify(r1));
    check('내 run id 행은 제거된다', !r1.next.includes(RUN), r1.next);
    check('되감기가 아니라 선별 제거다', r1.action === 'filtered', JSON.stringify(r1));

    // 접두가 아니면 손대지 않는다 (GC·아카이브 롤오버가 앞을 잘랐을 때)
    const truncated = '{"a":2}\n{"a":9}\n';
    const r2 = R.restoreAppendOnly(truncated, before, RUN);
    check('접두가 아니면 되감지 않는다', r2.next === truncated, JSON.stringify(r2));
    check('그 사실을 경고로 남긴다', r2.action === 'skipped-not-prefix', JSON.stringify(r2));

    // 아무것도 늘지 않았으면 그대로
    const r3 = R.restoreAppendOnly(before, before, RUN);
    check('변화가 없으면 그대로 둔다', r3.next === before && r3.action === 'unchanged', JSON.stringify(r3));

    // run id 가 없으면(구형 행) 보수적으로 남긴다 — 지우는 쪽이 되돌릴 수 없다
    const noTag = before + '{"a":5}\n';
    const r4 = R.restoreAppendOnly(noTag, before, RUN);
    check('run id 없는 새 행은 남긴다 — 지우는 쪽이 되돌릴 수 없다', r4.next === noTag, JSON.stringify(r4));
  }
}

// ── RI-05: 디렉터리 복원이 남의 파일을 지우지 않는다 (D5) ─────────────
console.log('\n[RI-05] should_not_delete_foreign_dir_entries');
{
  const ok = R && typeof R.isOwnArtifact === 'function';
  check('run.js 가 isOwnArtifact 를 export 한다', ok,
    R ? 'exports: ' + Object.keys(R).join(',') : 'run.js 로드 실패');
  if (ok) {
    const RUN = 'run-abc123';
    check('내 run id 가 붙은 파일은 내 것이다', R.isOwnArtifact('sessions/ppid-1.md', RUN, RUN) === true);
    check('남의 run id 는 내 것이 아니다', R.isOwnArtifact('sessions/ppid-2.md', 'run-other', RUN) === false);
    check('소유자를 모르면 남의 것으로 본다 — 삭제는 되돌릴 수 없다',
      R.isOwnArtifact('sessions/ppid-3.md', null, RUN) === false);
  }
}

// ── RI-06: 스냅샷 범위와 정리 보장 (D6) ──────────────────────────────
// [v3.6/N-08/IMPL-05] **소스 문자열 매칭을 버렸다** (FR-08).
//   예전 이 블록은 `tests/run.js` 를 읽어 정규식을 걸었다. 그런데 `/state-backups/` 는
//   **주석 두 줄에만** 매칭되어 통과했고, 그 주석 본문은 "백업 보호가 작동한 적이 없었다"
//   라고 적혀 있었다 — 테스트가 "지킨다" 고 초록을 내는 순간 매칭된 텍스트는 정반대를
//   말하고 있었다. 사실을 거꾸로 고정한 단언이다(N-07 이 만들었고 조사가 잡았다).
//   이제 러너를 **실제로 돌려서** 남의 브랜치 상태가 되감기지 않는지 본다.
console.log('\n[RI-06] should_scope_snapshot_to_current_branch');
{
  try {
    // 남의 브랜치 상태 디렉터리를 하나 만들고, 러너를 짧게 돌린 뒤 그것이 그대로인지 본다.
    const foreignDir = path.join(REPO, '.claude', '.branch-zz-foreign-n08probe');
    const foreignState = path.join(foreignDir, 'pipeline-state.json');
    const existed = fs.existsSync(foreignDir);
    if (!existed) fs.mkdirSync(foreignDir, { recursive: true });
    const mark = JSON.stringify({ phase: 'dev', probe: 'n08', at: Date.now() }, null, 2) + '\n';
    fs.writeFileSync(foreignState, mark);
    try {
      // 러너를 한 파일만 돌린다(빠르다). 그 사이 남의 상태를 바꿔도 되감기면 안 된다.
      const changed = JSON.stringify({ phase: 'complete', probe: 'n08-changed' }, null, 2) + '\n';
      const r = spawnSync(process.execPath, [path.join(REPO, 'tests', 'run.js'), 'test_git_status_parse'], {
        cwd: REPO, encoding: 'utf8', timeout: 120000,
      });
      fs.writeFileSync(foreignState, changed);
      // 러너가 이미 끝났으므로, 러너가 남의 브랜치를 스냅샷했다면 그 시점 값으로 되돌렸을 것이다.
      check('러너가 실행된다', (r.status === 0 || r.status === 1), (r.stdout || '').slice(-200));
      check('남의 브랜치 상태가 되감기지 않는다',
        JSON.parse(fs.readFileSync(foreignState, 'utf8')).probe === 'n08-changed',
        fs.readFileSync(foreignState, 'utf8').slice(0, 200));
      check('러너가 단언 수를 보고한다', /단언 \d+/.test(r.stdout || ''), (r.stdout || '').slice(-300));
    } finally {
      if (!existed) try { fs.rmSync(foreignDir, { recursive: true, force: true }); } catch { /* 무시 */ }
    }
  } catch (e) { check('RI-06', false, e.message); }
}

// ── RI-07: verify.lock 회수 (D9) ─────────────────────────────────────
// 락이 남으면 다음 verify 가 영원히 막힌다 — `audit.lock` 이 정확히 그 실패다.
// PID 생존 검사와 나이 GC 가 **둘 다** 있어야 한다.
console.log('\n[RI-07] should_reclaim_stale_verify_lock');
{
  const C = require(path.join(HOOKS_SRC, '_common.js'));
  check('_common 이 verifyLockState 를 export 한다', typeof C.verifyLockState === 'function',
    Object.keys(C).join(','));
  if (typeof C.verifyLockState === 'function') {
    const now = Date.now();
    check('죽은 PID 의 락은 stale 이다',
      C.verifyLockState({ pid: DEAD_PID, acquired_at: now }, now).stale === true,
      JSON.stringify(C.verifyLockState({ pid: DEAD_PID, acquired_at: now }, now)));
    check('살아 있는 PID 의 락은 유효하다',
      C.verifyLockState({ pid: process.pid, acquired_at: now }, now).stale === false);
    check('내 PID 라도 너무 오래되면 stale 이다',
      C.verifyLockState({ pid: process.pid, acquired_at: now - 3600_000 }, now).stale === true);
    check('깨진 락은 stale 이다', C.verifyLockState(null, now).stale === true);
  }
}

// ── RI-08: 락은 경로 기반이라 복사 샌드박스에 보이지 않는다 (D10) ─────
// env 기반으로 만들면 훅 파일을 복사해 격리하는 테스트 6종이 전부 침묵한다.
console.log('\n[RI-08] should_not_leak_lock_into_copied_sandbox');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    // 실 저장소에 락이 있어도 샌드박스는 자기 루트를 본다
    const realLock = path.join(REPO, 'docs', 'memory', 'verify.lock');
    const had = fs.existsSync(realLock);
    if (!had) fs.writeFileSync(realLock, JSON.stringify({ pid: process.pid, acquired_at: Date.now(), run: 'ri08' }));
    try {
      const r = hook(d, 'stop-observation.js', { hook_event_name: 'Stop', session_id: 'sA' });
      check('샌드박스 훅은 실 저장소 락과 무관하게 동작한다', r.code === 0, r.out);
      check('샌드박스에 락이 생기지 않았다', !fs.existsSync(path.join(d, 'docs', 'memory', 'verify.lock')));
    } finally {
      if (!had) try { fs.unlinkSync(realLock); } catch { /* 무시 */ }
    }
  } catch (e) { check('RI-08', false, e.message); } finally { rm(d); }
}

// ── RI-09: 외부 verify 가 도는 동안 전이를 거부한다 (D11) ─────────────
// 거부는 되돌릴 수 있다(90초 뒤 재시도). 반대로 허용하면 그 전이가 되감긴다.
console.log('\n[RI-09] should_refuse_transition_while_foreign_verify_runs');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const lock = path.join(d, 'docs', 'memory', 'verify.lock');
    fs.writeFileSync(lock, JSON.stringify({ pid: process.pid, acquired_at: Date.now(), run: 'other-run' }));
    const r = cli(d, ['prd']);
    const out = (r.stdout || '') + (r.stderr || '');
    check('외부 verify 중에는 phase 전이를 거부한다', r.status !== 0, out);
    check('무엇을 기다리는지 알려준다', /verify|검증/.test(out), out);

    // 내 실행의 락이면 막지 않는다 — 자기잠금 금지
    fs.writeFileSync(lock, JSON.stringify({ pid: process.pid, acquired_at: Date.now(), run: 'mine' }));
    const r2 = cli(d, ['prd'], { _HARNESS_TEST_RUN: 'mine' });
    check('내 실행의 락은 나를 막지 않는다', r2.status === 0, (r2.stdout || '') + (r2.stderr || ''));

    // 죽은 PID 의 락은 회수되고 통과한다 — 영구 차단 금지
    fs.writeFileSync(lock, JSON.stringify({ pid: DEAD_PID, acquired_at: Date.now(), run: 'dead' }));
    const r3 = cli(d, ['plan']);
    const out3 = (r3.stdout || '') + (r3.stderr || '');
    // 다른 게이트(activePrd 없음 등)에서 막히는 것은 정상이다 — 락 때문에 막히지 않는 것이 요점이다
    check('죽은 PID 의 락은 더 이상 막지 않는다', !/다른 세션의 검증/.test(out3), out3);
    check('회수되어 파일이 사라진다', !fs.existsSync(lock));
  } catch (e) { check('RI-09', false, e.message); } finally { rm(d); }
}


// ── RI-10: 테스트가 저장소를 오염시키면 드러난다 (v4/N-10 · FR-01·FR-02) ──
//
// 왜: 러너의 존재 이유는 "실행 후 트리가 깨끗할 것" 한 문장이다. 그런데 스냅샷 목록이
//   CHANGELOG.md 는 담으면서 CLAUDE.md·install.js·README.md·docs/Manual.md 는 담지 않았다.
//   **실측(2026-09-08)**: N-05 를 만들며 쓴 테스트가 버전 동기화를 실제 훅 경로로 호출해
//   그 네 파일을 전부 9.9.9 로 바꿨고, 러너는 되돌리지 못했으며 **스위트는 초록이었다** —
//   네 곳이 일관되게 바뀌어 버전 일치 검사를 통과했기 때문이다.
//
//   목록에 항목을 더하지 않는다(아홉 번 걸린 함정). 판정 기준을 바꾼다:
//   실행 후 새로 dirty 해진 **추적 파일** 중 러너·훅 소유가 아닌 것을 실패로 드러낸다.
console.log('\n[RI-10] should_surface_repo_contamination_by_tests');
{
  const os = require('os');
  const { spawnSync } = require('child_process');

  // 저장소 흉내 — git 초기화 후 계약 파일을 커밋해 둔다
  function repo(unitFiles) {
    const d = fs.mkdtempSync(path.join(os.tmpdir(), 'ri10-'));
    fs.mkdirSync(path.join(d, 'tests', 'unit'), { recursive: true });
    fs.mkdirSync(path.join(d, 'docs', 'memory'), { recursive: true });
    fs.copyFileSync(path.join(REPO, 'tests', 'run.js'), path.join(d, 'tests', 'run.js'));
    fs.writeFileSync(path.join(d, 'CLAUDE.md'), '# CLAUDE\nversion: "1.0.0"\n');
    fs.writeFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'), '');
    for (const [n, body] of Object.entries(unitFiles)) {
      fs.writeFileSync(path.join(d, 'tests', 'unit', n), body);
    }
    const g = a => spawnSync('git', a, { cwd: d, encoding: 'utf8' });
    g(['init', '-q']); g(['config', 'user.email', 't@t']); g(['config', 'user.name', 't']);
    g(['config', 'core.autocrlf', 'false']); g(['add', '-A']); g(['commit', '-q', '-m', 'init']);
    return d;
  }
  const run = d => {
    const r = spawnSync(process.execPath, [path.join(d, 'tests', 'run.js')], {
      cwd: d, encoding: 'utf8', timeout: 120000,
      env: Object.assign({}, process.env, { _HARNESS_TEST_RUN: 'ri10' }),
    });
    return { code: r.status, out: (r.stdout || '') + (r.stderr || '') };
  };
  const clean = d => { try { fs.rmSync(d, { recursive: true, force: true }); } catch { /* 임시 */ } };

  const OK_TEST = `
const { makeReporter } = require('../run.js');
const R = makeReporter(); R.check('ok', true); R.done();
`;
  // 저장소 계약 파일을 오염시키는 테스트 — 통과하면서 CLAUDE.md 를 바꾼다
  const DIRTY_TEST = `
const fs = require('fs'), path = require('path');
fs.writeFileSync(path.join(__dirname, '..', '..', 'CLAUDE.md'), '# CLAUDE\\nversion: "9.9.9"\\n');
const { makeReporter } = require('../run.js');
const R = makeReporter(); R.check('ok', true); R.done();
`;
  // 러너·훅이 정상적으로 쓰는 경로 — 실패로 보고되면 안 된다
  const OWNED_TEST = `
const fs = require('fs'), path = require('path');
const root = path.join(__dirname, '..', '..');
fs.appendFileSync(path.join(root, 'docs', 'memory', 'audit-log.jsonl'), JSON.stringify({ e: 1 }) + '\\n');
const { makeReporter } = require('../run.js');
const R = makeReporter(); R.check('ok', true); R.done();
`;

  // ① 깨끗한 스위트는 통과한다 (오탐 없음)
  let d = repo({ 'test_ok.js': OK_TEST });
  try {
    const r = run(d);
    check('깨끗한 스위트는 통과한다', r.code === 0, r.out.slice(-300));
  } finally { clean(d); }

  // ② 저장소 계약 파일 오염은 실패로 드러난다
  d = repo({ 'test_dirty.js': DIRTY_TEST });
  try {
    const r = run(d);
    check('저장소 계약 파일을 오염시키면 러너가 실패한다', r.code !== 0, `code=${r.code} ` + r.out.slice(-300));
    check('무엇이 오염됐는지 말한다', /CLAUDE\.md/.test(r.out), r.out.slice(-300));
  } finally { clean(d); }

  // ③ 러너·훅 소유 경로는 오탐이 아니다
  d = repo({ 'test_owned.js': OWNED_TEST });
  try {
    const r = run(d);
    check('러너 소유 경로 쓰기는 실패로 보고되지 않는다', r.code === 0, `code=${r.code} ` + r.out.slice(-300));
  } finally { clean(d); }
}

// ── [v12/N-08] 검증 잠금은 하나 · verify/task 는 남의 러너를 기다린다 · 실패 원문은 파일로 ──
//   러너는 verify.lock 을 wx 없이 덮어썼다 — 두 번째 러너가 첫 러너의 락을 덮고 먼저 끝나 지우면
//   첫 러너가 도는 동안 락이 없는 창이 생겼다(통합 분석 F-09). verify·task 분기는 락 검사보다 앞에서 끝났다.
//   실패 출력은 2000/500자로 잘려, N-04 의 검증 실패가 `실패: (불명)` 으로만 남았다(원인은 러너의 격리 위반 판정).
//   대조(같은 run 재진입·죽은 PID 회수·락 없음·내 실행)는 수정 전에도 초록이어야 한다 — 러너 안의 러너(RI-06)가 그 경로를 쓴다.

console.log('\n[RI-11] should_hold_single_verify_lock_and_allow_reentry');
{
  let RR = null;
  try { RR = require(path.join(REPO, 'tests', 'run.js')); } catch (e) { RR = null; }
  const acquire = RR && RR.acquireVerifyLock;
  check('run.js 가 acquireVerifyLock 을 export 한다', typeof acquire === 'function', RR ? Object.keys(RR).join(',') : 'run.js 로드 실패');
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'ri11-'));
  const lock = path.join(tmp, 'verify.lock');
  try {
    if (typeof acquire === 'function') {
      const foreign = JSON.stringify({ pid: process.pid, acquired_at: new Date().toISOString(), run: 'other-run' }) + '\n';
      fs.writeFileSync(lock, foreign);
      const r1 = acquire(lock, 'my-run', Date.now());
      check('살아 있는 남의 락이면 거부한다', !!r1 && r1.ok === false, JSON.stringify(r1));
      check('남의 락 파일을 덮어쓰지 않는다', fs.readFileSync(lock, 'utf8') === foreign, fs.readFileSync(lock, 'utf8'));
      const r2 = acquire(lock, 'other-run', Date.now());
      check('같은 run 은 재진입한다 (대조)', !!r2 && r2.ok === true, JSON.stringify(r2));
      fs.writeFileSync(lock, JSON.stringify({ pid: DEAD_PID, acquired_at: new Date().toISOString(), run: 'dead-run' }) + '\n');
      const r3 = acquire(lock, 'my-run', Date.now());
      let held3 = null; try { held3 = JSON.parse(fs.readFileSync(lock, 'utf8')); } catch { held3 = null; }
      check('죽은 PID 락은 회수하고 잡는다 (대조)', !!r3 && r3.ok === true && !!held3 && held3.run === 'my-run', JSON.stringify({ r3, held3 }));
      fs.unlinkSync(lock);
      const r4 = acquire(lock, 'my-run', Date.now());
      check('락이 없으면 만든다 (대조)', !!r4 && r4.ok === true && fs.existsSync(lock), JSON.stringify(r4));
    }
    // 소스 검사 — 러너 본체가 이 함수로 잡고, 생성은 배타(wx)다. 틀린 사본으로 판정이 헛돌지 않는지 본다.
    const src = fs.readFileSync(path.join(REPO, 'tests', 'run.js'), 'utf8');
    const usesExclusive = s => /acquireVerifyLock\s*\(\s*VERIFY_LOCK/.test(s) && /flag:\s*'wx'/.test(s);
    check('러너 본체가 acquireVerifyLock(VERIFY_LOCK, …) 을 부르고 wx 로 만든다', usesExclusive(src));
    const bad = src.replace(/flag:\s*'wx'/g, "flag: 'w'");
    check('반증: wx 를 뺀 사본에서는 판정이 빨개진다', bad !== src && !usesExclusive(bad), bad === src ? '치환이 헛돌았다' : '');

    // Loop A(v12/N-08, HIGH): 판정기(_common.js)를 못 불러오는 복사 위치에서 죽은 락이 **영구 거부**로 굳었다.
    //   판정 불가는 거부가 아니라 degraded 로 진행한다 — 소유하지 않으므로 해제도 하지 않는다.
    const bare = fs.mkdtempSync(path.join(os.tmpdir(), 'ri11bare-'));
    try {
      fs.mkdirSync(path.join(bare, 'tests'), { recursive: true });
      fs.copyFileSync(path.join(REPO, 'tests', 'run.js'), path.join(bare, 'tests', 'run.js'));
      const bareMod = require(path.join(bare, 'tests', 'run.js'));
      const bareLock = path.join(bare, 'docs', 'memory', 'verify.lock');
      fs.mkdirSync(path.dirname(bareLock), { recursive: true });
      fs.writeFileSync(bareLock, JSON.stringify({ pid: DEAD_PID, acquired_at: new Date().toISOString(), run: 'dead-run' }) + '\n');
      const rb = typeof bareMod.acquireVerifyLock === 'function' ? bareMod.acquireVerifyLock(bareLock, 'my-run', Date.now()) : null;
      check('판정기가 없는 복사 위치에서도 죽은 락에 영구히 막히지 않는다', !!rb && rb.ok === true, JSON.stringify(rb));
      check('판정기가 없으면 락을 소유하지 않는다(해제하지 않는다)', !!rb && !rb.created, JSON.stringify(rb));
    } finally { rm(bare); }
  } catch (e) { check('RI-11', false, e.message); } finally { rm(tmp); }
}

console.log('\n[RI-12] should_refuse_verify_and_task_while_foreign_runner_holds_lock');
{
  const d = sandbox();
  try {
    cli(d, ['status']);
    const lock = path.join(d, 'docs', 'memory', 'verify.lock');
    fs.writeFileSync(lock, JSON.stringify({ pid: process.pid, acquired_at: Date.now(), run: 'other-run' }));
    // Loop A(v12/N-08, MEDIUM): test_command 가 비면 verify 는 skip 경로라, 아래 loopV·감사 단언이 거부 없이도 초록이었다.
    //   실제로 도는 명령을 둬서 거부가 없으면 loopV·감사가 바뀌게 한다 — 보조 단언이 판별력을 갖는다.
    fs.writeFileSync(path.join(d, 'CLAUDE.md'), CLAUDE_MD.replace('test_command: ""', 'test_command: "node ok.js"'));
    fs.writeFileSync(path.join(d, 'ok.js'), "console.log('ok');\n");
    const noRun = { _HARNESS_TEST_RUN: '' };   // 바깥 러너의 실행 id 가 새지 않게 — 이 호출은 '남' 이다
    const before = readState(d) || {};
    const auditBefore = audit(d).length;
    const v = cli(d, ['verify'], noRun);
    const vout = (v.stdout || '') + (v.stderr || '');
    check('남의 러너가 돌면 verify 를 거부한다', v.status !== 0 && /다른 세션의 검증/.test(vout), `exit=${v.status} ` + vout.slice(-300));
    const after = readState(d) || {};
    check('Loop V 상태가 변하지 않는다', JSON.stringify(after.loopV || null) === JSON.stringify(before.loopV || null),
      JSON.stringify({ before: before.loopV || null, after: after.loopV || null }));
    check('loopv 감사가 쌓이지 않는다', !/"event":"loopv-/.test(audit(d).slice(auditBefore)), audit(d).slice(auditBefore).slice(0, 300));
    const t = cli(d, ['task', 'IMPL-01', 'start'], noRun);
    const tout = (t.stdout || '') + (t.stderr || '');
    check('task 쓰기도 거부한다 — plan 없음이 아니라 잠금 때문에', t.status !== 0 && /다른 세션의 검증/.test(tout), `exit=${t.status} ` + tout.slice(-300));
    // 대조 — 락이 내 실행의 것이면 막지 않는다(자기잠금 금지)
    const m = cli(d, ['verify'], { _HARNESS_TEST_RUN: 'other-run' });
    const mout = (m.stdout || '') + (m.stderr || '');
    check('내 실행의 락이면 verify 가 잠금으로 막히지 않는다 (대조)', !/다른 세션의 검증/.test(mout), mout.slice(-300));
  } catch (e) { check('RI-12', false, e.message); } finally { rm(d); }
}

console.log('\n[RI-13] should_keep_full_failure_output_and_treat_lock_refusal_as_infra');
{
  const d = sandbox();
  try {
    fs.writeFileSync(path.join(d, 'CLAUDE.md'), CLAUDE_MD.replace('test_command: ""', 'test_command: "node fail.js"'));
    const LONG = 'x'.repeat(120);
    const failBody = lastLine => `for (let i = 0; i < 40; i++) console.log('line ' + i + ' ${LONG}');\nconsole.log(${JSON.stringify(lastLine)});\nprocess.exit(1);\n`;
    const noRun = { _HARNESS_TEST_RUN: '' };
    cli(d, ['status']);

    // ① 실패 원문이 잘리지 않고 파일로 남는다 · 실패 파일 이름이 없어도 이유를 보인다
    fs.writeFileSync(path.join(d, 'fail.js'), failBody('  ❌ [격리 위반] 테스트가 저장소 파일 1개를 오염시켰습니다'));
    const r = cli(d, ['verify'], noRun);
    const out = (r.stdout || '') + (r.stderr || '');
    const lf = (readState(d) || {}).lastFailure || {};
    const logAbs = lf.logFile ? path.join(d, lf.logFile) : null;
    const logText = logAbs && fs.existsSync(logAbs) ? fs.readFileSync(logAbs, 'utf8') : '';
    check('verify 는 실패로 끝난다', r.status !== 0, out.slice(-200));
    check('lastFailure.logFile 이 가리키는 파일이 있다', !!logText, JSON.stringify(lf).slice(0, 240));
    check('로그에 잘리지 않은 전체 출력이 있다(첫 줄·마지막 줄)', logText.length > 4000 && /line 0 /.test(logText) && /격리 위반/.test(logText),
      `len=${logText.length}`);
    check('화면에 실패 원문 경로를 알려 준다', /실패 원문:/.test(out) && !!lf.logFile && out.includes(path.basename(String(lf.logFile))), out.slice(-300));
    check('(불명) 대신 실패 이유를 보인다', !/\(불명\)/.test(out) && /격리 위반/.test(out), out.slice(-300));
    check('감사 loopv-fail 에 logFile 이 남는다', /"event":"loopv-fail"[^\n]*"logFile"/.test(audit(d)), audit(d).slice(-300));

    // ② 러너의 잠금 거부는 코드 실패가 아니다 — Loop V 시도 수를 태우지 않는다
    fs.writeFileSync(path.join(d, 'fail.js'), failBody('  ❌ [검증 잠금] 다른 실행(run x, pid 1)이 검증 중입니다 — 끝난 뒤 다시 실행하세요'));
    const attemptsBefore = ((readState(d) || {}).loopV || {}).attempts;
    const r2 = cli(d, ['verify'], noRun);
    const out2 = (r2.stdout || '') + (r2.stderr || '');
    check('러너의 잠금 거부는 INFRA 로 분류한다', /INFRA/.test(out2), out2.slice(-300));
    check('Loop V 시도 수를 태우지 않는다', ((readState(d) || {}).loopV || {}).attempts === attemptsBefore,
      JSON.stringify({ attemptsBefore, after: ((readState(d) || {}).loopV || {}).attempts }));

  } catch (e) { check('RI-13', false, e.message); } finally { rm(d); }

  // ③ Red 실행도 원문을 남긴다 — 실패의 이유를 사람이 확인할 수 있어야 한다(N-04: 문법 오류가 Red 로 기록됨).
  //   새 샌드박스에서 본다 — 앞 단계의 Loop V 상태(STUCK 등)에 기대면 Red 의 실패 이유가 흐려진다.
  const d3 = sandbox();
  try {
    fs.writeFileSync(path.join(d3, 'CLAUDE.md'), CLAUDE_MD.replace('test_command: ""', 'test_command: "node fail.js"'));
    fs.writeFileSync(path.join(d3, 'fail.js'), "console.log('  ❌ src/a.js 기대한 실패');\nprocess.exit(1);\n");
    cli(d3, ['status']);
    const r3 = cli(d3, ['verify', '--expect-fail', '--task', 'T-RED'], { _HARNESS_TEST_RUN: '' });
    const out3 = (r3.stdout || '') + (r3.stderr || '');
    const logDir = path.join(d3, '.claude', 'verify-logs');
    const logs = fs.existsSync(logDir) ? fs.readdirSync(logDir) : [];
    check('Red 기대 검증은 실패를 확인한다 (대조)', /Red 기대/.test(out3) && /실패 확인/.test(out3), out3.slice(-300));
    check('Red 실행도 원문 로그를 남긴다', logs.some(f => /-red\.log$/.test(f)), logs.join(',') || '(로그 없음)');
    check('Red 결과에 원문 경로를 보인다', /실패 원문:/.test(out3), out3.slice(-300));
  } catch (e) { check('RI-13 ③', false, e.message); } finally { rm(d3); }

  // ④ Loop A(v12/N-08, HIGH): 실패 로그가 증거 지문을 흔들지 않는다 — .gitignore 가 없는 트리에서도.
  //   install-logs·.branch-* 처럼 매번 바뀌는 런타임 산물은 treeSig·writerDirty 가 코드로 뺀다. 로그 폴더도 같다.
  const d4 = sandbox();
  try {
    fs.writeFileSync(path.join(d4, 'CLAUDE.md'), CLAUDE_MD.replace('test_command: ""', 'test_command: "node fail.js"'));
    fs.writeFileSync(path.join(d4, 'fail.js'), "console.log('  ❌ src/a.js 실패');\nprocess.exit(1);\n");
    cli(d4, ['status']);
    const noRun4 = { _HARNESS_TEST_RUN: '' };
    cli(d4, ['verify'], noRun4);
    const sig1 = ((readState(d4) || {}).lastVerify || {}).treeSig;
    cli(d4, ['verify'], noRun4);
    const sig2 = ((readState(d4) || {}).lastVerify || {}).treeSig;
    const logDir4 = path.join(d4, '.claude', 'verify-logs');
    const logs4 = fs.existsSync(logDir4) ? fs.readdirSync(logDir4) : [];
    check('실패 로그가 실제로 쌓였다 (전제)', logs4.length >= 1, logs4.join(',') || '(로그 없음)');
    check('코드 변경 없는 두 실패 verify 의 treeSig 가 같다 — 로그 파일은 지문에 들지 않는다', !!sig1 && sig1 === sig2, JSON.stringify({ sig1, sig2 }));
  } catch (e) { check('RI-13 ④', false, e.message); } finally { rm(d4); }
}

// ── RI-14: 오염 판정이 **경로 단위**로 남의 쓰기를 가른다 (v16/N-22) ──
// 왜 필요한가(2026-09-17 실측): 오염 방어는 두 계층인데 한쪽만 남의 것을 구분한다.
//   계층 A(텔레메트리 .jsonl)는 줄마다 `run` 태그가 있어 "남의 기록 보존 N건" 을 낸다.
//   계층 B(소스 파일)는 전체 교체라 줄 태깅이 불가능해, 유일한 방어가
//   `liveInteractiveWindows()` — "살아 있는 창이 2개 이상이면 스위트 **전체**를 경고로 낮춘다" 다.
//   경로 단위가 아니라 실행 전체 단위라, 오늘 다른 세션의 편집 4건이 내 오염으로 잡혔고
//   **반대로 진짜 자기 오염도 창이 열려 있기만 하면 묻힌다**(오탐 완화가 미탐을 만들었다).
//
// 귀속 재료는 이미 있다 — `docs/memory/audit-log.jsonl` 의 `tool-write` 행
//   (`post-write-sync.js` 가 Write/Edit 마다 file·session_id·timestamp 를 남긴다).
//   `cycle.touched` 는 쓰지 않는다: `mergeTouched` 가 **최초 기록자를 유지**해 재작성 시
//   소유자가 갱신되지 않는다. `_coordinator.js` 도 쓰지 않는다: 활동 스키마에 경로가 없다.
//
// 이 노드는 **계약만** 세운다. `tests/run.js` 배선은 다른 세션이 같은 블록을 고치는 중이라
//   보류한다(봉투 N-22 절의 지시). 아래는 전부 I/O 없는 순수 판정이다.
console.log('\n[RI-14] should_attribute_contamination_per_path_not_per_run');
{
  const C = require(path.join(HOOKS_SRC, '_common.js'));
  check('_common 이 judgeForeignWrite 를 export 한다', typeof C.judgeForeignWrite === 'function',
    Object.keys(C).slice(0, 20).join(','));

  // [Loop A 교정 ⑤] `if (typeof … === 'function')` 로 감싸지 않는다.
  //   감싸면 함수가 사라졌을 때 아래 단언들이 **FAIL 이 아니라 실행조차 안 되고 사라진다** —
  //   리포트에는 "1건 실패" 만 뜬다. 이 저장소의 규율은 "skip 을 pass 로 세지 않는다" 다.
  //   대신 센티널을 쓴다: 함수가 없으면 절대 null 이 아닌 값을 돌려줘서 `=== null` 대조군까지
  //   전부 정직하게 깨지게 한다.
  {
    const t0 = Date.parse('2026-09-17T10:00:00.000Z');   // 스위트 시작
    const t1 = Date.parse('2026-09-17T10:02:00.000Z');   // 스위트 끝
    const MISSING = { __missingFn: true };
    // 생존 확인 판정자를 주입한다 — 감사 행은 인증되지 않으므로 계약이 이것을 요구한다.
    const LIVE = new Set(['other-session', 'another-live']);
    const J = (rows, p, me, over) => (typeof C.judgeForeignWrite !== 'function' ? MISSING
      : C.judgeForeignWrite(rows, p, t0, t1, me === undefined ? 'my-session' : me,
          over === undefined ? { isLiveSession: id => LIVE.has(id) } : over));
    const rows = [
      // ① 남의 세션이 창 구간 안에 그 경로를 썼다 — 이것만이 배제 근거다
      { timestamp: '2026-09-17T10:00:33.000Z', event: 'tool-write',
        file: '.claude/hooks/_coordinator.js', session_id: 'other-session', tool: 'Edit' },
      // ② 내 세션의 쓰기 — 근거로 치면 자기 오염을 자기가 면제하는 것이다
      { timestamp: '2026-09-17T10:00:50.000Z', event: 'tool-write',
        file: '.claude/hooks/_mine.js', session_id: 'my-session', tool: 'Edit' },
      // ③ 창 구간 **밖**(스위트 시작 전) — 이 스위트와 무관하다
      { timestamp: '2026-09-17T09:00:00.000Z', event: 'tool-write',
        file: 'tests/run.js', session_id: 'other-session', tool: 'Edit' },
      // ④ tool-write 가 아닌 이벤트 — 쓰기 근거가 아니다
      { timestamp: '2026-09-17T10:00:40.000Z', event: 'gate-block',
        file: '.claude/hooks/_gated.js', session_id: 'other-session' },
    ];

    const hit = J(rows, '.claude/hooks/_coordinator.js');
    check('남의 세션이 구간 안에 쓴 경로는 근거가 잡힌다',
      !!hit && hit.session_id === 'other-session', JSON.stringify(hit));

    check('대조군: 내 세션의 쓰기만 있으면 근거가 아니다 — 자기 오염을 자기가 면제하면 안 된다',
      J(rows, '.claude/hooks/_mine.js') === null, JSON.stringify(J(rows, '.claude/hooks/_mine.js')));

    check('대조군: 창 구간 밖 행은 근거가 아니다',
      J(rows, 'tests/run.js') === null, JSON.stringify(J(rows, 'tests/run.js')));

    check('대조군: 아무 근거 없는 경로는 여전히 오염 후보로 남는다 — 진짜 오염을 놓치면 검사가 무의미하다',
      J(rows, 'src/untouched.js') === null, '');

    check('대조군: tool-write 가 아닌 이벤트는 쓰기 근거가 아니다',
      J(rows, '.claude/hooks/_gated.js') === null, JSON.stringify(J(rows, '.claude/hooks/_gated.js')));

    // 경로 표기 정규화 — 감사 행이 Windows 역슬래시로 올 수 있다
    const winRows = [{ timestamp: '2026-09-17T10:00:10.000Z', event: 'tool-write',
      file: '.claude\\hooks\\_win.js', session_id: 'other-session', tool: 'Write' }];
    check('경로 구분자가 달라도 같은 파일로 본다', !!J(winRows, '.claude/hooks/_win.js'), '');

    // 내 세션 id 를 모를 때 — 남의 쓰기를 전부 근거로 보면 자기 오염이 면제된다
    check('대조군: 내 세션 id 를 모르면 판정하지 않는다',
      J(rows, '.claude/hooks/_coordinator.js', null) === null, '');


    // ── Loop A 교정 (2026-09-17 적대 검증 3렌즈) ──────────────────────
    //   ① Bash 로 만든 쓰기(`files:[]` 배열 스키마)도 근거여야 한다
    //   ② 시간 경계가 ISO 문자열로 와도 하한이 사라지면 안 된다
    //   ③ 감사 행은 인증되지 않는다 — 생존 확인 없이는 아무것도 면제하지 않는다
    //   ④ 양성 단언 보강 — 대조군이 전부 `=== null` 이라 빈 함수도 통과하던 구조였다
    const bashRows = [{ timestamp: '2026-09-17T10:00:20.000Z', event: 'tool-write',
      hook: 'post-bash-observe.js', tool: 'Bash', session_id: 'other-session',
      files: ['.claude/hooks/_viaBash.js', 'docs/x.md'] }];
    check('Bash 쓰기(files 배열 스키마)도 근거로 잡힌다 — 생산자가 둘이고 스키마가 다르다',
      !!J(bashRows, '.claude/hooks/_viaBash.js'), JSON.stringify(J(bashRows, '.claude/hooks/_viaBash.js')));
    check('대조군: files 배열에 없는 경로는 근거가 아니다',
      J(bashRows, '.claude/hooks/_other.js') === null, '');

    const JISO = typeof C.judgeForeignWrite !== 'function' ? () => MISSING
      : (rows, p, live) => C.judgeForeignWrite(rows, p, '2026-09-17T10:00:00.000Z',
          '2026-09-17T10:02:00.000Z', 'my-session', { isLiveSession: live });
    check('시간 경계를 ISO 문자열로 줘도 하한이 살아 있다',
      JISO(rows, 'tests/run.js', () => true) === null,
      'ISO 를 Number() 로 읽으면 NaN 이 되어 하한이 조용히 사라진다');

    check('생존 확인 판정자를 주지 않으면 아무것도 면제하지 않는다 (fail-closed)',
      J(rows, '.claude/hooks/_coordinator.js', 'my-session', null) === null, '');
    check('대조군: 죽은(또는 존재한 적 없는) 세션의 쓰기는 근거가 아니다 — 위조 방어',
      J(rows, '.claude/hooks/_coordinator.js', 'my-session', { isLiveSession: () => false }) === null, '');

    // 양성 단언 — 같은 경로에 남의 쓰기가 여럿이면 **가장 최근**을 고른다
    const manyRows = [
      { timestamp: '2026-09-17T10:00:10.000Z', event: 'tool-write', file: 'a.js', session_id: 'other-session', tool: 'Write' },
      { timestamp: '2026-09-17T10:01:30.000Z', event: 'tool-write', file: 'a.js', session_id: 'another-live', tool: 'Edit' },
      { timestamp: '2026-09-17T10:00:40.000Z', event: 'tool-write', file: 'a.js', session_id: 'other-session', tool: 'Edit' },
    ];
    const latest = J(manyRows, 'a.js');
    check('같은 경로에 남의 쓰기가 여럿이면 가장 최근을 고른다',
      !!latest && latest.session_id === 'another-live', JSON.stringify(latest));

    // 양성 단언 — 경계값은 포함된다(엄격 미만/초과로 잘라내지 않는다)
    const edgeRows = [
      { timestamp: '2026-09-17T10:00:00.000Z', event: 'tool-write', file: 'lo.js', session_id: 'other-session' },
      { timestamp: '2026-09-17T10:02:00.000Z', event: 'tool-write', file: 'hi.js', session_id: 'other-session' },
    ];
    check('경계: 창 시작 시각과 같은 행은 포함된다', !!J(edgeRows, 'lo.js'), '');
    check('경계: 창 종료 시각과 같은 행은 포함된다', !!J(edgeRows, 'hi.js'), '');

    check('대조군: 감사 행이 비어도 터지지 않는다', J([], 'a.js') === null, '');
    check('대조군: 감사 행이 배열이 아니어도 터지지 않는다', J(null, 'a.js') === null, '');
  }
}


// ══ 세션 소유 상태의 안전망 (charter harness-v16 / N-05) ═══════════════
//
// 러너의 스냅샷·복원이 세션 디렉터리를 담지 않으면, 전수 실행이 **개발자의 실제 상태를
//   오염시키고 복원하지 못한다** — 테스트 실패가 아니라 데이터 손실로 나타난다.
//   기본값이 꺼져 있어 지금은 그 디렉터리가 없지만, 켜는 순간 생긴다.
//   **안전망은 위험보다 먼저 있어야 한다.**
{
  const runnerSrc = fs.readFileSync(path.join(REPO, 'tests', 'run.js'), 'utf8');
  const guardsSession = t => /startsWith\(\s*['"]session-['"]\s*\)/.test(t)
    && /branchStateFiles/.test(t);
  check('러너가 세션 상태 디렉터리도 스냅샷한다', guardsSession(runnerSrc),
    (runnerSrc.match(/[^\n]*session-[^\n]*/g) || []).slice(0, 2).join(' | '));
  // 반증: 세션 순회를 지운 사본은 판정이 거짓이 된다.
  const stripped = runnerSrc.replace(/if \(!sd\.startsWith\('session-'\)\) continue;/, 'continue;');
  check('반증: 세션 순회를 지운 러너는 판정이 거짓이다',
    stripped !== runnerSrc && !guardsSession(stripped),
    stripped === runnerSrc ? '변형이 일어나지 않았다' : '지워도 참');

  const sbSrc = fs.readFileSync(path.join(REPO, 'tests', 'unit', '_sandbox.js'), 'utf8');
  check('샌드박스가 세션 배치를 안다', /startsWith\(\s*['"]session-['"]\s*\)/.test(sbSrc),
    (sbSrc.match(/[^\n]*session-[^\n]*/g) || []).slice(0, 2).join(' | '));

  // 대조군: 기본값에서는 세션 배치가 **꺼져 있다.** 이것이 참이 아니면 이 저장소의
  //   살아 있는 상태가 이미 움직인 것이다.
  const S = require(path.join(REPO, '.claude', 'hooks', '_state.js'));
  const saved = process.env.HARNESS_SESSION_STATE;
  try {
    delete process.env.HARNESS_SESSION_STATE;
    check('대조군: 기본값에서 세션 배치는 꺼져 있다', S.sessionStateEnabled() === false, String(S.sessionStateEnabled()));
    check('대조군: 기본값 해석 결과가 브랜치 파일과 같다',
      String(S.resolveStatePath()) === String(S.STATE_FILE),
      `${S.resolveStatePath()} vs ${S.STATE_FILE}`);
  } finally { if (saved === undefined) delete process.env.HARNESS_SESSION_STATE; else process.env.HARNESS_SESSION_STATE = saved; }
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
