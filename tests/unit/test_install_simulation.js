// tests/unit/test_install_simulation.js
// install.js 안전장치 시뮬레이션 — 10개 시나리오
//
// 검증 항목:
//   S01: 신규 설치 (기존 하네스 없음) → 성공
//   S02: 기존 하네스, 플래그 없음 → BLOCKED
//   S03: 기존 하네스 + --upgrade → 백업 생성 + 설치 성공
//   S04: 기존 하네스 + --force-fresh → 경고 후 재설치
//   S05: --upgrade + --dry-run → pre-flight만 출력, 파일 미변경
//   S06: --rollback + 백업 있음 → 복원 성공
//   S07: --rollback + 백업 없음 → exit 1
//   S08: --target 존재하지 않는 경로 → 신규 생성 후 설치
//   S09: SCRIPT_DIR = TARGET 동일 → exit 1
//   S10: --upgrade 후 docs/memory 보존 검증

'use strict';

const { spawnSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const os = require('os');

const INSTALL_JS = path.resolve(__dirname, '../../install.js');
const SCRIPT_DIR = path.dirname(INSTALL_JS);
const TEMP_BASE = path.join(os.tmpdir(), `skill-set-install-sim-${Date.now()}`);

let passed = 0;
let failed = 0;
const results = [];

// ── 유틸리티 ────────────────────────────────────────────────────────

function run(args, options = {}) {
  const { cwd = SCRIPT_DIR } = options;
  return spawnSync('node', [INSTALL_JS, ...args], {
    cwd,
    encoding: 'utf8',
    timeout: 30000,
    env: process.env,
  });
}

function check(sid, name, condition, detail = '') {
  if (condition) {
    console.log(`    ✅ ${name}`);
    passed++;
    results.push({ sid, name, pass: true });
  } else {
    console.log(`    ❌ ${name}${detail ? ' — ' + detail : ''}`);
    failed++;
    results.push({ sid, name, pass: false, detail });
  }
}

function mkdir(p) {
  fs.mkdirSync(p, { recursive: true });
  return p;
}

function rmRecursive(p) {
  if (!fs.existsSync(p)) return;
  if (fs.statSync(p).isDirectory()) {
    for (const f of fs.readdirSync(p)) rmRecursive(path.join(p, f));
    fs.rmdirSync(p);
  } else {
    fs.unlinkSync(p);
  }
}

function seedExistingHarness(targetDir) {
  // 기존 하네스가 설치된 것처럼 mock 파일 심기
  const hooksDir = path.join(targetDir, '.claude', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  fs.writeFileSync(path.join(hooksDir, 'session-gate.js'), '// mock session-gate');
  fs.writeFileSync(path.join(hooksDir, 'pre-tool-gate.js'), '// mock pre-tool-gate');
  fs.writeFileSync(path.join(hooksDir, 'advance-phase.js'), '// mock advance-phase');
  const memDir = path.join(targetDir, 'docs', 'memory');
  fs.mkdirSync(memDir, { recursive: true });
  fs.writeFileSync(path.join(memDir, 'pipeline-state.json'), JSON.stringify({
    phase: 'dev', track: 'B', activePrd: 'test-prd', project: 'mock-project',
  }, null, 2));
  fs.writeFileSync(path.join(memDir, 'session-context.md'),
    '# 세션 컨텍스트\n\n- 마지막 업데이트: 2026-06-01\n- Phase: dev\n');
  fs.writeFileSync(path.join(memDir, 'feedback-rules.json'), '[{"id":"FB-001","rule":"테스트 규칙"}]');
  fs.writeFileSync(path.join(targetDir, 'CLAUDE.md'),
    '# CLAUDE.md\n\n```yaml\nproject_name: "mock-project"\nlanguage: "C#"\nversion: "2.4.0"\n```\n');
}

// 임시 디렉토리 초기화
fs.mkdirSync(TEMP_BASE, { recursive: true });

console.log('╔═════════════════════════════════════════════════════╗');
console.log('║  install.js 안전장치 시뮬레이션 (10개 시나리오)       ║');
console.log(`╚═════════════════════════════════════════════════════╝`);
console.log(`  임시 경로: ${TEMP_BASE}\n`);

// ══════════════════════════════════════════════════════════════════════
// S01: 신규 설치 — 기존 하네스 없음
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S01';
  console.log(`\n[${sid}] 신규 설치 (기존 하네스 없음)`);
  const target = mkdir(path.join(TEMP_BASE, 's01'));
  const r = run(['--target', target]);

  check(sid, 'exit 0', r.status === 0, `실제 exit: ${r.status}`);
  check(sid, '.claude/hooks/session-gate.js 설치됨',
    fs.existsSync(path.join(target, '.claude', 'hooks', 'session-gate.js')));
  check(sid, 'CLAUDE.md 생성됨',
    fs.existsSync(path.join(target, 'CLAUDE.md')));
  check(sid, 'settings.json 훅 등록됨',
    fs.existsSync(path.join(target, '.claude', 'settings.json')) &&
    JSON.parse(fs.readFileSync(path.join(target, '.claude', 'settings.json'), 'utf8')).hooks?.UserPromptSubmit != null);
  check(sid, 'pipeline-state.json 생성됨',
    fs.existsSync(path.join(target, 'docs', 'memory', 'pipeline-state.json')));
  check(sid, 'BLOCKED 메시지 없음',
    !r.stderr.includes('BLOCKED') && !r.stdout.includes('BLOCKED'));
}

// ══════════════════════════════════════════════════════════════════════
// S02: 기존 하네스 감지, 플래그 없음 → smart-install auto-upgrade (v2.4.0 < source)
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S02';
  console.log(`\n[${sid}] 기존 하네스 감지 + 플래그 없음 → smart-install auto-upgrade`);
  const target = mkdir(path.join(TEMP_BASE, 's02'));
  seedExistingHarness(target); // seeds version: "2.4.0" < current → upgrade
  const r = run(['--target', target]);

  // smart-install: 구 버전 감지 시 auto-upgrade (exit 0)
  check(sid, 'exit 0 (auto-upgrade)', r.status === 0, `실제 exit: ${r.status}`);
  check(sid, '업그레이드 모드 메시지',
    r.stderr.includes('업그레이드') || r.stdout.includes('업그레이드') ||
    r.stderr.includes('upgrade') || r.stdout.includes('upgrade'));
  check(sid, 'session-gate.js 재설치됨',
    fs.existsSync(require('path').join(target, '.claude', 'hooks', 'session-gate.js')));

  // 기존 파일 손상 없음 확인
  const state = JSON.parse(fs.readFileSync(path.join(target, 'docs', 'memory', 'pipeline-state.json'), 'utf8'));
  check(sid, '기존 pipeline-state 손상 없음 (phase=dev)',
    state.phase === 'dev', `실제 phase: ${state.phase}`);
}

// ══════════════════════════════════════════════════════════════════════
// S03: 기존 하네스 + --upgrade → 백업 + 설치 성공
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S03';
  console.log(`\n[${sid}] 기존 하네스 + --upgrade → 백업 + 안전 설치`);
  const target = mkdir(path.join(TEMP_BASE, 's03'));
  seedExistingHarness(target);
  const r = run(['--upgrade', '--target', target]);

  check(sid, 'exit 0', r.status === 0, `실제 exit: ${r.status}\nstdout: ${r.stdout.slice(0,300)}`);

  // 백업 디렉토리 생성 확인
  const today = new Date().toISOString().slice(0, 10);
  const backupDir = path.join(target, `.skill-set-backup-${today}`);
  check(sid, '타임스탬프 백업 디렉토리 생성됨', fs.existsSync(backupDir), `기대: ${backupDir}`);
  check(sid, '백업에 hooks 포함됨',
    fs.existsSync(path.join(backupDir, '.claude', 'hooks', 'session-gate.js')));

  // 새 버전 파일 설치 확인
  check(sid, '새 session-gate.js 설치됨',
    fs.existsSync(path.join(target, '.claude', 'hooks', 'session-gate.js')));

  // 메모리 보존 확인
  check(sid, 'docs/memory/session-context.md 보존됨',
    fs.existsSync(path.join(target, 'docs', 'memory', 'session-context.md')) &&
    fs.readFileSync(path.join(target, 'docs', 'memory', 'session-context.md'), 'utf8').includes('Phase: dev'));
  check(sid, 'feedback-rules.json 보존됨',
    fs.readFileSync(path.join(target, 'docs', 'memory', 'feedback-rules.json'), 'utf8').includes('FB-001'));

  // Pre-flight 출력 확인
  const output = r.stdout + r.stderr;
  check(sid, 'Pre-flight 출력 포함',
    output.includes('Pre-flight') || output.includes('보존 예정'));
}

// ══════════════════════════════════════════════════════════════════════
// S04: 기존 하네스 + --force-fresh → 경고 후 재설치
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S04';
  console.log(`\n[${sid}] 기존 하네스 + --force-fresh → 경고 후 재설치`);
  const target = mkdir(path.join(TEMP_BASE, 's04'));
  seedExistingHarness(target);
  const r = run(['--force-fresh', '--target', target]);

  check(sid, 'exit 0 (차단 없이 진행)', r.status === 0, `실제 exit: ${r.status}`);
  check(sid, 'FORCE-FRESH 경고 출력됨',
    r.stdout.includes('FORCE-FRESH') || r.stdout.includes('force-fresh'));
  check(sid, '새 hooks 설치됨',
    fs.existsSync(path.join(target, '.claude', 'hooks', 'session-gate.js')));

  // docs/memory 보존 확인 (fresh install은 존재 파일 덮어쓰지 않음)
  check(sid, 'docs/memory/session-context.md 보존됨',
    fs.existsSync(path.join(target, 'docs', 'memory', 'session-context.md')));
}

// ══════════════════════════════════════════════════════════════════════
// S05: --upgrade + --dry-run → pre-flight만 출력, 파일 미변경
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S05';
  console.log(`\n[${sid}] --upgrade + --dry-run → 변경 없이 pre-flight 출력`);
  const target = mkdir(path.join(TEMP_BASE, 's05'));
  seedExistingHarness(target);

  // 변경 전 hooks 내용 기록
  const beforeHook = fs.readFileSync(
    path.join(target, '.claude', 'hooks', 'session-gate.js'), 'utf8');

  const r = run(['--upgrade', '--dry-run', '--target', target]);

  check(sid, 'exit 0', r.status === 0, `실제 exit: ${r.status}`);
  check(sid, 'DRY-RUN 메시지 출력됨',
    r.stdout.includes('DRY-RUN') || r.stderr.includes('DRY-RUN'));

  // 백업 미생성 확인
  const today = new Date().toISOString().slice(0, 10);
  const backupDir = path.join(target, `.skill-set-backup-${today}`);
  check(sid, '백업 디렉토리 미생성 (dry-run)', !fs.existsSync(backupDir));

  // 파일 변경 없음 확인
  const afterHook = fs.readFileSync(
    path.join(target, '.claude', 'hooks', 'session-gate.js'), 'utf8');
  check(sid, 'session-gate.js 변경 없음', beforeHook === afterHook);
}

// ══════════════════════════════════════════════════════════════════════
// S06: --rollback + 백업 있음 → 복원 성공
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S06';
  console.log(`\n[${sid}] --rollback + 백업 있음 → 복원 성공`);
  const target = mkdir(path.join(TEMP_BASE, 's06'));
  seedExistingHarness(target);

  // 백업 디렉토리 수동 생성 (rollback은 .skill-set-backup 폴더를 사용)
  const backupDir = path.join(target, '.skill-set-backup');
  const backupHooks = path.join(backupDir, '.claude', 'hooks');
  fs.mkdirSync(backupHooks, { recursive: true });
  fs.writeFileSync(path.join(backupHooks, 'session-gate.js'), '// RESTORED version');
  fs.writeFileSync(path.join(backupHooks, 'pre-tool-gate.js'), '// RESTORED pre-tool-gate');

  const r = run(['--rollback', '--target', target]);

  check(sid, 'exit 0', r.status === 0, `실제 exit: ${r.status}\n${r.stdout}`);
  check(sid, '복원된 파일 내용 확인',
    fs.readFileSync(path.join(target, '.claude', 'hooks', 'session-gate.js'), 'utf8')
      .includes('RESTORED version'));
}

// ══════════════════════════════════════════════════════════════════════
// S07: --rollback + 백업 없음 → exit 1
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S07';
  console.log(`\n[${sid}] --rollback + 백업 없음 → exit 1`);
  const target = mkdir(path.join(TEMP_BASE, 's07'));
  // 백업 없이 타겟만 생성

  const r = run(['--rollback', '--target', target]);

  check(sid, 'exit 1', r.status === 1, `실제 exit: ${r.status}`);
  check(sid, '백업 없음 오류 메시지',
    r.stderr.includes('백업 없음') || r.stdout.includes('백업 없음') ||
    r.stderr.includes('ERROR') || r.stdout.includes('ERROR'));
}

// ══════════════════════════════════════════════════════════════════════
// S08: --target 존재하지 않는 경로 → 디렉토리 자동 생성 + 신규 설치
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S08';
  console.log(`\n[${sid}] --target 미존재 경로 → 자동 생성 후 신규 설치`);
  const target = path.join(TEMP_BASE, 's08', 'deep', 'new-project');
  // 경로 미생성 상태에서 실행

  const r = run(['--target', target]);

  check(sid, 'exit 0', r.status === 0, `실제 exit: ${r.status}`);
  check(sid, '중간 디렉토리 자동 생성됨', fs.existsSync(target));
  check(sid, 'CLAUDE.md 생성됨', fs.existsSync(path.join(target, 'CLAUDE.md')));
  check(sid, 'hooks 설치됨',
    fs.existsSync(path.join(target, '.claude', 'hooks', 'session-gate.js')));
}

// ══════════════════════════════════════════════════════════════════════
// S09: SCRIPT_DIR = TARGET 동일 → exit 1
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S09';
  console.log(`\n[${sid}] SCRIPT_DIR = TARGET 동일 → exit 1`);
  // --target을 SCRIPT_DIR(install.js가 있는 곳)과 동일하게 지정
  const r = run(['--target', SCRIPT_DIR]);

  check(sid, 'exit 1', r.status === 1, `실제 exit: ${r.status}`);
  check(sid, '동일 경로 오류 메시지',
    r.stderr.includes('ERROR') || r.stdout.includes('ERROR'));
}

// ══════════════════════════════════════════════════════════════════════
// S10: --upgrade 후 docs/memory 보존 + CLAUDE.md yaml 보존 검증
// ══════════════════════════════════════════════════════════════════════
{
  const sid = 'S10';
  console.log(`\n[${sid}] --upgrade 후 데이터 보존 종합 검증`);
  const target = mkdir(path.join(TEMP_BASE, 's10'));
  seedExistingHarness(target);

  const r = run(['--upgrade', '--target', target]);

  check(sid, 'exit 0', r.status === 0, `실제 exit: ${r.status}`);

  // pipeline-state.json 보존
  const state = JSON.parse(fs.readFileSync(
    path.join(target, 'docs', 'memory', 'pipeline-state.json'), 'utf8'));
  check(sid, 'pipeline-state phase 보존됨 (dev)',
    state.phase === 'dev', `실제 phase: ${state.phase}`);
  check(sid, 'pipeline-state project 보존됨',
    state.project === 'mock-project' || state.activePrd != null);

  // session-context 보존
  const ctx = fs.readFileSync(
    path.join(target, 'docs', 'memory', 'session-context.md'), 'utf8');
  check(sid, 'session-context.md 내용 보존됨', ctx.includes('Phase: dev'));

  // feedback-rules 보존
  const fb = JSON.parse(fs.readFileSync(
    path.join(target, 'docs', 'memory', 'feedback-rules.json'), 'utf8'));
  check(sid, 'feedback-rules.json 항목 보존됨', fb.length > 0 && fb[0].id === 'FB-001');

  // CLAUDE.md yaml 보존
  const claude = fs.readFileSync(path.join(target, 'CLAUDE.md'), 'utf8');
  check(sid, 'CLAUDE.md project_name 보존됨', claude.includes('mock-project'));
  check(sid, 'CLAUDE.md language 보존됨', claude.includes('C#'));

  // post-install 검증 출력 확인
  const output = r.stdout + r.stderr;
  check(sid, 'Post-install 검증 메시지 출력됨',
    output.includes('데이터 보존 검증') || output.includes('핵심 파일'));
}

// ══════════════════════════════════════════════════════════════════════
// 결과 요약
// ══════════════════════════════════════════════════════════════════════
console.log('\n' + '═'.repeat(55));
console.log(`  결과: ${passed} PASS / ${failed} FAIL / ${passed + failed} 총계`);
console.log('═'.repeat(55));

if (failed > 0) {
  console.log('\n  실패 항목:');
  for (const r of results.filter(r => !r.pass)) {
    console.log(`    ✗ [${r.sid}] ${r.name}${r.detail ? ': ' + r.detail : ''}`);
  }
}

// 임시 파일 정리
try { rmRecursive(TEMP_BASE); } catch {}

process.exit(failed > 0 ? 1 : 0);
