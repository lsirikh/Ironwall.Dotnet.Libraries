// tests/unit/test_orphan_guard_install.js
// orphan-guard 동봉 회귀 테스트 — PRD orphan-gitlink-protection §4 후속
//
// 검증 항목:
//   OG-01: 신규 설치 → orphan-guard-check.js 가 UserPromptSubmit·Stop 에 등록됨
//   OG-02: 신규 설치 → orphan-guard.js / orphan-guard-check.js 파일 동봉됨
//   OG-03: 재설치(회귀) → 수동 등록된 orphan-guard-check 가 삭제되지 않음
//   OG-04: 재설치 → 커스텀 timeout(기본값보다 큰 값) 스크립트별 보존
//   OG-05: 재설치 → 사용자(비하네스) 훅 보존
//   OG-06: .gitignore — 매니페스트만 추적(.claude/* + !orphan-guard.json)

'use strict';

const { spawnSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const os = require('os');

const INSTALL_JS = path.resolve(__dirname, '../../install.js');
const SCRIPT_DIR = path.dirname(INSTALL_JS);
const TEMP_BASE = path.join(os.tmpdir(), `skill-set-orphan-guard-${Date.now()}`);

let passed = 0;
let failed = 0;

function check(name, condition, detail = '') {
  if (condition) { console.log(`    ✅ ${name}`); passed++; }
  else { console.log(`    ❌ ${name}${detail ? ' — ' + detail : ''}`); failed++; }
}

function run(args) {
  return spawnSync('node', [INSTALL_JS, ...args], {
    cwd: SCRIPT_DIR, encoding: 'utf8', timeout: 60000, env: process.env,
  });
}

function rmRecursive(p) {
  if (!fs.existsSync(p)) return;
  if (fs.statSync(p).isDirectory()) {
    for (const f of fs.readdirSync(p)) rmRecursive(path.join(p, f));
    fs.rmdirSync(p);
  } else { fs.unlinkSync(p); }
}

// 이벤트에서 특정 스크립트의 timeout 추출 (없으면 null)
function timeoutOf(settings, event, script) {
  const arr = settings.hooks?.[event] || [];
  for (const e of arr) {
    for (const h of (e.hooks || [])) {
      if (h.command && h.command.includes(script)) return h.timeout;
    }
  }
  return null;
}
function hasScript(settings, event, script) { return timeoutOf(settings, event, script) !== null; }

function main() {
  fs.mkdirSync(TEMP_BASE, { recursive: true });
  const TGT = path.join(TEMP_BASE, 'target');

  try {
    // ── 신규 설치 ────────────────────────────────────────────────
    console.log('[OG] 신규 설치');
    const r1 = run(['--target', TGT, '--yes']);
    check('OG-00 신규 설치 성공 (exit 0)', r1.status === 0, `exit=${r1.status}`);

    const settingsPath = path.join(TGT, '.claude', 'settings.json');
    let s = JSON.parse(fs.readFileSync(settingsPath, 'utf8'));

    check('OG-01 UserPromptSubmit 에 orphan-guard-check 등록',
      hasScript(s, 'UserPromptSubmit', 'orphan-guard-check.js'));
    check('OG-01 Stop 에 orphan-guard-check 등록',
      hasScript(s, 'Stop', 'orphan-guard-check.js'));

    const hooksDir = path.join(TGT, '.claude', 'hooks');
    check('OG-02 orphan-guard.js 동봉', fs.existsSync(path.join(hooksDir, 'orphan-guard.js')));
    check('OG-02 orphan-guard-check.js 동봉', fs.existsSync(path.join(hooksDir, 'orphan-guard-check.js')));

    // ── 사용자 커스텀 시드 후 재설치 ──────────────────────────────
    console.log('[OG] 커스텀 timeout/사용자 훅 시드 후 재설치');
    s.hooks.UserPromptSubmit = [
      { hooks: [{ type: 'command', command: 'node .claude/hooks/session-gate.js', timeout: 10 }] },
      { hooks: [{ type: 'command', command: 'node .claude/hooks/orphan-guard-check.js', timeout: 10 }] },
      { hooks: [{ type: 'command', command: 'node /my/custom/user-hook.js', timeout: 7 }] },
    ];
    s.hooks.PreToolUse = [
      { matcher: 'Bash', hooks: [{ type: 'command', command: 'node .claude/hooks/pre-tool-gate.js', timeout: 12 }] },
    ];
    fs.writeFileSync(settingsPath, JSON.stringify(s, null, 2));

    const r2 = run(['--target', TGT, '--yes']);
    check('OG-03 재설치 성공 (exit 0)', r2.status === 0, `exit=${r2.status}`);

    s = JSON.parse(fs.readFileSync(settingsPath, 'utf8'));
    // 회귀: orphan-guard-check 가 살아있어야 함 (과거엔 이벤트당 1개만 남기고 삭제됨)
    check('OG-03 재설치 후 orphan-guard-check 생존 (UserPromptSubmit)',
      hasScript(s, 'UserPromptSubmit', 'orphan-guard-check.js'));
    check('OG-03 재설치 후 orphan-guard-check 생존 (Stop)',
      hasScript(s, 'Stop', 'orphan-guard-check.js'));
    // 커스텀 timeout 보존(기본 5/8 보다 큰 10/12)
    check('OG-04 session-gate 커스텀 timeout=10 보존',
      timeoutOf(s, 'UserPromptSubmit', 'session-gate.js') === 10,
      `actual=${timeoutOf(s, 'UserPromptSubmit', 'session-gate.js')}`);
    check('OG-04 pre-tool-gate 커스텀 timeout=12 보존',
      timeoutOf(s, 'PreToolUse', 'pre-tool-gate.js') === 12,
      `actual=${timeoutOf(s, 'PreToolUse', 'pre-tool-gate.js')}`);
    // 사용자 훅 보존
    const upsCmds = (s.hooks.UserPromptSubmit || []).flatMap(e => (e.hooks || []).map(h => h.command));
    check('OG-05 사용자 훅 보존', upsCmds.some(c => c.includes('/my/custom/user-hook.js')));

    // ── .gitignore 매니페스트 추적 라인 ───────────────────────────
    console.log('[OG] .gitignore 매니페스트 추적 라인');
    const gi = fs.readFileSync(path.join(TGT, '.gitignore'), 'utf8');
    check('OG-06 .claude/* 패턴(디렉터리째 무시 아님)', /^\.claude\/\*\s*$/m.test(gi));
    check('OG-06 !.claude/orphan-guard.json negation 존재',
      gi.includes('!.claude/orphan-guard.json'));
  } finally {
    rmRecursive(TEMP_BASE);
  }

  console.log('\n═══════════════════════════════════════════════════════');
  console.log(`  결과: ${passed} PASS / ${failed} FAIL / ${passed + failed} 총계`);
  console.log('═══════════════════════════════════════════════════════');
  process.exit(failed === 0 ? 0 : 1);
}

main();
