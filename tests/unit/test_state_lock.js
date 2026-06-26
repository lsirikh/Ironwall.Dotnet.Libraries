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

let passed = 0, failed = 0;

function test(name, fn) {
  try { fn(); passed++; console.log(`  ✅ ${name}`); }
  catch (e) { failed++; console.log(`  ❌ ${name}: ${e.message}`); }
}

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

// ── 결과 ──────────────────────────────────────────────────────────────

console.log(`\n══ 결과: ${passed} passed, ${failed} failed ══`);
if (failed > 0) process.exit(1);
