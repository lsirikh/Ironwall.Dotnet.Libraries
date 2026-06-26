// tests/unit/test_critical_regression.js
// Critical regression tests — FR-16, FR-17

'use strict';

const assert = require('assert');
const fs = require('fs');
const path = require('path');

let passed = 0, failed = 0;

function test(name, fn) {
  try {
    fn();
    passed++;
    console.log(`  ✅ ${name}`);
  } catch (e) {
    failed++;
    console.log(`  ❌ ${name}: ${e.message}`);
  }
}

console.log('\n=== Critical Regression Tests (FR-16, FR-17) ===\n');

// ─────────────────────────────────────────────────────────────────────────────
// TC-1 (S-37 회귀): advance-phase.js가 STATE_FILE에 직접 writeFileSync 하지 않음
// atomicStateUpdate(_state.js)를 통해서만 상태를 변경해야 한다.
// ─────────────────────────────────────────────────────────────────────────────
test('should_use_atomicStateUpdate_exclusively_when_advance_phase_writes_state', () => {
  const src = fs.readFileSync(
    path.join(__dirname, '../../.claude/hooks/advance-phase.js'), 'utf8'
  );

  // advance-phase.js 내에서 STATE_FILE 변수를 직접 writeFileSync에 넘기는 패턴 검출
  // atomicStateUpdate 내부의 writeFileSync는 _state.js에 있으므로 advance-phase.js에는 없어야 함
  const directWrites = (src.match(/writeFileSync\s*\(\s*STATE_FILE/g) || []).length;
  assert.strictEqual(directWrites, 0,
    `advance-phase.js에 STATE_FILE 직접 writeFileSync ${directWrites}개 발견 — atomicStateUpdate 사용 필요`);
});

// ─────────────────────────────────────────────────────────────────────────────
// TC-2 (S-36 회귀): post-write-sync.js에 session-context .bak 복원 로직이 있음
// withSessionCtxLock 함수가 실패 시 SESSION_CTX_BAK에서 복원해야 한다.
// ─────────────────────────────────────────────────────────────────────────────
test('should_restore_session_context_from_bak_when_file_corrupted', () => {
  const src = fs.readFileSync(
    path.join(__dirname, '../../.claude/hooks/post-write-sync.js'), 'utf8'
  );

  assert.ok(src.includes('withSessionCtxLock'),
    'withSessionCtxLock 함수가 post-write-sync.js에 없음');
  assert.ok(src.includes('SESSION_CTX_BAK'),
    'session-context.md .bak 백업 로직이 없음');
  assert.ok(src.includes('copyFileSync'),
    '.bak 복사 로직(copyFileSync)이 없음');
});

// ─────────────────────────────────────────────────────────────────────────────
// TC-3 (S-37 정적 검증): advance-phase.js에서 STATE_FILE 관련 더 넓은 직접 쓰기 패턴 없음
// path.join(...)으로 STATE_FILE에 준하는 경로를 직접 writeFileSync하는 패턴도 검출
// ─────────────────────────────────────────────────────────────────────────────
test('should_not_call_writeFileSync_directly_on_state_file', () => {
  const src = fs.readFileSync(
    path.join(__dirname, '../../.claude/hooks/advance-phase.js'), 'utf8'
  );

  // 패턴 1: writeFileSync(STATE_FILE
  const pattern1 = (src.match(/writeFileSync\s*\(\s*STATE_FILE/g) || []).length;

  // 패턴 2: writeFileSync(path.join(...), ...pipeline-state... 형태
  // pipeline-state.json을 직접 쓰는 줄 검출
  const lines = src.split('\n');
  const suspiciousLines = lines.filter(line =>
    /writeFileSync/.test(line) && /pipeline-state/.test(line)
  );

  assert.strictEqual(pattern1, 0,
    `advance-phase.js에서 STATE_FILE 직접 writeFileSync ${pattern1}개 — atomicStateUpdate 사용 필요`);
  assert.strictEqual(suspiciousLines.length, 0,
    `advance-phase.js에서 pipeline-state 직접 쓰기 의심 라인 발견:\n  ${suspiciousLines.join('\n  ')}`);
});

// ─────────────────────────────────────────────────────────────────────────────
// TC-4 (FR-24): _state.js atomicStateUpdate에서 graceful degradation 없음
// 잠금 실패 시 throw하고, locked 없이 진행하는 폴백 경로가 없어야 한다.
// ─────────────────────────────────────────────────────────────────────────────
test('should_have_graceful_degradation_removed_from_atomicStateUpdate', () => {
  const src = fs.readFileSync(
    path.join(__dirname, '../../.claude/hooks/_state.js'), 'utf8'
  );

  // atomicStateUpdate 함수 본문만 추출 (exports 이전 부분에서 검색)
  const fnMatch = src.match(/function atomicStateUpdate[\s\S]*?\n\}/);
  assert.ok(fnMatch, 'atomicStateUpdate 함수를 찾을 수 없음');

  const fnBody = fnMatch[0];

  // "잠금 실패 시 throw" 패턴이 있어야 함
  const hasThrowOnLockFail = /if\s*\(\s*!locked\s*\)\s*[\s\S]*?throw/.test(fnBody);
  assert.ok(hasThrowOnLockFail,
    'atomicStateUpdate에 !locked → throw 패턴이 없음 — 잠금 실패 시 명시적 오류를 던져야 함');

  // graceful degradation 패턴: !locked인데 그냥 계속 진행하는 코드 없어야 함
  // "if (!locked)" 뒤에 throw가 아닌 다른 처리(return, loadState 직접 호출 등)가 없는지 확인
  const degradationPattern = /if\s*\(\s*!locked\s*\)\s*\{[^}]*(?:loadState|saveState|return\s+(?!null))[^}]*\}/;
  const hasDegradation = degradationPattern.test(fnBody);
  assert.ok(!hasDegradation,
    'atomicStateUpdate에 잠금 없이 진행하는 graceful degradation 경로 발견 — 제거 필요');
});

// ─────────────────────────────────────────────────────────────────────────────

console.log(`\n결과: ${passed} passed, ${failed} failed\n`);
if (failed > 0) process.exit(1);
