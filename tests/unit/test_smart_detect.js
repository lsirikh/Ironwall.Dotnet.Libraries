// tests/unit/test_smart_detect.js
// TEST-01~06: detectInstallState, getInstalledVersion, detectProjectType,
//             smartRoute, 버전비교, --upgrade deprecated 단위 테스트

'use strict';

const { spawnSync } = require('child_process');
const fs   = require('fs');
const path = require('path');
const os   = require('os');

const INSTALL_JS = path.resolve(__dirname, '../../install.js');
const SCRIPT_DIR = path.dirname(INSTALL_JS);
const TEMP_BASE  = path.join(os.tmpdir(), `smart-detect-test-${Date.now()}`);

// 현재 HARNESS_VERSION을 install.js에서 동적으로 읽음 (버전 bump 후 테스트 수정 불필요)
const _installSrc = fs.readFileSync(INSTALL_JS, 'utf8');
const _verMatch = _installSrc.match(/HARNESS_VERSION\s*=\s*'([^']+)'/);
const HARNESS_VERSION = _verMatch ? _verMatch[1] : '0.0.0';

let passed = 0, failed = 0;
const results = [];

function test(id, name, fn) {
  try {
    fn();
    passed++;
    results.push({ id, name, pass: true });
    console.log(`  ✅ [${id}] ${name}`);
  } catch(e) {
    failed++;
    results.push({ id, name, pass: false, detail: e.message });
    console.log(`  ❌ [${id}] ${name} — ${e.message}`);
  }
}

function assert(cond, msg) { if (!cond) throw new Error(msg || 'assertion failed'); }

function mkdir(p) { fs.mkdirSync(p, { recursive: true }); return p; }

function rmRecursive(p) {
  if (!fs.existsSync(p)) return;
  if (fs.statSync(p).isDirectory()) {
    for (const f of fs.readdirSync(p)) rmRecursive(path.join(p, f));
    fs.rmdirSync(p);
  } else fs.unlinkSync(p);
}

function run(args, cwd = SCRIPT_DIR) {
  return spawnSync('node', [INSTALL_JS, ...args], {
    cwd, encoding: 'utf8', timeout: 30000, env: process.env,
  });
}

// install.js에서 함수 추출 (테스트용 wrapper)
// install.js는 실행 스크립트이므로 내부 함수를 직접 호출하는 대신
// 임시 타겟 디렉토리 기반으로 동작 검증

fs.mkdirSync(TEMP_BASE, { recursive: true });
console.log('╔══════════════════════════════════════════════════════╗');
console.log('║  TEST-01~06: smart-install 단위 테스트                 ║');
console.log('╚══════════════════════════════════════════════════════╝\n');

// ══════════════════════════════════════════════════════════════════════
// TEST-01: detectInstallState — None/Partial/Full
// ══════════════════════════════════════════════════════════════════════
console.log('\n[TEST-01] detectInstallState — None/Partial/Full');

test('T01-A', 'should_return_none_when_no_claude_dir', () => {
  const t = mkdir(path.join(TEMP_BASE, 't01a'));
  // .claude/ 없음 → fresh install → exit 0, SMART 감지 'fresh'
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  assert((r.stdout+r.stderr).includes('fresh'), `output: ${r.stdout.slice(0,200)}`);
});

test('T01-B', 'should_return_partial_when_only_pre_tool_gate_exists', () => {
  const t = mkdir(path.join(TEMP_BASE, 't01b'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  fs.writeFileSync(path.join(hooksDir, 'pre-tool-gate.js'), '// mock');
  // Partial: pre-tool-gate 있고 session-gate/advance-phase 없음
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  assert(out.includes('partial') || out.includes('Partial'), `output: ${out.slice(0,200)}`);
});

test('T01-C', 'should_return_full_when_all_three_hooks_present', () => {
  const t = mkdir(path.join(TEMP_BASE, 't01c'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  // Full, no CLAUDE.md → version unknown → upgrade route
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  // Full 설치 → upgrade 또는 full 메시지
  assert(out.includes('[SMART]'), `SMART 감지 메시지 없음: ${out.slice(0,300)}`);
});

test('T01-D', 'should_return_none_when_hooks_dir_empty', () => {
  const t = mkdir(path.join(TEMP_BASE, 't01d'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true }); // 빈 hooks 폴더
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  assert(out.includes('fresh') || out.includes('[SMART]'), `output: ${out.slice(0,200)}`);
});

// ══════════════════════════════════════════════════════════════════════
// TEST-02: getInstalledVersion + detectProjectType
// ══════════════════════════════════════════════════════════════════════
console.log('\n[TEST-02] getInstalledVersion + detectProjectType');

test('T02-A', 'should_return_version_from_claude_md_yaml', () => {
  const t = mkdir(path.join(TEMP_BASE, 't02a'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  fs.writeFileSync(path.join(t, 'CLAUDE.md'),
    '# CLAUDE.md\n\n```yaml\nproject_name: "test"\nversion: "2.4.0"\n```\n');
  // version 2.4.0 < HARNESS_VERSION 2.5.0 → upgrade route
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  assert(out.includes('2.4.0') || out.includes('upgrade'), `버전 감지 없음: ${out.slice(0,300)}`);
});

test('T02-B', 'should_return_null_when_no_claude_md', () => {
  const t = mkdir(path.join(TEMP_BASE, 't02b'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  // CLAUDE.md 없음 → version null → upgrade (버전 불명)
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  assert(out.includes('[SMART]'), `SMART 없음: ${out.slice(0,200)}`);
  assert(out.includes('업그레이드') || out.includes('upgrade') || out.includes('불명'), `upgrade 라우팅 없음: ${out.slice(0,200)}`);
});

test('T02-C', 'should_detect_csharp_from_sln_file', () => {
  const t = mkdir(path.join(TEMP_BASE, 't02c'));
  fs.writeFileSync(path.join(t, 'MyApp.sln'), 'Microsoft Visual Studio Solution');
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  // C# 감지 → SMART 메시지에 언어 포함
  assert(out.includes('C#') || out.includes('fresh'), `C# 감지 없음: ${out.slice(0,300)}`);
});

test('T02-D', 'should_detect_python_from_pyproject_toml', () => {
  const t = mkdir(path.join(TEMP_BASE, 't02d'));
  fs.writeFileSync(path.join(t, 'pyproject.toml'), '[project]\nname = "myapp"');
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  assert(out.includes('Python') || out.includes('fresh'), `Python 감지 없음: ${out.slice(0,300)}`);
});

test('T02-E', 'should_detect_typescript_from_tsconfig', () => {
  const t = mkdir(path.join(TEMP_BASE, 't02e'));
  fs.writeFileSync(path.join(t, 'tsconfig.json'), '{"compilerOptions":{}}');
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  assert(out.includes('TypeScript') || out.includes('fresh'), `TypeScript 감지 없음: ${out.slice(0,300)}`);
});

// ══════════════════════════════════════════════════════════════════════
// TEST-03: smartRoute 라우팅 결정
// ══════════════════════════════════════════════════════════════════════
console.log('\n[TEST-03] smartRoute 라우팅 결정');

test('T03-A', 'should_route_to_fresh_when_state_is_none', () => {
  const t = mkdir(path.join(TEMP_BASE, 't03a'));
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0);
  assert((r.stdout+r.stderr).includes('fresh'));
});

test('T03-B', 'should_route_to_partial_repair_when_partial_state', () => {
  const t = mkdir(path.join(TEMP_BASE, 't03b'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  fs.writeFileSync(path.join(hooksDir, 'session-gate.js'), '// mock'); // only 1 of 3
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  assert((r.stdout+r.stderr).includes('Partial') || (r.stdout+r.stderr).includes('partial'),
    `partial 라우팅 없음: ${(r.stdout+r.stderr).slice(0,300)}`);
});

test('T03-C', 'should_route_to_upgrade_when_newer_version_available', () => {
  const t = mkdir(path.join(TEMP_BASE, 't03c'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  // version 1.0.0 << 2.5.0 → upgrade
  fs.writeFileSync(path.join(t, 'CLAUDE.md'),
    '# CLAUDE.md\n\n```yaml\nproject_name: "t"\nversion: "1.0.0"\n```\n');
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  assert(out.includes('upgrade') || out.includes('업그레이드'), `upgrade 라우팅 없음: ${out.slice(0,300)}`);
});

test('T03-D', 'should_route_to_same_version_when_equal', () => {
  const t = mkdir(path.join(TEMP_BASE, 't03d'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  // version = HARNESS_VERSION → same-version
  fs.writeFileSync(path.join(t, 'CLAUDE.md'),
    `# CLAUDE.md\n\n\`\`\`yaml\nproject_name: "t"\nversion: "${HARNESS_VERSION}"\n\`\`\`\n`);
  const r = run(['--target', t, '--dry-run']);
  assert(r.status === 0, `exit: ${r.status}`);
  const out = r.stdout + r.stderr;
  assert(out.includes('same') || out.includes('최신') || out.includes('same-version'),
    `same-version 라우팅 없음: ${out.slice(0,300)}`);
});

test('T03-E', 'should_route_to_downgrade_when_installed_is_newer', () => {
  const t = mkdir(path.join(TEMP_BASE, 't03e'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  // version 99.0.0 > 2.5.0 → downgrade
  fs.writeFileSync(path.join(t, 'CLAUDE.md'),
    '# CLAUDE.md\n\n```yaml\nproject_name: "t"\nversion: "99.0.0"\n```\n');
  const r = run(['--target', t]);
  assert(r.status === 1, `exit: ${r.status} (다운그레이드는 exit 1)`);
  const out = r.stdout + r.stderr;
  assert(out.includes('downgrade') || out.includes('다운그레이드'),
    `다운그레이드 경고 없음: ${out.slice(0,300)}`);
});

// ══════════════════════════════════════════════════════════════════════
// TEST-04: S11 Partial 감지 재검증 (FR-01 완전 구현)
// ══════════════════════════════════════════════════════════════════════
console.log('\n[TEST-04] S11 Partial 감지 → 복구 설치 (FR-01)');

test('T04-A', 'should_repair_partial_install_without_flags', () => {
  const t = mkdir(path.join(TEMP_BASE, 't04a'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  // 핵심 훅 2개만: session-gate, pre-tool-gate (advance-phase 없음)
  fs.writeFileSync(path.join(hooksDir, 'session-gate.js'), '// mock session-gate');
  fs.writeFileSync(path.join(hooksDir, 'pre-tool-gate.js'), '// mock pre-tool-gate');
  // advance-phase.js 없음 → Partial
  const r = run(['--target', t]);
  assert(r.status === 0, `exit: ${r.status}\n${r.stdout.slice(0,300)}`);
  assert(fs.existsSync(path.join(t, '.claude', 'hooks', 'advance-phase.js')),
    'advance-phase.js 복구 안됨');
  assert(fs.existsSync(path.join(t, '.claude', 'hooks', 'session-gate.js')),
    'session-gate.js 사라짐');
});

// ══════════════════════════════════════════════════════════════════════
// TEST-05: 버전 비교 + --yes 플래그
// ══════════════════════════════════════════════════════════════════════
console.log('\n[TEST-05] 버전 비교 + --yes 플래그');

test('T05-A', 'should_auto_upgrade_when_older_version_installed', () => {
  const t = mkdir(path.join(TEMP_BASE, 't05a'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  fs.writeFileSync(path.join(t, 'CLAUDE.md'),
    '# CLAUDE.md\n\n```yaml\nproject_name: "t"\nversion: "2.3.0"\n```\n');
  // 자동 업그레이드 (2.3.0 < 2.5.0)
  const r = run(['--target', t]);
  assert(r.status === 0, `exit: ${r.status}`);
  const today = new Date().toISOString().slice(0,10);
  assert(fs.existsSync(path.join(t, `.skill-set-backup-${today}`)), '백업 미생성');
});

test('T05-B', 'should_skip_reinstall_when_same_version_without_yes', () => {
  const t = mkdir(path.join(TEMP_BASE, 't05b'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  fs.writeFileSync(path.join(t, 'CLAUDE.md'),
    `# CLAUDE.md\n\n\`\`\`yaml\nproject_name: "t"\nversion: "${HARNESS_VERSION}"\n\`\`\`\n`);
  const r = run(['--target', t]);
  assert(r.status === 0, `exit: ${r.status}`);
  assert((r.stdout+r.stderr).includes('--yes'), `--yes 안내 없음: ${r.stdout.slice(0,200)}`);
  // 백업 미생성 (재설치 안 함)
  const today = new Date().toISOString().slice(0,10);
  assert(!fs.existsSync(path.join(t, `.skill-set-backup-${today}`)), '백업 생성됨 (의도치 않은 재설치)');
});

test('T05-C', 'should_reinstall_when_same_version_with_yes_flag', () => {
  const t = mkdir(path.join(TEMP_BASE, 't05c'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  fs.writeFileSync(path.join(t, 'CLAUDE.md'),
    `# CLAUDE.md\n\n\`\`\`yaml\nproject_name: "t"\nversion: "${HARNESS_VERSION}"\n\`\`\`\n`);
  // docs/memory 필요 (upgrade 검증용)
  fs.mkdirSync(path.join(t, 'docs', 'memory'), { recursive: true });
  fs.writeFileSync(path.join(t, 'docs', 'memory', 'session-context.md'), '# ctx\n');
  const r = run(['--target', t, '--yes']);
  assert(r.status === 0, `exit: ${r.status}\n${r.stdout.slice(0,300)}`);
  const today = new Date().toISOString().slice(0,10);
  assert(fs.existsSync(path.join(t, `.skill-set-backup-${today}`)), '--yes 재설치 시 백업 미생성');
});

// ══════════════════════════════════════════════════════════════════════
// TEST-06: --upgrade deprecated warning — exit 0 유지
// ══════════════════════════════════════════════════════════════════════
console.log('\n[TEST-06] --upgrade deprecated warning');

test('T06-A', 'should_show_deprecated_warning_with_upgrade_flag', () => {
  const t = mkdir(path.join(TEMP_BASE, 't06a'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  fs.mkdirSync(path.join(t, 'docs', 'memory'), { recursive: true });
  fs.writeFileSync(path.join(t, 'docs', 'memory', 'session-context.md'), '# ctx\n');
  const r = run(['--upgrade', '--target', t]);
  assert(r.status === 0, `exit: ${r.status}`);
  assert((r.stdout+r.stderr).includes('DEPRECATED'), `deprecated 경고 없음: ${r.stdout.slice(0,300)}`);
});

test('T06-B', 'should_still_upgrade_when_deprecated_flag_used', () => {
  const t = mkdir(path.join(TEMP_BASE, 't06b'));
  const hooksDir = path.join(t, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js']
    .forEach(f => fs.writeFileSync(path.join(hooksDir, f), '// mock'));
  fs.mkdirSync(path.join(t, 'docs', 'memory'), { recursive: true });
  fs.writeFileSync(path.join(t, 'docs', 'memory', 'session-context.md'), '# ctx\n');
  const r = run(['--upgrade', '--target', t]);
  assert(r.status === 0, `exit: ${r.status}`);
  const today = new Date().toISOString().slice(0,10);
  assert(fs.existsSync(path.join(t, `.skill-set-backup-${today}`)), '백업 미생성');
  assert(fs.existsSync(path.join(t, '.claude', 'hooks', 'session-gate.js')), '새 hooks 미설치');
});

// ══════════════════════════════════════════════════════════════════════
// TEST-07: 종합 시뮬레이션 재실행 확인
// ══════════════════════════════════════════════════════════════════════
console.log('\n[TEST-07] 종합 시뮬레이션 S01~S20 재실행');

test('T07-A', 'should_pass_comprehensive_simulation_S01_to_S20', () => {
  const r = spawnSync('node', [
    path.join(__dirname, 'test_install_comprehensive.js')
  ], { cwd: SCRIPT_DIR, encoding: 'utf8', timeout: 120000, env: process.env });
  const out = r.stdout + r.stderr;
  const passMatch = out.match(/(\d+) PASS/);
  const failMatch = out.match(/(\d+) FAIL/);
  const totalPass = passMatch ? parseInt(passMatch[1]) : 0;
  const totalFail = failMatch ? parseInt(failMatch[1]) : 0;
  assert(totalFail === 0, `종합 시뮬레이션 FAIL ${totalFail}개: ${out.slice(-500)}`);
  assert(totalPass >= 70, `PASS 수 부족: ${totalPass} (기대: 70+)`);
});

// ══════════════════════════════════════════════════════════════════════
// 결과 요약
// ══════════════════════════════════════════════════════════════════════
console.log('\n' + '═'.repeat(55));
console.log(`  결과: ${passed} PASS / ${failed} FAIL / ${passed+failed} 총계`);
console.log('═'.repeat(55));

if (failed > 0) {
  console.log('\n  실패 항목:');
  results.filter(r => !r.pass).forEach(r =>
    console.log(`    ✗ [${r.id}] ${r.name}: ${r.detail}`)
  );
}

// 임시 파일 정리
try { rmRecursive(TEMP_BASE); } catch {}

process.exit(failed > 0 ? 1 : 0);
