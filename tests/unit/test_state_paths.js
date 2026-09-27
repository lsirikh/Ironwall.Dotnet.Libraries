// tests/unit/test_state_paths.js
// 상태 경로 지식은 한 벌인가 — charter harness-v16 / N-27
//
// 왜 이 파일이 존재하는가:
//   `_state.js` 는 경로를 한 곳에서 정하는데 **값(`STATE_FILE`)을 그대로 export 한다.**
//   그래서 소비자들이 그 값을 받아 각자 `path.dirname` 하고, 정규식으로 역파싱하고,
//   같은 판정을 리터럴로 다시 적었다 — 실측 결과 `.claude/.branch-` 접두 판정만
//   **9벌**이 서로 독립이었다(`advance-phase.js` 안에만 5벌).
//
//   같은 판정이 여러 벌이면 하나를 고칠 때 나머지는 따라오지 않는다. N-24 에서
//   `HARNESS_OWNED` 가 네 번째 소비자를 놓쳐 교착이 난 것이 정확히 그 형태였다.
//
//   그리고 **실패의 방향이 나쁘다.** 상태 경로가 바뀌면 세 자리가 예외도 감사도 없이
//   게이트를 통과시키는 쪽으로 무너진다:
//     · stop-observation  경로에서 브랜치를 역파싱 → 모든 세션이 'nobranch' 로 뭉개진다
//                         (그 파일 주석이 **정확히 그 사고**를 기록해 놨다)
//     · _git WATCH_PATHS  상태 위조 탐지가 무음화 → cycle.stateOutsideCli 가 영영 안 뜬다
//     · pre-tool-gate     직접수정 차단 정규식이 아무것도 매치하지 않는다
//
//   이 노드는 **저장 위치를 바꾸지 않는다.** 지식만 모은다 — 옮기는 것은 N-05 다.
//   그래서 전수 스위트가 하나도 깨지지 않는 것이 이 사이클의 가장 강한 안전망이다.

'use strict';

const fs = require('fs');
const path = require('path');

const REPO = path.resolve(__dirname, '..', '..');
const HOOKS = path.join(REPO, '.claude', 'hooks');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 240) : ''}`); }
}
const hook = name => { try { return fs.readFileSync(path.join(HOOKS, name), 'utf8'); } catch { return ''; } };

// 주석은 "왜" 를 적는 자리다. 리터럴 금지는 **코드**에만 건다 —
//   낱말을 통째로 금지하면 그 이유를 적은 문장이 스스로를 위반한다(D-2026-09-07-5fc264).
function codeOnly(src) {
  return String(src)
    .split('\n')
    .filter(l => !/^\s*(\/\/|\*|\/\*)/.test(l))
    .join('\n');
}

console.log('\n═══ SP: 상태 경로 판정이 한 벌인가 ═══');

// ── SP-01: 정본이 값이 아니라 판정을 내보낸다 ────────────────────────
console.log('\n[SP-01] should_export_predicates_not_just_the_path');
const S = (() => { try { return require(path.join(HOOKS, '_state.js')); } catch (e) { return { __err: e.message }; } })();
{
  check('_state.js 를 불러올 수 있다', !S.__err, S.__err);
  for (const fn of ['isStateFile', 'isBranchStateDir', 'stateSiblings', 'currentBranchTag']) {
    check(`정본이 ${fn}() 를 내보낸다`, typeof S[fn] === 'function', typeof S[fn]);
  }
}

// ── SP-02: 판정이 실제로 경로를 본다 (공허하지 않다) ─────────────────
//   함수만 있고 아무 입력에나 같은 답을 주면 세 벽을 붙여도 소용없다.
console.log('\n[SP-02] should_actually_judge_the_path');
{
  const ok = typeof S.isStateFile === 'function' && typeof S.isBranchStateDir === 'function';
  const YES = '.claude/.branch-feature-x-abc123/pipeline-state.json';
  const NO_ = 'docs/prds/some-prd.md';
  const MOVED = '.claude/state/s-0a1b2c/pipeline-state.json';   // N-05 이후의 가상 형태
  check('현재 형태의 상태 파일을 참으로 본다', ok && S.isStateFile(YES) === true, ok ? String(S.isStateFile(YES)) : '함수 없음');
  check('무관한 파일을 거짓으로 본다', ok && S.isStateFile(NO_) === false, ok ? String(S.isStateFile(NO_)) : '함수 없음');
  check('브랜치 상태 디렉터리를 알아본다', ok && S.isBranchStateDir('.claude/.branch-feature-x-abc123') === true,
    ok ? String(S.isBranchStateDir('.claude/.branch-feature-x-abc123')) : '함수 없음');
  // 반증: 아직 옮기지 않았으므로 옮겨진 형태는 **거짓**이어야 한다.
  //   참이 나오면 판정이 파일명만 보고 있다는 뜻이고, 그러면 위치가 바뀌어도 아무것도 안 바뀐다.
  check('반증: 옮겨진 형태는 아직 거짓이다 — 판정이 파일명만 보지 않는다',
    ok && S.isStateFile(MOVED) === false, ok ? String(S.isStateFile(MOVED)) : '함수 없음');
  // [Loop A 보안 렌즈] **판정을 좁히면 벽에 구멍이 난다.** 첫 판은 대소문자를 구분하고
  //   중복 슬래시를 정규화하지 않아, 예전 정규식(`/pipeline-state\.json$/i`)이 막던 것을
  //   통과시켰다. 윈도우에서는 아래 넷이 전부 **같은 파일**이다.
  for (const v of [
    'docs/memory/Pipeline-State.json',
    'docs//memory//pipeline-state.json',
    'docs/memory/./pipeline-state.json',
    '.claude/.BRANCH-feature-x-abc123/pipeline-state.json',
  ]) {
    check(`정규화: ${v} 도 상태 파일로 본다`, ok && S.isStateFile(v) === true, ok ? String(S.isStateFile(v)) : '함수 없음');
  }
  // 대조군: 정규화가 과하면 무관한 파일까지 잡는다. 이름이 다르면 여전히 거짓이어야 한다.
  check('대조군: 이름이 다르면 정규화해도 거짓이다',
    ok && S.isStateFile('docs/memory/pipeline-state.json.bak') === false,
    ok ? String(S.isStateFile('docs/memory/pipeline-state.json.bak')) : '함수 없음');

  // 감시목록은 상태와 그래프 캐시를 모두 담아야 한다 — 하나라도 빠지면 위조 탐지에 구멍이 난다.
  const sib = (typeof S.stateSiblings === 'function') ? S.stateSiblings() : [];
  check('감시목록이 상태·영향그래프·태스크그래프를 담는다',
    Array.isArray(sib) && ['pipeline-state.json', 'impact-graph.json', 'task-graph.json']
      .every(f => sib.some(p => String(p).endsWith(f))), JSON.stringify(sib).slice(0, 200));
  const tag = (typeof S.currentBranchTag === 'function') ? S.currentBranchTag() : null;
  check('브랜치 태그가 비어 있지 않다', !!tag && tag !== 'nobranch', String(tag));
}

// ── SP-03: 세 벽이 정본을 참조한다 ───────────────────────────────────
//   판정은 **한 벌**이다 — 아래 반증 2건이 이 술어를 그대로 쓴다.
console.log('\n[SP-03] should_make_the_three_walls_reference_the_canonical');
const WALLS = {
  // 금지할 것은 리터럴의 **존재**가 아니라 리터럴이 **1차 판정인 것**이다.
  //   정본이 죽어도 닫혀 있으려면 이름 기반 폴백은 오히려 있어야 한다(SP-05b) —
  //   두 단언이 모순되지 않도록, 여기서는 "리터럴이 곧바로 `if` 조건인 형태" 만 막는다.
  'pre-tool-gate.js': { uses: /isStateFile\s*\(/, forbids: /if\s*\(\s*\/[^/\n]*pipeline-state/ },
  '_git.js': { uses: /stateSiblings\s*\(/, forbids: /'pipeline-state\.json'/ },
  'stop-observation.js': { uses: /currentBranchTag\s*\(/, forbids: /match\(\s*\/\\\.branch-/ },
};
function wallJudge(name, src) {
  const w = WALLS[name];
  const code = codeOnly(src);
  return { uses: w.uses.test(code), clean: !w.forbids.test(code) };
}
for (const name of Object.keys(WALLS)) {
  const v = wallJudge(name, hook(name));
  check(`${name} 이 정본 판정을 부른다`, v.uses, '호출 없음');
  check(`${name} 에 자기 경로 지식이 없다`, v.clean, '리터럴/역파싱 잔존');
}

// ── SP-04: 반증 — 리터럴·역파싱을 되살린 사본에서는 판정이 거짓이다 ──
//   치환이 헛돌면(원문과 같으면) 반증도 공허하다 — 그것도 본다.
console.log('\n[SP-04] should_go_red_when_a_wall_grows_its_own_path_knowledge');
{
  const g = hook('pre-tool-gate.js');
  // 리터럴이 **곧바로 `if` 조건**이 되는 옛 형태로 되살린다 — 그것이 금지 대상이다.
  const gBad = g.replace('if (_isState', "if (/pipeline-state\\.json$/i.test(filePath)");
  check('반증: 직접수정 차단에 리터럴을 되살리면 거짓이 된다',
    gBad !== g && !wallJudge('pre-tool-gate.js', gBad).clean,
    gBad === g ? '변형이 일어나지 않았다' : '되살려도 참');

  const s = hook('stop-observation.js');
  const sBad = s.replace(/currentBranchTag\s*\(\s*\)/,
    "String(STATE_FILE).replace(/\\\\/g,'/').match(/\\.branch-([^/]+)\\//)");
  check('반증: 마커 소유자에 역파싱을 되살리면 거짓이 된다',
    sBad !== s && !wallJudge('stop-observation.js', sBad).clean,
    sBad === s ? '변형이 일어나지 않았다' : '되살려도 참');

  // 주석에도 함수 이름이 나오므로 **코드 줄**을 겨냥한다 — 설명 문장을 바꾸는 반증은 공허하다.
  const w = hook('_git.js');
  const wBad = w.replace(/require\('\.\/_state'\)\.stateSiblings\(\)/,
    "['pipeline-state.json','impact-graph.json','task-graph.json']");
  check('반증: 감시목록을 하드코딩으로 되돌리면 거짓이 된다',
    wBad !== w && !wallJudge('_git.js', wBad).clean,
    wBad === w ? '변형이 일어나지 않았다' : '되살려도 참');
}

// ── SP-05: 리터럴을 가진 훅 파일 수가 준다 ───────────────────────────
//   사람의 기억이 아니라 **숫자**가 판정한다. 기준선(2026-09-18): 10파일 · 24회.
//   `_state.js` 는 정본이므로 리터럴을 갖는 것이 정상이다 — 그것까지 금지하면
//   정본이 자기를 표현할 수 없다.
console.log('\n[SP-05] should_shrink_the_number_of_files_that_know_the_path');
{
  const files = fs.readdirSync(HOOKS).filter(f => f.endsWith('.js'));
  const owners = files.filter(f => /\.branch-/.test(codeOnly(hook(f))));
  const LIMIT = 6;   // 범위 밖 4파일(_coordinator · advance-phase-harness · post-write-sync · session-gate) + 정본
  check(`.claude/.branch- 리터럴을 가진 훅 파일이 ${LIMIT}개 이하다 (기준선 10)`,
    owners.length <= LIMIT, `${owners.length}개: ${owners.join(', ')}`);
  check('대조군: 정본(_state.js)은 여전히 리터럴을 갖는다 — 정본은 자기를 표현한다',
    owners.includes('_state.js'), owners.join(', '));
}

// ── SP-06: 락 없는 제2 상태 쓰기가 없다 ──────────────────────────────
//   `_common.js:26-48` 의 주석은 "저장 (atomic)" 인데 실제로는 락·백업·검증이 전부 없는
//   생 writeFileSync 였다. 지금 호출자가 없지만 export 돼 **장전돼 있었다.**
// ── SP-05b: 벽은 정본이 죽어도 닫혀 있다 ─────────────────────────────
//   [Loop A 보안 렌즈] 정본에 **단독 의존**하면 `_state` 로드 실패나 판정 축소가 곧 개방이다.
//   `pre-tool-gate` 의 전체 catch 는 설계상 allow 방향이므로, 그 안에서 던지면 벽이 통째로 열린다.
console.log('\n[SP-05b] should_stay_closed_even_if_the_canonical_fails');
{
  const g = codeOnly(hook('pre-tool-gate.js'));
  const hasFallback = t => /_stateFileByName\s*=/.test(t)
    && /return\s+_stateFileByName\(/.test(t)
    && /isStateFile/.test(t);
  check('정본 판정이 실패해도 이름 기반 폴백이 남는다', hasFallback(g),
    (g.match(/[^\n]*pipeline-state[^\n]*/g) || []).slice(0, 3).join(' | '));
  // 반증: 폴백을 지운 사본은 판정이 거짓이 된다.
  const stripped = g.replace('return _stateFileByName(filePath);', 'return false;');
  check('반증: 폴백을 지운 사본은 판정이 거짓이다',
    stripped !== g && !hasFallback(stripped),
    stripped === g ? '변형이 일어나지 않았다' : '지워도 참');

  // 모듈 로드가 던지면 게이트가 통째로 열린다 — 격리 기록은 로드를 막지 않아야 한다.
  const st = codeOnly(hook('_state.js'));
  check('격리 감사 기록이 모듈 로드를 던지게 하지 않는다',
    /try\s*\{\s*\n?\s*logAudit\(\{\s*event:\s*'state-quarantined'/.test(st),
    (st.match(/[^\n]*state-quarantined[^\n]*/g) || []).join(' | '));
}

console.log('\n[SP-06] should_not_keep_a_lock_free_state_writer');
{
  const c = codeOnly(hook('_common.js'));
  const hasRawWrite = t => /writeFileSync\s*\(\s*STATE_FILE/.test(t);
  check('_common.js 에 락 없는 상태 쓰기가 없다', !hasRawWrite(c),
    (c.match(/[^\n]*writeFileSync\s*\(\s*STATE_FILE[^\n]*/g) || []).join(' | '));
  // 반증: 되살린 사본에서는 판정이 거짓이 된다 — 술어가 공허하지 않다.
  const revived = c + "\nfunction saveState(s){ fs.writeFileSync(STATE_FILE, JSON.stringify(s)); }\n";
  check('반증: 락 없는 쓰기를 되살린 사본은 판정이 거짓이다', hasRawWrite(revived), '');
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
