// tests/unit/test_codex_integration.js
// Codex 결합 — charter harness-v16-one-graph-one-screen / N-02
//
// 왜 이 파일이 존재하는가:
//   사람이 연 Codex 창은 하네스에 존재하지 않았다. join() 호출부가 하네스 내부 2곳뿐이라
//   work run 으로 띄운 세션만 화면에 떴다. v15 는 이것을 래퍼로 풀려 했으나 전제가 틀렸다 —
//   설치본 0.154.0-alpha.6.2 의 --help 에 `--dangerously-bypass-hook-trust` 가 있고,
//   소스(codex-rs/config/src/hook_config.rs)의 이벤트 이름은 Claude Code 와 같다.
//   공식 훅이 있는데 우회 수단을 만들지 않는다.
//
//   N-01 에서 같은 일을 Claude 쪽으로 했고 Loop A 가 결함 여덟 건을 찾았다. 같은 함정을
//   두 번 파지 않으려고, 그때 얻은 계약을 여기서 처음부터 단언한다.
'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs'), os = require('node:os'), path = require('node:path');
const { spawnSync, execFileSync } = require('node:child_process');

const ROOT = path.resolve(__dirname, '../..');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log('    ✅ ' + label); }
  else { fail++; console.log('    ❌ ' + label + (detail ? ' — ' + detail : '')); }
}

const HOOK = path.join(ROOT, '.claude', 'hooks', '_codex-hooks.js');

console.log('\n═══ CX-01: Codex 훅이 세션을 등록한다 ═══\n');
{
  const box = fs.mkdtempSync(path.join(os.tmpdir(), 'codexhook-'));
  try {
    execFileSync('git', ['init', '-q'], { cwd: box, windowsHide: true });
    const fire = (payload) => spawnSync(process.execPath, [HOOK], {
      input: JSON.stringify(payload), encoding: 'utf8', windowsHide: true, timeout: 60000,
      env: { ...process.env, CODEX_PROJECT_DIR: box, _HARNESS_TEST_RUN: 'codexhook', CLAUDE_PID: String(process.pid) },
    });
    const sessions = () => {
      try {
        const { repository } = require(path.join(ROOT, '.claude', 'hooks', '_harness-store'));
        const repo = repository(box);
        try { return repo.store.list('session/').map(r => r.value); } finally { repo.store.close(); }
      } catch { return []; }
    };

    check('훅 스크립트가 존재한다', fs.existsSync(HOOK), HOOK);

    const start = fire({ hook_event_name: 'SessionStart', session_id: 'cx-1', source: 'startup', cwd: box });
    check('SessionStart 가 exit 0', start.status === 0, `exit=${start.status} ${String(start.stderr || '').slice(0, 200)}`);
    const active = sessions().filter(s => s.status === 'active');
    check('codex 런타임의 대화형 세션으로 등록된다',
      active.length === 1 && active[0].runtime === 'codex' && active[0].kind === 'interactive',
      JSON.stringify(active.map(s => ({ r: s.runtime, k: s.kind }))));
    check('훅이 아니라 창(부모)의 pid 를 싣는다', active.length === 1 && active[0].pid === process.pid,
      active.length ? `pid=${active[0].pid} 기대=${process.pid}` : '세션 없음');

    fire({ hook_event_name: 'SessionStart', session_id: 'cx-1', source: 'compact', cwd: box });
    fire({ hook_event_name: 'SessionStart', session_id: 'cx-1', source: 'resume', cwd: box });
    check('압축·재개가 활성 세션을 늘리지 않는다', sessions().filter(s => s.status === 'active').length === 1,
      `활성 ${sessions().filter(s => s.status === 'active').length}개`);

    const prompt = fire({ hook_event_name: 'UserPromptSubmit', prompt: '상태', session_id: 'cx-1', cwd: box });
    check('UserPromptSubmit 이 exit 0', prompt.status === 0, `exit=${prompt.status}`);
    const synced = sessions().find(s => s.status === 'active');
    check('작업 내용이 서버에 실린다', !!(synced && synced.activity && typeof synced.activity === 'object'),
      JSON.stringify(synced && synced.activity));

    const end = fire({ hook_event_name: 'SessionEnd', session_id: 'cx-1', reason: 'other', cwd: box });
    check('SessionEnd 가 exit 0', end.status === 0, `exit=${end.status}`);
    check('SessionEnd 가 세션을 닫는다', sessions().filter(s => s.status === 'active').length === 0,
      `활성 ${sessions().filter(s => s.status === 'active').length}개`);
  } finally { try { fs.rmSync(box, { recursive: true, force: true }); } catch {} }
}

console.log('\n═══ CX-02: 관측이 죽어도 Codex 작업은 멈추지 않는다 ═══\n');
{
  const bare = fs.mkdtempSync(path.join(os.tmpdir(), 'codexbare-'));
  try {
    for (const event of ['SessionStart', 'UserPromptSubmit', 'SessionEnd']) {
      const r = spawnSync(process.execPath, [HOOK], {
        input: JSON.stringify({ hook_event_name: event, session_id: 'bare', cwd: bare }),
        encoding: 'utf8', windowsHide: true, timeout: 60000,
        env: { ...process.env, CODEX_PROJECT_DIR: bare, _HARNESS_TEST_RUN: 'codexbare' },
      });
      check(`${event} 는 저장소가 없어도 통과한다`, r.status === 0, `exit=${r.status}`);
      check(`${event} 는 표준출력을 더럽히지 않는다`, (r.stdout || '') === '', JSON.stringify(String(r.stdout).slice(0, 120)));
    }
  } finally { try { fs.rmSync(bare, { recursive: true, force: true }); } catch {} }
}

console.log('\n═══ CX-03: 설정이 Codex 공식 스키마를 따른다 ═══\n');
{
  // 이벤트 이름과 핸들러 형식은 codex-rs/config/src/hook_config.rs 의 serde rename 과 같아야 한다.
  const OFFICIAL = ['SessionStart', 'SessionEnd', 'UserPromptSubmit'];
  let build = null;
  try { build = require(path.join(ROOT, '.claude', 'hooks', '_codex-hooks.js')).hooksConfig; } catch { /* 아직 없음 */ }
  check('설정 생성기를 노출한다', typeof build === 'function', String(typeof build));
  if (typeof build === 'function') {
    const cfg = build('C:/proj');
    const events = Object.keys(cfg.hooks || {});
    check('공식 이벤트 이름만 쓴다', OFFICIAL.every(e => events.includes(e)) && events.every(e => OFFICIAL.includes(e)), events.join(','));
    const groups = Object.values(cfg.hooks || {}).flat();
    check('matcher + hooks[] 구조다', groups.length > 0 && groups.every(g => Array.isArray(g.hooks)), JSON.stringify(groups).slice(0, 160));
    const handlers = groups.flatMap(g => g.hooks || []);
    check('핸들러가 type:command 형식이다', handlers.length > 0 && handlers.every(h => h.type === 'command' && typeof h.command === 'string' && h.command),
      JSON.stringify(handlers).slice(0, 200));
    check('Windows 명령은 commandWindows 로 준다', handlers.every(h => h.commandWindows === undefined || typeof h.commandWindows === 'string'),
      JSON.stringify(handlers).slice(0, 200));
  }
}

console.log('\n═══ CX-06: 관측이 스스로 회복한다 (Loop A) ═══\n');
{
  const box = fs.mkdtempSync(path.join(os.tmpdir(), 'codexheal-'));
  try {
    execFileSync('git', ['init', '-q'], { cwd: box, windowsHide: true });
    const fire = (payload) => spawnSync(process.execPath, [HOOK], {
      input: JSON.stringify(payload), encoding: 'utf8', windowsHide: true, timeout: 60000,
      env: { ...process.env, CODEX_PROJECT_DIR: box, _HARNESS_TEST_RUN: 'codexheal' },
    });
    const sessions = () => {
      try {
        const { repository } = require(path.join(ROOT, '.claude', 'hooks', '_harness-store'));
        const repo = repository(box);
        try { return repo.store.list('session/').map(r => r.value); } finally { repo.store.close(); }
      } catch { return []; }
    };
    // 창의 신원은 페이로드의 session_id 다 — 훅이 쓰는 경로도 그것으로 만들어진다.
    const file = path.join(box, '.claude', '.session-credential-codex-heal.json');

    fire({ hook_event_name: 'SessionStart', session_id: 'heal', source: 'startup', cwd: box });
    const first = JSON.parse(fs.readFileSync(file, 'utf8')).session_id;

    // 자격 파일이 잘려 저장되면(강제 종료·디스크 지연) 그 창은 영원히 화면에서 사라진다.
    //   개수만 보면 공허하게 통과한다 — 자격이 **다시 쓸 수 있는 상태**가 됐는지를 본다.
    fs.writeFileSync(file, '{"session_id":"s-broke');
    fire({ hook_event_name: 'UserPromptSubmit', prompt: 'x', session_id: 'heal', cwd: box });
    let healed = null;
    try { healed = JSON.parse(fs.readFileSync(file, 'utf8')); } catch { /* 그대로 손상 */ }
    check('손상된 자격을 만나면 자격을 다시 만든다', !!(healed && healed.session_id && healed.token),
      healed ? JSON.stringify(healed).slice(0, 80) : '여전히 손상됨');
    check('그 자격이 활성 세션을 가리킨다',
      !!(healed && sessions().some(s => s.session_id === healed.session_id && s.status === 'active')),
      healed ? healed.session_id : '-');

    // 자격은 멀쩡한데 세션이 이미 닫힌 경우 — 다음 프롬프트에 **새 자격**으로 다시 붙어야 한다.
    const current = JSON.parse(fs.readFileSync(file, 'utf8'));
    {
      const { repository } = require(path.join(ROOT, '.claude', 'hooks', '_harness-store'));
      const { Coordinator } = require(path.join(ROOT, '.claude', 'hooks', '_coordinator'));
      const repo = repository(box);
      try { new Coordinator(repo.store, { repo_id: repo.repo_id }).leave(current); } finally { repo.store.close(); }
    }
    // 재전송 하한(20초) 안에는 저장소를 열지 않으므로 죽은 세션을 알 수 없다 — 그게 설계다.
    //   하한이 지난 상태로 만들어, 실제로 접촉하는 경로가 회복하는지 본다.
    fs.writeFileSync(file, JSON.stringify({ ...current, synced_at: Date.now() - 60000 }));
    fire({ hook_event_name: 'UserPromptSubmit', prompt: 'y', session_id: 'heal', cwd: box });
    const after = JSON.parse(fs.readFileSync(file, 'utf8'));
    check('닫힌 세션을 만나면 새 자격으로 다시 붙는다',
      after.session_id !== current.session_id && sessions().some(s => s.session_id === after.session_id && s.status === 'active'),
      `이전 ${current.session_id.slice(0, 10)} → 지금 ${String(after.session_id).slice(0, 10)}`);
    check('활성 세션이 하나만 남는다', sessions().filter(s => s.status === 'active').length === 1,
      `활성 ${sessions().filter(s => s.status === 'active').length}개`);
    check('첫 세션은 그대로 살아 있지 않다', !sessions().some(s => s.session_id === first && s.status === 'active'), first);
  } finally { try { fs.rmSync(box, { recursive: true, force: true }); } catch {} }
}

console.log('\n═══ CX-08: 창 신원은 매 호출마다 바뀌지 않는다 ═══\n');
{
  // [실측] ppid 로 창을 식별하면 안 된다 — Claude Code·Codex 는 훅마다 새 셸을 띄우므로
  //   ppid 가 매번 다르다. 그 결과 자격 파일이 턴마다 새로 생기고 **매 턴 새 세션**이 등록되어
  //   활성 한도(10, 저장소 전체 공유)를 빠르게 먹는다. 실제로 자격 파일 16개가 쌓였다.
  //   공식 페이로드의 session_id 가 창의 수명 동안 안정적인 신원이다.
  const { credentialPath } = require(path.join(ROOT, '.claude', 'hooks', '_codex-hooks.js'));
  const a = credentialPath('C:/proj', 'sess-abc');
  const b = credentialPath('C:/proj', 'sess-abc');
  const c = credentialPath('C:/proj', 'sess-xyz');
  check('같은 session_id 는 같은 자격 경로를 준다', a === b, `${a}\n${b}`);
  check('다른 session_id 는 다른 자격 경로를 준다', a !== c, `${a}\n${c}`);
  check('경로에 session_id 가 들어간다', /sess-abc/.test(a), a);
  check('경로 구분자·상위 참조를 담은 session_id 를 그대로 쓰지 않는다',
    !/[\\/]|\.\./.test(path.basename(credentialPath('C:/proj', '../../evil/x'))),
    path.basename(credentialPath('C:/proj', '../../evil/x')));
}

console.log('\n═══ CX-07: 남의 작업을 자기 것으로 보고하지 않는다 (Loop A) ═══\n');
{
  // 레거시 phase 상태는 **브랜치 단위**로 공유된다. Codex 창이 그것을 자기 활동으로 실으면,
  //   화면은 놀고 있는 Codex 창을 "Node N-02 진행 중" 으로 표시한다 — 화면이 거짓을 말한다.
  const { activityOf } = require(path.join(ROOT, '.claude', 'hooks', '_codex-hooks.js'));
  const a = activityOf(ROOT) || {};
  check('Codex 창은 Claude 의 phase 를 자기 것으로 싣지 않는다', !a.phase && !a.node && !a.prd,
    JSON.stringify(a));
  check('대신 무엇인지는 밝힌다', typeof a.summary === 'string' && a.summary.length > 0, JSON.stringify(a));
}

console.log('\n═══ CX-05: 설치가 두 런타임을 함께 배선한다 ═══\n');
{
  const installSrc = fs.readFileSync(path.join(ROOT, 'install.js'), 'utf8');
  check('설치가 Codex hooks.json 을 다룬다', /hooks\.json/.test(installSrc), '언급 없음');
  check('설치가 _codex-hooks 설정 생성기를 쓴다', /hooksConfig/.test(installSrc), '언급 없음');

  // 기존 항목을 지우면 사용자가 직접 넣은 훅이 사라진다 — 병합해야 한다.
  let merge = null;
  try { merge = require(path.join(ROOT, '.claude', 'hooks', '_codex-hooks.js')).mergeHooks; } catch { /* 아직 없음 */ }
  check('병합 함수를 노출한다', typeof merge === 'function', String(typeof merge));
  if (typeof merge === 'function') {
    const mine = { hooks: { SessionStart: [{ hooks: [{ type: 'command', command: 'node harness.js' }] }] } };
    const theirs = { hooks: { SessionStart: [{ hooks: [{ type: 'command', command: 'node theirs.js' }] }] }, description: '사용자 설정' };
    const out = merge(theirs, mine);
    const commands = (out.hooks.SessionStart || []).flatMap(g => g.hooks || []).map(h => h.command);
    check('사용자 훅을 지우지 않는다', commands.includes('node theirs.js'), commands.join(' | '));
    check('하네스 훅을 더한다', commands.includes('node harness.js'), commands.join(' | '));
    check('두 번 병합해도 중복되지 않는다',
      (merge(out, mine).hooks.SessionStart || []).flatMap(g => g.hooks || []).filter(h => h.command === 'node harness.js').length === 1,
      JSON.stringify(merge(out, mine).hooks.SessionStart).slice(0, 200));
    check('사용자의 다른 키를 보존한다', merge(theirs, mine).description === '사용자 설정', String(merge(theirs, mine).description));
  }

  // 소스에 문자열이 있다고 동작하는 것은 아니다 — install.js 의 Codex 블록을 실제로 실행한다.
  //   (처음 구현은 선언되지 않은 os 를 써서 ReferenceError 가 났고, catch 가 그것을 삼켜
  //    "배선을 건너뜁니다" 로 조용히 넘어갔다. 문자열 검사는 그것을 못 잡는다.)
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'codexhome-'));
  try {
    const block = (installSrc.match(/\/\/ \[v16\/N-02\][\s\S]*?\n\} catch \(e\) \{[\s\S]*?\n\}/) || [])[0];
    check('설치 소스에서 Codex 블록을 찾는다', !!block, '패턴 불일치');
    if (block) {
      const run = spawnSync(process.execPath, ['-e',
        `const path=require('node:path');const fs=require('node:fs');const TARGET=${JSON.stringify(ROOT)};${block}`],
        { encoding: 'utf8', windowsHide: true, timeout: 60000, env: { ...process.env, CODEX_HOME: home } });
      const written = path.join(home, 'hooks.json');
      check('Codex 배선이 실제로 hooks.json 을 만든다', fs.existsSync(written),
        `${String(run.stdout || '').slice(-200)} ${String(run.stderr || '').slice(-200)}`);
      if (fs.existsSync(written)) {
        const cfg = JSON.parse(fs.readFileSync(written, 'utf8'));
        check('만들어진 설정이 공식 이벤트를 담는다',
          ['SessionStart', 'SessionEnd', 'UserPromptSubmit'].every(e => Array.isArray(cfg.hooks?.[e]) && cfg.hooks[e].length),
          Object.keys(cfg.hooks || {}).join(','));
      }
      check('배선이 조용히 건너뛰지 않는다', !/건너뜁니다/.test(String(run.stdout || '') + String(run.stderr || '')),
        String(run.stdout || '').slice(-200));
    }
  } finally { try { fs.rmSync(home, { recursive: true, force: true }); } catch {} }
}

console.log('\n═══ CX-04: 훅 신뢰 모델을 우회하지 않는다 ═══\n');
{
  // 하네스가 사용자 대신 신뢰를 건너뛰면, 사용자가 검토하지 않은 훅이 조용히 돈다.
  const hooksDir = path.join(ROOT, '.claude', 'hooks');
  const offenders = [];
  for (const name of fs.readdirSync(hooksDir)) {
    if (!name.endsWith('.js')) continue;
    const text = fs.readFileSync(path.join(hooksDir, name), 'utf8');
    if (text.includes('--dangerously-bypass-hook-trust')) offenders.push(name);
  }
  const installSrc = fs.readFileSync(path.join(ROOT, 'install.js'), 'utf8');
  if (installSrc.includes('--dangerously-bypass-hook-trust')) offenders.push('install.js');
  check('신뢰 우회 플래그가 하네스 경로에 없다', offenders.length === 0, offenders.join(', '));

  // 프로젝트 레이어에서 무시되는 키를 쓰면 설정한 줄 알고 넘어간다(공식 문서).
  const forbidden = ['approval_policy', 'sandbox_mode'];
  const writes = forbidden.filter(k => installSrc.includes(`${k} =`) || installSrc.includes(`"${k}"`) || installSrc.includes(`'${k}'`));
  check('프로젝트 설정으로 덮을 수 없는 키를 쓰지 않는다', writes.length === 0, writes.join(', '));
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('\n═══ CX-09: 승인 요청에 규격대로 거절한다 (N-03) ═══\n');
{
  // 왜 이 절이 존재하는가:
  //   `_codex-rpc.js` 는 id 가 있는 **모든** 서버 요청에 JSON-RPC error -32600 으로 답했다.
  //   JSON-RPC 에서 error 는 "요청을 처리하지 못했다" 이고 result 는 "이해했고 이것이 답이다" 다.
  //   우리는 동의를 합성하지 않겠다는 **결정**을 내렸는데 상대에게는 고장으로 전달됐다.
  //
  //   거절의 모양은 계열마다 다르다. 봉투는 "거절 값은 decline" 이라 적었고(틀렸다 —
  //   decline 은 턴을 계속한다), 그것을 고친 분석 문서는 "전부 cancel" 이라 적었다
  //   (그것도 틀렸다 — 레거시는 abort, 권한은 decision 필드가 없고, 입력은 answers 이며
  //   MCP elicitation 은 action 이다). **추정으로 적었다면 다섯 자리에서 틀렸을 것이다.**
  //
  //   기대표는 실측에서 온다. codex-cli 0.154.0-alpha.6.2 에서
  //     codex.exe app-server generate-json-schema --out <DIR>
  //   로 뽑은 ServerRequest.json · *Response.json 이 출처다. 이 표는 **구현을 import 하지
  //   않는다** — import 하면 반증이 공허해진다.
  const EXPECT = {
    'item/commandExecution/requestApproval': { decision: 'cancel' },
    'item/fileChange/requestApproval': { decision: 'cancel' },
    'item/permissions/requestApproval': { permissions: {} },
    'item/tool/requestUserInput': { answers: {} },
    'mcpServer/elicitation/request': { action: 'cancel' },
    execCommandApproval: { decision: 'abort' },
    applyPatchApproval: { decision: 'abort' },
  };
  // 사용자 판단이 아니라 **능력** 요청이다. 우리는 그 능력을 제공하지 않는다 —
  //   특히 인증 토큰 갱신에는 절대 result 로 답하지 않는다.
  const CAPABILITY = ['item/tool/call', 'account/chatgptAuthTokens/refresh', 'attestation/generate'];
  const ALL = [...Object.keys(EXPECT), ...CAPABILITY];

  // 판정은 한 벌이다 — 아래 반증 2건이 이 함수를 그대로 쓴다.
  const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);
  function judge(replies) {
    const bad = [];
    for (const m of Object.keys(EXPECT)) {
      const r = replies[m];
      if (!r) { bad.push(m + ': 응답 없음'); continue; }
      if (r.error) { bad.push(m + ': error 로 답했다'); continue; }
      if (!eq(r.result, EXPECT[m])) bad.push(m + ': ' + JSON.stringify(r.result));
    }
    return bad;
  }
  const judgeCapability = replies =>
    CAPABILITY.filter(m => !replies[m] || !replies[m].error || replies[m].result !== undefined);

  const box = fs.mkdtempSync(path.join(os.tmpdir(), 'codexrpc-'));
  let probe = { replies: {}, announced: [], error: null };
  try {
    // 가짜 app-server — `node app-server` 는 cwd 의 app-server.js 로 해석된다.
    //   initialize 를 받으면 서버 → 클라이언트 요청 10종을 보내고, 돌아온 프레임을
    //   stderr 로 되돌려준다. **전선에 실제로 무엇이 실렸는지**를 본다.
    fs.writeFileSync(path.join(box, 'app-server.js'), [
      'const ALL = ' + JSON.stringify(ALL) + ';',
      'let buf = "";',
      'process.stdin.on("data", d => {',
      '  buf += d.toString();',
      '  let i;',
      '  while ((i = buf.indexOf("\\n")) >= 0) {',
      '    const line = buf.slice(0, i); buf = buf.slice(i + 1);',
      '    let m; try { m = JSON.parse(line); } catch { continue; }',
      '    if (m.method === "initialize") {',
      '      process.stdout.write(JSON.stringify({ id: m.id, result: {} }) + "\\n");',
      '      ALL.forEach((name, k) => process.stdout.write(',
      '        JSON.stringify({ id: 9000 + k, method: name, params: {} }) + "\\n"));',
      '      continue;',
      '    }',
      '    if (typeof m.id === "number" && m.id >= 9000) {',
      '      process.stderr.write("REPLY " + ALL[m.id - 9000] + " " + line + "\\n");',
      '    }',
      '  }',
      '});',
    ].join('\n'));

    // 탐침은 **자식 프로세스**에서 돈다. 부모에서 동기 대기하면 이벤트 루프가 막혀
    //   자식의 stdout/stderr 가 도착하지 못한다(실측 — 처음 판이 그래서 빈손이었다).
    fs.writeFileSync(path.join(box, 'probe.js'), [
      'const ROOT = process.argv[2], BOX = process.argv[3];',
      'const ALL = ' + JSON.stringify(ALL) + ';',
      'const path = require("path");',
      'const { CodexRPC } = require(path.join(ROOT, ".claude", "hooks", "_codex-rpc.js"));',
      'const announced = [];',
      'const rpc = new CodexRPC(process.execPath, { cwd: BOX, timeout: 15000,',
      '  onEvent: e => { if (e && e.method === "harness/approvalRequired" && e.params) announced.push(e.params.method); } });',
      'rpc.initialize().catch(() => {});',
      'const replies = {};',
      'const collect = () => {',
      '  for (const line of String(rpc.stderr).split("\\n")) {',
      '    const m = /^REPLY (\\S+) (.+)$/.exec(line);',
      '    if (m) { try { replies[m[1]] = JSON.parse(m[2]); } catch {} }',
      '  }',
      '  return Object.keys(replies).length;',
      '};',
      'const deadline = Date.now() + 25000;',
      'const tick = setInterval(() => {',
      '  if (collect() >= ALL.length || Date.now() > deadline) {',
      '    clearInterval(tick);',
      '    try { rpc.close(); } catch {}',
      '    process.stdout.write("PROBE " + JSON.stringify({ replies, announced }) + "\\n");',
      '    process.exit(0);',
      '  }',
      '}, 120);',
    ].join('\n'));

    const r = spawnSync(process.execPath, [path.join(box, 'probe.js'), ROOT, box],
      { encoding: 'utf8', timeout: 60000 });
    const line = String(r.stdout || '').split('\n').find(l => l.startsWith('PROBE '));
    if (line) probe = { ...probe, ...JSON.parse(line.slice(6)) };
    else probe.error = (String(r.stderr || '') + String(r.stdout || '')).slice(0, 300);
  } finally {
    try { fs.rmSync(box, { recursive: true, force: true }); } catch { /* 임시 */ }
  }

  const replies = probe.replies || {};
  check('가짜 app-server 와 핸드셰이크했다 — 전선을 실제로 봤다',
    Object.keys(replies).length > 0, probe.error || '응답 0건');
  check('서버 요청 ' + ALL.length + '종 전부에 답했다',
    Object.keys(replies).length === ALL.length, '받은 것: ' + Object.keys(replies).join(', '));

  const bad = judge(replies);
  check('승인 계열 7종에 규격 result 로 답한다 (계열별 값)', bad.length === 0, bad.join(' | '));

  const capBad = judgeCapability(replies);
  check('대조군: 능력 요청 3종은 여전히 JSON-RPC error 로 거절한다', capBad.length === 0, capBad.join(', '));
  const auth = replies['account/chatgptAuthTokens/refresh'] || {};
  check('대조군: 인증 토큰 갱신에 result 를 보내지 않는다',
    !!auth.error && auth.result === undefined, JSON.stringify(auth));

  const announced = new Set(probe.announced || []);
  check('대조군: 전부 harness/approvalRequired 를 발화한다 — 턴이 조용히 계속되지 않는다',
    ALL.every(m => announced.has(m)), ALL.filter(m => !announced.has(m)).join(', '));

  // 반증 ① 예전 동작(전부 error)에서는 판정이 거짓이 된다.
  const allError = {};
  for (const m of ALL) allError[m] = { id: 1, error: { code: -32600, message: 'x' } };
  check('반증: 전부 error 로 답하는 사본은 판정이 거짓이다',
    judge(allError).length === Object.keys(EXPECT).length, judge(allError).join(' | ').slice(0, 160));

  // 반증 ② "전부 cancel" 사본은 **5종**에서 거짓이 된다 — 레거시 2 · 권한 · 입력 · MCP elicitation.
  //    `{decision:"cancel"}` 이 맞는 것은 commandExecution 과 fileChange **둘뿐**이고,
  //    이것이 이 사이클이 고친 바로 그 오해다.
  const allCancel = {};
  for (const m of ALL) allCancel[m] = { id: 1, result: { decision: 'cancel' } };
  const cancelBad = judge(allCancel);
  check('반증: 전부 cancel 로 답하는 사본은 5종(레거시 2 · 권한 · 입력 · elicitation)에서 거짓이다',
    cancelBad.length === 5, cancelBad.join(' | '));

  // 실측 근거가 코드에 남는다 — [experimental] 이라 이름이 또 바뀔 수 있고,
  //   바뀌면 "모르는 메서드 → error" 로 조용히 후퇴한다. 그 후퇴를 감지할 기준점이 필요하다.
  const rpcSrc = fs.readFileSync(path.join(ROOT, '.claude', 'hooks', '_codex-rpc.js'), 'utf8');
  check('어느 빌드에서 뽑았는지 코드에 남는다', /0\.154\.0-alpha\.6\.2/.test(rpcSrc), '');
  check('다시 뽑는 명령이 코드에 남는다', /generate-json-schema/.test(rpcSrc), '');
  check('대조군: 허용 값을 코드에 두지 않는다 — 동의를 합성할 길 자체를 만들지 않는다',
    !/accept|approved/.test(rpcSrc),
    (rpcSrc.match(/[^\n]*(accept|approved)[^\n]*/g) || []).slice(0, 2).join(' | '));
  // ── Loop A 반영 3건 ────────────────────────────────────────────────
  //   두 렌즈(정합·보안)가 같은 지적에 수렴했다: 표가 동결 없이 export 돼 있었다.
  //   `const` 는 재할당만 막지 내용을 막지 않는다.
  {
    const rpc = require(path.join(ROOT, '.claude', 'hooks', '_codex-rpc.js'));
    check('거절 표가 동결돼 있다 — 같은 프로세스에서 허용으로 바꿀 수 없다',
      Object.isFrozen(rpc.DENY_RESULT)
      && Object.values(rpc.DENY_RESULT).every(v => Object.isFrozen(v)),
      '표: ' + Object.isFrozen(rpc.DENY_RESULT));

    // 반증: 동결이 실제로 쓰기를 막는가. strict mode 면 throw, 아니면 조용히 무시된다 —
    //   어느 쪽이든 **값이 바뀌지 않는 것**이 판정 대상이다.
    const before = JSON.stringify(rpc.DENY_RESULT['item/permissions/requestApproval']);
    try { rpc.DENY_RESULT['item/permissions/requestApproval'] = { permissions: { network: { enabled: true } } }; } catch { /* strict */ }
    check('반증: 표를 허용으로 바꾸려 해도 값이 변하지 않는다',
      JSON.stringify(rpc.DENY_RESULT['item/permissions/requestApproval']) === before,
      JSON.stringify(rpc.DENY_RESULT['item/permissions/requestApproval']));

    // 절대 답하면 안 되는 것은 **적재 시점에** 막힌다. 테스트가 이미 지키지만
    //   이 저장소의 전수 스위트는 461초라 매 커밋마다 돌지 않는다 — 벽은 코드가 도는 때 선다.
    const rpcSrcGuard = fs.readFileSync(path.join(ROOT, '.claude', 'hooks', '_codex-rpc.js'), 'utf8');
    check('인증 토큰 갱신이 표에 들어오면 적재가 실패한다 (런타임 자기검증)',
      /NEVER_ANSWER/.test(rpcSrcGuard) && /CODEX_DENY_TABLE_INVALID/.test(rpcSrcGuard)
      && /account\/chatgptAuthTokens\/refresh/.test(rpcSrcGuard), '');

    // 승인 이름이 알림(id 없음)으로 오면 답할 곳이 없다. 그래도 발화는 한다 —
    //   없으면 턴이 조용히 계속된다.
    check('승인 이름이 알림으로 와도 발화한다 — 침묵하지 않는다',
      /asNotification/.test(rpcSrcGuard), '');
  }
}

console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
