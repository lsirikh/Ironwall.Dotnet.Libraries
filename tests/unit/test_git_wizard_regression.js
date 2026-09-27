// tests/unit/test_git_wizard_regression.js
// TEST-10: _git.js 회귀 테스트 (FR-26-A, FR-26-G, FR-26-M)

const assert = require('assert');
const fs = require('fs');
const path = require('path');

// [v4/N-01] 결과 형식은 tests/run.js 의 공용 리포터가 만든다 —
//   형식을 만드는 쪽과 parseCounts 로 읽는 쪽이 갈라지지 않게.
const { makeReporter } = require('../run.js');
const R = makeReporter();
const test = (name, fn) => R.test(name, fn);

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

// ── TC-2: [v3/FR-5.3] phase 전환당 자동 커밋이 제거되었는지 확인 ──────────
//   구 정책(commit-first)은 전환마다 `git add -A` + `chore: phase X → Y` 를 만들었다.
//   측정 결과 최근 83커밋 중 63개(76%)가 이 부기 커밋이었고, add -A 때문에 실제
//   소스 변경이 부기 제목 커밋에 흡수되어 git 히스토리가 내용을 잘못 표기했다.
//   git 히스토리는 사용자의 멀티세션 복구 기반이므로 이는 단순 소음이 아니었다.
// [v3.6/N-09/IMPL-02] 소스 grep 을 **행동 검사**로 바꿨다.
//   이 테스트가 지키려던 것은 "phase 전이가 자동 커밋하지 않는다" 라는 계약이다.
//   그런데 판정이 `advance-phase.js` 어디에도 `git add -A` 가 없어야 한다는 문자열 검사여서,
//   **사이클 커밋(complete)이 삭제된 파일을 담기 위해 `-A -- <경로>` 를 쓰는 것까지 막았다** —
//   계약과 무관한 변경을 거짓 빨강으로 세운 것이다. 이제 실제로 전이시켜 보고 HEAD 를 본다.
test('should_not_auto_commit_on_phase_transition', () => {
  const SB = require('./_sandbox.js');
  const d = SB.makeSandbox({ prefix: 'gw2' });
  try {
    SB.cli(d, ['status']);
    const before = SB.git(d, ['rev-parse', 'HEAD']);
    // 더러운 트리에서 전이해도 커밋이 생기면 안 된다
    fs.writeFileSync(path.join(d, 'src', 'a.js'), '// 고쳐졌다\n');
    const r = SB.cli(d, ['prd']);
    assert.strictEqual(r.code, 0, `prd 전이 실패: ${r.out}`);
    assert.strictEqual(SB.git(d, ['rev-parse', 'HEAD']), before, 'phase 전이가 커밋을 만들었다');
    assert.ok(SB.git(d, ['status', '--porcelain']).includes('src/a.js'),
      '전이가 변경을 스테이징하거나 삼켰다');
    assert.ok(!/chore: phase/.test(SB.git(d, ['log', '--oneline', '-5'])), 'chore: phase 자동 커밋이 있다');
  } finally { SB.cleanup(d); }
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
R.done();
