// tests/unit/test_v250_simulation.js
// v2.5.0 Phase 시뮬레이션 — 8 Phase × 10 시나리오 = 80개
//
// 검증 항목:
//   1) Phase 전환 허용/차단 (canTransitionTo)
//   2) 파일 쓰기 게이트 (trackAllowsWrite)
//   3) Bash 명령 게이트 (isBlockingBashCommand)
//   4) Track 경로 무결성
//   5) 역방향 전환 규칙

'use strict';

const assert = require('assert');
const path = require('path');

const { canTransitionTo, trackAllowsWrite, isBlockingBashCommand, TRACK_ROUTES, ALLOWED_BACKWARDS } =
  require(path.join(__dirname, '../../.claude/hooks/_gates.js'));

let passed = 0;
let failed = 0;
const results = [];

function test(phase, id, name, fn) {
  try {
    fn();
    passed++;
    const entry = { phase, id, name, status: 'PASS' };
    results.push(entry);
    console.log(`  ✅ [${id}] ${name}`);
  } catch (e) {
    failed++;
    const entry = { phase, id, name, status: 'FAIL', reason: e.message };
    results.push(entry);
    console.log(`  ❌ [${id}] ${name}`);
    console.log(`       → ${e.message}`);
  }
}

// ═══════════════════════════════════════════════════════════════════
// PHASE 1: setup (S-01 ~ S-10)
// ═══════════════════════════════════════════════════════════════════
console.log('\n╔══════════════════════════════════════════════════╗');
console.log('║  PHASE: setup  (S-01 ~ S-10)                     ║');
console.log('╚══════════════════════════════════════════════════╝\n');

test('setup', 'S-01', 'should_allow_setup→analysis_on_track_C', () => {
  const r = canTransitionTo('setup', 'analysis', 'C');
  assert.strictEqual(r.allowed, true, `전환 차단: ${r.message}`);
  assert.strictEqual(r.isBackward, false);
});

test('setup', 'S-02', 'should_block_setup→prd_skip_on_track_C', () => {
  const r = canTransitionTo('setup', 'prd', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('skip') || r.message.includes('next'), `예상 메시지 없음: ${r.message}`);
});

test('setup', 'S-03', 'should_allow_setup→analysis_on_track_A', () => {
  const r = canTransitionTo('setup', 'analysis', 'A');
  assert.strictEqual(r.allowed, true);
});

test('setup', 'S-04', 'should_block_setup→dev_on_track_B_without_analysis', () => {
  const r = canTransitionTo('setup', 'dev', 'B');
  assert.strictEqual(r.allowed, false, '두 칸 건너뛰기가 허용됨');
});

test('setup', 'S-05', 'should_verify_track_A_route_contains_only_3_phases', () => {
  assert.deepStrictEqual(TRACK_ROUTES.A, ['setup', 'analysis', 'complete']);
});

test('setup', 'S-06', 'should_verify_track_B_route_has_no_prd_or_plan', () => {
  assert.ok(!TRACK_ROUTES.B.includes('prd'), 'Track B에 prd가 포함됨');
  assert.ok(!TRACK_ROUTES.B.includes('plan'), 'Track B에 plan이 포함됨');
});

test('setup', 'S-07', 'should_verify_track_C_route_has_all_8_phases', () => {
  const expected = ['setup', 'analysis', 'prd', 'plan', 'dev', 'test', 'report', 'complete'];
  assert.deepStrictEqual(TRACK_ROUTES.C, expected);
});

test('setup', 'S-08', 'should_allow_docs_write_in_setup_phase', () => {
  const r = trackAllowsWrite('C', 'setup', '/project/docs/memory/session-context.md',
    { isSourceFile: false });
  assert.strictEqual(r.allowed, true);
});

test('setup', 'S-09', 'should_block_source_write_in_setup_phase', () => {
  const r = trackAllowsWrite('C', 'setup', '/project/src/main.py',
    { isSourceFile: true });
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('HARD GATE'));
});

test('setup', 'S-10', 'should_block_transition_with_invalid_track', () => {
  const r = canTransitionTo('setup', 'analysis', 'X');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('invalid track'), `메시지 없음: ${r.message}`);
});

// ═══════════════════════════════════════════════════════════════════
// PHASE 2: analysis (A-01 ~ A-10)
// ═══════════════════════════════════════════════════════════════════
console.log('\n╔══════════════════════════════════════════════════╗');
console.log('║  PHASE: analysis  (A-01 ~ A-10)                  ║');
console.log('╚══════════════════════════════════════════════════╝\n');

test('analysis', 'A-01', 'should_allow_analysis→prd_on_track_C', () => {
  const r = canTransitionTo('analysis', 'prd', 'C');
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, false);
});

test('analysis', 'A-02', 'should_block_analysis→plan_skip_on_track_C', () => {
  const r = canTransitionTo('analysis', 'plan', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('skip') || r.message.includes('next'));
});

test('analysis', 'A-03', 'should_allow_analysis→complete_on_track_A', () => {
  const r = canTransitionTo('analysis', 'complete', 'A');
  assert.strictEqual(r.allowed, true);
});

test('analysis', 'A-04', 'should_allow_analysis→dev_on_track_B', () => {
  const r = canTransitionTo('analysis', 'dev', 'B');
  assert.strictEqual(r.allowed, true);
});

test('analysis', 'A-05', 'should_block_source_write_in_analysis_phase', () => {
  const r = trackAllowsWrite('C', 'analysis', '/project/src/app.js',
    { isSourceFile: true });
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('HARD GATE'));
});

test('analysis', 'A-06', 'should_allow_docs_write_in_analysis_phase', () => {
  const r = trackAllowsWrite('C', 'analysis', '/project/docs/analyses/my-analysis.md',
    { isSourceFile: false });
  assert.strictEqual(r.allowed, true);
});

test('analysis', 'A-07', 'should_block_analysis→analysis_same_phase', () => {
  const r = canTransitionTo('analysis', 'analysis', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('already'));
});

test('analysis', 'A-08', 'should_block_analysis→setup_backward_not_in_whitelist', () => {
  const r = canTransitionTo('analysis', 'setup', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.isBackward === true);
  assert.ok(!ALLOWED_BACKWARDS['analysis→setup'], 'analysis→setup이 ALLOWED_BACKWARDS에 있음');
});

test('analysis', 'A-09', 'should_confirm_prd_is_next_step_after_analysis_on_track_C', () => {
  const route = TRACK_ROUTES.C;
  const analysisIdx = route.indexOf('analysis');
  assert.strictEqual(route[analysisIdx + 1], 'prd');
});

test('analysis', 'A-10', 'should_block_analysis→prd_on_track_A_not_in_route', () => {
  const r = canTransitionTo('analysis', 'prd', 'A');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('not in Track') || r.message.includes('skip') || r.message.includes('next'));
});

// ═══════════════════════════════════════════════════════════════════
// PHASE 3: prd (P-01 ~ P-10)
// ═══════════════════════════════════════════════════════════════════
console.log('\n╔══════════════════════════════════════════════════╗');
console.log('║  PHASE: prd  (P-01 ~ P-10)                       ║');
console.log('╚══════════════════════════════════════════════════╝\n');

test('prd', 'P-01', 'should_allow_prd→plan_on_track_C', () => {
  const r = canTransitionTo('prd', 'plan', 'C');
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, false);
});

test('prd', 'P-02', 'should_block_prd→dev_skip_on_track_C', () => {
  const r = canTransitionTo('prd', 'dev', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('skip') || r.message.includes('next'));
});

test('prd', 'P-03', 'should_block_prd→analysis_backward_not_in_whitelist', () => {
  const r = canTransitionTo('prd', 'analysis', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.isBackward === true);
  assert.ok(!ALLOWED_BACKWARDS['prd→analysis']);
});

test('prd', 'P-04', 'should_allow_plan→prd_backward_with_reason', () => {
  const r = canTransitionTo('plan', 'prd', 'C', { reason: '요구사항 변경' });
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, true);
});

test('prd', 'P-05', 'should_block_plan→prd_backward_without_reason', () => {
  const r = canTransitionTo('plan', 'prd', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('--reason'));
});

test('prd', 'P-06', 'should_block_source_write_in_prd_phase', () => {
  const r = trackAllowsWrite('C', 'prd', '/project/src/service.py',
    { isSourceFile: true });
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('HARD GATE'));
});

test('prd', 'P-07', 'should_allow_docs_write_in_prd_phase', () => {
  const r = trackAllowsWrite('C', 'prd', '/project/docs/prds/feature-prd.md',
    { isSourceFile: false });
  assert.strictEqual(r.allowed, true);
});

test('prd', 'P-08', 'should_allow_dotclause_write_in_prd_phase', () => {
  const r = trackAllowsWrite('C', 'prd', '/project/.claude/skills/prd/SKILL.md',
    { isSourceFile: false });
  assert.strictEqual(r.allowed, true);
});

test('prd', 'P-09', 'should_block_prd→prd_same_phase', () => {
  const r = canTransitionTo('prd', 'prd', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('already'));
});

test('prd', 'P-10', 'should_confirm_prd_absent_in_track_A_and_B', () => {
  assert.ok(!TRACK_ROUTES.A.includes('prd'), 'Track A에 prd가 있음');
  assert.ok(!TRACK_ROUTES.B.includes('prd'), 'Track B에 prd가 있음');
});

// ═══════════════════════════════════════════════════════════════════
// PHASE 4: plan (PL-01 ~ PL-10)
// ═══════════════════════════════════════════════════════════════════
console.log('\n╔══════════════════════════════════════════════════╗');
console.log('║  PHASE: plan  (PL-01 ~ PL-10)                    ║');
console.log('╚══════════════════════════════════════════════════╝\n');

test('plan', 'PL-01', 'should_allow_plan→dev_on_track_C', () => {
  const r = canTransitionTo('plan', 'dev', 'C');
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, false);
});

test('plan', 'PL-02', 'should_block_plan→test_skip_on_track_C', () => {
  const r = canTransitionTo('plan', 'test', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('skip') || r.message.includes('next'));
});

test('plan', 'PL-03', 'should_allow_dev→plan_backward_with_reason', () => {
  const r = canTransitionTo('dev', 'plan', 'C', { reason: '개발 중 계획 수정' });
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, true);
});

test('plan', 'PL-04', 'should_block_dev→plan_backward_without_reason', () => {
  const r = canTransitionTo('dev', 'plan', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('--reason'));
});

test('plan', 'PL-05', 'should_allow_plan→prd_backward_in_whitelist', () => {
  assert.ok(ALLOWED_BACKWARDS['plan→prd'] !== undefined, 'plan→prd가 ALLOWED_BACKWARDS에 없음');
});

test('plan', 'PL-06', 'should_block_plan→analysis_backward_not_in_whitelist', () => {
  const r = canTransitionTo('plan', 'analysis', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.isBackward === true);
  assert.ok(!ALLOWED_BACKWARDS['plan→analysis']);
});

test('plan', 'PL-07', 'should_block_source_write_in_plan_phase', () => {
  const r = trackAllowsWrite('C', 'plan', '/project/cmd/main.go',
    { isSourceFile: true });
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('HARD GATE'));
});

test('plan', 'PL-08', 'should_allow_tests_write_in_plan_phase', () => {
  const r = trackAllowsWrite('C', 'plan', '/project/tests/unit/my.test.js',
    { isSourceFile: false });
  assert.strictEqual(r.allowed, true);
});

test('plan', 'PL-09', 'should_block_plan→plan_same_phase', () => {
  const r = canTransitionTo('plan', 'plan', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('already'));
});

test('plan', 'PL-10', 'should_confirm_plan_absent_in_track_A_and_B', () => {
  assert.ok(!TRACK_ROUTES.A.includes('plan'), 'Track A에 plan이 있음');
  assert.ok(!TRACK_ROUTES.B.includes('plan'), 'Track B에 plan이 있음');
});

// ═══════════════════════════════════════════════════════════════════
// PHASE 5: dev (D-01 ~ D-10)
// ═══════════════════════════════════════════════════════════════════
console.log('\n╔══════════════════════════════════════════════════╗');
console.log('║  PHASE: dev  (D-01 ~ D-10)                       ║');
console.log('╚══════════════════════════════════════════════════╝\n');

test('dev', 'D-01', 'should_allow_dev→test_on_track_C', () => {
  const r = canTransitionTo('dev', 'test', 'C');
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, false);
});

test('dev', 'D-02', 'should_block_dev→report_skip_on_track_C', () => {
  const r = canTransitionTo('dev', 'report', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('skip') || r.message.includes('next'));
});

test('dev', 'D-03', 'should_allow_test→dev_backward_with_reason', () => {
  const r = canTransitionTo('test', 'dev', 'C', { reason: '테스트 실패로 코드 수정' });
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, true);
});

test('dev', 'D-04', 'should_block_test→dev_backward_without_reason', () => {
  const r = canTransitionTo('test', 'dev', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('--reason'));
});

test('dev', 'D-05', 'should_allow_report→dev_backward_in_whitelist', () => {
  const r = canTransitionTo('report', 'dev', 'C', { reason: '리포트 중 추가 수정' });
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, true);
});

test('dev', 'D-06', 'should_block_source_write_in_dev_track_A', () => {
  const r = trackAllowsWrite('A', 'dev', '/project/src/main.py',
    { isSourceFile: true });
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('Track A'));
});

test('dev', 'D-07', 'should_block_source_write_in_dev_track_B_without_journal', () => {
  const r = trackAllowsWrite('B', 'dev', '/project/src/utils.js',
    { isSourceFile: true, hasDevJournal: false });
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('dev-journal'));
});

test('dev', 'D-08', 'should_allow_source_write_in_dev_track_B_with_journal', () => {
  const r = trackAllowsWrite('B', 'dev', '/project/src/utils.js',
    { isSourceFile: true, hasDevJournal: true });
  assert.strictEqual(r.allowed, true);
});

test('dev', 'D-09', 'should_block_source_write_in_dev_track_C_without_prd', () => {
  const r = trackAllowsWrite('C', 'dev', '/project/src/api.py',
    { isSourceFile: true, hasPrdApproved: false, hasPlan: true });
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('PRD'));
});

test('dev', 'D-10', 'should_allow_source_write_in_dev_track_C_with_prd_and_plan', () => {
  const r = trackAllowsWrite('C', 'dev', '/project/src/api.py',
    { isSourceFile: true, hasPrdApproved: true, hasPlan: true });
  assert.strictEqual(r.allowed, true);
});

// ═══════════════════════════════════════════════════════════════════
// PHASE 6: test (T-01 ~ T-10)
// ═══════════════════════════════════════════════════════════════════
console.log('\n╔══════════════════════════════════════════════════╗');
console.log('║  PHASE: test  (T-01 ~ T-10)                      ║');
console.log('╚══════════════════════════════════════════════════╝\n');

test('test', 'T-01', 'should_allow_test→report_on_track_C', () => {
  const r = canTransitionTo('test', 'report', 'C');
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, false);
});

test('test', 'T-02', 'should_block_test→complete_skip_on_track_C', () => {
  const r = canTransitionTo('test', 'complete', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('skip') || r.message.includes('next'));
});

test('test', 'T-03', 'should_allow_test→complete_on_track_B', () => {
  const routeB = TRACK_ROUTES.B;
  const testIdx = routeB.indexOf('test');
  assert.strictEqual(routeB[testIdx + 1], 'complete', 'Track B에서 test 다음이 complete가 아님');
  const r = canTransitionTo('test', 'complete', 'B');
  assert.strictEqual(r.allowed, true);
});

test('test', 'T-04', 'should_allow_test→dev_backward_with_reason', () => {
  const r = canTransitionTo('test', 'dev', 'C', { reason: '실패 수정' });
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, true);
});

test('test', 'T-05', 'should_allow_test→plan_backward_with_reason', () => {
  const r = canTransitionTo('test', 'plan', 'C', { reason: '태스크 추가' });
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, true);
});

test('test', 'T-06', 'should_block_test→analysis_backward_not_in_whitelist', () => {
  const r = canTransitionTo('test', 'analysis', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.isBackward === true);
  assert.ok(!ALLOWED_BACKWARDS['test→analysis']);
});

test('test', 'T-07', 'should_allow_source_write_in_test_phase_track_C', () => {
  // test phase는 blockedPhases에 없으므로 소스 쓰기 허용 (Track C + PRD+Plan)
  const r = trackAllowsWrite('C', 'test', '/project/src/helper.py',
    { isSourceFile: true, hasPrdApproved: true, hasPlan: true });
  assert.strictEqual(r.allowed, true);
});

test('test', 'T-08', 'should_allow_docs_write_in_test_phase', () => {
  const r = trackAllowsWrite('C', 'test', '/project/docs/tests/result.md',
    { isSourceFile: false });
  assert.strictEqual(r.allowed, true);
});

test('test', 'T-09', 'should_block_test→test_same_phase', () => {
  const r = canTransitionTo('test', 'test', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('already'));
});

test('test', 'T-10', 'should_confirm_test_absent_in_track_A', () => {
  assert.ok(!TRACK_ROUTES.A.includes('test'), 'Track A에 test가 포함됨');
});

// ═══════════════════════════════════════════════════════════════════
// PHASE 7: report (R-01 ~ R-10)
// ═══════════════════════════════════════════════════════════════════
console.log('\n╔══════════════════════════════════════════════════╗');
console.log('║  PHASE: report  (R-01 ~ R-10)                    ║');
console.log('╚══════════════════════════════════════════════════╝\n');

test('report', 'R-01', 'should_allow_report→complete_on_track_C', () => {
  const r = canTransitionTo('report', 'complete', 'C');
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, false);
});

test('report', 'R-02', 'should_block_report→analysis_skip_backward_not_in_whitelist', () => {
  const r = canTransitionTo('report', 'analysis', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(!ALLOWED_BACKWARDS['report→analysis']);
});

test('report', 'R-03', 'should_allow_report→dev_backward_with_reason', () => {
  const r = canTransitionTo('report', 'dev', 'C', { reason: '추가 수정' });
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, true);
});

test('report', 'R-04', 'should_allow_report→test_backward_with_reason', () => {
  const r = canTransitionTo('report', 'test', 'C', { reason: '추가 테스트' });
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, true);
});

test('report', 'R-05', 'should_block_report→plan_backward_not_in_whitelist', () => {
  const r = canTransitionTo('report', 'plan', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.isBackward === true);
  assert.ok(!ALLOWED_BACKWARDS['report→plan']);
});

test('report', 'R-06', 'should_allow_source_write_in_report_phase_track_C', () => {
  // report phase는 blockedPhases에 없으므로 허용 (Track C + PRD+Plan)
  const r = trackAllowsWrite('C', 'report', '/project/src/fix.py',
    { isSourceFile: true, hasPrdApproved: true, hasPlan: true });
  assert.strictEqual(r.allowed, true);
});

test('report', 'R-07', 'should_allow_docs_write_in_report_phase', () => {
  const r = trackAllowsWrite('C', 'report', '/project/docs/reports/final-report.md',
    { isSourceFile: false });
  assert.strictEqual(r.allowed, true);
});

test('report', 'R-08', 'should_block_report→report_same_phase', () => {
  const r = canTransitionTo('report', 'report', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('already'));
});

test('report', 'R-09', 'should_confirm_report_absent_in_track_A_and_B', () => {
  assert.ok(!TRACK_ROUTES.A.includes('report'), 'Track A에 report가 있음');
  assert.ok(!TRACK_ROUTES.B.includes('report'), 'Track B에 report가 있음');
});

test('report', 'R-10', 'should_block_report→dev_backward_without_reason', () => {
  const r = canTransitionTo('report', 'dev', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('--reason'));
});

// ═══════════════════════════════════════════════════════════════════
// PHASE 8: complete (C-01 ~ C-10)
// ═══════════════════════════════════════════════════════════════════
console.log('\n╔══════════════════════════════════════════════════╗');
console.log('║  PHASE: complete  (C-01 ~ C-10)                  ║');
console.log('╚══════════════════════════════════════════════════╝\n');

test('complete', 'C-01', 'should_allow_complete→analysis_backward_with_reason', () => {
  const r = canTransitionTo('complete', 'analysis', 'C', { reason: '새 사이클 시작' });
  assert.strictEqual(r.allowed, true);
  assert.strictEqual(r.isBackward, true);
});

test('complete', 'C-02', 'should_allow_complete→analysis_backward_with_force', () => {
  const r = canTransitionTo('complete', 'analysis', 'C', { force: true });
  assert.strictEqual(r.allowed, true);
});

test('complete', 'C-03', 'should_block_complete→analysis_backward_without_reason_or_force', () => {
  const r = canTransitionTo('complete', 'analysis', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('--reason'));
});

test('complete', 'C-04', 'should_block_source_write_in_complete_phase', () => {
  // FB-005: complete 단계 코드 수정 차단
  const r = trackAllowsWrite('C', 'complete', '/project/src/main.py',
    { isSourceFile: true });
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('HARD GATE'));
});

test('complete', 'C-05', 'should_allow_docs_write_in_complete_phase', () => {
  const r = trackAllowsWrite('C', 'complete', '/project/docs/memory/session-context.md',
    { isSourceFile: false });
  assert.strictEqual(r.allowed, true);
});

test('complete', 'C-06', 'should_block_complete→complete_same_phase', () => {
  const r = canTransitionTo('complete', 'complete', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.message.includes('already'));
});

test('complete', 'C-07', 'should_block_complete→dev_backward_not_in_whitelist', () => {
  const r = canTransitionTo('complete', 'dev', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(r.isBackward === true);
  assert.ok(!ALLOWED_BACKWARDS['complete→dev']);
});

test('complete', 'C-08', 'should_block_complete→test_backward_not_in_whitelist', () => {
  const r = canTransitionTo('complete', 'test', 'C');
  assert.strictEqual(r.allowed, false);
  assert.ok(!ALLOWED_BACKWARDS['complete→test']);
});

test('complete', 'C-09', 'should_block_bash_dangerous_rm_in_complete_phase', () => {
  const r = isBlockingBashCommand('rm -rf /', 'complete', ['src/']);
  assert.strictEqual(r.blocked, true);
  assert.ok(r.reason.includes('SAFETY'));
});

test('complete', 'C-10', 'should_confirm_complete→analysis_in_allowed_backwards', () => {
  assert.ok(ALLOWED_BACKWARDS['complete→analysis'] !== undefined,
    'complete→analysis가 ALLOWED_BACKWARDS에 없음');
});

// ═══════════════════════════════════════════════════════════════════
// 결과 요약
// ═══════════════════════════════════════════════════════════════════
console.log('\n' + '═'.repeat(55));
console.log(`  총 시나리오: ${passed + failed}개 (8 Phase × 10)`);
console.log(`  ✅ PASS: ${passed}개`);
console.log(`  ❌ FAIL: ${failed}개`);
console.log('═'.repeat(55));

// Phase별 집계
const phaseOrder = ['setup', 'analysis', 'prd', 'plan', 'dev', 'test', 'report', 'complete'];
console.log('\n[Phase별 결과]');
for (const phase of phaseOrder) {
  const phaseResults = results.filter(r => r.phase === phase);
  const phasePassed = phaseResults.filter(r => r.status === 'PASS').length;
  const phaseFailed = phaseResults.filter(r => r.status === 'FAIL').length;
  const bar = phasePassed === 10 ? '✅' : phaseFailed > 5 ? '❌' : '⚠️ ';
  console.log(`  ${bar} ${phase.padEnd(10)} ${phasePassed}/10`);
}

if (failed > 0) {
  console.log('\n[실패 목록]');
  results.filter(r => r.status === 'FAIL').forEach(r => {
    console.log(`  ❌ [${r.id}] ${r.name}`);
    console.log(`       → ${r.reason}`);
  });
  process.exit(1);
} else {
  console.log('\n🎉 전체 통과!');
  process.exit(0);
}
