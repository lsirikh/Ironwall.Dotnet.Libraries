// tests/unit/test_critical_regression.js
// Critical regression tests — FR-16, FR-17

'use strict';

const assert = require('assert');
const fs = require('fs');
const path = require('path');

// [v4/N-01] 결과 형식은 tests/run.js 의 공용 리포터가 만든다 —
//   형식을 만드는 쪽과 parseCounts 로 읽는 쪽이 갈라지지 않게.
const { makeReporter } = require('../run.js');
const R = makeReporter();
const test = (name, fn) => R.test(name, fn);

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
// [v3.6/N-08/TEST-02] TC-3 를 **행동 검사**로 바꿨다.
//   예전 TC-3 는 TC-1 과 글자 그대로 같은 단언(`writeFileSync(STATE_FILE` 0건)을 한 번 더
//   세고, 추가 검사는 주석 줄을 걸러내지 않았다 — 실측으로 **동작 변경 없이 주석 한 줄만
//   덧붙여도 실패**했다(거짓 빨강). 반대로 `atomicStateUpdate` 안에서 상태를 깨뜨려도
//   이 파일은 전부 초록이었다. 이제 실제로 상태를 갱신해 보고 결과를 본다.
test('should_update_state_through_atomic_helper', () => {
  const SB = require('./_sandbox.js');
  const d = SB.makeSandbox({ prefix: 'tc3' });
  try {
    SB.cli(d, ['status']);
    const before = SB.readState(d);
    assert.ok(before && before.phase, '상태가 시딩되어야 한다');

    // phase 전이는 atomicStateUpdate 를 통과한다 — 그 결과가 디스크에 남아야 한다.
    const r = SB.cli(d, ['set-track', 'B']);
    assert.strictEqual(r.code, 0, `set-track 실패: ${r.out}`);
    const after = SB.readState(d);
    assert.strictEqual(after.track, 'B', `track 이 반영되지 않았다: ${JSON.stringify(after.track)}`);
    assert.ok(after.updated_at && after.updated_at !== before.updated_at, 'updated_at 이 갱신되어야 한다');

    // 잠금 파일이 남지 않아야 한다 — 남으면 다음 쓰기가 조용히 막힌다
    const lock = path.join(path.dirname(SB.stateFile(d)), '_state.lock');
    assert.ok(!fs.existsSync(lock), '_state.lock 이 남았다');
  } finally { SB.cleanup(d); }
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

R.done();
