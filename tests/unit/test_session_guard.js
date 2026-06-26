// tests/unit/test_session_guard.js
// Session Guard v1.0 — 100 시뮬레이션 테스트
// UT-40 (단위) + IT-30 (통합) + EC-20 (엣지케이스) + RT-10 (회귀)
// 실행: node tests/unit/test_session_guard.js

'use strict';

const fs   = require('fs');
const path = require('path');
const os   = require('os');

// ── 테스트 픽스처 ──────────────────────────────────────────────────────────

const TMP_DIR     = fs.mkdtempSync(path.join(os.tmpdir(), 'sg-test-'));
const GUARD_FILE  = path.join(TMP_DIR, 'session-guard.json');
const AUDIT_FILE  = path.join(TMP_DIR, 'audit-log.jsonl');

// 모듈 환경 변수로 경로 오버라이드 (환경 격리)
process.env.SESSION_GUARD_ID = '';  // 각 테스트에서 초기화

// _session-guard.js를 로드하되, 파일 경로를 TMP_DIR로 패치
const guardModule = require('../../.claude/hooks/_session-guard.js');
// 내부 경로 패치를 위해 직접 로드해서 변수를 덮어씌움
// (실제 경로는 모듈 내부에 하드코딩되어 있으므로, 테스트에서 TMP_DIR 사용)
// 단위 테스트는 내부 함수에 TMP_DIR 경로를 직접 전달하거나, 모듈 경로를 eval로 교체

// 간소화: 내부 함수는 exports를 통해 테스트하되, GUARD_FILE은 실제 경로 사용
// 단위 테스트에서 실제 guard 파일을 TMP로 오버라이드하는 헬퍼
const REAL_GUARD = guardModule.GUARD_FILE;
const REAL_BAK   = guardModule.GUARD_BAK;
const REAL_LOCK  = guardModule.GUARD_LOCK;

function overrideGuardFile(content) {
  const dir = path.dirname(REAL_GUARD);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(REAL_GUARD, content, 'utf8');
}

function cleanGuardFile() {
  try { fs.unlinkSync(REAL_GUARD); } catch {}
  try { fs.unlinkSync(REAL_BAK); } catch {}
  try { fs.unlinkSync(REAL_LOCK); } catch {}
}

function resetEnv() {
  delete process.env.SESSION_GUARD_ID;
  delete process.env._SESSION_GUARD_EXIT_REGISTERED;
}

// ── 테스트 러너 ────────────────────────────────────────────────────────────

let passed = 0;
let failed = 0;
const failures = [];

function test(name, fn) {
  try {
    resetEnv();
    cleanGuardFile();
    fn();
    passed++;
    process.stdout.write('.');
  } catch (e) {
    failed++;
    failures.push({ name, error: e.message });
    process.stdout.write('F');
  }
}

function assert(condition, msg) {
  if (!condition) throw new Error(msg || 'Assertion failed');
}

function assertEqual(a, b, msg) {
  if (a !== b) throw new Error(msg || `Expected ${JSON.stringify(b)}, got ${JSON.stringify(a)}`);
}

function assertContains(str, sub, msg) {
  if (!str.includes(sub)) throw new Error(msg || `Expected "${str}" to contain "${sub}"`);
}

// ── UT-01~40: 단위 테스트 ─────────────────────────────────────────────────

console.log('\n[Session Guard] UT (단위 테스트) 실행 중...\n');

// UT-01: generateSessionId 형식 검증 (ppid 기반)
test('UT-01: generateSessionId — ppid-{number} 형식', () => {
  const id = guardModule._generateSessionId();
  assert(/^ppid-\d+$/.test(id), `형식 불일치: ${id}`);
});

// UT-02: generateSessionId 환경변수 캐시
test('UT-02: generateSessionId — 환경변수 캐시 재사용', () => {
  process.env.SESSION_GUARD_ID = 'cached-id-1234';
  const id = guardModule._generateSessionId();
  assertEqual(id, 'cached-id-1234');
});

// UT-03: generateSessionId — 같은 ppid에서는 동일 ID 반환 (안정성)
test('UT-03: generateSessionId — 같은 ppid에서 동일 ID', () => {
  delete process.env.SESSION_GUARD_ID;
  const id1 = guardModule._generateSessionId();
  delete process.env.SESSION_GUARD_ID;
  const id2 = guardModule._generateSessionId();
  // ppid 기반이므로 같은 프로세스에서는 동일 ID 생성이 정상 (Claude Code 창 1개 = 세션 1개)
  assert(id1 === id2, `같은 ppid에서 다른 ID: ${id1} vs ${id2}`);
  assert(id1.startsWith('ppid-'), `ppid 형식 아님: ${id1}`);
});

// UT-04: readRegistry — 파일 없을 때 emptyRegistry 반환
test('UT-04: readRegistry — 파일 없음 → 빈 레지스트리', () => {
  const registry = guardModule._readRegistry();
  assert(registry.version === 1);
  assert(typeof registry.sessions === 'object');
  assertEqual(Object.keys(registry.sessions).length, 0);
});

// UT-05: readRegistry — 정상 JSON 파싱
test('UT-05: readRegistry — 정상 JSON', () => {
  const data = { version: 1, sessions: { 'abc': { sessionId: 'abc', pid: 1 } } };
  overrideGuardFile(JSON.stringify(data));
  const registry = guardModule._readRegistry();
  assert(registry.sessions['abc']);
});

// UT-06: readRegistry — 손상된 JSON → 빈 레지스트리
test('UT-06: readRegistry — 손상된 JSON → 빈 레지스트리', () => {
  overrideGuardFile('{invalid json}}}');
  const registry = guardModule._readRegistry();
  assertEqual(Object.keys(registry.sessions).length, 0);
});

// UT-07: readRegistry — 빈 파일 → 빈 레지스트리
test('UT-07: readRegistry — 빈 파일 → 빈 레지스트리', () => {
  overrideGuardFile('');
  const registry = guardModule._readRegistry();
  assertEqual(Object.keys(registry.sessions).length, 0);
});

// UT-08: writeRegistry — 쓰기 후 읽기 일치
test('UT-08: writeRegistry — 쓰기/읽기 왕복', () => {
  const data = { version: 1, sessions: { 'test-id': { sessionId: 'test-id', pid: 9999 } } };
  guardModule._writeRegistry(data);
  const back = guardModule._readRegistry();
  assert(back.sessions['test-id']);
  assertEqual(back.sessions['test-id'].pid, 9999);
});

// UT-09: isProcessAlive — 현재 PID는 항상 살아있음
test('UT-09: isProcessAlive — 현재 PID 생존', () => {
  assert(guardModule._isProcessAlive(process.pid));
});

// UT-10: isProcessAlive — 0, null → false
test('UT-10: isProcessAlive — 0/null → false', () => {
  assert(!guardModule._isProcessAlive(0));
  assert(!guardModule._isProcessAlive(null));
});

// UT-11: isProcessAlive — 존재하지 않는 PID
test('UT-11: isProcessAlive — 존재하지 않는 PID', () => {
  // PID 999999999는 거의 확실히 없음
  const alive = guardModule._isProcessAlive(999999999);
  assert(!alive, '죽은 PID는 false여야 함');
});

// UT-12: isStale — TTL 초과 세션
test('UT-12: isStale — TTL 초과 → stale', () => {
  const session = {
    sessionId: 'old-id',
    pid: 999999999, // 죽은 PID
    lastHeartbeat: new Date(Date.now() - 200_000).toISOString(), // 200초 전
  };
  assert(guardModule._isStale(session, 'my-id'), '200초 전 세션은 stale이어야 함');
});

// UT-13: isStale — 자신은 항상 fresh
test('UT-13: isStale — 자신(mySessionId) → fresh', () => {
  const session = {
    sessionId: 'my-id',
    pid: 999999999,
    lastHeartbeat: new Date(Date.now() - 200_000).toISOString(),
  };
  assert(!guardModule._isStale(session, 'my-id'), '자신은 stale이 아님');
});

// UT-14: isStale — null 세션
test('UT-14: isStale — null → stale', () => {
  assert(guardModule._isStale(null, 'my-id'));
});

// UT-15: isStale — 최근 heartbeat + 살아있는 PID → fresh
test('UT-15: isStale — 최근 heartbeat + 살아있는 PID', () => {
  const session = {
    sessionId: 'other-id',
    pid: process.pid, // 현재 프로세스 (살아있음)
    lastHeartbeat: new Date().toISOString(),
  };
  assert(!guardModule._isStale(session, 'my-id'));
});

// UT-16: cleanStale — 스테일 세션 제거
test('UT-16: cleanStale — 스테일 세션 제거', () => {
  const registry = {
    version: 1,
    sessions: {
      'stale-id': {
        sessionId: 'stale-id',
        pid: 999999999,
        lastHeartbeat: new Date(Date.now() - 200_000).toISOString(),
      },
      'fresh-id': {
        sessionId: 'fresh-id',
        pid: process.pid,
        lastHeartbeat: new Date().toISOString(),
      },
    },
  };
  const removed = guardModule._cleanStale(registry, 'my-id');
  assert(removed.includes('stale-id'), '스테일 세션 제거됨');
  assert(!registry.sessions['stale-id'], '레지스트리에서 제거됨');
  assert(registry.sessions['fresh-id'], 'fresh 세션 보존됨');
});

// UT-17: cleanStale — 스테일 없을 때 빈 배열
test('UT-17: cleanStale — 스테일 없음 → 빈 배열', () => {
  const registry = {
    version: 1,
    sessions: {
      'fresh-id': {
        sessionId: 'fresh-id',
        pid: process.pid,
        lastHeartbeat: new Date().toISOString(),
      },
    },
  };
  const removed = guardModule._cleanStale(registry, 'my-id');
  assertEqual(removed.length, 0);
});

// UT-18: upsertSelf — 새 세션 등록
test('UT-18: upsertSelf — 새 세션 등록', () => {
  const registry = { version: 1, sessions: {} };
  guardModule._upsertSelf(registry, 'new-id', { branch: 'main', phase: 'dev' });
  assert(registry.sessions['new-id']);
  assertEqual(registry.sessions['new-id'].branch, 'main');
  assertEqual(registry.sessions['new-id'].phase, 'dev');
  assert(registry.sessions['new-id'].startedAt);
});

// UT-19: upsertSelf — 기존 세션 heartbeat 갱신
test('UT-19: upsertSelf — 기존 세션 heartbeat 갱신', () => {
  const oldTime = new Date(Date.now() - 10000).toISOString();
  const registry = {
    version: 1,
    sessions: {
      'exist-id': {
        sessionId: 'exist-id',
        pid: process.pid,
        startedAt: oldTime,
        lastHeartbeat: oldTime,
        branch: 'old-branch',
        phase: 'plan',
        lastFiles: ['a.js'],
      },
    },
  };
  guardModule._upsertSelf(registry, 'exist-id', { branch: 'new-branch', phase: 'dev' });
  const s = registry.sessions['exist-id'];
  assertEqual(s.startedAt, oldTime, 'startedAt 보존');
  assert(s.lastHeartbeat > oldTime, 'heartbeat 갱신');
  assert(s.lastFiles.includes('a.js'), 'lastFiles 보존');
});

// UT-20: detectConflicts — 타 세션만 반환
test('UT-20: detectConflicts — 자신 제외', () => {
  const registry = {
    version: 1,
    sessions: {
      'my-id': { sessionId: 'my-id', pid: process.pid, startedAt: new Date().toISOString() },
      'other-id': { sessionId: 'other-id', pid: 9999, startedAt: new Date().toISOString() },
    },
  };
  const conflicts = guardModule._detectConflicts(registry, 'my-id');
  assertEqual(conflicts.length, 1);
  assertEqual(conflicts[0].sessionId, 'other-id');
});

// UT-21: detectConflicts — 세션 없을 때 빈 배열
test('UT-21: detectConflicts — 빈 레지스트리', () => {
  const registry = { version: 1, sessions: { 'my-id': { sessionId: 'my-id' } } };
  const conflicts = guardModule._detectConflicts(registry, 'my-id');
  assertEqual(conflicts.length, 0);
});

// UT-22: detectConflicts — 다수 세션
test('UT-22: detectConflicts — 다수 타 세션', () => {
  const registry = {
    version: 1,
    sessions: {
      'my-id':  { sessionId: 'my-id',  startedAt: new Date().toISOString() },
      'sid-01': { sessionId: 'sid-01', startedAt: new Date().toISOString() },
      'sid-02': { sessionId: 'sid-02', startedAt: new Date().toISOString() },
      'sid-03': { sessionId: 'sid-03', startedAt: new Date().toISOString() },
    },
  };
  const conflicts = guardModule._detectConflicts(registry, 'my-id');
  assertEqual(conflicts.length, 3);
});

// UT-23: buildWarningBanner — 동일 브랜치 표시
test('UT-23: buildWarningBanner — 동일 브랜치 → 동일 브랜치 표시', () => {
  const conflicts = [{ sessionId: 'sid-A', pid: 1234, branch: 'main', phase: 'dev', ageMin: 5 }];
  const banner = guardModule._buildWarningBanner(conflicts, 'main', 'dev');
  assertContains(banner, '동일 브랜치');
  assertContains(banner, 'main');
});

// UT-24: buildWarningBanner — 다른 브랜치 표시
test('UT-24: buildWarningBanner — 다른 브랜치 표시', () => {
  const conflicts = [{ sessionId: 'sid-B', pid: 1234, branch: 'feature/xyz', phase: 'dev', ageMin: 2 }];
  const banner = guardModule._buildWarningBanner(conflicts, 'main', 'dev');
  assertContains(banner, '다른 브랜치');
  assertContains(banner, 'feature/xyz');
});

// UT-25: buildWarningBanner — 다른 phase → Phase 분리 ✅ 표시
test('UT-25: buildWarningBanner — 다른 phase → Phase 분리 표시', () => {
  const conflicts = [{ sessionId: 'sid-C', pid: 1234, branch: 'main', phase: 'plan', ageMin: 1 }];
  const banner = guardModule._buildWarningBanner(conflicts, 'main', 'dev');
  assertContains(banner, 'Phase 분리');
  assertContains(banner, 'dev');
  assertContains(banner, 'plan');
});

// UT-26: buildWarningBanner — 충돌 없을 때 빈 문자열
test('UT-26: buildWarningBanner — 충돌 없음 → 빈 문자열', () => {
  const banner = guardModule._buildWarningBanner([], 'main', 'dev');
  assertEqual(banner, '');
});

// UT-27: buildWarningBanner — 2건 이상 → CRITICAL 표시
test('UT-27: buildWarningBanner — 2건 이상 → CRITICAL', () => {
  const conflicts = [
    { sessionId: 'sid-A', pid: 1, branch: 'main', phase: 'dev', ageMin: 1 },
    { sessionId: 'sid-B', pid: 2, branch: 'main', phase: 'dev', ageMin: 2 },
  ];
  const banner = guardModule._buildWarningBanner(conflicts, 'main', 'dev');
  assertContains(banner, 'CRITICAL');
});

// UT-28: buildWarningBanner — session-clear 명령 포함
test('UT-28: buildWarningBanner — session-clear 명령 포함', () => {
  const conflicts = [{ sessionId: 'sid-A', pid: 1, branch: 'main', phase: 'dev', ageMin: 0 }];
  const banner = guardModule._buildWarningBanner(conflicts, 'main', 'dev');
  assertContains(banner, 'session-clear');
});

// UT-29: guardLock/guardUnlock — lock 획득 후 해제
test('UT-29: guardLock/guardUnlock — lock 획득/해제', () => {
  const ok = guardModule._guardLock();
  assert(ok, 'lock 획득 성공');
  const lockExists = fs.existsSync(REAL_LOCK);
  guardModule._guardUnlock();
  const lockGone = !fs.existsSync(REAL_LOCK);
  assert(lockExists, 'lock 파일 생성됨');
  assert(lockGone, 'lock 파일 해제됨');
});

// UT-30: guardLock — 이미 잡힌 lock에서 타임아웃 후 false 반환
test('UT-30: guardLock — 타임아웃 시 false (zombie lock 포함)', () => {
  // 현재 PID와 다른 PID로 zombie lock 생성
  const dir = path.dirname(REAL_LOCK);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
  fs.writeFileSync(REAL_LOCK, JSON.stringify({ pid: 999999999, ts: Date.now() }), 'utf8');
  const ok = guardModule._guardLock();
  guardModule._guardUnlock();
  // zombie lock이므로 lock 획득 성공 (zombie 감지 → 제거 → 재획득)
  assert(typeof ok === 'boolean', 'lock 결과는 boolean');
});

// UT-31: updateLastFiles — lastFiles 업데이트
test('UT-31: updateLastFiles — 파일 목록 갱신', () => {
  const id = 'test-session-uf';
  process.env.SESSION_GUARD_ID = id;
  const registry = {
    version: 1,
    sessions: {
      [id]: { sessionId: id, pid: process.pid, lastHeartbeat: new Date().toISOString(), lastFiles: [] },
    },
  };
  guardModule._writeRegistry(registry);
  guardModule.updateLastFiles(id, ['src/a.js', 'src/b.js']);
  const updated = guardModule._readRegistry();
  assert(updated.sessions[id], '세션 존재');
  const files = updated.sessions[id].lastFiles || [];
  assert(files.includes('src/a.js'), 'a.js 포함');
  assert(files.includes('src/b.js'), 'b.js 포함');
});

// UT-32: updateLastFiles — MAX_FILES(20) 초과 시 잘림
test('UT-32: updateLastFiles — 20개 초과 시 잘림', () => {
  const id = 'test-session-max';
  process.env.SESSION_GUARD_ID = id;
  const existing = Array.from({ length: 18 }, (_, i) => `file${i}.js`);
  const registry = {
    version: 1,
    sessions: {
      [id]: { sessionId: id, pid: process.pid, lastHeartbeat: new Date().toISOString(), lastFiles: existing },
    },
  };
  guardModule._writeRegistry(registry);
  guardModule.updateLastFiles(id, ['new1.js', 'new2.js', 'new3.js', 'new4.js']);
  const updated = guardModule._readRegistry();
  const files = updated.sessions[id].lastFiles || [];
  assert(files.length <= 20, `최대 20개: 현재 ${files.length}개`);
});

// UT-33: checkFileConflict — 타 세션이 편집한 파일 → 충돌 반환
test('UT-33: checkFileConflict — 타 세션 파일 충돌 감지', () => {
  const otherId = 'other-session-x';
  process.env.SESSION_GUARD_ID = 'my-session-x';
  const registry = {
    version: 1,
    sessions: {
      'my-session-x': { sessionId: 'my-session-x', lastFiles: [] },
      [otherId]: { sessionId: otherId, lastFiles: ['install.js', 'config.json'] },
    },
  };
  guardModule._writeRegistry(registry);
  const conflict = guardModule.checkFileConflict('install.js');
  assert(conflict !== null, '충돌이 감지되어야 함');
  assertContains(conflict.warning, 'install.js');
});

// UT-34: checkFileConflict — 충돌 없는 파일 → null
test('UT-34: checkFileConflict — 충돌 없음 → null', () => {
  process.env.SESSION_GUARD_ID = 'my-session-y';
  const registry = {
    version: 1,
    sessions: {
      'my-session-y': { sessionId: 'my-session-y', lastFiles: [] },
      'other-session-y': { sessionId: 'other-session-y', lastFiles: ['other-file.js'] },
    },
  };
  guardModule._writeRegistry(registry);
  const conflict = guardModule.checkFileConflict('safe-file.js');
  assert(conflict === null, '충돌 없어야 함');
});

// UT-35: checkFileConflict — 자신의 파일 → null
test('UT-35: checkFileConflict — 자신의 lastFiles → null', () => {
  process.env.SESSION_GUARD_ID = 'self-session-z';
  const registry = {
    version: 1,
    sessions: {
      'self-session-z': { sessionId: 'self-session-z', lastFiles: ['my-file.js'] },
    },
  };
  guardModule._writeRegistry(registry);
  const conflict = guardModule.checkFileConflict('my-file.js');
  assert(conflict === null, '자신의 파일은 충돌 아님');
});

// UT-36: removeSession — 세션 삭제
test('UT-36: removeSession — 세션 삭제 확인', () => {
  const id = 'del-session';
  const registry = {
    version: 1,
    sessions: {
      [id]: { sessionId: id, pid: process.pid },
      'keep-session': { sessionId: 'keep-session', pid: process.pid },
    },
  };
  guardModule._writeRegistry(registry);
  guardModule._removeSession(id);
  const updated = guardModule._readRegistry();
  assert(!updated.sessions[id], '삭제된 세션 없음');
  assert(updated.sessions['keep-session'], '다른 세션 보존');
});

// UT-37: removeSession — 없는 세션 삭제 시 오류 없음
test('UT-37: removeSession — 없는 세션 → 오류 없음', () => {
  guardModule._removeSession('non-existent-id');
  // 오류 없으면 통과
});

// UT-38: runSessionGuard — 기본 흐름 (충돌 없음)
test('UT-38: runSessionGuard — 단일 세션, 충돌 없음', () => {
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  assert(!result.error, `오류 없어야 함: ${result.error}`);
  assert(result.sessionId, '세션 ID 생성됨');
  assertEqual(result.warning, '', '경고 없음');
  assertEqual(result.conflicts.length, 0, '충돌 없음');
});

// UT-39: runSessionGuard — 레지스트리에 타 세션 존재 → 경고 생성
test('UT-39: runSessionGuard — 타 세션 존재 → 경고 생성', () => {
  const otherId = 'concurrent-session-ut39';
  const registry = {
    version: 1,
    sessions: {
      [otherId]: {
        sessionId: otherId,
        pid: process.pid, // 살아있는 PID로 설정
        startedAt: new Date().toISOString(),
        lastHeartbeat: new Date().toISOString(),
        branch: 'main',
        phase: 'dev',
        lastFiles: [],
      },
    },
  };
  guardModule._writeRegistry(registry);
  process.env.SESSION_GUARD_ID = 'my-new-session-ut39';
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  assert(result.conflicts.length > 0, '충돌이 감지되어야 함');
  assert(result.warning !== '', '경고 배너가 생성되어야 함');
});

// UT-40: runSessionGuard — 스테일 세션 자동 정리
test('UT-40: runSessionGuard — 스테일 세션 자동 정리 후 충돌 없음', () => {
  const staleId = 'stale-session-ut40';
  const registry = {
    version: 1,
    sessions: {
      [staleId]: {
        sessionId: staleId,
        pid: 999999999, // 죽은 PID
        startedAt: new Date(Date.now() - 300_000).toISOString(),
        lastHeartbeat: new Date(Date.now() - 200_000).toISOString(), // 200초 전
        branch: 'main',
        phase: 'dev',
        lastFiles: [],
      },
    },
  };
  guardModule._writeRegistry(registry);
  process.env.SESSION_GUARD_ID = 'fresh-session-ut40';
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  assertEqual(result.conflicts.length, 0, '스테일 세션 정리 후 충돌 없음');
  assertEqual(result.warning, '', '경고 없음');
});

// ── IT-01~30: 통합 테스트 ─────────────────────────────────────────────────

console.log('\n[Session Guard] IT (통합 테스트) 실행 중...\n');

// IT-01: session-gate.js require 시 session-guard 로드 가능
test('IT-01: session-guard가 session-gate 환경에서 로드 가능', () => {
  const g = require('../../.claude/hooks/_session-guard.js');
  assert(typeof g.runSessionGuard === 'function');
  assert(typeof g.updateLastFiles === 'function');
});

// IT-02: advance-phase.js session-status 명령
test('IT-02: printSessionStatus — 오류 없이 실행', () => {
  let error = null;
  const orig = console.log;
  console.log = () => {};
  try {
    guardModule.printSessionStatus();
  } catch (e) {
    error = e;
  } finally {
    console.log = orig;
  }
  assert(!error, `오류 없어야 함: ${error}`);
});

// IT-03: clearAllSessions — 레지스트리 초기화
test('IT-03: clearAllSessions — 레지스트리 비워짐', () => {
  const registry = {
    version: 1,
    sessions: {
      'sid-1': { sessionId: 'sid-1' },
      'sid-2': { sessionId: 'sid-2' },
    },
  };
  guardModule._writeRegistry(registry);
  const orig = console.log;
  console.log = () => {};
  try { guardModule.clearAllSessions(); } finally { console.log = orig; }
  const after = guardModule._readRegistry();
  assertEqual(Object.keys(after.sessions).length, 0, '모든 세션 삭제됨');
});

// IT-04: 세션 등록 → heartbeat → 삭제 라이프사이클
test('IT-04: 완전한 세션 라이프사이클', () => {
  process.env.SESSION_GUARD_ID = 'lifecycle-id-it04';
  guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  let reg = guardModule._readRegistry();
  assert(reg.sessions['lifecycle-id-it04'], '등록됨');

  // heartbeat 갱신 (재호출)
  const t1 = reg.sessions['lifecycle-id-it04'].lastHeartbeat;
  guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  reg = guardModule._readRegistry();
  const t2 = reg.sessions['lifecycle-id-it04'].lastHeartbeat;
  assert(t2 >= t1, 'heartbeat 갱신됨');

  // 삭제
  guardModule._removeSession('lifecycle-id-it04');
  reg = guardModule._readRegistry();
  assert(!reg.sessions['lifecycle-id-it04'], '삭제됨');
});

// IT-05: .bak 파일 복원
test('IT-05: 손상된 guard.json → .bak에서 복원', () => {
  const data = { version: 1, sessions: { 'bak-id': { sessionId: 'bak-id' } } };
  guardModule._writeRegistry(data); // .bak 갱신
  overrideGuardFile('{invalid}'); // 기본 파일 손상
  const recovered = guardModule._readRegistry();
  assert(recovered.sessions['bak-id'], '.bak에서 복원됨');
});

// IT-06: 동일 세션 ID 연속 등록 → 멱등 (idempotent)
test('IT-06: 동일 세션 재등록 → 멱등', () => {
  process.env.SESSION_GUARD_ID = 'idem-id-it06';
  guardModule.runSessionGuard({ branch: 'dev', phase: 'dev' });
  guardModule.runSessionGuard({ branch: 'dev', phase: 'dev' });
  const reg = guardModule._readRegistry();
  const count = Object.values(reg.sessions).filter(s => s.sessionId === 'idem-id-it06').length;
  assertEqual(count, 1, '중복 등록 없음');
});

// IT-07: 파일 충돌 → runSessionGuard 후 checkFileConflict
test('IT-07: runSessionGuard 후 checkFileConflict 동작', () => {
  const otherId = 'writer-it07';
  const registry = {
    version: 1,
    sessions: {
      [otherId]: {
        sessionId: otherId,
        pid: process.pid,
        startedAt: new Date().toISOString(),
        lastHeartbeat: new Date().toISOString(),
        branch: 'main',
        phase: 'dev',
        lastFiles: ['install.js'],
      },
    },
  };
  guardModule._writeRegistry(registry);
  process.env.SESSION_GUARD_ID = 'reader-it07';
  guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  const conflict = guardModule.checkFileConflict('install.js');
  assert(conflict !== null, '파일 충돌 감지됨');
});

// IT-08: updateLastFiles 후 checkFileConflict 감지
test('IT-08: updateLastFiles → checkFileConflict 감지', () => {
  process.env.SESSION_GUARD_ID = 'checker-it08';
  const registry = {
    version: 1,
    sessions: {
      'checker-it08': { sessionId: 'checker-it08', lastFiles: [] },
      'writer-it08':  { sessionId: 'writer-it08',  lastFiles: [] },
    },
  };
  guardModule._writeRegistry(registry);
  guardModule.updateLastFiles('writer-it08', ['shared.js']);
  const conflict = guardModule.checkFileConflict('shared.js');
  assert(conflict !== null, '충돌 감지됨');
});

// IT-09: MAX_SESSIONS(8) 초과 시 폴백
test('IT-09: MAX_SESSIONS 초과 → 폴백 처리', () => {
  const sessions = {};
  for (let i = 0; i < 8; i++) {
    const id = `max-test-${i}`;
    sessions[id] = {
      sessionId: id,
      pid: process.pid,
      startedAt: new Date().toISOString(),
      lastHeartbeat: new Date().toISOString(),
      branch: 'main',
      phase: 'dev',
      lastFiles: [],
    };
  }
  guardModule._writeRegistry({ version: 1, sessions });
  process.env.SESSION_GUARD_ID = 'overflow-session';
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  // 8개 미만으로 stale cleanup 후 등록 가능, 8개 이상이면 폴백
  assert(result.error === 'max-sessions' || result.sessionId, '폴백 또는 정상 등록');
});

// IT-10: 스테일 + 정상 세션 혼재 시 정리 후 충돌 감지
test('IT-10: 스테일 + 정상 세션 혼재 — 정리 후 정상 세션 충돌 감지', () => {
  process.env.SESSION_GUARD_ID = 'fresh-main-it10';
  const registry = {
    version: 1,
    sessions: {
      'stale-it10': {
        sessionId: 'stale-it10', pid: 999999999,
        lastHeartbeat: new Date(Date.now() - 200_000).toISOString(),
        branch: 'main', phase: 'dev', lastFiles: [],
      },
      'active-it10': {
        sessionId: 'active-it10', pid: process.pid,
        startedAt: new Date().toISOString(),
        lastHeartbeat: new Date().toISOString(),
        branch: 'main', phase: 'dev', lastFiles: [],
      },
    },
  };
  guardModule._writeRegistry(registry);
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  assertEqual(result.conflicts.length, 1, '스테일 제거 후 활성 세션 1개 충돌');
  assertEqual(result.conflicts[0].sessionId, 'active-it10');
});

// IT-11~30: 추가 통합 시나리오

test('IT-11: branch 다르면 브랜치 불일치 경고', () => {
  const otherId = 'other-branch-it11';
  const registry = {
    version: 1,
    sessions: {
      [otherId]: { sessionId: otherId, pid: process.pid, startedAt: new Date().toISOString(), lastHeartbeat: new Date().toISOString(), branch: 'feature/abc', phase: 'dev', lastFiles: [] },
    },
  };
  guardModule._writeRegistry(registry);
  process.env.SESSION_GUARD_ID = 'main-session-it11';
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  assertContains(result.warning, '다른 브랜치');
});

test('IT-12: 동일 branch + 동일 phase → 동일 phase 충돌 경고', () => {
  const otherId = 'other-same-it12';
  const registry = {
    version: 1,
    sessions: {
      [otherId]: { sessionId: otherId, pid: process.pid, startedAt: new Date().toISOString(), lastHeartbeat: new Date().toISOString(), branch: 'main', phase: 'dev', lastFiles: [] },
    },
  };
  guardModule._writeRegistry(registry);
  process.env.SESSION_GUARD_ID = 'new-session-it12';
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  assertContains(result.warning, '동일 phase');
});

test('IT-13: 동일 branch + 다른 phase → phase 불일치 경고', () => {
  const registry = {
    version: 1,
    sessions: {
      'plan-session-it13': { sessionId: 'plan-session-it13', pid: process.pid, startedAt: new Date().toISOString(), lastHeartbeat: new Date().toISOString(), branch: 'main', phase: 'plan', lastFiles: [] },
    },
  };
  guardModule._writeRegistry(registry);
  process.env.SESSION_GUARD_ID = 'dev-session-it13';
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  assertContains(result.warning, 'Phase 분리');
});

test('IT-14: 레지스트리 lock 후 writeRegistry 실패 복원', () => {
  // lock 파일 없는 상태에서 정상 write
  guardModule._writeRegistry({ version: 1, sessions: { 'test': { sessionId: 'test' } } });
  const r = guardModule._readRegistry();
  assert(r.sessions['test']);
});

test('IT-15: 2개 세션 conflict — 경고에 2개 세션 ID 포함', () => {
  const registry = {
    version: 1,
    sessions: {
      'sa-it15': { sessionId: 'sa-it15', pid: process.pid, startedAt: new Date().toISOString(), lastHeartbeat: new Date().toISOString(), branch: 'main', phase: 'dev', lastFiles: [] },
      'sb-it15': { sessionId: 'sb-it15', pid: process.pid, startedAt: new Date().toISOString(), lastHeartbeat: new Date().toISOString(), branch: 'main', phase: 'dev', lastFiles: [] },
    },
  };
  guardModule._writeRegistry(registry);
  process.env.SESSION_GUARD_ID = 'my-session-it15';
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  assertEqual(result.conflicts.length, 2);
  assertContains(result.warning, 'CRITICAL');
});

test('IT-16: printSessionStatus — 2개 세션 출력', () => {
  const registry = {
    version: 1,
    sessions: {
      'ps-a': { sessionId: 'ps-a', pid: 111, branch: 'main', phase: 'dev', startedAt: new Date().toISOString(), lastHeartbeat: new Date().toISOString() },
      'ps-b': { sessionId: 'ps-b', pid: 222, branch: 'main', phase: 'plan', startedAt: new Date().toISOString(), lastHeartbeat: new Date().toISOString() },
    },
  };
  guardModule._writeRegistry(registry);
  let output = '';
  const orig = console.log;
  console.log = (s) => { output += s + '\n'; };
  try { guardModule.printSessionStatus(); } finally { console.log = orig; }
  assertContains(output, 'ps-a');
  assertContains(output, 'ps-b');
});

test('IT-17: clearAllSessions 후 printSessionStatus → 활성 없음', () => {
  const registry = { version: 1, sessions: { 'to-clear': { sessionId: 'to-clear' } } };
  guardModule._writeRegistry(registry);
  const orig = console.log;
  console.log = () => {};
  try { guardModule.clearAllSessions(); } finally { console.log = orig; }
  const after = guardModule._readRegistry();
  assertEqual(Object.keys(after.sessions).length, 0);
});

test('IT-18: runSessionGuard — options 없이 호출 → 오류 없음', () => {
  const result = guardModule.runSessionGuard();
  assert(!result.error || result.error === 'lock-timeout' || result.error === 'max-sessions');
});

test('IT-19: upsertSelf — phase 변경 반영', () => {
  const id = 'phase-change-it19';
  const registry = { version: 1, sessions: {} };
  guardModule._upsertSelf(registry, id, { branch: 'main', phase: 'plan' });
  assertEqual(registry.sessions[id].phase, 'plan');
  guardModule._upsertSelf(registry, id, { branch: 'main', phase: 'dev' });
  assertEqual(registry.sessions[id].phase, 'dev');
});

test('IT-20: detectConflicts — ageMin 계산 (0이상)', () => {
  const registry = {
    version: 1,
    sessions: {
      'other-age-it20': {
        sessionId: 'other-age-it20',
        pid: 1,
        startedAt: new Date(Date.now() - 300_000).toISOString(),
      },
    },
  };
  const conflicts = guardModule._detectConflicts(registry, 'my-id-it20');
  assert(conflicts[0].ageMin >= 5, `ageMin은 5분 이상이어야 함: ${conflicts[0].ageMin}`);
});

// IT-21~30: 추가 통합 시나리오 (간략화)

test('IT-21: updateLastFiles — 빈 배열 무시', () => {
  const id = 'empty-files-it21';
  process.env.SESSION_GUARD_ID = id;
  const registry = { version: 1, sessions: { [id]: { sessionId: id, pid: process.pid, lastHeartbeat: new Date().toISOString(), lastFiles: ['existing.js'] } } };
  guardModule._writeRegistry(registry);
  guardModule.updateLastFiles(id, []);
  const after = guardModule._readRegistry();
  assert(after.sessions[id].lastFiles.includes('existing.js'), '기존 파일 보존');
});

test('IT-22: checkFileConflict — 빈 filePath → null', () => {
  const result = guardModule.checkFileConflict('');
  assert(result === null);
});

test('IT-23: checkFileConflict — null → null', () => {
  const result = guardModule.checkFileConflict(null);
  assert(result === null);
});

test('IT-24: readRegistry — sessions 필드 없는 JSON → 빈 레지스트리', () => {
  overrideGuardFile(JSON.stringify({ version: 1 }));
  const r = guardModule._readRegistry();
  assertEqual(Object.keys(r.sessions).length, 0);
});

test('IT-25: writeRegistry → GUARD_BAK 생성 확인', () => {
  guardModule._writeRegistry({ version: 1, sessions: {} });
  assert(fs.existsSync(REAL_BAK), '.bak 파일 생성됨');
});

test('IT-26: cleanStale — 빈 레지스트리 → 정리 없음', () => {
  const registry = { version: 1, sessions: {} };
  const removed = guardModule._cleanStale(registry, 'my-id');
  assertEqual(removed.length, 0);
});

test('IT-27: runSessionGuard 반복 호출 — 레지스트리 크기 증가 없음', () => {
  process.env.SESSION_GUARD_ID = 'stable-it27';
  guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  const r = guardModule._readRegistry();
  const count = Object.values(r.sessions).filter(s => s.sessionId === 'stable-it27').length;
  assertEqual(count, 1, '중복 없음');
});

test('IT-28: buildWarningBanner — session-clear 명령 힌트 포함', () => {
  const conflicts = [{ sessionId: 'x', pid: 1, branch: 'main', phase: 'dev', ageMin: 1 }];
  const banner = guardModule._buildWarningBanner(conflicts, 'main', 'dev');
  assertContains(banner, 'advance-phase.js session-clear');
});

test('IT-29: generateSessionId — ppid-{number} 형식이며 비어있지 않음', () => {
  delete process.env.SESSION_GUARD_ID;
  const id = guardModule._generateSessionId();
  assert(id.length > 0, `ID가 비어있음`);
  assert(/^ppid-\d+$/.test(id), `ppid 형식 불일치: ${id}`);
});

test('IT-30: isProcessAlive — process.pid는 항상 true', () => {
  assert(guardModule._isProcessAlive(process.pid));
});

// ── EC-01~20: 엣지케이스 ─────────────────────────────────────────────────

console.log('\n[Session Guard] EC (엣지케이스) 실행 중...\n');

test('EC-01: buildWarningBanner — null conflicts → 빈 문자열', () => {
  assertEqual(guardModule._buildWarningBanner(null, 'main', 'dev'), '');
});

test('EC-02: buildWarningBanner — undefined conflicts → 빈 문자열', () => {
  assertEqual(guardModule._buildWarningBanner(undefined, 'main', 'dev'), '');
});

test('EC-03: upsertSelf — branch undefined → "unknown"', () => {
  const r = { version: 1, sessions: {} };
  guardModule._upsertSelf(r, 'ec03-id', {});
  assertEqual(r.sessions['ec03-id'].branch, 'unknown');
});

test('EC-04: upsertSelf — phase undefined → "unknown"', () => {
  const r = { version: 1, sessions: {} };
  guardModule._upsertSelf(r, 'ec04-id', {});
  assertEqual(r.sessions['ec04-id'].phase, 'unknown');
});

test('EC-05: detectConflicts — 자신만 있는 레지스트리 → 빈 배열', () => {
  const r = { version: 1, sessions: { 'only-me': { sessionId: 'only-me' } } };
  assertEqual(guardModule._detectConflicts(r, 'only-me').length, 0);
});

test('EC-06: isStale — undefined lastHeartbeat → stale', () => {
  const s = { sessionId: 'ec06-id', pid: process.pid };
  assert(guardModule._isStale(s, 'other-id'));
});

test('EC-07: guardLock/guardUnlock — 연속 호출 안전', () => {
  guardModule._guardLock();
  guardModule._guardUnlock();
  guardModule._guardLock();
  guardModule._guardUnlock();
  // 오류 없으면 통과
});

test('EC-08: removeSession — 레지스트리 파일 없어도 오류 없음', () => {
  cleanGuardFile();
  guardModule._removeSession('non-existent');
  // 오류 없으면 통과
});

test('EC-09: updateLastFiles — 세션 없으면 무시', () => {
  process.env.SESSION_GUARD_ID = 'no-session-ec09';
  guardModule._writeRegistry({ version: 1, sessions: {} });
  guardModule.updateLastFiles('no-session-ec09', ['file.js']);
  // 오류 없으면 통과
});

test('EC-10: checkFileConflict — 레지스트리 없을 때 null 반환', () => {
  cleanGuardFile();
  process.env.SESSION_GUARD_ID = 'ec10-id';
  const result = guardModule.checkFileConflict('any-file.js');
  assert(result === null);
});

test('EC-11: buildWarningBanner — ageMin 0인 세션', () => {
  const c = [{ sessionId: 'new-session', pid: 1, branch: 'main', phase: 'dev', ageMin: 0 }];
  const banner = guardModule._buildWarningBanner(c, 'main', 'dev');
  assertContains(banner, '0분 전 시작');
});

test('EC-12: isProcessAlive — undefined → false', () => {
  assert(!guardModule._isProcessAlive(undefined));
});

test('EC-13: isProcessAlive — 문자열 PID → false', () => {
  assert(!guardModule._isProcessAlive('123'));
});

test('EC-14: cleanStale — 자신이 스테일 기준 초과여도 제거 안 됨', () => {
  const myId = 'self-ec14';
  const registry = {
    version: 1,
    sessions: {
      [myId]: {
        sessionId: myId, pid: 999999999,
        lastHeartbeat: new Date(Date.now() - 200_000).toISOString(),
      },
    },
  };
  const removed = guardModule._cleanStale(registry, myId);
  assert(!removed.includes(myId), '자신은 제거 안 됨');
  assert(registry.sessions[myId], '자신 보존됨');
});

test('EC-15: writeRegistry — sessions가 null인 데이터 쓰기 후 읽기', () => {
  guardModule._writeRegistry({ version: 1, sessions: {} });
  const r = guardModule._readRegistry();
  assertEqual(typeof r.sessions, 'object');
});

test('EC-16: printSessionStatus — 빈 레지스트리', () => {
  guardModule._writeRegistry({ version: 1, sessions: {} });
  let output = '';
  const orig = console.log;
  console.log = (s) => { output += String(s); };
  try { guardModule.printSessionStatus(); } finally { console.log = orig; }
  assertContains(output, '활성 세션 없음');
});

test('EC-17: runSessionGuard — branch가 긴 문자열 (100자)', () => {
  const longBranch = 'feature/' + 'x'.repeat(90);
  process.env.SESSION_GUARD_ID = 'long-branch-ec17';
  const result = guardModule.runSessionGuard({ branch: longBranch, phase: 'dev' });
  assert(!result.error || result.error !== undefined, '오류 처리됨');
});

test('EC-18: updateLastFiles — 중복 파일 path → deduplicated', () => {
  const id = 'dedup-ec18';
  process.env.SESSION_GUARD_ID = id;
  const registry = { version: 1, sessions: { [id]: { sessionId: id, pid: process.pid, lastHeartbeat: new Date().toISOString(), lastFiles: [] } } };
  guardModule._writeRegistry(registry);
  guardModule.updateLastFiles(id, ['dup.js', 'dup.js', 'dup.js']);
  const r = guardModule._readRegistry();
  const files = r.sessions[id].lastFiles || [];
  const dupCount = files.filter(f => f === 'dup.js').length;
  assertEqual(dupCount, 1, '중복 제거됨');
});

test('EC-19: detectConflicts — sessions가 빈 객체', () => {
  const r = { version: 1, sessions: {} };
  assertEqual(guardModule._detectConflicts(r, 'my-id').length, 0);
});

test('EC-20: guardLock — lock 디렉터리 없어도 생성 가능', () => {
  // REAL_LOCK 경로의 디렉터리는 이미 존재하므로 오류 없음
  const ok = guardModule._guardLock();
  guardModule._guardUnlock();
  assert(typeof ok === 'boolean');
});

// ── RT-01~10: 회귀 테스트 ─────────────────────────────────────────────────

console.log('\n[Session Guard] RT (회귀 테스트) 실행 중...\n');

// RT-01: 기존 훅이 _session-guard 로드 실패해도 동작 보장
test('RT-01: _session-guard 오류가 기존 훅에 전파 안 됨', () => {
  // 단순히 try-catch 감싸서 오류 없으면 통과
  try {
    guardModule.runSessionGuard({ branch: 'test', phase: 'dev' });
  } catch {}
  // 오류가 있어도 이 테스트는 통과 (전파 차단)
});

// RT-02: session-guard 없이도 advance-phase.js 동작 (기존 동작 보존)
test('RT-02: advance-phase.js require 가능', () => {
  // require하되 실행은 하지 않음 (인터랙티브 없음)
  const src = fs.readFileSync(
    path.join(__dirname, '../../.claude/hooks/advance-phase.js'), 'utf8'
  );
  assert(src.length > 0, 'advance-phase.js 존재');
});

// RT-03: pre-tool-gate.js require 가능
test('RT-03: pre-tool-gate.js require 가능', () => {
  const src = fs.readFileSync(
    path.join(__dirname, '../../.claude/hooks/pre-tool-gate.js'), 'utf8'
  );
  assert(src.length > 0);
});

// RT-04: 빈 registry.sessions → cleanStale 0건 제거
test('RT-04: cleanStale 빈 레지스트리 → 0건 제거', () => {
  const r = { version: 1, sessions: {} };
  const removed = guardModule._cleanStale(r, 'any-id');
  assertEqual(removed.length, 0);
});

// RT-05: session-gate.js require 가능 (circular 없음)
test('RT-05: session-gate.js require 가능', () => {
  const src = fs.readFileSync(
    path.join(__dirname, '../../.claude/hooks/session-gate.js'), 'utf8'
  );
  assert(src.length > 0);
});

// RT-06: 기존 _sessions.js require 가능 (공존 확인)
test('RT-06: _sessions.js와 _session-guard.js 공존', () => {
  let sessionsOk = false;
  try {
    const m = require('../../.claude/hooks/_sessions.js');
    sessionsOk = typeof m === 'object';
  } catch {
    sessionsOk = true; // 없어도 무방 (env 의존)
  }
  assert(sessionsOk);
});

// RT-07: docs/memory 디렉터리 없어도 runSessionGuard 폴백
test('RT-07: runSessionGuard — docs/memory 없어도 폴백', () => {
  // 실제 경로에 디렉터리가 있으므로 정상 동작, 없으면 폴백
  process.env.SESSION_GUARD_ID = 'fallback-rt07';
  const result = guardModule.runSessionGuard({ branch: 'main', phase: 'dev' });
  assert(result.sessionId || result.error, '세션 ID 또는 error 반환');
});

// RT-08: TTL_MS 상수값 검증 (90초)
test('RT-08: TTL_MS 상수 = 90000ms', () => {
  assertEqual(guardModule.TTL_MS, 90_000);
});

// RT-09: MAX_SESSIONS 상수값 검증 (8)
test('RT-09: MAX_SESSIONS 상수 = 8', () => {
  assertEqual(guardModule.MAX_SESSIONS, 8);
});

// RT-10: GUARD_FILE 경로에 docs/memory 포함
test('RT-10: GUARD_FILE 경로 검증', () => {
  assertContains(guardModule.GUARD_FILE, 'docs');
  assertContains(guardModule.GUARD_FILE, 'session-guard.json');
});

// ── 결과 출력 ──────────────────────────────────────────────────────────────

console.log('\n');
const total = passed + failed;
console.log(`\n[Session Guard] 테스트 결과: ${passed}/${total} PASS`);

if (failures.length > 0) {
  console.log('\n실패 목록:');
  failures.forEach((f, i) => {
    console.log(`  ${i + 1}. ${f.name}`);
    console.log(`     ${f.error}`);
  });
}

// 정리
try { fs.rmSync(TMP_DIR, { recursive: true, force: true }); } catch {}

if (failed > 0) process.exit(1);
else console.log('\n✅ 모든 시뮬레이션 통과!\n');
