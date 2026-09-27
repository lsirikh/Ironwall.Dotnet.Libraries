// tests/unit/test_hook_registration.js
// 훅 등록 정합 — charter harness-v3 / N-01 (FR-15·FR-19 · D9·D10)
//
// 왜 이 파일이 존재하는가:
//   훅 등록 정보가 다섯 곳에 흩어져 있고 서로를 검증하지 않았다 —
//   matcher·timeout 은 .claude/settings.json · install.js hookDefs · build-package README 예시
//   세 곳에, 훅 **파일 목록**은 build-package INCLUDE · install.js hookFiles 두 곳에.
//   실제로 드리프트가 있었다: 빌드 템플릿의 session-gate timeout 이 5(실제 15)였고
//   SubagentStop 항목이 통째로 빠져 있었다. 한 곳만 고치면 여기가 빨개진다.
//
//   matcher 문법도 여기서 고정한다. Claude Code 는 **두 모드**로 판정한다:
//     · 메타문자가 하나도 없으면 → `|` 로 쪼갠 **완전 문자열 목록**
//     · 하나라도 있으면 → `new RegExp(matcher)` 의 **앵커 없는 부분 매칭**
//   후자로 넘어가면 `Edit` 이 `MultiEdit`·`NotebookEdit` 을, `Bash` 가 `BashOutput` 을 잡는다.
//   그래서 이 하네스는 메타문자를 쓰지 않고 도구를 열거한다. 아래 REG-04 가 그것을 지킨다.

'use strict';

const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..', '..');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).slice(0, 240) : ''}`); }
}

const settings = JSON.parse(fs.readFileSync(path.join(ROOT, '.claude', 'settings.json'), 'utf8'));
const installSrc = fs.readFileSync(path.join(ROOT, 'install.js'), 'utf8');
const buildSrc = fs.readFileSync(path.join(ROOT, 'scripts', 'build-package.js'), 'utf8');

console.log('\n═══ REG: 훅 등록 정합 ═══');

// ── REG-01: settings.json 의 (이벤트, 스크립트, matcher, timeout) ────
console.log('\n[REG-01] should_declare_every_harness_hook_in_settings');
// [v16/N-01] SessionStart·SessionEnd 는 Claude Code 의 공식 세션 생명주기 이벤트다(설치본 2.1.248).
//   무정지 목표(최소 6시간~최대 3일)의 전제이기도 하다 — 세션이 죽거나 압축돼도 resume·compact
//   matcher 로 다시 붙어야 이어받기가 성립한다. 매 프롬프트 lazy join 으로는 그 시점을 알 수 없다.
const EXPECT_EVENTS = ['SessionStart', 'SessionEnd', 'UserPromptSubmit', 'PreToolUse', 'PostToolUse', 'PreCompact', 'Stop', 'SubagentStop'];
const live = {};
for (const [event, arr] of Object.entries(settings.hooks || {})) {
  for (const entry of arr) {
    for (const h of entry.hooks || []) {
      const m = (h.command || '').match(/\.claude\/hooks\/([A-Za-z0-9_.-]+\.js)/);
      if (!m) continue;
      // [v3.6/N-03] 이벤트당 **배열**이다. 단일 슬롯이면 PostToolUse 두 번째 엔트리(post-bash-observe)가
      //   REG 검증에서 조용히 빠진다 — 등록 드리프트를 막으려 만든 테스트가 새 훅에 눈을 감는다.
      (live[event] = live[event] || []).push({ script: m[1], matcher: entry.matcher || null, timeout: h.timeout });
    }
  }
}
for (const e of EXPECT_EVENTS) check(`settings.json 에 ${e} 등록`, !!live[e], Object.keys(live).join(','));

// [v16/N-01] SessionStart 는 새 창뿐 아니라 **재개·압축**에서도 붙어야 한다. 그 matcher 가 빠지면
//   긴 작업이 압축을 한 번 넘길 때마다 세션이 화면에서 사라진다.
{
  const entries = settings.hooks?.SessionStart || [];
  const starts = entries.map(e => String(e.matcher ?? '*')).join('|');
  for (const m of ['startup', 'resume', 'compact']) {
    // matcher 를 비우면 모든 source 를 받는다. 등록이 아예 없으면 공허하게 통과하지 않는다.
    check(`SessionStart 가 ${m} 를 받는다`, entries.length > 0 && (starts.includes('*') || starts.includes(m)), starts || '(등록 없음)');
  }
}

// [v16/N-01 · Loop A] 훅은 등록하자마자 죽는다. 훅 자신의 pid 를 실으면 세션은 등록 즉시
//   'owner_alive=false' 가 되어 화면에 "연결 끊김"으로 뜨고, 대시보드의 복구 버튼이 살아 있는
//   창을 죽인다. 실어야 할 것은 훅을 띄운 **부모(창)** 의 pid 다.
//   그리고 SessionStart 는 compact·resume 마다 다시 온다 — 그때마다 새 세션을 만들면
//   활성 세션 한도(10)가 압축 열 번에 고갈되고, 그 뒤로는 관리형 실행까지 굶는다.
{
  const { spawnSync } = require('node:child_process');
  const os = require('node:os');
  const { execFileSync } = require('node:child_process');
  const box = fs.mkdtempSync(path.join(os.tmpdir(), 'hookses-'));
  try {
    execFileSync('git', ['init', '-q'], { cwd: box, windowsHide: true });
    const gate = path.join(ROOT, '.claude', 'hooks', 'session-gate.js');
    const fire = (payload) => spawnSync(process.execPath, [gate], {
      input: JSON.stringify(payload), encoding: 'utf8', windowsHide: true, timeout: 60000,
      // [v16/N-28] 이 테스트는 **창**을 흉내 낸다. 창의 pid 는 CLAUDE_PID 로 선언된다 —
      //   예전에는 훅의 process.ppid 가 우연히 같았지만, 실제 런타임에서 ppid 는 매 호출
      //   새로 뜨는 임시 셸이라 즉시 죽는다(실측: 자격 파일 6개 전부 pid 사망).
      env: { ...process.env, CLAUDE_PROJECT_DIR: box, _HARNESS_TEST_RUN: 'hookses', CLAUDE_PID: String(process.pid) },
    });
    const sessions = () => {
      try {
        const { repository } = require(path.join(ROOT, '.claude', 'hooks', '_harness-store'));
        const repo = repository(box);
        try { return repo.store.list('session/').map(r => r.value); } finally { repo.store.close(); }
      } catch { return []; }
    };
    fire({ hook_event_name: 'SessionStart', session_id: 'pid-probe', source: 'startup', cwd: box });
    const first = sessions().filter(s => s.status === 'active');
    check('SessionStart 는 훅이 아니라 창의 pid 를 싣는다', first.length === 1 && first[0].pid === process.pid,
      first.length ? `pid=${first[0].pid} 기대=${process.pid}` : '세션 없음');

    fire({ hook_event_name: 'SessionStart', session_id: 'pid-probe', source: 'compact', cwd: box });
    fire({ hook_event_name: 'SessionStart', session_id: 'pid-probe', source: 'resume', cwd: box });
    const after = sessions().filter(s => s.status === 'active');
    check('압축·재개가 활성 세션을 늘리지 않는다', after.length === 1, `활성 ${after.length}개`);

    fire({ hook_event_name: 'SessionStart', source: 'startup', cwd: box });
    check('session_id 가 없어도 등록된다', sessions().filter(s => s.status === 'active').length >= 1,
      `활성 ${sessions().filter(s => s.status === 'active').length}개`);
  } finally { try { fs.rmSync(box, { recursive: true, force: true }); } catch {} }
}

// [v16/N-01] 등록은 거들 뿐이다 — 스토어가 없거나 잠겨 있어도 훅은 통과해야 한다.
//   무정지의 최소 조건: 관측 장치가 죽어도 사용자 작업은 멈추지 않는다.
{
  const { spawnSync } = require('node:child_process');
  const os = require('node:os');
  const sandbox = fs.mkdtempSync(path.join(os.tmpdir(), 'hookreg-'));
  try {
    for (const [event, script] of [['SessionStart', 'session-gate.js'], ['SessionEnd', 'session-gate.js']]) {
      const file = path.join(ROOT, '.claude', 'hooks', script);
      const r = spawnSync(process.execPath, [file], {
        input: JSON.stringify({ hook_event_name: event, session_id: 'reg-probe', source: 'startup', cwd: sandbox }),
        encoding: 'utf8', windowsHide: true, timeout: 60000,
        env: { ...process.env, CLAUDE_PROJECT_DIR: sandbox, _HARNESS_TEST_RUN: 'hookreg' },
      });
      check(`${event} 훅이 스토어 없이도 통과한다`, r.status === 0, `exit=${r.status} ${String(r.stderr || '').slice(0, 160)}`);
    }
  } finally { try { fs.rmSync(sandbox, { recursive: true, force: true }); } catch {} }
}

// ── REG-02: install.js hookDefs 가 settings.json 과 같다 ────────────
console.log('\n[REG-02] should_match_installer_hook_definitions');
for (const [event, cfgs] of Object.entries(live)) for (const cfg of cfgs) {
  // install.js 는 "$CLAUDE_PROJECT_DIR/.claude/hooks/<script>" 형태로 같은 스크립트를 등록한다
  check(`install.js 가 ${cfg.script} 를 등록`, installSrc.includes(cfg.script), null);
  const tRe = new RegExp(cfg.script.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '"[^\\n]*timeout:\\s*(\\d+)');
  const tm = installSrc.match(tRe);
  check(`install.js 의 ${cfg.script} timeout = ${cfg.timeout}`, !!tm && Number(tm[1]) === cfg.timeout,
    tm ? 'install=' + tm[1] + ' settings=' + cfg.timeout : 'timeout 을 찾지 못함');
  if (cfg.matcher) {
    check(`install.js 의 ${event} matcher 가 문자 단위로 동일`, installSrc.includes(cfg.matcher),
      'settings: ' + cfg.matcher);
  }
}

// ── REG-03: build-package README 예시가 같은 값이다 ────────────────
console.log('\n[REG-03] should_match_packaged_readme_example');
for (const [event, cfgs] of Object.entries(live)) for (const cfg of cfgs) {
  check(`README 예시에 ${cfg.script}`, buildSrc.includes(cfg.script), null);
  if (cfg.matcher) check(`README 예시의 ${event} matcher 동일`, buildSrc.includes(cfg.matcher), cfg.matcher);
}
{
  const m = buildSrc.match(/session-gate\.js",\s*"timeout":\s*(\d+)/);
  check('README 예시의 session-gate timeout 이 실제와 같다',
    !!m && Number(m[1]) === ((live.UserPromptSubmit || [])[0] || {}).timeout, m ? 'readme=' + m[1] : '못 찾음');
}

// ── REG-04: matcher 는 메타문자를 쓰지 않는다 (완전 문자열 모드 유지) ─
console.log('\n[REG-04] should_keep_matcher_in_exact_string_mode');
const EXACT_MODE = /^[a-zA-Z0-9_|]+$/;
for (const [event, cfgs] of Object.entries(live)) for (const cfg of cfgs) {
  if (!cfg.matcher) continue;
  check(`${event} matcher 에 정규식 메타문자 없음`, EXACT_MODE.test(cfg.matcher), cfg.matcher);
}

// ── REG-05: 의도한 도구가 매칭되고, 읽기 전용 도구는 매칭되지 않는다 ─
//   Claude Code 의 판정 로직을 그대로 재현한다(위 주석의 2모드).
console.log('\n[REG-05] should_match_intended_tools_only');
function matches(matcher, tool) {
  if (!matcher || matcher === '*') return true;
  if (EXACT_MODE.test(matcher)) return matcher.split('|').map(s => s.trim()).includes(tool);
  try { return new RegExp(matcher).test(tool); } catch { return false; }
}
const preM = ((live.PreToolUse || [])[0] || {}).matcher;
for (const t of ['Write', 'Edit', 'MultiEdit', 'NotebookEdit', 'Bash', 'PowerShell',
  'mcp__filesystem__write_file', 'mcp__git__git_reset', 'mcp__git__git_checkout']) {
  check(`PreToolUse 가 ${t} 를 잡는다`, matches(preM, t), preM);
}
for (const t of ['Read', 'Grep', 'Glob', 'mcp__filesystem__read_file', 'mcp__git__git_log', 'BashOutput']) {
  check(`PreToolUse 가 ${t} 를 잡지 않는다`, !matches(preM, t), preM);
}
const postM = ((live.PostToolUse || [])[0] || {}).matcher;
for (const t of ['Write', 'Edit', 'MultiEdit', 'NotebookEdit']) {
  check(`PostToolUse 가 ${t} 를 잡는다`, matches(postM, t), postM);
}
check('PostToolUse 가 Read 를 잡지 않는다', !matches(postM, 'Read'), postM);

// ── REG-06: 설치 검증 목록은 **파생**이어야 한다 (v3.6/N-10/FR-07·FR-08) ──
//   예전에는 `install.js` 의 손으로 적은 `hookFiles` 배열을 정규식으로 긁어 대조했다.
//   그 목록은 16개짜리였고 `_charter.js`·`_signals.js`·`_impact.js`·`_git.js`·
//   `_plan-graph.js`·`_session-*.js` 가 빠져 있었다 — **그것들이 사라져도 "Hook: 16/16" 이 떴다.**
//   목록을 파생으로 바꿨으므로, 여기서는 "손으로 적지 않았는가" 와 "등록된 훅이 배포되는가" 를 본다.
console.log('\n[REG-06] should_derive_install_verification_from_shipped_hooks');
{
  check('hookFiles 를 손으로 나열하지 않는다',
    !/const hookFiles = \[\s*'session-gate\.js'/.test(installSrc),
    (installSrc.match(/const hookFiles[^\n]*/g) || []).join(' | ').slice(0, 160));
  check('hookFiles 가 배포 트리에서 파생된다',
    /const hookFiles = \(\(\) =>[\s\S]{0,400}readdirSync/.test(installSrc), null);

  // settings 가 등록한 훅은 반드시 배포돼야 한다 — 없으면 설치본에서 매 이벤트마다 조용히 죽는다
  for (const cfg of Object.values(live).flat()) {
    check(`build-package 가 ${cfg.script} 를 포함`, buildSrc.includes(cfg.script), null);
    check(`${cfg.script} 가 저장소에 실재`, fs.existsSync(path.join(ROOT, '.claude', 'hooks', cfg.script)), null);
  }
}

// ── REG-07: 등록 정의를 **구조로** 비교한다 (v3.6/N-10/FR-07) ────────
//   N-08·N-10 조사가 같은 것을 지적했다: 이 파일의 단언 다수가 `installSrc.includes()` 라서
//   **command 문자열 형식을 한 번도 비교하지 않았다.** 그래서 저장소 settings.json 은
//   `node .claude/hooks/X.js`(상대), install.js 는 `node "$CLAUDE_PROJECT_DIR/…"` 로
//   갈라져 있었는데 97/97 이 통과했다. `includes()` 는 주석에만 이름이 있어도 통과한다.
console.log('\n[REG-07] should_compare_hook_definitions_structurally');
{
  // install.js 의 hookDefs 를 파싱한다 — 이벤트 → [{script, timeout, matcher}]
  const defsBlock = (installSrc.match(/const hookDefs = \{([\s\S]*?)\n  \};/) || ['', ''])[1];
  check('install.js 에서 hookDefs 를 파싱한다', defsBlock.length > 200, String(defsBlock.length));

  const parsed = {};
  let curEvent = null;
  for (const line of defsBlock.split('\n')) {
    const ev = line.match(/^\s{4}(\w+):\s*\[/);
    if (ev) { curEvent = ev[1]; parsed[curEvent] = []; continue; }
    const cmd = line.match(/command:\s*'node "\$CLAUDE_PROJECT_DIR\/\.claude\/hooks\/([\w.-]+\.js)"'/);
    if (cmd && curEvent) {
      const to = (line.match(/timeout:\s*(\d+)/) || [])[1];
      parsed[curEvent].push({ script: cmd[1], timeout: to ? Number(to) : null });
    }
  }
  const evs = Object.keys(parsed).filter(e => parsed[e].length);
  check('이벤트가 하나 이상 파싱된다', evs.length >= 5, evs.join(','));

  // 저장소 settings.json 과 **스크립트·타임아웃**이 같아야 한다.
  //   경로 형식(`$CLAUDE_PROJECT_DIR` vs 상대)은 의도적으로 다르다 — 설치본은 cwd 를
  //   보장할 수 없고 저장소는 루트에서 돈다. 그래서 형식이 아니라 **가리키는 대상**을 본다.
  for (const ev of evs) {
    const mine = parsed[ev].map(d => `${d.script}@${d.timeout}`).sort().join(',');
    const theirs = (live[ev] || []).map(d => `${d.script}@${d.timeout}`).sort().join(',');
    check(`${ev} 의 훅·타임아웃이 일치한다`, mine === theirs, `install=${mine} / settings=${theirs}`);
  }
  // 저장소에만 있고 install 에 없는 이벤트가 있으면 설치본에서 그 훅이 통째로 빠진다
  for (const ev of Object.keys(live)) {
    check(`install.js 가 ${ev} 를 등록한다`, !!parsed[ev] && parsed[ev].length > 0, Object.keys(parsed).join(','));
  }
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
