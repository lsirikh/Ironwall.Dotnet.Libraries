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

    // [v3/FR-2.4] 정책 변경: orphan-guard-check 자동 등록 폐지.
    //   매니페스트(.claude/orphan-guard.json)가 없는 프로젝트에서는 확정적 no-op이면서
    //   턴당 ~152ms(UserPromptSubmit+Stop 2회)를 소모했다. 파일 자체는 계속 동봉되므로
    //   필요한 프로젝트는 settings.json에 직접 등록해 쓸 수 있다.
    check('OG-01 UserPromptSubmit 에 orphan-guard-check 미등록(v3 정책)',
      !hasScript(s, 'UserPromptSubmit', 'orphan-guard-check.js'));
    check('OG-01 Stop 에 orphan-guard-check 미등록(v3 정책)',
      !hasScript(s, 'Stop', 'orphan-guard-check.js'));

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
    // [v3/FR-2.4] orphan-guard-check 는 더 이상 하네스 정의가 아니므로 재설치 시
    //   settings 에서 정리된다. 이는 회귀가 아니라 의도된 정책이다.
    check('OG-03 재설치 후 orphan-guard-check 제거됨 (UserPromptSubmit)',
      !hasScript(s, 'UserPromptSubmit', 'orphan-guard-check.js'));
    check('OG-03 재설치 후 orphan-guard-check 제거됨 (Stop)',
      !hasScript(s, 'Stop', 'orphan-guard-check.js'));
    // [v3/FR-2.4] session-gate 기본 timeout 이 5 → 15 로 상향됐다.
    //   보존 규칙은 `(기존 > 기본) ? 기존 : 기본` 이므로 커스텀 10 은 기본 15 보다 작아
    //   15 가 적용된다. 이는 타임아웃 부족으로 주입이 유실되던 문제의 근본 수정이다.
    check('OG-04 session-gate timeout 은 기본 15 이상',
      timeoutOf(s, 'UserPromptSubmit', 'session-gate.js') >= 15,
      `actual=${timeoutOf(s, 'UserPromptSubmit', 'session-gate.js')}`);
    check('OG-04 pre-tool-gate 커스텀 timeout=12 보존',
      timeoutOf(s, 'PreToolUse', 'pre-tool-gate.js') === 12,
      `actual=${timeoutOf(s, 'PreToolUse', 'pre-tool-gate.js')}`);
    // 사용자 훅 보존
    const upsCmds = (s.hooks.UserPromptSubmit || []).flatMap(e => (e.hooks || []).map(h => h.command));
    check('OG-05 사용자 훅 보존', upsCmds.some(c => c.includes('/my/custom/user-hook.js')));

    // ── .gitignore: 무엇이 추적되고 무엇이 무시되는가 (v5/N-05) ────
    //   예전에는 `.gitignore` 의 **문자열**을 봤다 — `.claude/*` 줄이 있는가,
    //   `!.claude/orphan-guard.json` negation 이 있는가. 그것은 규칙의 **표기**이지
    //   결과가 아니다. 그리고 그 표기가 정확히 결함이었다: `.claude/*` 는 훅·스킬·규칙
    //   까지 무시했고, 다운스트림은 `advance-phase.js` 의 치명 버그를 고친 뒤
    //   `git status` 가 깨끗하게 나오는 것을 겪었다. 고친 것이 재설치로 조용히 사라진다.
    //
    //   그래서 **실제 git 에게 묻는다.** 부정 패턴은 상위가 무시되면 무력하다는 것처럼,
    //   gitignore 의 의미는 표기만 봐서는 알 수 없다.
    console.log('[OG] .gitignore — 로직은 추적, 부산물은 무시 (실제 git 판정)');
    const gi = fs.readFileSync(path.join(TGT, '.gitignore'), 'utf8');
    check('OG-06 .claude/* 통째 무시 줄이 없다', !/^\.claude\/\*\s*$/m.test(gi),
      (gi.match(/^\.claude\/[^\n]*/gm) || []).join(' | ').slice(0, 200));

    const gitInit = spawnSync('git', ['init', '-q'], { cwd: TGT, encoding: 'utf8' });
    check('OG-06 git 저장소로 판정 가능', gitInit.status === 0, String(gitInit.stderr || '').slice(0, 200));
    if (gitInit.status === 0) {
      const ignored = rel => {
        const abs = path.join(TGT, rel);
        fs.mkdirSync(path.dirname(abs), { recursive: true });
        if (!fs.existsSync(abs)) fs.writeFileSync(abs, '');
        return spawnSync('git', ['check-ignore', '-q', '--', rel], { cwd: TGT, encoding: 'utf8' }).status === 0;
      };
      // 로직 — 부산물이 아니다. 고치면 커밋되어야 하고, 재설치가 diff 로 드러나야 한다.
      for (const rel of [
        '.claude/hooks/advance-phase.js',
        '.claude/skills/process/SKILL.md',
        '.claude/rules/common/security.md',
        '.claude/settings.json',
        '.claude/orphan-guard.json',
      ]) check(`OG-06 추적 가능: ${rel}`, !ignored(rel));
      // 부산물 — 이것들은 계속 무시된다. 완화가 여기까지 오면 런타임 상태가 커밋된다.
      for (const rel of [
        '.claude/.branch-x/pipeline-state.json',
        '.claude/install-logs/a.log',
        '.claude/settings.local.json',
        '.claude/mailbox/x.json',
      ]) check(`OG-06 무시됨: ${rel}`, ignored(rel));
    }
  } finally {
    rmRecursive(TEMP_BASE);
  }

  console.log('\n═══════════════════════════════════════════════════════');
  console.log(`  결과: ${passed} PASS / ${failed} FAIL / ${passed + failed} 총계`);
  console.log('═══════════════════════════════════════════════════════');
  process.exit(failed === 0 ? 0 : 1);
}

main();
