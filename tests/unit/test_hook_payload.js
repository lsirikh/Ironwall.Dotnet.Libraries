// tests/unit/test_hook_payload.js
// 훅 페이로드 실측 자산 — charter harness-v3 / N-01 (FR-01·FR-02·D1·D2)
//
// 왜 이 파일이 존재하는가:
//   하네스는 "Claude Code 가 무엇을 보내는가"를 추측으로 알고 있었다. 그 추측 하나가
//   틀렸던 것이 실측으로 드러났다 — session-gate 의 주석은 UserPromptSubmit 의 필드가
//   user_prompt 라고 적었지만 실제 필드는 prompt 이고, 코드는 두 번째 폴백으로만
//   우연히 동작해 왔다. 훅 계약을 코드가 아니라 **여기 적힌 표**로 고정한다.
//
//   실측 근거(2026-09-07, Claude Code 2.1.248): 이벤트명은 스키마가 아니라 발화 지점에
//   리터럴로 박혀 있고, 이벤트마다 아래 필드가 함께 온다. 이 표가 틀렸다고 의심되면
//   훅이 남기는 audit `hook-payload` 행(readHookInput 이 이벤트당 1회 기록)을 보라.

'use strict';

const { execFileSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const ROOT = path.resolve(__dirname, '..', '..');
const HOOKS = path.join(ROOT, '.claude', 'hooks');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).slice(0, 200) : ''}`); }
}

// ── 실측 표 — 이벤트 → (훅 파일, 반드시 오는 필드) ──────────────────
const CONTRACT = [
  { event: 'UserPromptSubmit', hook: 'session-gate.js', fields: ['session_id', 'prompt'] },
  { event: 'PreToolUse', hook: 'pre-tool-gate.js', fields: ['session_id', 'tool_name', 'tool_input'] },
  { event: 'PostToolUse', hook: 'post-write-sync.js', fields: ['session_id', 'tool_name', 'tool_input'] },
  { event: 'Stop', hook: 'stop-observation.js', fields: ['session_id', 'stop_hook_active'] },
  { event: 'SubagentStop', hook: 'subagent-stop.js', fields: ['session_id', 'stop_hook_active', 'agent_id', 'agent_type', 'agent_transcript_path'] },
  { event: 'PreCompact', hook: 'pre-compact.js', fields: ['session_id', 'trigger'] },
];

// 훅을 stdin JSON 으로 구동 — env 방식으로 구동하지 말 것(HC-01 의 교훈)
function drive(hookFile, payload, env) {
  try {
    const out = execFileSync(process.execPath, [path.join(HOOKS, hookFile)], {
      input: JSON.stringify(payload), encoding: 'utf8', cwd: ROOT, timeout: 15000,
      env: Object.assign({}, process.env, env || {}),
    });
    return { code: 0, out: (out || '').trim() };
  } catch (e) {
    return { code: e.status === undefined ? -1 : e.status, out: ((e.stdout || '') + (e.stderr || '')).trim() };
  }
}

console.log('\n═══ HP: 훅 페이로드 계약 ═══');

// ── HP-01: 공용 리더가 존재하고 계약을 지킨다 ────────────────────────
console.log('\n[HP-01] should_expose_read_hook_input_with_event_check');
{
  let C = null;
  try { C = require(path.join(HOOKS, '_common.js')); } catch (e) { /* 로드 실패는 아래에서 잡힌다 */ }
  check('_common 이 readHookInput 을 export 한다', !!(C && typeof C.readHookInput === 'function'),
    C ? 'exports: ' + Object.keys(C).join(',') : '_common 로드 실패');
}

// ── HP-02: 이벤트명이 다르면 아무 일도 하지 않는다 (무출력 exit 0) ──
//   훅을 남의 이벤트로 부르는 것은 정상 운용이 아니다. 조용히 아무것도 하지 않는 것이
//   유일하게 안전한 동작이다 — block 을 내면 v3 의 폴백 방향(오류 시 통과)과 어긋나고,
//   exit 1 을 내면 Loop A 결정 D4(훅은 게이트가 아니다)와 어긋난다.
console.log('\n[HP-02] should_noop_silently_when_event_name_mismatches');
for (const c of CONTRACT) {
  const r = drive(c.hook, { hook_event_name: 'SomeOtherEvent', session_id: 'hp02' });
  check(`${c.hook}: 다른 이벤트 → 무출력`, r.out === '', r.out);
  check(`${c.hook}: 다른 이벤트 → exit 0`, r.code === 0, 'exit ' + r.code);
}

// ── HP-03: 자기 이벤트는 그대로 처리한다 (계약 필드로 구동) ──────────
console.log('\n[HP-03] should_accept_its_own_event_payload');
for (const c of CONTRACT) {
  const payload = { hook_event_name: c.event, session_id: 'hp03' };
  // 계약 필드를 최소값으로 채운다 — 상태를 바꾸지 않는 무해한 값만
  if (c.event === 'UserPromptSubmit') payload.prompt = '상태 확인';
  if (c.event === 'PreToolUse' || c.event === 'PostToolUse') { payload.tool_name = 'Read'; payload.tool_input = {}; }
  if (c.event === 'Stop' || c.event === 'SubagentStop') payload.stop_hook_active = false;
  if (c.event === 'SubagentStop') { payload.agent_id = 'hp03a'; payload.agent_type = 'general-purpose'; payload.agent_transcript_path = ''; }
  if (c.event === 'PreCompact') payload.trigger = 'manual';
  const r = drive(c.hook, payload);
  check(`${c.hook}: 자기 이벤트 → exit 0`, r.code === 0, 'exit ' + r.code + ' ' + r.out.slice(0, 120));
}

// ── HP-04: 이벤트명이 없으면 기존 동작을 유지한다 ────────────────────
//   test_hook_contract.js 의 '빈 페이로드는 통과' 계약을 깨지 않기 위한 조건이다.
console.log('\n[HP-04] should_keep_legacy_behavior_when_event_name_absent');
{
  const r = drive('pre-tool-gate.js', { tool_name: 'Read', tool_input: {} });
  check('이벤트명 없는 페이로드 → exit 0', r.code === 0, 'exit ' + r.code);
  check('이벤트명 없는 페이로드 → 차단 아님', !r.out.includes('"decision":"block"'), r.out.slice(0, 120));
}

// ── HP-05: UserPromptSubmit 의 발화 필드는 prompt 다 ─────────────────
//   실측: 바이너리의 발화부가 `prompt` 로 보낸다. `user_prompt` 는 OTel 스팬 속성명이다.
//   session-gate 가 prompt 를 읽지 못하면 승인 키워드 감지 전체가 죽는다.
console.log('\n[HP-05] should_read_user_utterance_from_prompt_field');
{
  const src = fs.readFileSync(path.join(HOOKS, 'session-gate.js'), 'utf8');
  const codeLines = src.split('\n').filter(l => { const t = l.trim(); return !(t.startsWith('//') || t.startsWith('*')); }).join('\n');
  check('session-gate 코드가 prompt 필드를 읽는다', /\.prompt\b/.test(codeLines));
  check('session-gate 코드에 CLAUDE_USER_MESSAGE env 폴백이 없다',
    !/process\.env\.CLAUDE_USER_MESSAGE/.test(codeLines),
    (codeLines.match(/.*CLAUDE_USER_MESSAGE.*/) || [''])[0].trim());
}

// ── HP-06: 계약 표가 훅 소스와 어긋나지 않는다 ───────────────────────
//   훅이 자기 이벤트명을 알고 있어야 HP-02 가 성립한다.
console.log('\n[HP-06] should_declare_expected_event_in_each_hook');
for (const c of CONTRACT) {
  const src = fs.readFileSync(path.join(HOOKS, c.hook), 'utf8');
  check(`${c.hook} 이 '${c.event}' 을 선언한다`, src.includes(`'${c.event}'`) || src.includes(`"${c.event}"`));
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
