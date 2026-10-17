// tests/unit/test_learning.js
// 학습 경로가 **값을 하는가** — charter harness-v16-one-graph-one-screen / N-21
//
// 왜 이 파일이 존재하는가:
//   하네스는 "배운 것을 다음 세션에 적용한다" 고 말해 왔다. 그 서사의 출력물이
//   docs/memory/feedback-rules.json 이고, 그것을 매 턴 배너에 실어 모델에게 보여줬다.
//   2026-09-16~17 실측: 규칙 5개가 **144회 발화하고 0회 차단했다**(FB-004 137 · FB-005 7).
//   전부 action:'remind' 였으니 막을 수도 없었고, 안내였으니 따를 이유도 없었다.
//   같은 기간 실제로 막은 것은 게이트였다 — destructive-command 6 · prd-self-approval 3 ·
//   approved-declaration-edit 1 · pipeline-state-write 9 · bash-state-write 4.
//
//   그래서 지웠다. 지우는 작업의 위험은 **과삭제를 나중에야 안다**는 것이다.
//   이 파일이 그 위험을 맡는다: 지워야 할 것과 **남아야 할 것(대조군)** 을 한 블록에서
//   함께 단언한다. 차단 게이트를 하나라도 건드리면 여기가 즉시 깨진다.
//
//   대조군이 왜 이 형태인가: FB-003 은 "PRD 자가 승인 주의" 라고 안내했지만, 자가 승인을
//   실제로 세 번 막은 것은 pre-tool-gate 의 prd-self-approval 이다. 안내가 막은 게 아니다.
//   그러니 안내가 사라져도 그 차단은 그대로여야 하고, 그것을 여기서 못박는다.
//
//   (학습 기록 자체의 단위 테스트는 tests/unit/learning.test.js 가 맡는다. 이 파일은
//    "학습의 산출물이 실제로 값을 했는가" 라는 다른 질문을 본다.)

'use strict';

const path = require('path');
const fs = require('fs');

const ROOT = path.resolve(__dirname, '..', '..');
const HOOKS = path.join(ROOT, '.claude', 'hooks');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).slice(0, 200) : ''}`); }
}

const read = rel => { try { return fs.readFileSync(path.join(ROOT, rel), 'utf8'); } catch { return ''; } };

console.log('\n═══ FB: 값을 못 한 안내 ═══');

console.log('\n[FB-01] should_drop_reminders_that_never_blocked');
{
  let rules;
  try {
    const raw = JSON.parse(read('docs/memory/feedback-rules.json'));
    rules = Array.isArray(raw) ? raw : (raw.rules || []);
  } catch { rules = []; }
  check('FB 규칙이 남아 있지 않다', rules.length === 0, rules.map(r => r.id).join(','));

  const gate = read('.claude/hooks/session-gate.js');
  const sync = read('.claude/hooks/post-write-sync.js');
  check('배너에 BEHAVIORAL REMINDERS 블록이 없다', !gate.includes('BEHAVIORAL REMINDERS'), '남아 있음');
  check('규칙을 읽는 경로도 지웠다 — 빈 목록을 조용히 도는 코드를 남기지 않는다',
    !gate.includes('feedback-rules.json') && !sync.includes('feedback-rules.json'),
    `gate=${gate.includes('feedback-rules.json')} sync=${sync.includes('feedback-rules.json')}`);
  check('feedback-violation 감사 이벤트를 더는 내지 않는다', !sync.includes('feedback-violation'), '남아 있음');
}

// ── 대조군 — 실제로 막는 것들은 그대로여야 한다 ──────────────────────
//   이 5건이 깨지면 "너무 많이 지웠다" 는 뜻이다. Red 시점에도 전부 통과했다.
console.log('\n[FB-02] should_keep_the_gates_that_actually_block');
{
  const preGate = read('.claude/hooks/pre-tool-gate.js');
  const charter = read('.claude/hooks/_charter.js');
  const gate = read('.claude/hooks/session-gate.js');
  check('대조군: prd-self-approval 차단이 살아 있다', preGate.includes('prd-self-approval'), null);
  check('대조군: destructive-command 차단이 살아 있다', preGate.includes('destructive-command'), null);
  check('대조군: 승인된 선언줄 보호가 살아 있다', preGate.includes('approved-declaration-edit'), null);
  check('대조군: 상태·원장 직접 수정 차단이 살아 있다', preGate.includes('pipeline-state-write'), null);
  check('대조군: 승인벽 awaitingApproval 이 살아 있다', charter.includes('function awaitingApproval'), null);
  check('대조군: 주입 위조 탐지 looksInjected 가 살아 있다', gate.includes('looksInjected'), null);
}

// ── 학습 경로의 형식은 남는다 ────────────────────────────────────────
//   지운 것은 규칙이지 파일 형식이 아니다. _learning.js 의 기록 경로는 그대로 쓴다.
console.log('\n[FB-03] should_keep_the_learning_file_format');
{
  const learning = fs.readFileSync(path.join(HOOKS, '_learning.js'), 'utf8');
  check('_learning 이 여전히 규칙 파일 형식을 안다', /feedback-rules\.json/.test(learning), null);
  let parsed = null;
  try { parsed = JSON.parse(read('docs/memory/feedback-rules.json')); } catch { parsed = null; }
  check('규칙 파일이 여전히 유효한 JSON 배열이다', Array.isArray(parsed), String(parsed));
}

// ── 쓰는 쪽이 거짓말하지 않는다 (2026-09-17 Loop A 지적) ─────────────
//   읽는 경로를 지웠는데 learn 스킬은 여전히 "feedback-rules.json 으로 자동 변환하여
//   다음 세션에 적용한다" 고 적고 있었다. 사용자가 그 흐름을 따르면 성공 메시지를 보지만
//   실제로는 아무 효과가 없다 — "값을 못 한 안내" 가 형태만 바꿔 되살아난 것이다.
//   문서가 다시 그 약속을 하면 여기가 깨진다.
console.log('\n[FB-04] should_not_promise_what_nothing_reads');
{
  const skill = read('.claude/skills/learn/SKILL.md');
  check('learn 스킬이 존재한다', skill.length > 0, null);
  check('learn 스킬이 "다음 세션에 적용" 을 약속하지 않는다',
    !/feedback-rules\.json[^\n]*적용|자동 변환하여 다음 세션에 적용/.test(skill),
    (skill.match(/[^\n]*다음 세션에 적용[^\n]*/g) || []).join(' | '));
  check('learn 스킬이 규칙 추가를 사용자에게 묻지 않는다',
    !/feedback-rules\.json에 추가할까요/.test(skill),
    (skill.match(/[^\n]*추가할까요[^\n]*/g) || []).join(' | '));
  check('learn 스킬이 변환 건수를 보고하지 않는다',
    !/- 변환: \{M\}개 feedback rule/.test(skill),
    (skill.match(/[^\n]*변환: \{M\}[^\n]*/g) || []).join(' | '));
  check('폐지 사실과 근거를 문서가 담는다',
    /144회 발화/.test(skill) && /폐지/.test(skill), null);
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
