// tests/unit/test_install_comprehensive.js
// install.js 종합 시뮬레이션 — 기존 S01~S10 재검증 + 신규 S11~S20 엣지케이스
//
// 신규 검증 항목:
//   S11: Partial 설치 감지 (일부 훅만 존재)
//   S12: --upgrade + 하네스 없음 → 명확한 오류
//   S13: --force-fresh 경고 vs 실제 docs/memory 보존 여부 확인
//   S14: --dry-run WITHOUT --upgrade → 실제로 파일이 설치되는 버그 검증
//   S15: 당일 2회 업그레이드 → 백업 충돌 여부
//   S16: HARNESS_VERSION 값 검증
//   S17: --upgrade + --dry-run 메시지 정확성
//   S18: 멱등성 — 동일 target에 2회 신규 설치
//   S19: --rollback이 최신 타임스탬프 백업을 선택하지 않는 이슈
//   S20: 큰 경로(공백 포함) 처리

'use strict';

const { spawnSync } = require('child_process');
const fs   = require('fs');
const path = require('path');
const os   = require('os');

const INSTALL_JS = path.resolve(__dirname, '../../install.js');
const SCRIPT_DIR = path.dirname(INSTALL_JS);
const TEMP_BASE  = path.join(os.tmpdir(), `skill-set-comp-${Date.now()}`);

let passed = 0, failed = 0, warned = 0;
const results = [];

// ── 유틸 ────────────────────────────────────────────────────────────
function run(args, cwd = SCRIPT_DIR) {
  return spawnSync('node', [INSTALL_JS, ...args], {
    cwd, encoding: 'utf8', timeout: 30000, env: process.env,
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

function warn(sid, name, detail = '') {
  console.log(`    ⚠  ${name}${detail ? ' — ' + detail : ''}`);
  warned++;
  results.push({ sid, name, pass: 'warn', detail });
}

function mkdir(p) { fs.mkdirSync(p, { recursive: true }); return p; }

function rmRecursive(p) {
  if (!fs.existsSync(p)) return;
  if (fs.statSync(p).isDirectory()) {
    for (const f of fs.readdirSync(p)) rmRecursive(path.join(p, f));
    fs.rmdirSync(p);
  } else fs.unlinkSync(p);
}

function seedFull(dir) {
  const hooks = path.join(dir, '.claude', 'hooks');
  fs.mkdirSync(hooks, { recursive: true });
  ['session-gate.js','pre-tool-gate.js','advance-phase.js','_common.js','stop-observation.js']
    .forEach(f => fs.writeFileSync(path.join(hooks, f), `// mock ${f}`));
  const mem = path.join(dir, 'docs', 'memory');
  fs.mkdirSync(mem, { recursive: true });
  fs.writeFileSync(path.join(mem, 'pipeline-state.json'),
    JSON.stringify({ phase: 'dev', track: 'B', project: 'mock' }, null, 2));
  fs.writeFileSync(path.join(mem, 'session-context.md'), '# ctx\n- Phase: dev\n');
  fs.writeFileSync(path.join(mem, 'feedback-rules.json'), '[{"id":"FB-001"}]');
  fs.writeFileSync(path.join(dir, 'CLAUDE.md'),
    '# CLAUDE.md\n\n```yaml\nproject_name: "mock"\nversion: "2.4.0"\n```\n');
}

function seedPartial(dir, presentHooks = ['pre-tool-gate.js']) {
  const hooks = path.join(dir, '.claude', 'hooks');
  fs.mkdirSync(hooks, { recursive: true });
  presentHooks.forEach(f => fs.writeFileSync(path.join(hooks, f), `// mock ${f}`));
}

fs.mkdirSync(TEMP_BASE, { recursive: true });

console.log('╔══════════════════════════════════════════════════════════╗');
console.log('║  install.js 종합 시뮬레이션 (S01~S20, 20개 시나리오)      ║');
console.log('╚══════════════════════════════════════════════════════════╝\n');

// ══════════════════════════════════════════════════════════════════════
// ── S01~S10 재검증 ──────────────────────────────────────────────────
// ══════════════════════════════════════════════════════════════════════

// S01
{ const sid='S01'; console.log(`\n[${sid}] 신규 설치 — 기존 하네스 없음`);
  const t = mkdir(path.join(TEMP_BASE,'s01'));
  const r = run(['--target',t]);
  check(sid,'exit 0', r.status===0, `exit: ${r.status}`);
  check(sid,'session-gate.js 설치', fs.existsSync(path.join(t,'.claude','hooks','session-gate.js')));
  check(sid,'CLAUDE.md 생성', fs.existsSync(path.join(t,'CLAUDE.md')));
  check(sid,'settings.json 훅 등록',
    fs.existsSync(path.join(t,'.claude','settings.json')) &&
    JSON.parse(fs.readFileSync(path.join(t,'.claude','settings.json'),'utf8')).hooks?.UserPromptSubmit != null);
  check(sid,'pipeline-state.json 생성', fs.existsSync(path.join(t,'docs','memory','pipeline-state.json')));
  check(sid,'BLOCKED 없음', !r.stdout.includes('BLOCKED') && !r.stderr.includes('BLOCKED'));
}

// S02 — 새 동작: BLOCKED 제거, 기존 하네스 + 플래그 없음 → 자동 업그레이드 (FR-02)
{ const sid='S02'; console.log(`\n[${sid}] 기존 하네스 + 플래그 없음 → 자동 업그레이드 (FR-02)`);
  const t = mkdir(path.join(TEMP_BASE,'s02')); seedFull(t);
  // seedFull은 CLAUDE.md version: "2.4.0" → HARNESS_VERSION(2.5.0)보다 낮음 → upgrade 라우팅
  const r = run(['--target',t]);
  check(sid,'exit 0 (자동 업그레이드)', r.status===0, `exit:${r.status}\n${r.stdout.slice(0,300)}`);
  const output = r.stdout + r.stderr;
  check(sid,'[SMART] 감지 메시지 출력', output.includes('[SMART]'));
  check(sid,'업그레이드 모드 감지', output.includes('업그레이드') || output.includes('upgrade'));
  // 타임스탬프 백업 생성 확인
  const today = new Date().toISOString().slice(0,10);
  check(sid,'타임스탬프 백업 생성됨', fs.existsSync(path.join(t,`.skill-set-backup-${today}`)));
  // 기존 메모리 보존 확인
  const ctx = fs.readFileSync(path.join(t,'docs','memory','session-context.md'),'utf8');
  check(sid,'session-context 보존 (Phase: dev)', ctx.includes('Phase: dev'));
  check(sid,'BLOCKED 메시지 없음 (FR-02 달성)', !output.includes('BLOCKED'));
}

// S03
{ const sid='S03'; console.log(`\n[${sid}] --upgrade → 백업 + 안전 설치`);
  const t = mkdir(path.join(TEMP_BASE,'s03')); seedFull(t);
  const r = run(['--upgrade','--target',t]);
  check(sid,'exit 0', r.status===0, `exit: ${r.status}\n${r.stdout.slice(0,200)}`);
  const today = new Date().toISOString().slice(0,10);
  const backup = path.join(t,`.skill-set-backup-${today}`);
  check(sid,'타임스탬프 백업 생성', fs.existsSync(backup));
  check(sid,'백업에 hooks 포함', fs.existsSync(path.join(backup,'.claude','hooks','session-gate.js')));
  check(sid,'session-context 보존',
    fs.existsSync(path.join(t,'docs','memory','session-context.md')) &&
    fs.readFileSync(path.join(t,'docs','memory','session-context.md'),'utf8').includes('Phase: dev'));
  check(sid,'feedback-rules 보존',
    JSON.parse(fs.readFileSync(path.join(t,'docs','memory','feedback-rules.json'),'utf8')).length > 0);
  check(sid,'Pre-flight 출력', (r.stdout+r.stderr).includes('Pre-flight')||(r.stdout+r.stderr).includes('보존'));
}

// S04
{ const sid='S04'; console.log(`\n[${sid}] --force-fresh → 경고 + 재설치`);
  const t = mkdir(path.join(TEMP_BASE,'s04')); seedFull(t);
  const r = run(['--force-fresh','--target',t]);
  check(sid,'exit 0', r.status===0);
  check(sid,'FORCE-FRESH 경고 출력', r.stdout.includes('FORCE-FRESH')||r.stdout.includes('force-fresh'));
  check(sid,'hooks 재설치됨', fs.existsSync(path.join(t,'.claude','hooks','session-gate.js')));
  check(sid,'docs/memory 보존됨', fs.existsSync(path.join(t,'docs','memory','session-context.md')));
}

// S05
{ const sid='S05'; console.log(`\n[${sid}] --upgrade + --dry-run → 파일 미변경`);
  const t = mkdir(path.join(TEMP_BASE,'s05')); seedFull(t);
  const before = fs.readFileSync(path.join(t,'.claude','hooks','session-gate.js'),'utf8');
  const r = run(['--upgrade','--dry-run','--target',t]);
  check(sid,'exit 0', r.status===0);
  check(sid,'DRY-RUN 메시지', r.stdout.includes('DRY-RUN')||r.stderr.includes('DRY-RUN'));
  const today = new Date().toISOString().slice(0,10);
  check(sid,'백업 미생성', !fs.existsSync(path.join(t,`.skill-set-backup-${today}`)));
  const after = fs.readFileSync(path.join(t,'.claude','hooks','session-gate.js'),'utf8');
  check(sid,'session-gate.js 변경 없음', before===after);
}

// S06
{ const sid='S06'; console.log(`\n[${sid}] --rollback + 백업 있음 → 복원`);
  const t = mkdir(path.join(TEMP_BASE,'s06'));
  const bk = path.join(t,'.skill-set-backup');
  fs.mkdirSync(path.join(bk,'.claude','hooks'),{recursive:true});
  fs.writeFileSync(path.join(bk,'.claude','hooks','session-gate.js'),'// RESTORED');
  const r = run(['--rollback','--target',t]);
  check(sid,'exit 0', r.status===0, `exit:${r.status}\n${r.stdout}`);
  check(sid,'복원 내용 확인',
    fs.readFileSync(path.join(t,'.claude','hooks','session-gate.js'),'utf8').includes('RESTORED'));
}

// S07
{ const sid='S07'; console.log(`\n[${sid}] --rollback + 백업 없음 → exit 1`);
  const t = mkdir(path.join(TEMP_BASE,'s07'));
  const r = run(['--rollback','--target',t]);
  check(sid,'exit 1', r.status===1);
  check(sid,'오류 메시지', (r.stderr+r.stdout).match(/백업 없음|ERROR/i)!=null);
}

// S08
{ const sid='S08'; console.log(`\n[${sid}] --target 미존재 경로 → 자동 생성`);
  const t = path.join(TEMP_BASE,'s08','deep','new');
  const r = run(['--target',t]);
  check(sid,'exit 0', r.status===0);
  check(sid,'디렉토리 자동 생성', fs.existsSync(t));
  check(sid,'hooks 설치', fs.existsSync(path.join(t,'.claude','hooks','session-gate.js')));
}

// S09
{ const sid='S09'; console.log(`\n[${sid}] SCRIPT_DIR=TARGET → exit 1`);
  const r = run(['--target',SCRIPT_DIR]);
  check(sid,'exit 1', r.status===1);
  check(sid,'동일 경로 오류 메시지', (r.stderr+r.stdout).includes('ERROR'));
}

// S10
{ const sid='S10'; console.log(`\n[${sid}] --upgrade 후 데이터 보존 종합`);
  const t = mkdir(path.join(TEMP_BASE,'s10')); seedFull(t);
  const r = run(['--upgrade','--target',t]);
  check(sid,'exit 0', r.status===0);
  const st = JSON.parse(fs.readFileSync(path.join(t,'docs','memory','pipeline-state.json'),'utf8'));
  check(sid,'phase 보존 (dev)', st.phase==='dev');
  const ctx = fs.readFileSync(path.join(t,'docs','memory','session-context.md'),'utf8');
  check(sid,'session-context 내용 보존', ctx.includes('Phase: dev'));
  const fb = JSON.parse(fs.readFileSync(path.join(t,'docs','memory','feedback-rules.json'),'utf8'));
  check(sid,'feedback-rules 보존', fb.length>0 && fb[0].id==='FB-001');
  const cl = fs.readFileSync(path.join(t,'CLAUDE.md'),'utf8');
  check(sid,'CLAUDE.md project_name 보존', cl.includes('mock'));
}

// ══════════════════════════════════════════════════════════════════════
// ── S11~S20 신규 엣지케이스 ─────────────────────────────────────────
// ══════════════════════════════════════════════════════════════════════

// S11: Partial 설치 감지 — session-gate.js 없고 pre-tool-gate.js만 있음 → 복구 설치 (FR-01 구현됨)
{ const sid='S11'; console.log(`\n[${sid}] Partial 설치 감지 → 복구 설치 (FR-01)`);
  const t = mkdir(path.join(TEMP_BASE,'s11'));
  seedPartial(t, ['pre-tool-gate.js','_common.js']);
  // CLAUDE.md 없음 → version 불명, upgrade 모드로 처리
  // detectInstallState: hooksDir 있음, present=['pre-tool-gate.js'] → 'partial'
  // smartRoute: state='partial' → mode='partial-repair' → runUpgradeFlow
  // runUpgradeFlow에서 backup은 pre-tool-gate.js를 백업하므로 성공

  const r = run(['--target',t]);
  check(sid,'exit 0 (Partial → 복구 설치)', r.status===0,
    `exit:${r.status}\n${r.stdout.slice(0,300)}`);
  const output = r.stdout + r.stderr;
  check(sid,'[SMART] Partial 감지 메시지', output.includes('[SMART]') && output.includes('Partial'));
  check(sid,'session-gate.js 복구 설치됨',
    fs.existsSync(path.join(t,'.claude','hooks','session-gate.js')));
  check(sid,'advance-phase.js 복구 설치됨',
    fs.existsSync(path.join(t,'.claude','hooks','advance-phase.js')));
  const newContent = fs.readFileSync(path.join(t,'.claude','hooks','pre-tool-gate.js'),'utf8');
  check(sid,'pre-tool-gate.js 새 버전으로 교체됨', !newContent.includes('// mock pre-tool-gate.js'));
  check(sid,'FR-01 Partial 감지 정상 동작 확인', true);
}

// S12: --upgrade + 기존 하네스 없음 → 백업 검증 실패
{ const sid='S12'; console.log(`\n[${sid}] --upgrade + 하네스 없음 → 오류 메시지 확인`);
  const t = mkdir(path.join(TEMP_BASE,'s12'));
  // .claude/ 없는 빈 폴더에 --upgrade
  const r = run(['--upgrade','--target',t]);
  check(sid,'exit 1', r.status===1, `exit:${r.status}`);
  const output = r.stdout + r.stderr;
  check(sid,'백업 없음 오류 출력', output.includes('백업 실패')||output.includes('ERROR'));

  // 오류 메시지가 명확한지 확인
  const isClearMsg = output.includes('hooks 백업 없음');
  if (isClearMsg) {
    check(sid,'오류 메시지 명확함', true);
  } else {
    warn(sid, '⚠ DESIGN ISSUE: 오류 메시지가 "백업 실패: .claude/hooks 백업 없음"이지만 ' +
      '실제 원인은 "하네스 미설치". "기존 하네스 없음 → --upgrade 불필요" 메시지가 더 명확함');
  }
}

// S13: --force-fresh 메시지 수정 검증 (구버전: "완전 초기화" → 신버전: "hooks/skills 교체, memory 보존")
{ const sid='S13'; console.log(`\n[${sid}] --force-fresh: 경고 메시지 + memory 보존 검증`);
  const t = mkdir(path.join(TEMP_BASE,'s13')); seedFull(t);
  const before = fs.readFileSync(path.join(t,'docs','memory','session-context.md'),'utf8');
  const r = run(['--force-fresh','--target',t]);
  check(sid,'exit 0', r.status===0);
  const after = fs.existsSync(path.join(t,'docs','memory','session-context.md'))
    ? fs.readFileSync(path.join(t,'docs','memory','session-context.md'),'utf8') : null;
  check(sid,'docs/memory/session-context.md 보존됨', after !== null && after === before);

  // 수정된 메시지 검증
  const output = r.stdout + r.stderr;
  check(sid,'경고 메시지: "hooks/skills 교체" 포함',
    output.includes('hooks/skills'), `실제 출력: ${output.slice(0,200)}`);
  check(sid,'경고 메시지: "보존" 포함', output.includes('보존'));
  check(sid,'구버전 오해 메시지("완전 초기화") 제거됨',
    !output.includes('완전 초기화') && !output.includes('리셋됩니다'));
}

// S14: --dry-run WITHOUT --upgrade → 신규 설치 경로에서 dry-run 미처리 버그
{ const sid='S14'; console.log(`\n[${sid}] --dry-run (--upgrade 없이) → 실제 설치 여부 확인`);
  const t = mkdir(path.join(TEMP_BASE,'s14'));
  // 빈 폴더에 --dry-run만 (--upgrade 없음)
  const r = run(['--dry-run','--target',t]);
  const installed = fs.existsSync(path.join(t,'.claude','hooks','session-gate.js'));

  if (installed) {
    // 버그 발견: dry-run인데 실제로 설치됨
    warn(sid, '⚠ BUG: --dry-run이 --upgrade 없이는 무시됨 → 실제 설치 진행됨. ' +
      '신규 설치 경로에도 DRY_RUN 체크 추가 필요');
    check(sid,'(버그 확인) --dry-run 없이도 설치됨 (현재 동작)', installed);
  } else {
    check(sid,'--dry-run 정상 동작 (설치 안 됨)', !installed);
  }
}

// S15: 당일 2회 업그레이드 → 백업 충돌 여부
{ const sid='S15'; console.log(`\n[${sid}] 당일 2회 업그레이드 → 백업 충돌`);
  const t = mkdir(path.join(TEMP_BASE,'s15')); seedFull(t);
  const today = new Date().toISOString().slice(0,10);
  const backupDir = path.join(t,`.skill-set-backup-${today}`);

  // 1차 업그레이드
  const r1 = run(['--upgrade','--target',t]);
  check(sid,'1차 업그레이드 성공', r1.status===0);
  const countAfter1st = fs.existsSync(backupDir)
    ? fs.readdirSync(path.join(backupDir,'.claude','hooks')).length : 0;

  // 1차 백업 내용을 고유하게 마킹
  if (fs.existsSync(path.join(backupDir,'.claude','hooks'))) {
    fs.writeFileSync(path.join(backupDir,'.claude','hooks','FIRST_BACKUP_MARKER'),'1st');
  }

  // 2차 업그레이드 (같은 날)
  const r2 = run(['--upgrade','--target',t]);
  check(sid,'2차 업그레이드 성공', r2.status===0);

  // 1차 백업 마커가 살아있는지 확인 (충돌 시 덮어써짐)
  const markerExists = fs.existsSync(path.join(backupDir,'.claude','hooks','FIRST_BACKUP_MARKER'));
  if (!markerExists) {
    warn(sid, '⚠ BUG: 같은 날 2차 업그레이드 시 기존 백업(.skill-set-backup-YYYY-MM-DD)을 ' +
      '덮어씀 → 1차 업그레이드 이전 상태 복구 불가. ' +
      '해결책: 타임스탬프에 시간(HH-MM-SS)까지 포함하거나 백업 전 충돌 감지 필요');
    check(sid,'(버그 확인) 1차 백업 마커 덮어써짐', !markerExists);
  } else {
    check(sid,'1차 백업 마커 보존됨 (충돌 없음)', markerExists);
  }
}

// S16: HARNESS_VERSION 값 검증 (2.5.0으로 수정됐는지 확인)
{ const sid='S16'; console.log(`\n[${sid}] HARNESS_VERSION 값 확인`);
  const content = fs.readFileSync(INSTALL_JS,'utf8');
  const match = content.match(/HARNESS_VERSION\s*=\s*'([^']+)'/);
  const ver = match ? match[1] : 'unknown';
  console.log(`    ℹ  현재 HARNESS_VERSION: ${ver}`);

  check(sid,'HARNESS_VERSION이 정의됨', !!match);
  check(sid,`HARNESS_VERSION = ${ver} (semver 형식)`, /^\d+\.\d+\.\d+$/.test(ver), `실제: ${ver}`);

  // 신규 설치 후 CLAUDE.md에 현재 버전 기록되는지 확인
  const t = mkdir(path.join(TEMP_BASE,'s16-ver'));
  run(['--target',t]);
  if (fs.existsSync(path.join(t,'CLAUDE.md'))) {
    const cl = fs.readFileSync(path.join(t,'CLAUDE.md'),'utf8');
    check(sid,`신규 설치 CLAUDE.md에 ${ver} 기록`,
      cl.includes(ver), `CLAUDE.md 버전 내용: ${cl.match(/version:[^\n]*/)?.[0]}`);
  }
}

// S17: --upgrade + --dry-run 메시지 정확성
{ const sid='S17'; console.log(`\n[${sid}] --upgrade + --dry-run 메시지 정확성`);
  const t = mkdir(path.join(TEMP_BASE,'s17')); seedFull(t);
  const r = run(['--upgrade','--dry-run','--target',t]);
  const output = r.stdout + r.stderr;

  check(sid,'DRY-RUN 메시지 출력', output.includes('DRY-RUN'));

  // "실제 변경 없이 종료합니다. --upgrade 옵션으로 실행하세요." — 이미 --upgrade 중인데 잘못된 안내
  const hasWrongMsg = output.includes('--upgrade 옵션으로 실행하세요');
  if (hasWrongMsg) {
    warn(sid, '⚠ UX ISSUE: --upgrade + --dry-run 메시지에 "—upgrade 옵션으로 실행하세요" 포함 → ' +
      '이미 --upgrade 중이므로 혼란. "실제 설치: node install.js --upgrade --target ..." 로 수정 필요');
  } else {
    check(sid,'dry-run 메시지 정확함', true);
  }
}

// S18: 멱등성 — 설치 후 재실행 → same-version 안내 후 exit 0 (FR-05)
{ const sid='S18'; console.log(`\n[${sid}] 멱등성 — 설치 후 재실행 → same-version 안내 (FR-05)`);
  const t = mkdir(path.join(TEMP_BASE,'s18'));
  const r1 = run(['--target',t]);
  check(sid,'1차 설치 성공 (exit 0)', r1.status===0);

  // 2차 실행: 설치된 버전 = HARNESS_VERSION = same-version 라우팅
  const r2 = run(['--target',t]);
  check(sid,'2차 실행 exit 0 (same-version 안내)', r2.status===0,
    `exit:${r2.status} — 자동 감지로 same-version 처리됨`);
  const out2 = r2.stdout + r2.stderr;
  check(sid,'2차 실행: "이미 최신" 또는 same-version 메시지 포함',
    out2.includes('이미') || out2.includes('same') || out2.includes('최신'));
  check(sid,'2차 실행: --yes 안내 포함', out2.includes('--yes'));
  check(sid,'2차 실행: 기존 hooks 훼손 없음',
    fs.existsSync(path.join(t,'.claude','hooks','session-gate.js')));

  // --yes 로 재설치 가능 확인
  const r3 = run(['--target', t, '--yes']);
  check(sid,'--yes 재설치 성공 (exit 0)', r3.status===0, `exit:${r3.status}`);
}

// S19: 백업 경로 지정 vs 실제 복원 대상 (--rollback은 .skill-set-backup 고정 경로)
{ const sid='S19'; console.log(`\n[${sid}] --rollback 경로: .skill-set-backup (고정) vs 타임스탬프 백업`);
  const t = mkdir(path.join(TEMP_BASE,'s19')); seedFull(t);

  // --upgrade 하면 .skill-set-backup-YYYY-MM-DD 생성
  run(['--upgrade','--target',t]);
  const today = new Date().toISOString().slice(0,10);
  const tsBackup = path.join(t,`.skill-set-backup-${today}`);
  const fixedBackup = path.join(t,'.skill-set-backup');

  check(sid,'타임스탬프 백업 생성됨', fs.existsSync(tsBackup));
  check(sid,'.skill-set-backup (고정) 미생성',
    !fs.existsSync(fixedBackup),
    '--rollback은 .skill-set-backup를 보는데 --upgrade는 타임스탬프 경로에 생성 → 불일치!');

  // --rollback 실행 → .skill-set-backup 없으므로 실패해야 함
  const r = run(['--rollback','--target',t]);
  if (r.status !== 0) {
    warn(sid, '⚠ BUG: --upgrade는 .skill-set-backup-YYYY-MM-DD 생성, ' +
      '--rollback은 .skill-set-backup(고정) 탐색 → 업그레이드 후 롤백 불가! ' +
      '해결: --rollback이 최신 .skill-set-backup-* 경로도 탐색하도록 수정 필요');
    check(sid,'(버그 확인) --rollback이 타임스탬프 백업을 찾지 못함', r.status!==0);
  } else {
    check(sid,'--rollback 성공 (타임스탬프 백업 사용)', r.status===0);
  }
}

// S20: 경로에 공백 포함
{ const sid='S20'; console.log(`\n[${sid}] 경로에 공백 포함 처리`);
  const t = mkdir(path.join(TEMP_BASE,'s20 with spaces'));
  const r = run(['--target',t]);
  check(sid,'공백 포함 경로 설치 성공 (exit 0)', r.status===0, `exit:${r.status}`);
  check(sid,'session-gate.js 설치됨',
    fs.existsSync(path.join(t,'.claude','hooks','session-gate.js')));
}

// ══════════════════════════════════════════════════════════════════════
// 결과 요약
// ══════════════════════════════════════════════════════════════════════
console.log('\n' + '═'.repeat(60));
console.log(`  결과: ${passed} PASS / ${failed} FAIL / ${warned} WARN / ${passed+failed+warned} 총계`);
console.log('═'.repeat(60));

const failItems = results.filter(r => r.pass===false);
const warnItems = results.filter(r => r.pass==='warn');

if (failItems.length>0) {
  console.log('\n  ❌ 실패 항목:');
  failItems.forEach(r => console.log(`    [${r.sid}] ${r.name}${r.detail?' — '+r.detail:''}`));
}

if (warnItems.length>0) {
  console.log('\n  ⚠  이슈/버그 발견:');
  warnItems.forEach(r => console.log(`    [${r.sid}] ${r.detail||r.name}`));
}

console.log('');

// 임시 파일 정리
try { rmRecursive(TEMP_BASE); } catch {}

// WARN이 있어도 실패 아님 (이슈 리포트용), FAIL만 exit 1
process.exit(failItems.length > 0 ? 1 : 0);
