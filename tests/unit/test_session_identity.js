// tests/unit/test_session_identity.js
// 이 창은 누구인가 — charter harness-v16 / N-28
//
// 왜 이 파일이 존재하는가:
//   `credentialPath()` 가 두 파일에 사문으로 중복돼 있었고 **이미 갈라져 있었다** —
//   `session-gate.js` 는 `.session-credential-<id>.json`, `_codex-hooks.js` 는
//   `.session-credential-codex-<id>.json`. 같은 이름의 함수가 다른 파일 이름을 만든다.
//   둘 다 export 되지 않아 제3자(`advance-phase.js`)는 쓸 수 없었다.
//
//   그래서 봉투는 "CLI 가 자기 세션을 모른다" 를 선행 문제로 적었다. 실측해 보니
//   **답이 환경에 이미 있었다**: `CLAUDE_CODE_SESSION_ID` 가 자격 파일 이름과 정확히 맞고
//   `CLAUDE_PID` 가 창의 살아 있는 pid 다. 저장소의 어떤 훅도 그 둘을 쓰지 않았다.
//
//   그리고 기록된 `pid` 가 틀렸다 — `process.ppid` 는 훅을 띄운 **임시 셸**이라 즉시 죽는다.
//   실측: 자격 파일 6개 전부(이 세션 자신 포함) pid 사망, 실제 창 pid 는 생존.
//   그래서 "pid 가 살아 있으면 회수 거부" 라는 코디네이터의 안전장치가 **항상 거부하지 않는다.**
//
//   가장 중요한 규칙은 **"모르면 null"** 이다. 살아 있는 창이 둘일 때 하나를 임의로 고르면
//   조용히 남의 세션으로 기록한다. 판별 불가는 거부이고, 대조군이 그것을 지킨다.

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');

const REPO = path.resolve(__dirname, '..', '..');
const HOOKS = path.join(REPO, '.claude', 'hooks');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 220) : ''}`); }
}
const hook = name => { try { return fs.readFileSync(path.join(HOOKS, name), 'utf8'); } catch { return ''; } };
const codeOnly = s => String(s).split('\n').filter(l => !/^\s*(\/\/|\*|\/\*)/.test(l)).join('\n');

// 자격 파일을 심는다. pid 를 직접 정해 생존/사망을 만든다.
function seedCred(root, id, { pid, runtime = 'claude', heartbeat_at = Date.now() } = {}) {
  const dir = path.join(root, '.claude');
  fs.mkdirSync(dir, { recursive: true });
  const name = runtime === 'claude'
    ? `.session-credential-${id}.json`
    : `.session-credential-${runtime}-${id}.json`;
  fs.writeFileSync(path.join(dir, name), JSON.stringify({
    session_id: 's-' + id, runtime, pid, worker_pids: [pid],
    status: 'active', heartbeat_at, token: 't-' + id,
  }, null, 2));
  return path.join(dir, name);
}
// 런타임이 세션 id 를 알려주지 않은 상황을 만든다. 환경변수가 새면 스캔 분기를 못 본다 —
//   실제로 처음에 그래서 SI-05 가 엉뚱한 이유로 빨갰다.
function withoutRuntimeId(fn) {
  const a = process.env.CLAUDE_CODE_SESSION_ID, b = process.env.CODEX_SESSION_ID;
  delete process.env.CLAUDE_CODE_SESSION_ID; delete process.env.CODEX_SESSION_ID;
  try { return fn(); } finally {
    if (a === undefined) delete process.env.CLAUDE_CODE_SESSION_ID; else process.env.CLAUDE_CODE_SESSION_ID = a;
    if (b === undefined) delete process.env.CODEX_SESSION_ID; else process.env.CODEX_SESSION_ID = b;
  }
}
const DEAD_PID = 0x7ffffff0;   // 존재할 수 없는 pid

console.log('\n═══ SI: 이 창은 누구인가 ═══');

const M = (() => { try { return require(path.join(HOOKS, '_session-identity.js')); } catch (e) { return { __err: e.message }; } })();

// ── SI-01: 정본 모듈이 있다 ──────────────────────────────────────────
console.log('\n[SI-01] should_expose_one_identity_module');
{
  check('_session-identity.js 를 불러올 수 있다', !M.__err, M.__err);
  for (const fn of ['credentialPath', 'resolveSessionId', 'resolveCredential', 'windowPid', 'alive']) {
    check(`정본이 ${fn}() 를 내보낸다`, typeof M[fn] === 'function', typeof M[fn]);
  }
}

// ── SI-02: 파일 이름 규칙을 깨지 않는다 ──────────────────────────────
//   살아 있는 자격 파일이 고아가 되면 그 창은 신원을 잃는다. 런타임별 비대칭은
//   **지금 그대로** 유지하되 한 함수가 만든다.
console.log('\n[SI-02] should_keep_the_existing_file_naming');
{
  const ok = typeof M.credentialPath === 'function';
  const root = 'C:/x';
  const claude = ok ? String(M.credentialPath(root, 'abc-123', 'claude')).replace(/\\/g, '/') : '';
  const codex = ok ? String(M.credentialPath(root, 'abc-123', 'codex')).replace(/\\/g, '/') : '';
  check('claude 는 접두 없는 이름', claude.endsWith('.claude/.session-credential-abc-123.json'), claude);
  check('codex 는 런타임 접두 이름', codex.endsWith('.claude/.session-credential-codex-abc-123.json'), codex);
  // 경로 구분자·상위 참조·점은 지운다 — 파일 이름이 되기 때문이다.
  const nasty = ok ? String(M.credentialPath(root, '../../etc/passwd', 'claude')).replace(/\\/g, '/') : '';
  check('대조군: 상위 참조가 파일 이름으로 새지 않는다',
    !/\.\./.test(nasty) && nasty.includes('.claude/.session-credential-'), nasty);
}

// ── SI-03: 두 사본이 정본을 부른다 ───────────────────────────────────
console.log('\n[SI-03] should_make_both_copies_call_the_canonical');
{
  for (const f of ['session-gate.js', '_codex-hooks.js']) {
    const c = codeOnly(hook(f));
    // [Loop A] 문자열 존재만 보면 인라인 주석·리터럴로도 통과한다. **실제 require 호출**을 본다.
    check(`${f} 이 정본을 부른다`, /require\(\s*['"]\.\/_session-identity['"]\s*\)/.test(c), '호출 없음');
    // 화살표로 본문을 통째로 재구현해도 사본이다 — 정화 정규식과 이름 조립이 되살아나는지 본다.
    check(`${f} 에 자체 credentialPath 정의/재구현이 없다`,
      !/function\s+credentialPath\s*\(/.test(c)
      && !/session-credential-\$\{/.test(c)
      && !/\[\^A-Za-z0-9_-\]/.test(c), '자체 정의/재구현 잔존');
  }
}

// ── SI-04: 런타임 세션 id 가 있으면 그것을 고른다 ────────────────────
console.log('\n[SI-04] should_use_the_runtime_session_id_when_present');
{
  const ok = typeof M.resolveCredential === 'function';
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'si4-'));
  try {
    seedCred(root, 'mine-111', { pid: process.pid });
    seedCred(root, 'other-222', { pid: process.pid });
    const r = ok ? M.resolveCredential(root, { sessionId: 'mine-111', runtime: 'claude' }) : null;
    check('명시된 세션 id 의 자격을 고른다', !!r && /mine-111/.test(r.file), JSON.stringify(r && r.file));
    // 대조군: 그 id 의 파일이 없으면 다른 것을 집지 않는다.
    const r2 = ok ? M.resolveCredential(root, { sessionId: 'ghost-999', runtime: 'claude' }) : 'x';
    check('대조군: 지정한 id 의 파일이 없으면 null — 다른 것을 집지 않는다', r2 === null, JSON.stringify(r2));
  } finally { try { fs.rmSync(root, { recursive: true, force: true }); } catch {} }
}

// ── SI-05: 살아 있는 것만 고른다 ─────────────────────────────────────
console.log('\n[SI-05] should_pick_only_a_live_window');
{
  const ok = typeof M.resolveCredential === 'function';
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'si5-'));
  try {
    seedCred(root, 'dead-a', { pid: DEAD_PID });
    seedCred(root, 'dead-b', { pid: DEAD_PID });
    seedCred(root, 'live-c', { pid: process.pid });
    const r = ok ? withoutRuntimeId(() => M.resolveCredential(root, { runtime: 'claude' })) : null;
    check('자격 파일 3개 중 살아 있는 것을 고른다', !!r && /live-c/.test(r.file), JSON.stringify(r && r.file));
    check('대조군: 죽은 pid 의 것을 고르지 않는다', !r || !/dead-/.test(r.file), JSON.stringify(r && r.file));
  } finally { try { fs.rmSync(root, { recursive: true, force: true }); } catch {} }
}

// ── SI-06: 판별 불가면 null — **임의 선택 금지** ─────────────────────
//   가장 중요한 단언이다. 살아 있는 창이 둘일 때 하나를 고르면 남의 세션으로 기록한다.
console.log('\n[SI-06] should_return_null_when_it_cannot_tell');
{
  const ok = typeof M.resolveCredential === 'function';
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'si6-'));
  try {
    seedCred(root, 'live-1', { pid: process.pid });
    seedCred(root, 'live-2', { pid: process.pid });
    const r = ok ? withoutRuntimeId(() => M.resolveCredential(root, { runtime: 'claude' })) : 'x';
    check('반증: 살아 있는 후보가 둘이면 null (임의 선택 금지)', r === null, JSON.stringify(r && r.file));

    const empty = fs.mkdtempSync(path.join(os.tmpdir(), 'si6b-'));
    try {
      const r2 = ok ? withoutRuntimeId(() => M.resolveCredential(empty, { runtime: 'claude' })) : 'x';
      check('대조군: 후보가 없으면 null', r2 === null, JSON.stringify(r2));
    } finally { try { fs.rmSync(empty, { recursive: true, force: true }); } catch {} }

    const allDead = fs.mkdtempSync(path.join(os.tmpdir(), 'si6c-'));
    try {
      seedCred(allDead, 'dead-x', { pid: DEAD_PID });
      const r3 = ok ? withoutRuntimeId(() => M.resolveCredential(allDead, { runtime: 'claude' })) : 'x';
      check('대조군: 전부 죽었으면 null', r3 === null, JSON.stringify(r3));
    } finally { try { fs.rmSync(allDead, { recursive: true, force: true }); } catch {} }
  } finally { try { fs.rmSync(root, { recursive: true, force: true }); } catch {} }
}

// ── SI-07: 창의 pid 를 기록한다 ──────────────────────────────────────
//   `process.ppid` 는 훅을 띄운 임시 셸이다. 실측: 자격 파일 6개 전부 pid 사망,
//   실제 창(`CLAUDE_PID`)은 생존. 그 탓에 "살아 있으면 회수 거부" 가 작동하지 않는다.
console.log('\n[SI-07] should_record_the_window_pid_not_the_hook_shell');
{
  const ok = typeof M.windowPid === 'function';
  const saved = process.env.CLAUDE_PID;
  try {
    // 살아 있는 값이어야 채택된다 — 죽은 값은 회수 판정에 쓸모가 없고, 상시 생존 pid 를
    //   심는 것이 바로 공격이다(아래 대조군).
    process.env.CLAUDE_PID = String(process.pid);
    check('CLAUDE_PID 가 살아 있으면 그것을 쓴다', ok && M.windowPid() === process.pid, ok ? String(M.windowPid()) : '함수 없음');
    delete process.env.CLAUDE_PID;
    check('대조군: 없으면 ppid 로 떨어진다', ok && M.windowPid() === process.ppid, ok ? String(M.windowPid()) : '함수 없음');
    process.env.CLAUDE_PID = 'not-a-number';
    check('대조군: 숫자가 아니면 ppid 로 떨어진다', ok && M.windowPid() === process.ppid, ok ? String(M.windowPid()) : '함수 없음');
    // [Loop A 보안 렌즈] 죽은 값은 거부한다 — 그것을 기록하면 회수 판정이 거짓이 된다.
    process.env.CLAUDE_PID = String(DEAD_PID);
    check('반증: 죽은 pid 는 채택하지 않는다', ok && M.windowPid() === process.ppid, ok ? String(M.windowPid()) : '함수 없음');
    // **가장 중요한 대조군**: `1` 같은 상시 생존 pid 를 심으면 그 세션은 영원히 회수되지
    //   않고 활성 한도(10)를 채워 이후 모든 join 을 막는다 — 로컬 서비스 거부다.
    for (const sys of ['1', '0', '4']) {
      process.env.CLAUDE_PID = sys;
      check(`대조군: 시스템 pid ${sys} 은 창으로 받지 않는다`,
        ok && M.windowPid() === process.ppid, ok ? String(M.windowPid()) : '함수 없음');
    }
  } finally { if (saved === undefined) delete process.env.CLAUDE_PID; else process.env.CLAUDE_PID = saved; }

  // [Loop A 정합 렌즈] 첫 판은 `pid: windowPid()` **토큰 형태만** 봤다. 그래서
  //   `c.heartbeat(prior, [process.ppid])`(라벨 없는 위치 인자)와 `_codex-hooks.js` 전체를
  //   놓쳤고, **고친 결함이 같은 커밋 안에서 재발한 채로 초록이었다.**
  //   판정을 뒤집는다: "창 pid 를 쓰는가" 가 아니라 **"임시 셸 pid 가 어디에도 안 남았는가"**.
  //   세션 등록에 ppid 를 싣는 경로는 **한 줄도** 있으면 안 된다.
  const recordsShellPid = src => (codeOnly(src).match(/[^\n]*process\.ppid[^\n]*/g) || [])
    .filter(l => /pid:|worker_pids|heartbeat\(/.test(l));
  for (const f of ['session-gate.js', '_codex-hooks.js']) {
    const hits = recordsShellPid(hook(f));
    check(`${f} 이 세션 등록에 임시 셸 pid 를 싣지 않는다`, hits.length === 0, hits.join(' | '));
    check(`${f} 이 창 pid 를 쓴다`, /windowPid\(\)/.test(codeOnly(hook(f))), '호출 없음');
  }
  // 반증: 되돌린 사본은 **두 파일 각각**에서 판정이 거짓이 된다.
  for (const f of ['session-gate.js', '_codex-hooks.js']) {
    const src = hook(f);
    const back = src.replace(/\[windowPid\(\)\]/g, '[process.ppid]').replace(/pid:\s*windowPid\(\)/g, 'pid: process.ppid');
    check(`반증: ${f} 을 ppid 로 되돌리면 판정이 거짓이다`,
      back !== src && recordsShellPid(back).length > 0,
      back === src ? '변형이 일어나지 않았다' : '되돌려도 참');
  }
}

// ── SI-08: CLI 가 자기 세션을 안다 ───────────────────────────────────
console.log('\n[SI-08] should_let_the_cli_know_its_own_session');
{
  const ap = codeOnly(hook('advance-phase.js'));
  check('advance-phase 가 신원 정본을 부른다', /_session-identity/.test(ap), '호출 없음');
  const si = codeOnly(hook('_session-identity.js'));
  check('정본이 런타임 환경변수를 읽는다', /CLAUDE_CODE_SESSION_ID/.test(si),
    (si.match(/[^\n]*SESSION_ID[^\n]*/g) || []).slice(0, 2).join(' | '));
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
