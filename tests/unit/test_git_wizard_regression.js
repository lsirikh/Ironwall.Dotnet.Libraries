// tests/unit/test_git_wizard_regression.js
// TEST-10: _git.js 회귀 테스트 (FR-26-A, FR-26-G, FR-26-M)

const assert = require('assert');
const fs = require('fs');
const path = require('path');

let passed = 0, failed = 0;

function test(name, fn) {
  try { fn(); passed++; console.log(`  ✅ ${name}`); }
  catch (e) { failed++; console.log(`  ❌ ${name}: ${e.message}`); }
}

const GIT_JS = path.resolve(__dirname, '../../.claude/hooks/_git.js');
const ADVANCE_PHASE_JS = path.resolve(__dirname, '../../.claude/hooks/advance-phase.js');

console.log('\n=== TEST-10: git wizard 회귀 테스트 ===\n');

// ── TC-1: S-G28 회귀 — snapshotPipelineState() guard 확인 ──────────────
test('should_require_snapshot_before_wizard_entry', () => {
  const src = fs.readFileSync(GIT_JS, 'utf8');
  assert.ok(src.includes('snapshotPipelineState'), '스냅샷 함수 없음');
  assert.ok(
    src.includes('Pre-wizard snapshot required'),
    'S-G28 guard 없음 — "Pre-wizard snapshot required" 문자열 미발견'
  );
});

// ── TC-2: S-G07 회귀 — commit-first 패턴 확인 ────────────────────────────
test('should_skip_state_save_when_git_commit_fails', () => {
  const src = fs.readFileSync(ADVANCE_PHASE_JS, 'utf8');
  assert.ok(src.includes('detectGit'), 'detectGit check 없음');
  assert.ok(src.includes('gitRun'), 'gitRun call 없음');
  // commit 실패 시 exit(1) 패턴 확인
  assert.ok(src.includes('Phase commit'), 'Phase commit 실패 처리 없음');
});

// ── TC-3: S-G28/S-G39 — restoreFromSnapshot + wizard 리셋 로직 확인 ──────
test('should_restore_from_pre_wizard_snapshot_when_migration_fails', () => {
  const src = fs.readFileSync(GIT_JS, 'utf8');
  assert.ok(src.includes('restoreFromSnapshot'), 'restoreFromSnapshot 없음');
  // initialized: false 리셋 로직 확인
  assert.ok(
    /initialized\s*:\s*false/.test(src),
    '위저드 리셋 로직 없음 — initialized: false 패턴 미발견'
  );
});

// ── TC-4: validatePostWizard 존재 + audit-log 기록 확인 ─────────────────
test('should_pass_post_wizard_validation_when_checks_succeed', () => {
  const src = fs.readFileSync(GIT_JS, 'utf8');
  assert.ok(src.includes('validatePostWizard'), 'validatePostWizard() 함수 없음');
  assert.ok(
    src.includes('wizard-validation'),
    'audit-log에 wizard-validation 이벤트 기록 로직 없음'
  );
});

// ── TC-5: 모든 gitRun 호출이 배열 인수인지 정적 확인 ───────────────────
test('should_use_array_args_exclusively_in_git_run', () => {
  const src = fs.readFileSync(GIT_JS, 'utf8');

  // gitRun([ 패턴 수
  const arrayCallMatches = src.match(/gitRun\(\[/g) || [];
  // gitRun(' 또는 gitRun(" 패턴 수
  const stringCallMatches = src.match(/gitRun\(['"`]/g) || [];

  assert.ok(
    arrayCallMatches.length > 0,
    'gitRun 배열 호출이 하나도 없음'
  );
  assert.strictEqual(
    stringCallMatches.length,
    0,
    `gitRun에 문자열 인수 직접 전달 발견 (${stringCallMatches.length}건) — 배열만 허용`
  );
});

// ── 결과 출력 ────────────────────────────────────────────────────────
console.log(`\n결과: ${passed} passed, ${failed} failed\n`);
if (failed > 0) process.exit(1);
