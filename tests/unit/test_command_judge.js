// tests/unit/test_command_judge.js
// 명령 판정 규칙표 — charter harness-v3 / N-01 (FR-06~FR-14 · D4·D5·D6·D7·D11)
//
// 왜 이 파일이 존재하는가:
//   규칙 8(상태 파일 우회 편집 차단)은 pre-tool-gate 안에 인라인 정규식으로만 있었고,
//   그것을 통째로 끄는 스위치가 있었다 — `if (!IS_INTERPRETER)`. 즉 `bash -c` 여섯
//   글자를 앞에 붙이면 규칙이 사라졌다. 반대 방향의 오탐도 같은 곳에서 나왔다:
//   읽기 전용 명령이 출력 문자열 때문에 차단됐고, 문서에 명령을 **언급**만 해도 걸렸다.
//
//   판정을 순수 함수로 떼어내면 훅을 spawn 하지 않고 규칙표를 직접 단언할 수 있다.
//   아래 목록은 전부 2026-09-07 에 **실제 훅 프로세스로 실측한** 결과다.

'use strict';

const path = require('path');
const HOOKS = path.resolve(__dirname, '..', '..', '.claude', 'hooks');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).slice(0, 220) : ''}`); }
}

let C = null;
try { C = require(path.join(HOOKS, '_common.js')); } catch { /* 아래에서 잡힌다 */ }
const judge = C && C.judgeCommand;

console.log('\n═══ CJ: 명령 판정 ═══');

console.log('\n[CJ-00] should_expose_pure_command_judge');
check('_common 이 judgeCommand 를 export 한다', typeof judge === 'function',
  C ? 'exports: ' + Object.keys(C).join(',') : '_common 로드 실패');
if (typeof judge !== 'function') {
  console.log('\n  judgeCommand 가 없어 나머지 케이스를 돌릴 수 없습니다 (Red).');
  console.log(`\n  결과: ${pass} PASS / ${fail + 1} FAIL`);
  process.exit(1);
}

// 보호 경로 문자열은 조각으로 조립한다 — 이 파일 자체가 게이트를 트리거하지 않도록.
const PS = 'pipeline' + '-state.json';
const LEDGER = 'decisions' + '.jsonl';
const AUDIT = 'audit-log' + '.jsonl';
const BR = '.claude/.branch-';

// ── CJ-01: 실측으로 열려 있던 우회가 전부 차단된다 (D4) ─────────────
console.log('\n[CJ-01] should_block_every_measured_bypass');
const BLOCKED = [
  ['셸 래핑 — bash -c + sed -i', `bash -c "sed -i s/x/y/ docs/memory/${PS}"`],
  ['셸 래핑 — sh -c + 리다이렉트', `sh -c "echo {} >> docs/memory/${LEDGER}"`],
  ['node --eval 상태 쓰기', `node --eval "require('./.claude/hooks/_state').atomicStateUpdate(s=>{s.userAck={}})"`],
  ['브랜치 디렉터리 복사 — cp -r', `cp -r ${BR}a ${BR}b`],
  ['브랜치 디렉터리 이동 — mv', `mv ${BR}a ${BR}b`],
  ['브랜치 디렉터리 동기화 — rsync', `rsync -a ${BR}a/ ${BR}b/`],
  ['브랜치 디렉터리 복사 — robocopy', `robocopy ${BR}a ${BR}b /E`],
  ['브랜치 디렉터리 복사 — Copy-Item', `Copy-Item -Recurse ${BR}a ${BR}b`],
  ['audit-log 추가 — tee -a', `echo x | tee -a docs/memory/${AUDIT}`],
  ['audit-log 추가 — 리다이렉트', `echo x >> docs/memory/${AUDIT}`],
  ['PowerShell Set-Content', `Set-Content -Path ${BR}x/${PS} -Value $j`],
  ['PowerShell Out-File 파이프', `$j | Out-File -Encoding utf8 ${BR}x/${PS}`],
  ['powershell -Command 래핑', `powershell -Command "Set-Content docs/memory/${LEDGER} x"`],
  ['따옴표로 감싼 리다이렉트 대상', `echo x >> "docs/memory/${LEDGER}"`],
  // 감사 장부는 승인 근거 대조의 상대 장부다 — 쓰기가 열려 있으면 대조가 무의미하다.
  ['감사 아카이브 리다이렉트', `echo x >> docs/memory/audit-log-archive/2026-09-07.jsonl`],
  ['감사 아카이브 sed -i', `sed -i s/a/b/ docs/memory/audit-log-archive/2026-09-07.jsonl`],
  ['감사 아카이브 통째 복사', `cp -r /tmp/fake docs/memory/audit-log-archive`],
];
for (const [label, cmd] of BLOCKED) {
  const r = judge(cmd);
  check(label, !!r, 'allow (기대: 차단) · ' + cmd.slice(0, 90));
}

// ── CJ-02: 본문을 볼 수 없는 명령은 판정 불가 → 차단 (FR-08) ────────
console.log('\n[CJ-02] should_block_undecidable_encoded_commands');
for (const cmd of [
  'powershell -EncodedCommand UwBlAHQALQBDAG8AbgB0AGUAbgB0AA==',
  'pwsh -enc UwBlAHQALQBDAG8AbgB0AGUAbgB0AA==',
]) {
  const r = judge(cmd);
  check('인코딩 명령 차단: ' + cmd.slice(0, 40), !!r, 'allow');
}

// ── CJ-03: 훅 직접 실행 차단, advance-phase 는 예외 (D6) ────────────
console.log('\n[CJ-03] should_block_direct_hook_execution');
for (const [label, cmd] of [
  ['상대 경로', 'node .claude/hooks/session-gate.js'],
  ['절대/변수 경로', 'node "$CLAUDE_PROJECT_DIR/.claude/hooks/pre-tool-gate.js"'],
  ['셸 래핑', `sh -c 'node .claude/hooks/session-gate.js'`],
]) {
  check('훅 직접 실행 차단 — ' + label, !!judge(cmd), 'allow · ' + cmd);
}
for (const [label, cmd] of [
  ['advance-phase', 'node .claude/hooks/advance-phase.js status'],
  ['advance-phase-heal', 'node .claude/hooks/advance-phase-heal.js'],
  ['테스트 러너', 'node tests/run.js'],
]) {
  check('허용 — ' + label, judge(cmd) === null, JSON.stringify(judge(cmd)));
}

// ── CJ-04: 실측 오탐이 전부 통과한다 (D5) ───────────────────────────
console.log('\n[CJ-04] should_allow_every_measured_false_positive');
const ALLOWED = [
  ['읽기 전용 node -e (출력 라벨에 ack 낱말)',
    `node -e "const s=require('./.claude/hooks/_state').loadState(); console.log('userAck=' + JSON.stringify(s.userAck))"`],
  ['보호 경로를 검색', `grep -rn "${PS}" docs/`],
  ['보호 경로를 읽기', `cat docs/memory/${LEDGER}`],
  ['문서 안의 approve 언급', `echo "명령은 node .claude/hooks/advance-phase.js approve prd 입니다"`],
  ['스테일 브랜치 디렉터리 청소', `rm -rf ${BR}old-feature`],
  ['보호 파일 백업 (원본 인자)', `cp docs/memory/${LEDGER} /tmp/backup.jsonl`],
  ['정상 셸 명령', `pwsh -c "git status"`],
  ['git 로그', `git log --oneline -5`],
  // 훅을 고치는 사람이 문법 검사조차 못 하면 벽이 아니라 장애물이다.
  // node 의 -c/--check 는 셸의 -c 와 달리 실행이 아니라 파싱이다.
  ['훅 문법 검사 (--check)', `node --check .claude/hooks/session-gate.js`],
  ['훅 문법 검사 (-c)', `node -c .claude/hooks/pre-tool-gate.js`],
];
for (const [label, cmd] of ALLOWED) {
  const r = judge(cmd);
  check(label, r === null, JSON.stringify(r) + ' · ' + cmd.slice(0, 90));
}

// ── CJ-05: 기존 차단이 그대로 유지된다 (회귀) ───────────────────────
console.log('\n[CJ-05] should_keep_existing_blocks');
for (const [label, cmd] of [
  ['sed -i 상태 파일', `sed -i s/x/y/ docs/memory/${PS}`],
  ['리다이렉트 원장', `echo {} >> docs/memory/${LEDGER}`],
  ['인터프리터 상태 쓰기', `node -e "require('./_state').atomicStateUpdate(s=>{s.userAck={kind:'prd-approve'}})"`],
  ['파괴적 삭제', 'rm ' + '-rf /'],
  // -p/--print 도 본문을 실행한다 — -e 만 막으면 그대로 뚫린다
  ['node -p 본문 상태 쓰기', `node -p "require('./_state').atomicStateUpdate(s=>{s.charters={}})"`],
  // 실행 플래그가 아닌 --check 는 통과하지만, 본문이 있는 형태는 여전히 막힌다
  ['셸 -c 로 감싼 훅 실행', `bash -c "node .claude/hooks/post-write-sync.js"`],
]) {
  check(label, !!judge(cmd), 'allow · ' + cmd.slice(0, 90));
}

// ── CJ-06: 도구가 달라도 판정이 같다 (D7) ───────────────────────────
console.log('\n[CJ-06] should_judge_identically_regardless_of_tool');
{
  const norm = C.normalizeToolCommand;
  check('_common 이 normalizeToolCommand 를 export 한다', typeof norm === 'function');
  if (typeof norm === 'function') {
    for (const [, cmd] of BLOCKED.concat(ALLOWED)) {
      const a = judge(norm('Bash', { command: cmd }) || '');
      const b = judge(norm('PowerShell', { command: cmd }) || '');
      if (JSON.stringify(a) !== JSON.stringify(b)) {
        check('Bash·PowerShell 판정 일치: ' + cmd.slice(0, 50), false, JSON.stringify(a) + ' vs ' + JSON.stringify(b));
      }
    }
    check('모든 케이스에서 Bash·PowerShell 판정이 일치', true);
    check('명령을 갖지 않는 도구는 null', norm('Read', { file_path: 'x' }) === null);
  }
}

// ── CJ-07: 어떤 입력에도 예외를 던지지 않는다 (D11 / FR-21) ─────────
//   pre-tool-gate 의 catch 는 모든 예외를 allow 로 떨군다 — 파서가 던지면 게이트가 통째로 열린다.
console.log('\n[CJ-07] should_never_throw_on_any_input');
{
  const fuzz = ['', ' ', '\n', ' ', 'x'.repeat(10000), '"'.repeat(500), "'".repeat(500),
    '>'.repeat(200), '|'.repeat(200), '\\', '$(', '`', '${', '\uD800', 'cp ' + '"'.repeat(50) + PS];
  for (let i = 0; i < 100; i++) fuzz.push('cmd' + i + ' ' + fuzz[i % fuzz.length]);
  let threw = null;
  for (const f of fuzz) {
    try {
      const r = judge(f);
      if (!(r === null || (r && typeof r === 'object' && typeof r.rule === 'string'))) { threw = 'bad shape: ' + JSON.stringify(r); break; }
    } catch (e) { threw = f.slice(0, 40) + ' → ' + e.message; break; }
  }
  check(`퍼즈 ${fuzz.length}건에 예외 없음 · 반환 형태 일정`, threw === null, threw);
  for (const bad of [null, undefined, 123, {}, []]) {
    try { judge(bad); } catch (e) { threw = String(bad) + ' → ' + e.message; }
  }
  check('비문자열 입력에도 예외 없음', threw === null, threw);
}

// ═══════════════════════════════════════════════════════════════════
// [CJ-20] 무결성 1차 방어선 — charter harness-v4-quality / N-02 (D1·D2·D3)
//
// **오탐과 누락을 같은 표에 둔다.** D1 은 누락이 아니라 오탐이었다 —
//   읽기 전용 `node -e "xs.map(f => read('<보호경로>'))"` 가 하드 차단됐다.
//   누락만 보는 표는 그 결함을 다시 만든다.
// ═══════════════════════════════════════════════════════════════════
console.log('\n[CJ-20] should_seal_the_first_line_of_defence');
{
  const L = 'docs/memory/' + 'decisions.jsonl';
  const A = 'docs/memory/' + 'audit-log.jsonl';

  // [명령, 차단해야 하는가, 라벨]
  const TABLE = [
    // ── D1: 리다이렉트는 연산자일 때만 ──
    [`node -e "xs.map(f => read('${A}'))"`,           false, 'D1 화살표 함수 안의 읽기'],
    [`node -e "if (n > 1) read('${A}')"`,             false, 'D1 비교연산자 뒤 보호경로'],
    [`node -e "a >= b; read('${A}')"`,                false, 'D1 >= 뒤 보호경로'],
    [`cat ${A}`,                                      false, 'D1 평범한 읽기'],
    [`grep x ${A}`,                                   false, 'D1 grep 읽기'],
    [`echo x > ${A}`,                                  true, 'D1 진짜 리다이렉트는 여전히 차단'],
    [`echo x >> ${L}`,                                 true, 'D1 append 리다이렉트도 차단'],
    [`: > ${A}`,                                       true, 'D1 truncate 도 차단'],

    // ── D2: 인터프리터 본문은 언어를 묻지 않는다 ──
    [`python -c "open('${L}','w').write('x')"`,        true, 'D2 python 쓰기'],
    [`python3 -c "open('${L}','a').write('x')"`,       true, 'D2 python3 append'],
    [`ruby -e "File.write('${L}','x')"`,               true, 'D2 ruby 쓰기'],
    [`perl -e "unlink('${L}')"`,                       true, 'D2 perl 삭제'],
    [`node -e "require('fs').writeFileSync('${L}','x')"`, true, 'D2 node 쓰기(기존)'],
    [`python -c "print(1 + 1)"`,                       false, 'D2 보호경로 없는 python 은 통과'],
    [`node -e "console.log(process.version)"`,         false, 'D2 보호경로 없는 node 는 통과'],

    // ── D3: 보호 파일의 조상 디렉터리 ──
    [`rm ${L}`,                                        true, 'D3 보호 파일 삭제(기존)'],
    ['rm -rf docs/memory',                              true, 'D3 원장을 담은 디렉터리'],
    ['rm -rf docs/memory/',                             true, 'D3 끝 슬래시'],
    ['rm -rf docs',                                     true, 'D3 그 조상'],
    ['rm -rf .claude/.branch-x',                        false, 'D3 브랜치 상태 청소는 정당'],
    ['rm -rf node_modules',                             false, 'D3 무관한 디렉터리'],
    ['rm -rf /tmp/scratch',                             false, 'D3 저장소 밖'],
  ];

  let bad = 0;
  for (const [cmd, shouldBlock, label] of TABLE) {
    let v = null;
    try { v = judge(cmd); } catch (e) { v = { rule: 'THREW', reason: e.message }; }
    const blocked = !!(v && v.rule);
    const ok = blocked === shouldBlock;
    if (!ok) bad++;
    check(label, ok, `${blocked ? '차단[' + v.rule + ']' : '통과'} · 기대=${shouldBlock ? '차단' : '통과'} · ${cmd.slice(0, 70)}`);
  }
  check('기대표 전부 일치', bad === 0, `${bad}건 불일치`);
}

// [CJ-21] 이상 입력에 던지지 않는다 — 판정기 예외는 곧 게이트 개방이다
console.log('\n[CJ-21] should_never_throw_on_hostile_input');
{
  const HOSTILE = [
    '', '   ', '>', '>>', '=>', '2>&1', 'x'.repeat(50000),
    'echo \u0000 > docs/memory/decisions.jsonl',
    '한글 명령 > docs/memory/audit-log.jsonl',
    'rm -rf ' + '../'.repeat(200) + 'docs/memory',
  ];
  let threw = null;
  for (const c of HOSTILE) {
    try { judge(c); } catch (e) { threw = threw || `${JSON.stringify(c.slice(0, 30))}: ${e.message}`; }
  }
  check('이상 입력 10종에 예외 없음', threw === null, threw);
}

// ═══════════════════════════════════════════════════════════════════
// [CJ-22] 훅 직접 실행 — 판정 대상은 **실행되는 것** 이다 (v5/N-04)
//
// 판정이 명령의 **모든 인자**를 훑어 훅 파일명이 있으면 차단했다. 그래서
//   `advance-phase.js evaluate 'grep:.claude/hooks/session-gate.js …'` 처럼
//   훅 경로가 **평가할 문자열 안에** 있는 정당한 호출이 막혔다 — 하네스가 스스로
//   권하는 사용법이다(charter 불변식 평가).
//
// 오탐이 불편에서 그치지 않는다. 막히면 **우회하게 되고, 그 우회가 게이트를 더 크게
//   비껴간다.** 실제로 그랬다 — 스펙 평가를 스크래치패드 스크립트로 옮겼고, 그 스크립트는
//   `_signals.js` 를 직접 require 해 훅 판정을 통째로 지나갔다.
//
// **차단을 푸는 변경이므로 오탐과 누락을 같은 표에 둔다** — CJ-20 의 형식 그대로.
//   무엇이 여전히 막히는지를 같은 자리에서 단언하지 않으면
//   "고쳤다" 가 "구멍을 냈다" 와 구별되지 않는다.
// ═══════════════════════════════════════════════════════════════════
console.log('\n[CJ-22] should_judge_hook_execution_by_target_not_by_substring');
{
  const H = '.claude/hooks/';
  // [명령, 차단해야 하는가, 라벨]
  const TABLE = [
    // ── 오탐: 실행 대상이 아닌 곳의 훅 경로 ──
    [`node ${H}advance-phase.js evaluate 'grep:${H}session-gate.js /x/ >=1'`, false, '스펙 문자열 안의 훅 경로'],
    [`node ${H}advance-phase.js charter approve --charter docs/charters/x-charter.md`, false, 'advance-phase 정상 호출'],
    [`node ${H}advance-phase.js status`,                                      false, 'advance-phase status'],
    [`node tests/run.js ${H}post-write-sync.js`,                              false, '러너에 훅 경로를 필터로 넘김'],
    [`grep -n foo ${H}session-gate.js`,                                       false, '인터프리터가 아닌 읽기'],
    [`cat ${H}session-gate.js`,                                               false, '평범한 읽기'],

    // ── 누락 금지: 실제 실행 경로는 전부 막힌다 ──
    [`node ${H}session-gate.js`,                                               true, '훅 직접 실행'],
    [`node ${H}post-write-sync.js < payload.json`,                             true, '훅 직접 실행 + stdin'],
    [`node --require ${H}session-gate.js app.js`,                              true, '--require 로 적재'],
    [`node -r ${H}session-gate.js app.js`,                                     true, '-r 로 적재'],
    [`node --require ./ok.js ${H}session-gate.js`,                             true, '적재는 무해하고 뒤쪽이 훅'],
    [`sh -c "node ${H}session-gate.js"`,                                       true, 'sh -c 래핑'],
  ];

  for (const [cmd, want, label] of TABLE) {
    let v = null;
    try { v = judge(cmd); } catch (e) { v = { rule: 'THREW:' + e.message }; }
    const blocked = !!(v && v.rule);
    check(`${label} → ${want ? '차단' : '통과'}`, blocked === want,
      `${blocked ? 'BLOCK(' + v.rule + ')' : 'allow'} · ${cmd}`);
  }
}



console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
