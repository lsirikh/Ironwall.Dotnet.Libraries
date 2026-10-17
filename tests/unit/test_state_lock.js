// tests/unit/test_state_lock.js — _state.js 잠금/백업 단위 테스트
// FR-06: 원자적 상태 관리, NFR-01: lock 안전성
// 실행: node tests/unit/test_state_lock.js

const assert = require('assert');
const fs = require('fs');
const path = require('path');

const stateModule = require('../../.claude/hooks/_state');
const {
  LOCK_FILE,
  BACKUP_DIR,
  acquireLock,
  releaseLock,
  atomicStateUpdate,
  createBackup
} = stateModule;

// [v4/N-01] 결과 형식은 tests/run.js 의 공용 리포터가 만든다 —
//   형식을 만드는 쪽과 parseCounts 로 읽는 쪽이 갈라지지 않게.
const { makeReporter } = require('../run.js');
const R = makeReporter();
const test = (name, fn) => R.test(name, fn);

// ── 유틸 ──────────────────────────────────────────────────────────────

/** 테스트 전 기존 lock 파일 강제 제거 */
function cleanLock() {
  try { fs.unlinkSync(LOCK_FILE); } catch { /* 없으면 무시 */ }
}

/** 테스트 후 생성된 백업 파일 중 현재 테스트 pid 포함 파일 삭제 */
function cleanBackups(pid) {
  try {
    if (!fs.existsSync(BACKUP_DIR)) return;
    for (const f of fs.readdirSync(BACKUP_DIR)) {
      if (f.includes(String(pid))) {
        try { fs.unlinkSync(path.join(BACKUP_DIR, f)); } catch {}
      }
    }
  } catch { /* silent */ }
}

// ── 테스트 ─────────────────────────────────────────────────────────────

console.log('\n── Lock: orphaned (dead PID) ──');

test('should_clean_orphaned_lock_when_pid_is_dead', () => {
  cleanLock();
  // 존재하지 않는 PID로 lock 파일 직접 생성
  const deadPid = 999999999;
  fs.writeFileSync(LOCK_FILE, JSON.stringify({
    pid: deadPid,
    acquired_at: Date.now()
  }));

  // acquireLock이 orphaned lock을 정리하고 true를 반환해야 함
  const acquired = acquireLock(2000);
  try {
    assert.strictEqual(acquired, true, 'orphaned lock 정리 후 획득 성공 기대');
  } finally {
    releaseLock();
  }
});

console.log('\n── Lock: PID 기록 검증 ──');

test('should_write_pid_to_lock_file_when_lock_acquired', () => {
  cleanLock();
  const acquired = acquireLock(2000);
  assert.strictEqual(acquired, true, 'lock 획득 성공 기대');

  try {
    assert(fs.existsSync(LOCK_FILE), 'LOCK_FILE 이 존재해야 함');
    const content = JSON.parse(fs.readFileSync(LOCK_FILE, 'utf8'));
    assert('pid' in content, 'lock 파일에 pid 필드가 있어야 함');
    assert.strictEqual(content.pid, process.pid, '기록된 pid가 현재 프로세스 pid와 일치해야 함');
  } finally {
    releaseLock();
  }
});

console.log('\n── atomicStateUpdate: 정상 해제 확인 ──');

test('should_release_lock_in_finally_when_atomicStateUpdate_called', () => {
  cleanLock();
  // identity 함수 — 상태 변경 없이 그대로 반환
  atomicStateUpdate(s => s);

  // atomicStateUpdate 완료 후 lock 파일이 남아 있으면 안 됨
  assert(
    !fs.existsSync(LOCK_FILE),
    'atomicStateUpdate 완료 후 LOCK_FILE이 삭제되어야 함'
  );
});

console.log('\n── acquireLock: 타임아웃 (다른 세션이 lock 보유 시뮬레이션) ──');

test('should_return_false_when_lock_held_by_alive_process_and_timeout_expires', () => {
  cleanLock();
  // 현재 프로세스(살아있음) PID로 lock 파일을 직접 기록 → 타 세션이 점유한 것처럼 흉내
  // acquired_at을 충분히 과거로 설정해 wound-wait 조건(younger)을 피함
  const fakeLockData = {
    pid: process.pid,   // 살아있는 PID — process.kill(pid, 0) 성공
    acquired_at: Date.now() - 60000  // 60초 전 획득 → 이 테스트 세션보다 older
  };
  fs.writeFileSync(LOCK_FILE, JSON.stringify(fakeLockData));

  // 아주 짧은 타임아웃(100ms)으로 acquireLock 호출 → 거의 즉시 false 반환
  // sessionStartedAt을 현재보다 나중으로 설정해 younger 세션처럼 만들어
  // wound-wait 조건 진입 → retries>3 → false 반환
  const youngSessionStart = Date.now() + 99999;
  const result = acquireLock(100, youngSessionStart);
  try {
    assert.strictEqual(result, false, 'lock 획득 실패(false) 기대');
  } finally {
    cleanLock(); // 테스트용 lock 제거
  }
});

console.log('\n── createBackup: 파일명에 PID 포함 확인 ──');

test('should_include_pid_in_backup_filename', () => {
  const currentPid = process.pid;
  const sampleState = {
    phase: 'dev',
    track: 'C',
    activePrd: null,
    activePlan: null,
    artifacts: {},
    updated_at: new Date().toISOString(),
    iterations: { total_backwards: 0 },
    lastFailure: null,
    autoFixCount: 0,
    lastLintResult: null
  };

  const backupPath = createBackup(sampleState, 'test-reason');
  assert(backupPath !== null, 'createBackup이 경로를 반환해야 함');

  try {
    assert(fs.existsSync(BACKUP_DIR), 'BACKUP_DIR이 존재해야 함');
    const files = fs.readdirSync(BACKUP_DIR);
    const pidFiles = files.filter(f => f.includes(String(currentPid)));
    assert(
      pidFiles.length > 0,
      `BACKUP_DIR 내 현재 PID(${currentPid})가 포함된 파일이 있어야 함`
    );
  } finally {
    cleanBackups(currentPid);
  }
});

// ══ 세션 소유 상태 (charter harness-v16 / N-05) ════════════════════════
//
// 이 절이 지키는 것은 **두 방향**이다.
//   ① 켜면 세션별로 갈린다 — 같은 브랜치 두 창이 서로의 phase 를 덮지 않는다.
//   ② **끄면 지금과 바이트 단위로 같다** — 이것이 더 중요하다. 기본값이 움직이면
//      이 저장소의 살아 있는 상태가 움직인다.
//
// 기본값을 OFF 로 둔 이유는 증명할 수 없는 전제가 있기 때문이다: 훅 프로세스도
//   `CLAUDE_CODE_SESSION_ID` 를 받는가. 못 받으면 훅은 브랜치 파일로, CLI 는 세션 파일로
//   떨어져 **같은 창이 두 상태를 갖는다** — 게이트는 한쪽을 보고 커밋은 다른 쪽을 본다.

const os = require('os');

function withEnv(vars, fn) {
  const saved = {};
  for (const k of Object.keys(vars)) saved[k] = process.env[k];
  for (const [k, v] of Object.entries(vars)) { if (v === undefined) delete process.env[k]; else process.env[k] = v; }
  try { return fn(); } finally {
    for (const k of Object.keys(vars)) { if (saved[k] === undefined) delete process.env[k]; else process.env[k] = saved[k]; }
  }
}
const ON = { HARNESS_SESSION_STATE: '1' };
const OFF = { HARNESS_SESSION_STATE: undefined };

test('세션 범위를 끄면 브랜치 경로 그대로다 (대조군 — 기본값이 움직이면 안 된다)', () => {
  const p = withEnv(Object.assign({}, OFF, { CLAUDE_CODE_SESSION_ID: 'sess-aaa' }),
    () => stateModule.resolveStatePath());
  assert.strictEqual(String(p).replace(/\\/g, '/'), String(stateModule.STATE_FILE).replace(/\\/g, '/'),
    '기본값에서 해석 결과가 STATE_FILE 과 달라졌다');
});

test('세션 범위를 켜면 세션별 경로로 갈린다', () => {
  const a = withEnv(Object.assign({}, ON, { CLAUDE_CODE_SESSION_ID: 'sess-aaa' }),
    () => String(stateModule.resolveStatePath()).replace(/\\/g, '/'));
  const b = withEnv(Object.assign({}, ON, { CLAUDE_CODE_SESSION_ID: 'sess-bbb' }),
    () => String(stateModule.resolveStatePath()).replace(/\\/g, '/'));
  assert.notStrictEqual(a, b, '두 세션이 같은 파일을 가리킨다');
  assert.ok(a.includes('sess-aaa') && b.includes('sess-bbb'), `${a} | ${b}`);
  assert.ok(a.endsWith('/pipeline-state.json') && b.endsWith('/pipeline-state.json'), `${a} | ${b}`);
});

test('반증: 세션 id 를 모르면 켜져 있어도 브랜치 경로로 떨어진다 (fail-closed)', () => {
  const p = withEnv(Object.assign({}, ON, { CLAUDE_CODE_SESSION_ID: undefined, CODEX_SESSION_ID: undefined }),
    () => String(stateModule.resolveStatePath()).replace(/\\/g, '/'));
  assert.strictEqual(p, String(stateModule.STATE_FILE).replace(/\\/g, '/'),
    '신원을 모르는데 세션 경로를 만들었다 — 아무 세션이나 집는 것과 같다');
});

test('브랜치 상태가 세션 파일로 유실 없이 이전된다', () => {
  const box = fs.mkdtempSync(path.join(os.tmpdir(), 'ss-mig-'));
  try {
    const branchFile = path.join(box, 'pipeline-state.json');
    const payload = { phase: 'dev', track: 'C', activePrd: 'docs/prds/x.md', cycle: { startedAt: 'T', touched: [{ path: 'a.js' }] } };
    fs.writeFileSync(branchFile, JSON.stringify(payload, null, 2));
    const sessionFile = path.join(box, 'session-zzz', 'pipeline-state.json');

    const moved = stateModule.migrateStateToSession(branchFile, sessionFile, 'zzz');
    assert.strictEqual(moved, true, '이전이 일어나지 않았다');
    const got = JSON.parse(fs.readFileSync(sessionFile, 'utf8'));
    for (const k of ['phase', 'track', 'activePrd']) assert.strictEqual(got[k], payload[k], `${k} 가 유실됐다`);
    assert.deepStrictEqual(got.cycle.touched, payload.cycle.touched, 'cycle.touched 가 유실됐다');
    // [Loop A] 표식은 `migratedFrom` 이다. `seededFrom` 은 **문자열** 계약이고
    //   (`DEFAULT_STATE@<sha7>`) Manual·기존 테스트·화면 표시가 그것을 전제한다 —
    //   거기에 객체를 넣으면 `[object Object]` 가 찍히고 문자열 단언이 깨진다.
    assert.ok(got.migratedFrom && typeof got.migratedFrom === 'object', '이전 표식(migratedFrom)이 없다');
    assert.ok(!got.seededFrom || typeof got.seededFrom === 'string',
      'seededFrom 의 문자열 계약이 깨졌다: ' + typeof got.seededFrom);
    assert.ok(fs.existsSync(branchFile), '원본을 지웠다 — 이전은 복사이지 이동이 아니다');
  } finally { try { fs.rmSync(box, { recursive: true, force: true }); } catch {} }
});

test('반증: 이미 이전된 상태는 다시 덮지 않는다 (멱등)', () => {
  const box = fs.mkdtempSync(path.join(os.tmpdir(), 'ss-idem-'));
  try {
    const branchFile = path.join(box, 'pipeline-state.json');
    const sessionFile = path.join(box, 'session-zzz', 'pipeline-state.json');
    fs.writeFileSync(branchFile, JSON.stringify({ phase: 'setup', track: 'C' }));
    assert.strictEqual(stateModule.migrateStateToSession(branchFile, sessionFile, 'zzz'), true);
    // 세션 쪽이 진행됐다 — 두 번째 이전이 이것을 옛 상태로 되돌리면 작업을 잃는다.
    const advanced = Object.assign(JSON.parse(fs.readFileSync(sessionFile, 'utf8')), { phase: 'complete' });
    fs.writeFileSync(sessionFile, JSON.stringify(advanced, null, 2));
    const again = stateModule.migrateStateToSession(branchFile, sessionFile, 'zzz');
    assert.strictEqual(again, false, '두 번째 이전이 일어났다');
    assert.strictEqual(JSON.parse(fs.readFileSync(sessionFile, 'utf8')).phase, 'complete', '진행된 상태를 덮었다');
  } finally { try { fs.rmSync(box, { recursive: true, force: true }); } catch {} }
});

test('리스: 같은 세션 파일을 두 세션이 다투면 뒤의 것이 거부된다', () => {
  const r1 = stateModule.claimStateLease('br-test', 'sess-one');
  assert.strictEqual(r1.ok, true, JSON.stringify(r1));
  const r2 = stateModule.claimStateLease('br-test', 'sess-two');
  assert.strictEqual(r2.ok, false, '남의 리스를 빼앗았다');
  assert.strictEqual(r2.owner, 'sess-one', JSON.stringify(r2));
  // 반증: 같은 세션의 재claim 은 허용된다 — 창이 다시 붙을 때마다 막히면 안 된다.
  const r3 = stateModule.claimStateLease('br-test', 'sess-one');
  assert.strictEqual(r3.ok, true, '자기 리스를 다시 잡지 못한다');
  stateModule.releaseStateLease('br-test', 'sess-one');
});

// ── 결과 ──────────────────────────────────────────────────────────────

R.done();
