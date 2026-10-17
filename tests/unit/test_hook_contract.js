// tests/unit/test_hook_contract.js
// 훅 입출력 계약 회귀 테스트 — v3에서 신설
//
// 왜 이 파일이 존재하는가:
//   pre-tool-gate.js / post-write-sync.js 는 하네스의 역사 전체에서 한 번도 발화하지
//   않았다. process.env.CLAUDE_TOOL_NAME / CLAUDE_TOOL_INPUT 을 읽었는데 Claude Code 는
//   그런 환경변수를 설정하지 않기 때문이다(실제 계약은 stdin JSON).
//   그럼에도 테스트는 계속 통과했다 — 기존 테스트가 전부 **env 방식으로** 훅을
//   구동했기 때문이다. 즉 테스트가 깨진 계약을 검증하고 있었다.
//
//   따라서 이 파일의 규칙: 훅은 반드시 **stdin JSON 으로만** 구동한다.
//   env 방식으로 훅을 구동하는 테스트를 새로 추가하지 말 것.

'use strict';

const { execFileSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const ROOT = path.resolve(__dirname, '..', '..');
const HOOKS = path.join(ROOT, '.claude', 'hooks');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + detail : ''}`); }
}

// 훅을 stdin JSON 으로 구동하고 stdout 을 돌려준다.
// [v3.6/N-01/IMPL-14] 페이로드가 이벤트명을 명시하지 않으면 그 훅이 기대하는 이름을 채운다 —
//   실제 Claude Code 페이로드에는 항상 hook_event_name 이 있고, 테스트가 그것 없이 구동하면
//   훅의 이벤트 검증 경로가 한 번도 실행되지 않는다. 이름을 **일부러** 비우는 케이스(HC-05 등)는
//   payload 에 hook_event_name: null 을 명시해 이 기본값을 끈다.
const EVENT_OF = {
  'session-gate.js': 'UserPromptSubmit', 'pre-tool-gate.js': 'PreToolUse',
  'post-write-sync.js': 'PostToolUse', 'stop-observation.js': 'Stop',
  'subagent-stop.js': 'SubagentStop', 'pre-compact.js': 'PreCompact',
};
function drive(hookFile, payload) {
  const p = Object.assign({}, payload);
  if (!('hook_event_name' in p) && EVENT_OF[hookFile]) p.hook_event_name = EVENT_OF[hookFile];
  if (p.hook_event_name === null) delete p.hook_event_name;
  try {
    return (execFileSync(process.execPath, [path.join(HOOKS, hookFile)], {
      input: JSON.stringify(p), encoding: 'utf8', cwd: ROOT, timeout: 15000,
    }) || '').trim();
  } catch (e) {
    return '__ERROR__ ' + (e.message || '').slice(0, 120);
  }
}

function isBlocked(out) {
  return out.startsWith('{') && out.includes('"decision":"block"');
}

// 위험 문자열은 조각으로 조립 — 이 테스트 파일이 스스로 게이트를 트리거하지 않도록
const RM = 'rm' + ' -rf';

console.log('\n═══ HC: 훅 입력 계약 (stdin JSON) ═══');

// ── HC-01: 소스에 죽은 env 계약이 남아있지 않을 것 ──────────────────
console.log('\n[HC-01] env 계약 잔존 검사');
for (const f of fs.readdirSync(HOOKS).filter(n => n.endsWith('.js'))) {
  const src = fs.readFileSync(path.join(HOOKS, f), 'utf8');
  // 주석에서의 언급은 허용, 실제 참조만 금지
  const live = src.split('\n').filter(l => {
    const t = l.trim();
    if (t.startsWith('//') || t.startsWith('*')) return false;
    return /process\.env\.CLAUDE_TOOL_(NAME|INPUT)/.test(l);
  });
  check(`${f} 에 CLAUDE_TOOL_* env 참조 없음`, live.length === 0, live[0]);
}

// ── HC-02: pre-tool-gate 는 stdin 으로 차단해야 한다 ────────────────
console.log('\n[HC-02] pre-tool-gate 차단 규칙 (stdin 구동)');
check('pipeline-state.json 직접 쓰기 차단',
  isBlocked(drive('pre-tool-gate.js',
    { tool_name: 'Write', tool_input: { file_path: 'docs/memory/pipeline-state.json', content: '{}' } })));
check('브랜치별 pipeline-state 쓰기 차단',
  isBlocked(drive('pre-tool-gate.js',
    { tool_name: 'Write', tool_input: { file_path: '.claude/.branch-x/pipeline-state.json', content: '{}' } })));
check('MCP 쓰기 도구 우회 차단',
  isBlocked(drive('pre-tool-gate.js',
    { tool_name: 'mcp__filesystem__write_file', tool_input: { path: 'docs/memory/pipeline-state.json', content: '{}' } })));
check('기존 PRD 자가 Approved 차단',
  isBlocked(drive('pre-tool-gate.js',
    { tool_name: 'Write', tool_input: { file_path: 'docs/prds/harness-v3-lean-prd.md', content: '- **상태**: Approved' } })));
check('루트 재귀 삭제 차단',
  isBlocked(drive('pre-tool-gate.js', { tool_name: 'Bash', tool_input: { command: RM + ' /' } })));
check('체인 뒤 홈 삭제 차단',
  isBlocked(drive('pre-tool-gate.js', { tool_name: 'Bash', tool_input: { command: 'echo hi && ' + RM + ' ~' } })));

// ── HC-03: 정상 작업을 구속하지 않을 것 (오탐 회귀) ────────────────
console.log('\n[HC-03] 정상 작업 통과 (오탐 방지)');
check('src/ 소스 작성 허용 (phase 게이트 폐지)',
  !isBlocked(drive('pre-tool-gate.js',
    { tool_name: 'Write', tool_input: { file_path: 'src/foo.py', content: 'x=1' } })));
check('신규 PRD Draft 작성 허용',
  !isBlocked(drive('pre-tool-gate.js',
    { tool_name: 'Write', tool_input: { file_path: 'docs/prds/brand-new-prd.md', content: '- **상태**: Draft' } })));
check('node_modules 정리 허용',
  !isBlocked(drive('pre-tool-gate.js', { tool_name: 'Bash', tool_input: { command: RM + ' node_modules' } })));
check('빌드 산출물 정리 허용',
  !isBlocked(drive('pre-tool-gate.js', { tool_name: 'Bash', tool_input: { command: 'cd build && ' + RM + ' *' } })));
check('위험 명령을 문자열로 언급만 하는 경우 허용',
  !isBlocked(drive('pre-tool-gate.js',
    { tool_name: 'Bash', tool_input: { command: 'grep -rn "' + RM + ' /" docs/' } })));
check('빈 페이로드는 통과',
  drive('pre-tool-gate.js', {}) === '');

// ── HC-04: session-gate 는 stdout 배너를 낸다 ──────────────────────
console.log('\n[HC-04] session-gate 상태 주입');
const sg = drive('session-gate.js',
  { hook_event_name: 'UserPromptSubmit', user_prompt: '테스트', session_id: 'hc-test' });
check('배너 출력됨', sg.length > 0, `len=${sg.length}`);
check('Phase 를 포함', /Phase:/.test(sg));
check('오류 없이 종료', !sg.startsWith('__ERROR__'), sg.slice(0, 80));

// ── HC-05: 훅은 TTY 없이 즉시 종료해야 한다 (행 방지) ──────────────
console.log('\n[HC-05] 훅 종료 건전성');
for (const h of ['pre-tool-gate.js', 'post-write-sync.js', 'stop-observation.js']) {
  const out = drive(h, { hook_event_name: 'X' });
  check(`${h} 빈 입력에도 행 없이 종료`, !out.startsWith('__ERROR__'), out.slice(0, 80));
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
