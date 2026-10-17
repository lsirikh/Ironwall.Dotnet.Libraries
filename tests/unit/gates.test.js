// tests/unit/gates.test.js — _gates.js 단위 테스트 (FR-21)
// 실행: node tests/unit/gates.test.js

const assert = require('assert');
const gates = require('../../.claude/hooks/_gates');

// [v4/N-01] 결과 형식은 tests/run.js 의 공용 리포터가 만든다 —
//   형식을 만드는 쪽과 parseCounts 로 읽는 쪽이 갈라지지 않게.
const { makeReporter } = require('../run.js');
const R = makeReporter();
const test = (name, fn) => R.test(name, fn);

console.log('\n── canTransitionTo ──');
test('dev→test (C) allowed', () => { assert(gates.canTransitionTo('dev', 'test', 'C').allowed); });
test('test→dev (C, no reason) blocked', () => { assert(!gates.canTransitionTo('test', 'dev', 'C').allowed); });
test('test→dev (C, reason) allowed', () => { assert(gates.canTransitionTo('test', 'dev', 'C', { reason: 'fix' }).allowed); });
test('test→dev is backward', () => { assert(gates.canTransitionTo('test', 'dev', 'C', { reason: 'fix' }).isBackward); });
test('complete→analysis allowed', () => { assert(gates.canTransitionTo('complete', 'analysis', 'C', { reason: 'new' }).allowed); });
test('dev→prd blocked (not in whitelist)', () => { assert(!gates.canTransitionTo('dev', 'prd', 'C').allowed); });
test('analysis→complete (A) allowed', () => { assert(gates.canTransitionTo('analysis', 'complete', 'A').allowed); });
// [v3/FR-5.5] Track 경로는 "제안"이지 "우리"가 아니다. 경로 밖 전방 전환도 허용하고
//   note 로만 알린다. 과거 Track A 는 dev 가 없어서, 코드를 만져야 할 때 탈출에만
//   advance-phase 3회 + chore 커밋 2개가 들었다.
test('analysis→dev (A) 경로 밖이어도 허용 + note', () => {
  const r = gates.canTransitionTo('analysis', 'dev', 'A');
  assert(r.allowed && /경로 밖/.test(r.note || ''));
});
test('analysis→prd (B) 경로 밖이어도 허용', () => { assert(gates.canTransitionTo('analysis', 'prd', 'B').allowed); });
test('analysis→dev (B) allowed', () => { assert(gates.canTransitionTo('analysis', 'dev', 'B').allowed); });
test('invalid phase blocked', () => { assert(!gates.canTransitionTo('dev', 'invalid', 'C').allowed); });
test('invalid track blocked', () => { assert(!gates.canTransitionTo('dev', 'test', 'X').allowed); });
// [v3/FR-5.4] 인접성 하드 게이트 폐지 — 전방 점프는 허용하고 건너뛴 단계를 note 로 알린다.
//   순서 강제가 품질을 만들지 않는다. 산출물이 만든다.
test('전방 점프 허용 + 건너뛴 단계 note', () => {
  const r = gates.canTransitionTo('analysis', 'dev', 'C');
  assert(r.allowed && /건너뜀/.test(r.note || '') && /prd/.test(r.note));
});
test('전방 점프는 force 없이도 허용', () => { assert(gates.canTransitionTo('analysis', 'dev', 'C', { force: true }).allowed); });
test('same phase blocked', () => { assert(!gates.canTransitionTo('dev', 'dev', 'C').allowed); });

// [v3.6/N-02/IMPL-14] isBlockingBashCommand·matchesFeedbackTrigger·trackAllowsWrite 케이스를 제거했다.
//   세 함수는 v3.1 에서 phase 기반 쓰기 차단이 폐지된 뒤 어디서도 호출되지 않는 죽은 코드였고,
//   이 테스트가 그것들을 검증해 통과 수를 부풀렸다 — 지워도 아무 동작이 바뀌지 않는다.
//   명령 판정은 이제 _common.judgeCommand 가 하고 tests/unit/test_command_judge.js 가 지킨다.


R.done();
