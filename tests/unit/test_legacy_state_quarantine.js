// tests/unit/test_legacy_state_quarantine.js
// 옛 브랜치 상태의 출처 판정 — charter harness-v10-upgrade-integrity / N-01
//
// 왜 이 파일이 존재하는가:
//   구 하네스가 새 브랜치 상태를 git 추적 템플릿(docs/memory/pipeline-state.json)을 통째로 복사해 만들었다(S6-4).
//   v3.3.1 의 rename 마이그레이션은 그 복사본을 출처 확인 없이 새 이름으로 옮기고, seedBranchState 는
//   파일이 있으니 아무것도 하지 않는다(RISK-01). 다운스트림 api-test-server 의 release/v7.0 이
//   7월에 멈춘 **다른 주제의 Draft PRD** 를 활성으로 보였다.
//
//   이 노드의 위험은 "고치지 못함" 이 아니라 **"진행 중인 상태를 날림"** 이다. 그래서 격리해야 하는 경우와
//   **절대 격리하면 안 되는 경우**를 한 파일에 둔다. 후자(LQ-03·04)는 수정 전에도 후에도 초록이어야 한다.
//
// 왜 CLI 가 아니라 require 로 로드를 일으키는가:
//   _state.js 는 require 시점에 브랜치 상태를 옮기고 시딩하는 부작용이 있다. CLI(status 등)는 그 뒤에
//   자기 기록을 더할 수 있어 "원본이 바이트 그대로" 라는 단언이 이 노드와 무관한 이유로 깨질 수 있다.
//   require 만 하면 _state.js 의 로드 동작만 본다. 통합 확인(LQ-01 끝)만 CLI 로 한다.

'use strict';

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { spawnSync } = require('child_process');
const SB = require('./_sandbox.js');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 200) : ''}`); }
}

// 구 하네스 시절 템플릿의 실제 모양 (api-test-server 에서 관측한 키 구성)
const TEMPLATE = {
  phase: 'prd', track: 'C', activePrd: 'docs/prds/other-topic-prd.md', activePlan: null,
  artifacts: { prd: { path: 'docs/prds/other-topic-prd.md', status: 'Draft' } },
  updated_at: '2026-07-21T00:00:00.000Z', iterations: {}, lastFailure: null, autoFixCount: 0,
};
const BRANCH = 'feat/sbx';   // _sandbox.makeSandbox 의 기본 브랜치
// 브랜치 폴더 이름 — _state.js 의 sanitizeBranch / legacySanitize 와 같은 규칙.
//   require 로 계산하면 그 순간 시딩 부작용이 일어나 레거시 폴더를 먼저 둘 수 없다. 규칙이 바뀌면 LQ-01 이 빨개진다.
const LEGACY_DIR = '.branch-' + BRANCH.replace(/[^a-zA-Z0-9_-]/g, '-');
const NEW_DIR = LEGACY_DIR + '-' + crypto.createHash('sha1').update(BRANCH).digest('hex').slice(0, 6);

function box(opts) {
  opts = opts || {};
  const files = {};
  if (opts.template !== false) files['docs/memory/pipeline-state.json'] = JSON.stringify(opts.template || TEMPLATE, null, 2) + '\n';
  return SB.makeSandbox({ prefix: 'lq', files });
}
function putState(d, dirName, obj) {
  const dir = path.join(d, '.claude', dirName);
  fs.mkdirSync(dir, { recursive: true });
  const bytes = JSON.stringify(obj, null, 2) + '\n';
  fs.writeFileSync(path.join(dir, 'pipeline-state.json'), bytes);
  return bytes;
}
function load(d) {   // _state.js 의 로드 부작용만 일으킨다
  const r = spawnSync(process.execPath, ['-e', `require(${JSON.stringify(path.join(d, '.claude', 'hooks', '_state.js'))})`], {
    cwd: d, encoding: 'utf8', timeout: 30000, env: Object.assign({}, process.env, { CLAUDE_PROJECT_DIR: d }),
  });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || '') };
}
const readBytes = (d, dirName, f) => { try { return fs.readFileSync(path.join(d, '.claude', dirName, f || 'pipeline-state.json'), 'utf8'); } catch { return null; } };
const legacyFiles = (d, dirName) => { try { return fs.readdirSync(path.join(d, '.claude', dirName)).filter(f => /^pipeline-state\.legacy-.+\.json$/.test(f)); } catch { return []; } };
const events = (d, ev) => SB.auditRows(d).filter(r => r.event === ev);

console.log('\n═══ LQ: 옛 브랜치 상태의 출처 판정 (v10/N-01) ═══');

// ── LQ-01 레거시(접미 없는) 폴더의 템플릿 복사본 → 옮기고 격리하고 analysis 로 시딩 ──
console.log('\n[LQ-01] should_quarantine_template_copy_in_legacy_dir');
{
  const d = box();
  try {
    const original = putState(d, LEGACY_DIR, TEMPLATE);        // 구 하네스의 copyFileSync 재연
    const r = load(d);
    check('로드 성공', r.code === 0, r.out);
    check('레거시 폴더가 새 이름으로 옮겨졌다', !fs.existsSync(path.join(d, '.claude', LEGACY_DIR)) && fs.existsSync(path.join(d, '.claude', NEW_DIR)),
      fs.readdirSync(path.join(d, '.claude')).join(','));
    const st = JSON.parse(readBytes(d, NEW_DIR) || '{}');
    check('다시 시딩돼 phase 는 analysis', st.phase === 'analysis', `phase=${st.phase}`);
    check('옛 활성 PRD 를 물려받지 않는다', st.activePrd === null, `activePrd=${st.activePrd}`);
    check('seededFrom 이 생겼다', typeof st.seededFrom === 'string' && st.seededFrom.startsWith('DEFAULT_STATE@'), `seededFrom=${st.seededFrom}`);
    const lf = legacyFiles(d, NEW_DIR);
    check('원본이 격리 파일로 남았다', lf.length === 1, lf.join(','));
    check('격리 파일은 원본과 바이트 동일', lf.length === 1 && readBytes(d, NEW_DIR, lf[0]) === original);
    check('시딩 상태가 격리 파일을 가리킨다', lf.length === 1 && st.quarantinedLegacy === lf[0], `quarantinedLegacy=${st.quarantinedLegacy}`);
    check('감사: state-quarantined', events(d, 'state-quarantined').length === 1, JSON.stringify(events(d, 'state-quarantined')));
    check('감사: state-seeded 의 사유가 legacy-template-copy', events(d, 'state-seeded').some(e => e.reason === 'legacy-template-copy'),
      JSON.stringify(events(d, 'state-seeded').map(e => e.reason)));
    // 통합 — 실제 CLI 가 보는 상태
    const s = SB.cli(d, ['status']);
    check('status 가 analysis 를 보인다', /phase:\s*analysis/.test(s.out), s.out.slice(0, 200));
  } finally { SB.cleanup(d); }
}

// ── LQ-02 이미 마이그레이션된 폴더의 복사본 · 의미 없는 기록만 다름 → 격리 ─────────
//   "마이그레이션 직후 한 번만" 판정하면 이 경우를 영영 다시 보지 않는다.
console.log('\n[LQ-02] should_quarantine_already_migrated_copy_with_only_bookkeeping_diffs');
{
  const d = box();
  try {
    const original = putState(d, NEW_DIR, Object.assign({}, TEMPLATE, { promptSeq: 7, updated_at: '2026-09-14T01:48:34.000Z' }));
    const r = load(d);
    check('로드 성공', r.code === 0, r.out);
    const st = JSON.parse(readBytes(d, NEW_DIR) || '{}');
    check('격리돼 analysis 로 시딩됐다', st.phase === 'analysis' && typeof st.seededFrom === 'string', `phase=${st.phase} seededFrom=${st.seededFrom}`);
    const lf = legacyFiles(d, NEW_DIR);
    check('원본(promptSeq 포함)이 바이트 그대로 격리 파일에', lf.length === 1 && readBytes(d, NEW_DIR, lf[0]) === original, lf.join(','));
  } finally { SB.cleanup(d); }
}

// ── LQ-03 복사본이어도 v3 사이클이 시작됐으면 절대 건드리지 않는다 (RISK-01) ─────────
//   api-test-server release/v7.0 의 실제 모양 — 템플릿 복사 위에서 이미 사이클이 돌고 있다.
console.log('\n[LQ-03] should_never_quarantine_a_copy_that_has_a_cycle');
{
  const d = box();
  try {
    const original = putState(d, NEW_DIR, Object.assign({}, TEMPLATE, {
      promptSeq: 12, cycle: { startedAt: '2026-09-14T01:50:00.000Z', touched: [], needsDecision: [] },
    }));
    const r = load(d);
    check('로드 성공', r.code === 0, r.out);
    check('상태 파일이 바이트 그대로', readBytes(d, NEW_DIR) === original);
    check('격리 파일이 생기지 않았다', legacyFiles(d, NEW_DIR).length === 0);
    check('감사: state-quarantined 없음', events(d, 'state-quarantined').length === 0);
  } finally { SB.cleanup(d); }
}

// ── LQ-04 템플릿과 다른 옛 상태 · 템플릿 파일 없음 · seededFrom 있음 → 전부 바이트 그대로 ──
console.log('\n[LQ-04] should_leave_unprovable_states_byte_identical');
{
  const cases = [
    ['템플릿과 다른 옛 상태(진행 중이던 dev)', {}, Object.assign({}, TEMPLATE, { phase: 'dev', activePrd: 'docs/prds/real-work-prd.md' })],
    ['템플릿 파일이 없다', { template: false }, TEMPLATE],
    ['seededFrom 이 있다 (v3.1+ 에서 시딩된 상태)', {}, Object.assign({}, TEMPLATE, { seededFrom: 'DEFAULT_STATE@abc1234' })],
  ];
  for (const [label, opts, state] of cases) {
    const d = box(opts);
    try {
      const original = putState(d, NEW_DIR, state);
      const r = load(d);
      check(`${label} — 로드 성공`, r.code === 0, r.out);
      check(`${label} — 바이트 그대로`, readBytes(d, NEW_DIR) === original);
      check(`${label} — 격리 없음`, legacyFiles(d, NEW_DIR).length === 0 && events(d, 'state-quarantined').length === 0);
    } finally { SB.cleanup(d); }
  }
}

// ══ 배너 — 증명되지 않는 옛 상태는 막지 않고 활성 PRD 의 출처를 알린다 ══════════════
//   격리할 수 없는 경우(LQ-03 의 모양 — 복사 위에서 이미 사이클이 돈다)가 실사고였다.
//   경고 근거는 **이미 디스크에 있는 관측**: seededFrom 없음 + 활성 PRD 가 템플릿의 활성 PRD 와 같음 + 승인 없음.
//   새 하네스 CLI 가 활성 PRD 를 템플릿의 오래된 값으로 되돌려 설정할 이유는 없다.
function prompt(d) {
  return SB.hook(d, 'session-gate.js', { hook_event_name: 'UserPromptSubmit', session_id: 'sLQ', prompt: '상태 알려줘', cwd: d });
}
// 배너 박스는 62칸에서 줄을 자른다(session-gate BOX_W) — 설명·PRD 경로·안내를 한 줄에 담으면 뒤가 잘린다.
// 그래서 경고는 [설명 줄, 경로 줄, 안내 줄] 세 줄로 붙어 나와야 한다.
function provenanceWarning(out) {
  const lines = out.split('\n');
  const i = lines.findIndex(l => /옛 템플릿/.test(l));
  return i === -1 ? null : { line: lines[i], block: lines.slice(i + 1, i + 3) };
}
const CYCLE_COPY = Object.assign({}, TEMPLATE, { promptSeq: 12, cycle: { startedAt: '2026-09-14T01:50:00.000Z', touched: [], needsDecision: [] } });

console.log('\n[LQ-05] should_warn_when_active_prd_came_from_the_old_template');
{
  const d = box();
  try {
    putState(d, NEW_DIR, CYCLE_COPY);                  // api-test-server release/v7.0 의 모양
    const r = prompt(d);
    const w = provenanceWarning(r.stdout);
    check('옛 템플릿 출처 경고가 뜬다', !!w, r.stdout.slice(0, 400) || '(출력 없음)');
    check('바로 다음 줄이 활성 PRD 경로를 잘리지 않고 보인다', !!w && w.block[0].includes(TEMPLATE.activePrd), w ? w.block[0] : '');
    check('그다음 줄이 new-cycle 로 버리는 법을 안내한다', !!w && /new-cycle/.test(w.block[1] || ''), w ? w.block[1] : '');
    check('막지 않는다 (훅 정상 종료)', r.code === 0, `exit=${r.code}`);
  } finally { SB.cleanup(d); }
}

console.log('\n[LQ-06] should_not_warn_for_a_legacy_state_whose_prd_is_not_the_templates');
{
  // feature-tracking-gis-ingest 의 모양 — seededFrom 없지만 활성 PRD 가 템플릿과 다르다. 오탐이면 경고는 읽히지 않는다.
  const d = box();
  try {
    putState(d, NEW_DIR, Object.assign({}, TEMPLATE, { phase: 'dev', activePrd: 'docs/prds/real-work-prd.md' }));
    const r = prompt(d);
    check('출처 경고가 없다', !/옛 템플릿/.test(r.stdout), r.stdout.split('\n').filter(l => /템플릿/.test(l)).join(' | '));
  } finally { SB.cleanup(d); }
}

console.log('\n[LQ-07] should_stop_warning_once_the_user_approved_that_prd');
{
  const d = box();
  try {
    putState(d, NEW_DIR, Object.assign({}, CYCLE_COPY, { lastApproval: { prd: TEMPLATE.activePrd, at: '2026-09-14T02:00:00.000Z', via: 'user' } }));
    const r = prompt(d);
    check('사용자가 승인한 PRD 면 경고가 없다', !provenanceWarning(r.stdout),r.stdout.split('\n').filter(l => /옛 템플릿/.test(l)).join(' | '));
  } finally { SB.cleanup(d); }
}

console.log('\n[LQ-08] should_show_where_the_quarantined_original_went');
{
  const d = box();
  try {
    putState(d, LEGACY_DIR, TEMPLATE);
    const r = prompt(d);                               // 첫 로드가 격리·시딩하고 곧바로 배너를 만든다
    const st = JSON.parse(readBytes(d, NEW_DIR) || '{}');
    const name = st.quarantinedLegacy || '(격리 안 됨)';
    const lines = r.stdout.split('\n');
    const i = lines.findIndex(l => /격리/.test(l));
    check('배너가 격리를 알린다', i !== -1, lines.filter(l => /격리|시딩/.test(l)).join(' | ') || r.stdout.slice(0, 300));
    check('바로 다음 줄이 원본 파일 이름을 잘리지 않고 보인다', i !== -1 && (lines[i + 1] || '').includes(name),r.stdout.split('\n').filter(l => /격리|시딩/.test(l)).join(' | ') || r.stdout.slice(0, 300));
  } finally { SB.cleanup(d); }
}

// ══ Loop A 지적 반영 (리뷰 판정: 수정 필요) ══════════════════════════════════════
// 샌드박스의 _state.js 를 require 한 뒤 body 를 돌리고, 마지막 stdout 줄의 JSON 을 돌려준다.
function inBox(d, body) {
  const code = `const fs = require('fs'); const S = require(${JSON.stringify(path.join(d, '.claude', 'hooks', '_state.js'))}); ${body}`;
  const r = spawnSync(process.execPath, ['-e', code], {
    cwd: d, encoding: 'utf8', timeout: 30000, env: Object.assign({}, process.env, { CLAUDE_PROJECT_DIR: d }),
  });
  const last = (r.stdout || '').trim().split('\n').pop();
  try { return JSON.parse(last); } catch { return { error: ((r.stdout || '') + (r.stderr || '')).slice(0, 300) }; }
}
const FIRST_LOAD = { phase: 'analysis', track: null, activePrd: null, activePlan: null, artifacts: {}, seededFrom: 'DEFAULT_STATE@race000' };

console.log('\n[LQ-09] should_not_judge_provenance_when_the_state_file_is_the_template_itself');
{
  // 브랜치를 알 수 없는 저장소(git 없음) — _state.js 의 STATE_FILE 이 곧 docs/memory/pipeline-state.json 이다.
  //   그 상태는 복사본이 아니라 원본이다. 자기 자신과 비교하면 항상 같아 매 프롬프트 경고가 뜬다(Loop A HIGH).
  const os = require('os');
  const d = fs.mkdtempSync(path.join(os.tmpdir(), 'lq-nogit-'));
  try {
    fs.mkdirSync(path.join(d, '.claude', 'hooks'), { recursive: true });
    for (const f of fs.readdirSync(SB.HOOKS_SRC)) {
      if (f.endsWith('.js')) fs.copyFileSync(path.join(SB.HOOKS_SRC, f), path.join(d, '.claude', 'hooks', f));
    }
    fs.mkdirSync(path.join(d, 'docs', 'memory'), { recursive: true });
    fs.writeFileSync(path.join(d, 'CLAUDE.md'), SB.DEFAULT_CLAUDE_MD);
    fs.writeFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'), '');
    fs.writeFileSync(path.join(d, '.claude', 'wizard-state.json'), JSON.stringify({ initialized: true }));   // git 위저드를 건너뛴다 — 비대화형
    // phase 는 템플릿 그대로(prd). dev 이상이면 불변식 검사가 없는 PRD 파일을 읽다 던져 이 경로를 **가린다** —
    //   처음 이 단언을 dev 로 썼을 때 수정 전에도 초록이었다(공허). LQ-12 가 그 가림 자체를 따로 본다.
    const original = JSON.stringify(TEMPLATE, null, 2) + '\n';
    fs.writeFileSync(path.join(d, 'docs', 'memory', 'pipeline-state.json'), original);
    const r = prompt(d);
    check('배너가 이 상태를 읽었다 (활성 PRD 표시 — 공허 통과 아님)', r.stdout.includes('other-topic-prd'), r.stdout.slice(0, 300) || r.out.slice(0, 300));
    check('템플릿 자신과 비교하지 않는다 — 출처 경고 없음', !/옛 템플릿/.test(r.stdout), r.stdout.split('\n').filter(l => /템플릿/.test(l)).join(' | '));
    const memFiles = fs.readdirSync(path.join(d, 'docs', 'memory'));
    check('격리하지 않는다 — 격리 파일 없음', !memFiles.some(f => /legacy/.test(f)), memFiles.join(', '));
  } finally { SB.cleanup(d); }
}

console.log('\n[LQ-12] should_warn_even_when_the_copied_prd_file_is_not_on_this_branch');
{
  // 복사본이 dev 이상이고 그 PRD 파일이 이 브랜치에 없다 — 옛 브랜치 상태의 흔한 모양이다.
  //   불변식 검사(phase ≥ dev 면 PRD 파일을 읽는다)가 던지면 같은 try 안의 출처 경고까지 조용히 사라졌다.
  const d = box();
  try {
    putState(d, NEW_DIR, Object.assign({}, CYCLE_COPY, { phase: 'dev' }));
    const r = prompt(d);
    const w = provenanceWarning(r.stdout);
    check('PRD 파일이 없어도 출처 경고가 뜬다', !!w && w.block[0].includes(TEMPLATE.activePrd), r.stdout.split('\n').filter(l => /Phase|PRD|템플릿|불변식/.test(l)).join(' | '));
    check('막지 않는다 (훅 정상 종료)', r.code === 0, `exit=${r.code}`);
  } finally { SB.cleanup(d); }
}

console.log('\n[LQ-10] should_never_overwrite_an_existing_state_not_even_for_the_quarantine_seed');
{
  // 여러 훅이 같은 순간 로드한다: A 가 rename 으로 격리하는 사이 다른 프로세스가 상태를 먼저 만들고 **실제 저장까지** 할 수 있다.
  //   r2 는 격리 승자에게 overwrite 를 줘 quarantinedLegacy 연결을 지키려 했다 — 그러면 그 저장이 지워진다
  //   (잠금 밖, 백업 없음 — Loop A 재검 HIGH · RISK-01). 배너 알림 하나를 잃는 편이 진행을 잃는 것보다 낫다:
  //   원본은 격리 파일로 남고 감사 행이 그 이름을 적는다.
  //   경합 자체는 결정적으로 재현할 수 없어, 경합이 남기는 **모양**을 직접 둔다 — 그 자리에 이미 저장된 상태가 있다.
  const d = box();
  try {
    const SAVED = Object.assign({}, FIRST_LOAD, { phase: 'prd', activePrd: 'docs/prds/racing-save-prd.md', promptSeq: 3 });
    const original = putState(d, NEW_DIR, SAVED);
    const o = inBox(d, [
      "const f = S.STATE_FILE;",
      "const a = S.seedBranchState('first-load');",
      "const b = S.seedBranchState('legacy-template-copy', { quarantinedLegacy: 'pipeline-state.legacy-x.json' }, { overwrite: true });",
      "console.log(JSON.stringify({ a, b, bytes: fs.readFileSync(f, 'utf8') }));",
    ].join(' '));
    check('기본 시딩은 있는 상태를 덮지 않는다 (RISK-01)', o.a === false, JSON.stringify({ a: o.a, error: o.error }));
    check('격리 시딩도 덮지 않는다 — 덮어쓰기 옵션이 없다', o.b === false, JSON.stringify({ b: o.b, error: o.error }));
    check('경합해 저장된 상태가 바이트 그대로', o.bytes === original, (o.bytes || o.error || '').slice(0, 200));
  } finally { SB.cleanup(d); }
}

console.log('\n[LQ-11] should_not_let_extra_fields_rewrite_phase_or_seededFrom_anywhere');
{
  const d = box();
  try {
    putState(d, NEW_DIR, FIRST_LOAD);
    const o = inBox(d, [
      "fs.unlinkSync(S.STATE_FILE);",
      "const ok = S.seedBranchState('lq11', { phase: 'dev', seededFrom: 'EVIL', quarantinedLegacy: 'q.json' });",
      "const st = JSON.parse(fs.readFileSync(S.STATE_FILE, 'utf8'));",
      "console.log(JSON.stringify({ ok, phase: st.phase, seededFrom: st.seededFrom, q: st.quarantinedLegacy }));",
    ].join(' '));
    check('상태: phase·seededFrom 은 고정 · 사연(quarantinedLegacy)은 남는다', o.ok === true && o.phase === 'analysis' && /^DEFAULT_STATE@/.test(o.seededFrom || '') && o.q === 'q.json', JSON.stringify(o));
    const row = events(d, 'state-seeded').filter(x => x.reason === 'lq11').pop();
    check('감사 행: phase·seededFrom 도 고정 · 사연은 남는다', !!row && row.phase === 'analysis' && /^DEFAULT_STATE@/.test(row.seededFrom || '') && row.quarantinedLegacy === 'q.json', JSON.stringify(row || null));
  } finally { SB.cleanup(d); }
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
process.exit(fail > 0 ? 1 : 0);
