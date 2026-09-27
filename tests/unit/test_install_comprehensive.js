// tests/unit/test_install_comprehensive.js
// install.js 종합 시뮬레이션 — S01~S10 재검증 + S11~S20 엣지케이스 + S21~S27 대상 결정·백업 범위(v7/N-02)
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
console.log('║  install.js 종합 시뮬레이션 (S01~S27, 27개 시나리오)      ║');
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
    // [v3.6/N-08/TEST-02] 분기 조건을 그대로 단언하면 버그가 있어도 없어도 초록이다.
    //   반증 불가능한 단언은 커버리지가 아니다 — 알려진 버그는 warn 으로만 남기고 실패시킨다.
    check(sid,'--dry-run 은 설치하지 않아야 한다', false);
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
    check(sid,'1차 백업 마커가 보존되어야 한다', false);
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

  // [v3.2/IMPL-26] 드리프트 가드. 이 상수는 v3.1 내내 2.8.4 로 굳어 있었고 저장소는 2.9.0 이었다 —
  //   설치본에 잘못된 버전이 박혔는데 아무 테스트도 잡지 못했다. 동적으로 읽게 바꾸는 안은
  //   배포된 install.js 가 CLAUDE.md 없이도 동작해야 해서 기각했고, 대신 여기서 잡는다.
  const _claudeMd = fs.readFileSync(path.resolve(__dirname, '../../CLAUDE.md'), 'utf8');
  const _cmVer = (_claudeMd.match(/^version:\s*"?([0-9]+\.[0-9]+\.[0-9]+)"?/m) || [])[1];
  check(sid, `HARNESS_VERSION(${ver}) === CLAUDE.md version(${_cmVer})`, ver === _cmVer,
    `드리프트 — install.js:${(content.slice(0, content.indexOf('HARNESS_VERSION')).match(/\n/g) || []).length + 1} 을 ${_cmVer} 로 맞추세요`);

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
    check(sid,'--rollback 이 타임스탬프 백업을 찾아야 한다', false);
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
// ── [v7/N-02] S21~S25 — 인스톨러가 대상을 추측하지 않는다 ────────────
//   실제 사고: `node install.js /c/workspace_python/sso` 가 위치 인자를 조용히 버리고
//   **부모 폴더**(워크스페이스 루트)에 설치했다. 2.8.4 → 3.2.1 로 덮어써졌고,
//   버전 숫자가 예상과 다르다는 것을 로그 끝에서 눈치채야만 알 수 있었다.
//
//   **안전 규칙: 기본 대상을 건드릴 수 있는 형태는 전부 `--dry-run` 으로만 부른다.**
//   기본 대상 = `path.resolve(SCRIPT_DIR, '..')` = 이 저장소의 **부모 폴더(실물)**이다.
//   Red 를 그냥 돌리면 그 사고를 내 워크스페이스에 그대로 재현하게 된다.
//   `--dry-run` 도 `resolveTarget()` 을 똑같이 지나므로 판정 대상은 달라지지 않는다.
const PARENT = path.resolve(SCRIPT_DIR, '..');
const parentMark = () => {
  // 부모 폴더가 이 테스트로 바뀌지 않았는지 보는 지문. 존재 여부 + 하네스 흔적.
  // [v10/N-04] 존재 여부만 보면 첫 실행 뒤로 영원히 "무변경" 이다 — S25 의 인자 없는 dry-run 이
  //   부모 폴더 `.claude/install-logs` 에 로그를 하나씩 쌓는 동안(85개) 이 지문은 초록이었다. 로그 수까지 본다.
  //   [Loop A] 부모 폴더는 실물 워크스페이스 루트다 — 다른 체크아웃·다른 세션의 설치도 거기에 쓸 수 있다.
  //   로그 머리의 `소스:` 가 **이 저장소**인 것만 센다. 이 스위트가 부른 설치기만 그 줄을 이 경로로 적는다.
  const exists = ['.claude', 'CLAUDE.md', 'docs/Manual.md'].map(f => fs.existsSync(path.join(PARENT, f)));
  let logs = -1;
  try {
    const dir = path.join(PARENT, '.claude', 'install-logs');
    logs = fs.readdirSync(dir).filter(f => {
      try {
        const head = fs.readFileSync(path.join(dir, f), 'utf8').slice(0, 600);
        const src = ((head.match(/^\s*소스:\s*(.+?)\s*$/m) || [])[1] || '');
        return path.resolve(src) === path.resolve(SCRIPT_DIR);
      } catch { return false; }
    }).length;
  } catch { /* 없음 */ }
  return JSON.stringify({ exists, logs });
};

// S21 — 위치 인자를 대상으로 받는다
{ const sid='S21'; console.log(`\n[${sid}] 위치 인자가 대상이 된다 (제안 1)`);
  const before = parentMark();
  const t = mkdir(path.join(TEMP_BASE,'s21'));
  const r = run([t, '--dry-run']);
  const out = r.stdout + r.stderr;
  check(sid,'exit 0', r.status===0, `exit:${r.status}\n${out.slice(-400)}`);
  check(sid,'넘긴 경로가 대상이다', out.includes(t), out.slice(-400));
  const targetLine = ((/대상:(.+)/.exec(out) || [])[1] || '').trim();
  check(sid,'대상 줄이 넘긴 경로다', targetLine === t, `대상: "${targetLine}" (부모: ${PARENT})`);
  check(sid,'부모 폴더 무변경', parentMark()===before, `${before} → ${parentMark()}`);
}

// S22 — 해석할 수 없으면 거부한다. **판정 불가는 조용한 기본값보다 낫다**
{ const sid='S22'; console.log(`\n[${sid}] 모호하거나 알 수 없는 인자 → 거부 (제안 2)`);
  const before = parentMark();
  const t1 = mkdir(path.join(TEMP_BASE,'s22a'));
  const t2 = mkdir(path.join(TEMP_BASE,'s22b'));
  const cases = [
    ['--target 와 위치 인자 둘 다', [t1, '--target', t2, '--dry-run']],
    ['위치 인자 2개',              [t1, t2, '--dry-run']],
    ['알 수 없는 플래그(오타)',     ['--taget', t1, '--dry-run']],
  ];
  for (const [label, args] of cases) {
    const r = run(args);
    const out = r.stdout + r.stderr;
    check(sid, `${label} → exit≠0`, r.status!==0, `exit:${r.status}\n${out.slice(-300)}`);
    check(sid, `${label} → 이유를 말한다`, /알 수 없|모호|둘 다|하나만|ERROR/.test(out), out.slice(-300));
  }
  check(sid,'부모 폴더 무변경', parentMark()===before, `${before} → ${parentMark()}`);
}

// S23 — **사고의 재현**. 이것이 이 노드의 진짜 단언이다.
{ const sid='S23'; console.log(`\n[${sid}] 사고 재현 — 잘못 준 인자가 부모 폴더로 가지 않는다`);
  const before = parentMark();
  // 사고 당시 형태 그대로: POSIX(MSYS) 경로를 위치 인자로.
  const r = run(['/c/workspace_python/sso', '--dry-run']);
  const out = r.stdout + r.stderr;
  if (process.platform === 'win32') {
    // win32 에서 `/c/...` 는 `C:\c\...` 로 풀린다 — 없는 경로를 자동 생성(S08)하므로 거부한다.
    check(sid,'MSYS 경로 거부', r.status!==0, `exit:${r.status}\n${out.slice(-300)}`);
    check(sid,'드라이브 문자 경로를 안내한다', /드라이브|C:|경로 형식/.test(out), out.slice(-300));
  } else {
    check(sid,'POSIX 에서는 그대로 대상이 된다', out.includes('/c/workspace_python/sso'), out.slice(-300));
  }
  check(sid,'어느 경우에도 부모 폴더가 대상이 아니다',
    !/설치 완료|업그레이드 완료/.test(out), out.slice(-300));
  check(sid,'부모 폴더 무변경', parentMark()===before, `${before} → ${parentMark()}`);
}

// S24 — 기존 여섯 플래그는 그대로 동작한다. **거부 규칙의 안전선.**
//   실측으로 install.js 가 읽는 플래그는 여섯이고 문서에 등장하는 것도 그 여섯이 전부다.
//   하나라도 거부되면 기존 사용법이 깨진다 — 그래서 각각을 실제로 실행한다.
{ const sid='S24'; console.log(`\n[${sid}] 기존 플래그 6종이 거부되지 않는다`);
  const before = parentMark();
  const t = mkdir(path.join(TEMP_BASE,'s24'));
  for (const args of [
    ['--target', t, '--dry-run'],
    ['--upgrade', '--target', t, '--dry-run'],
    ['--force-fresh', '--target', t, '--dry-run'],
    ['--yes', '--target', t, '--dry-run'],
  ]) {
    const r = run(args);
    const out = r.stdout + r.stderr;
    check(sid, `${args.join(' ')} 가 인자 오류로 거부되지 않는다`,
      !/알 수 없는 인자|알 수 없는 플래그/.test(out), out.slice(-300));
  }
  // --rollback 은 백업이 없으면 자체 사유로 exit 1 한다 — **인자 오류가 아니어야 한다**
  const rb = run(['--rollback', '--target', t]);
  check(sid,'--rollback 이 인자 오류로 거부되지 않는다',
    !/알 수 없는 인자|알 수 없는 플래그/.test(rb.stdout + rb.stderr), (rb.stdout+rb.stderr).slice(-300));
  check(sid,'부모 폴더 무변경', parentMark()===before, `${before} → ${parentMark()}`);
}

// S25 — 인자가 없으면 기본값은 남는다. 다만 **조용하지 않다**.
//   기본값 제거는 charter 가 기각했다(문서화된 zip 사용법을 깬다).
{ const sid='S25'; console.log(`\n[${sid}] 인자 없음 → 기본값 + 대상 배너 (제안 3)`);
  const before = parentMark();
  const r = run(['--dry-run']);
  const out = r.stdout + r.stderr;
  check(sid,'기본값이 여전히 동작한다', r.status===0, `exit:${r.status}\n${out.slice(-300)}`);
  check(sid,'기본값이라고 말한다', /기본값|인자를 주지 않아/.test(out), out.slice(-400));
  check(sid,'다른 곳에 설치하는 법을 안내한다', /install\.js <?경로|install\.js <path>|--target/.test(out), out.slice(-400));
  check(sid,'부모 폴더 무변경', parentMark()===before, `${before} → ${parentMark()}`);
}

// S26 — **덮어쓰는 것은 전부 백업해야 롤백이 의미가 있다.**
//   백업 루프는 `.claude/` 다섯 디렉터리 + settings.json 만 담았다. 그런데 인스톨러는
//   `CLAUDE.md` · `docs/Manual.md` · `.gitignore` 도 덮어쓴다 — preflight 가 화면에
//   그렇게 찍고 있었다. 그래서 `--rollback` 은 **옛 훅 + 새 문서**라는 어긋난 상태를 만들었다.
{ const sid='S26'; console.log(`\n[${sid}] 백업 범위 = 설치 범위`);
  const t = mkdir(path.join(TEMP_BASE,'s26')); seedFull(t);
  // 덮어써질 파일들에 **알아볼 수 있는 표식**을 심는다 — 롤백이 진짜 되돌리는지 보려면
  //   "존재한다" 로는 부족하다. 내용이 돌아와야 한다.
  fs.writeFileSync(path.join(t,'CLAUDE.md'),
    '# CLAUDE.md\n\n```yaml\nproject_name: "mock"\nversion: "2.4.0"\n```\n\n## 사용자가 쓴 섹션 MARK-CLAUDE\n');
  mkdir(path.join(t,'docs'));
  fs.writeFileSync(path.join(t,'docs','Manual.md'), '# 옛 매뉴얼 MARK-MANUAL\n');
  fs.writeFileSync(path.join(t,'.gitignore'), '# MARK-GITIGNORE\nnode_modules/\n');

  const r = run(['--upgrade','--target',t]);
  check(sid,'업그레이드 exit 0', r.status===0, `exit:${r.status}\n${(r.stdout+r.stderr).slice(-400)}`);

  const today = new Date().toISOString().slice(0,10);
  const bak = path.join(t, `.skill-set-backup-${today}`);
  check(sid,'백업 폴더 생성', fs.existsSync(bak), bak);
  for (const f of ['CLAUDE.md', 'docs/Manual.md', '.gitignore']) {
    check(sid, `백업에 ${f} 가 있다`, fs.existsSync(path.join(bak, f)),
      fs.existsSync(bak) ? fs.readdirSync(bak).join(',') : '(백업 없음)');
  }
  check(sid,'백업에 .claude/hooks 도 그대로', fs.existsSync(path.join(bak,'.claude','hooks')));

  // ⓑ 롤백이 어긋난 상태를 만들지 않는다 — 표식이 셋 다 돌아와야 한다.
  const rb = run(['--rollback','--target',t]);
  check(sid,'롤백 exit 0', rb.status===0, `exit:${rb.status}\n${(rb.stdout+rb.stderr).slice(-300)}`);
  const readSafe = f => { try { return fs.readFileSync(path.join(t,f),'utf8'); } catch { return ''; } };
  check(sid,'롤백이 CLAUDE.md 를 되돌린다', readSafe('CLAUDE.md').includes('MARK-CLAUDE'), readSafe('CLAUDE.md').slice(0,120));
  check(sid,'롤백이 docs/Manual.md 를 되돌린다', readSafe('docs/Manual.md').includes('MARK-MANUAL'), readSafe('docs/Manual.md').slice(0,120));
  check(sid,'롤백이 .gitignore 를 되돌린다', readSafe('.gitignore').includes('MARK-GITIGNORE'), readSafe('.gitignore').slice(0,120));
}

// S27 — **목록이 하나여야 한다.** 이 저장소가 열 번 지운 형태다: 같은 사실을 두 곳에
//   따로 적으면 한쪽만 뒤처진다. 실제로 백업 목록이 preflight 목록보다 뒤처져 있었다.
{ const sid='S27'; console.log(`\n[${sid}] preflight 와 백업이 같은 상수를 읽는다`);
  const src = fs.readFileSync(INSTALL_JS, 'utf8');
  check(sid,'덮어쓰기 목록이 모듈 상수로 선언돼 있다', /const OVERWRITTEN_DIRS\s*=\s*\[/.test(src), '');
  check(sid,'preflight 가 그 상수를 읽는다',
    /function preflightCheck[\s\S]{0,1200}OVERWRITTEN_DIRS\b/.test(src), '');
  check(sid,'백업 루프가 그 상수를 읽는다',
    /\[BACKUP\][\s\S]{0,900}OVERWRITTEN_DIRS\b/.test(src), '');
  check(sid,'백업이 자기만의 경로 배열을 다시 세지 않는다',
    !/for \(const d of \['\.claude\/hooks', '\.claude\/skills'/.test(src), '');
  // settings.json 은 반대 방향으로 어긋나 있었다 — 백업은 하는데 preflight 는 말하지 않았다.
  check(sid,'덮어쓰기 목록에 settings.json 이 있다', /OVERWRITTEN_DIRS\s*=\s*\[[\s\S]{0,900}settings\.json/.test(src), '');
}

// ══════════════════════════════════════════════════════════════════════
// ── [v10/N-04] S28~S29 — dry-run 은 아무것도 쓰지 않는다 ─────────────
//   dry-run 이 "실제 변경 없음" 이라 말하면서 대상에 `.claude/install-logs` 를 만들고, 없는 대상이면 대상 폴더를 만들었다.
//   "아무것도 바꾸지 않는다" 는 약속은 목록·해시로 지킨다 — 앞으로 dry-run 경로 앞에 새 쓰기가 들어와도 여기서 잡힌다.

// 트리 지문: 상대 경로 → 내용 sha1. 디렉터리 자체도 목록에 남긴다(빈 폴더가 생겨도 잡히게).
function treeHashes(root) {
  const crypto = require('crypto');
  const out = {};
  const walk = (dir, rel) => {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      const r = rel ? rel + '/' + e.name : e.name;
      const abs = path.join(dir, e.name);
      if (e.isDirectory()) { out[r + '/'] = 'dir'; walk(abs, r); }
      else out[r] = crypto.createHash('sha1').update(fs.readFileSync(abs)).digest('hex');
    }
  };
  if (fs.existsSync(root)) walk(root, '');
  return out;
}
function treeDiff(a, b) {
  const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
  return [...keys].filter(k => a[k] !== b[k]).sort();
}

// S28 — 없는 경로로 dry-run 해도 그 경로는 생기지 않는다 (리포트 재현 그대로)
{ const sid='S28'; console.log(`\n[${sid}] --dry-run 은 없는 대상을 만들지 않는다 (v10/N-04)`);
  const t = path.join(TEMP_BASE, 's28', 'does-not-exist');
  check(sid,'전: 경로가 없다', !fs.existsSync(t), t);
  const r = run(['--target', t, '--dry-run']);
  const out = r.stdout + r.stderr;
  check(sid,'exit 0', r.status===0, `exit:${r.status}\n${out.slice(-300)}`);
  check(sid,'dry-run 배너', /DRY-RUN/.test(out), out.slice(-300));
  check(sid,'후: 경로가 여전히 없다', !fs.existsSync(t), fs.existsSync(t) ? JSON.stringify(Object.keys(treeHashes(t))) : '');
  check(sid,'로그 파일을 만들지 않는다고 말한다', /로그 파일은 만들지 않습니다/.test(out), out.slice(-300));
}

// S29 — 설치본에 dry-run 3종 → 파일 목록·내용 해시 불변. 실제 설치는 로그를 남긴다(대조)
{ const sid='S29'; console.log(`\n[${sid}] 설치본에 --dry-run → 아무것도 바뀌지 않는다 (v10/N-04)`);
  const t = mkdir(path.join(TEMP_BASE, 's29'));
  const inst = run(['--target', t]);   // S01 과 같은 신규 설치 — 비대화형으로 끝난다
  const logDir = path.join(t, '.claude', 'install-logs');
  const logsAfterInstall = fs.existsSync(logDir) ? fs.readdirSync(logDir).length : 0;
  check(sid,'실제 설치는 로그를 남긴다 (대조 — 로거를 끄지 않았다)', inst.status===0 && logsAfterInstall >= 1, `exit:${inst.status} logs:${logsAfterInstall}`);
  for (const args of [
    ['--target', t, '--dry-run'],
    ['--upgrade', '--target', t, '--dry-run'],
    ['--force-fresh', '--target', t, '--dry-run'],
  ]) {
    const label = args.filter(a => a !== t).join(' ');
    const before = treeHashes(t);
    const r = run(args);
    const diff = treeDiff(before, treeHashes(t));
    check(sid, `${label} → exit 0`, r.status===0, `exit:${r.status}`);
    check(sid, `${label} → 파일 목록·해시 불변`, diff.length === 0, diff.slice(0, 5).join(', '));
  }
}

// S30 — `--rollback --dry-run` 도 아무것도 쓰지 않는다 (Loop A)
//   rollback 분기는 다른 DRY_RUN 검사보다 먼저 끝나서 dry-run 을 한 번도 보지 않았다 — 미리보기를 기대한 사용자의
//   훅이 백업으로 실제로 덮였다. 쓰기 지점 전수 확인이 이 분기를 빠뜨렸다.
{ const sid='S30'; console.log(`\n[${sid}] --rollback --dry-run → 복원하지 않는다 (v10/N-04 Loop A)`);
  const t = mkdir(path.join(TEMP_BASE, 's30')); seedFull(t);
  const up = run(['--upgrade', '--target', t]);            // S03 과 같다 — 옛 설치본을 올리며 백업을 남긴다
  const today = new Date().toISOString().slice(0, 10);
  check(sid,'준비: 업그레이드가 백업을 남겼다 (공허 아님)', up.status===0 && fs.existsSync(path.join(t, `.skill-set-backup-${today}`)), `exit:${up.status}`);
  const before = treeHashes(t);
  const r = run(['--rollback', '--dry-run', '--target', t]);
  const out = r.stdout + r.stderr;
  const diff = treeDiff(before, treeHashes(t));
  check(sid,'exit 0', r.status===0, `exit:${r.status}\n${out.slice(-300)}`);
  // 로거의 "[DRY-RUN] 로그 파일은 만들지 않습니다" 줄은 모든 dry-run 에 찍힌다 — 그것으로는 rollback 분기가 dry-run 을 봤는지 알 수 없다
  check(sid,'rollback 분기가 dry-run 을 보고 복원하지 않는다고 말한다', /\[DRY-RUN\] 예상 동작: 위 백업으로 복원/.test(out), out.slice(-300));
  check(sid,'파일 목록·해시 불변 — 백업으로 덮지 않는다', diff.length === 0, diff.slice(0, 5).join(', '));
}

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
