// tests/unit/test_approval_wall.js
// 승인 벽 — charter harness-v3 / N-02 (FR-01·FR-03·FR-04·FR-05·FR-22 · D1·D2·D3·D13)
//
// 왜 이 파일이 존재하는가:
//   "사용자 키워드 없이 Track C 사이클이 닫히지 않는다" 는 명제가 **거짓이었다.** 샌드박스
//   실행으로 두 경로를 확인했다:
//     (a) `--force dev --reason x` → 정상 complete
//     (b) 플래그조차 없이 analysis → complete 직행 (전방 점프는 note 만 남긴다)
//   두 경우 모두 PRD 는 Draft 로 남고 사용자 키워드는 0회이며 커밋까지 생겼다.
//
//   승인 검사가 GATES.plan·GATES.dev 안에만 있었기 때문이다. 그 둘은 건너뛸 수 있다.
//   이 파일은 두 경로가 **같은 지점에서** 닫히는지, 그리고 정상 경로는 열려 있는지를 지킨다.
//   벽이 사용자를 가두면 그것도 실패다 — D2 가 그 방향을 지킨다.

'use strict';

const { spawnSync } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

const REPO = path.resolve(__dirname, '..', '..');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 220) : ''}`); }
}

function git(dir, args) {
  return spawnSync('git', args, { cwd: dir, encoding: 'utf8' }).stdout || '';
}

// 샌드박스 — 훅·CLI 를 실제로 spawn 하되 저장소를 오염시키지 않는다.
function sandbox() {
  const d = fs.mkdtempSync(path.join(os.tmpdir(), 'aw-'));
  fs.mkdirSync(path.join(d, '.claude', 'hooks'), { recursive: true });
  for (const f of fs.readdirSync(path.join(REPO, '.claude', 'hooks'))) {
    const src = path.join(REPO, '.claude', 'hooks', f);
    if (fs.statSync(src).isFile()) fs.copyFileSync(src, path.join(d, '.claude', 'hooks', f));
  }
  for (const sub of ['prds', 'plans', 'memory', 'analyses', 'reports', 'tests', 'charters']) {
    fs.mkdirSync(path.join(d, 'docs', sub), { recursive: true });
  }
  fs.writeFileSync(path.join(d, 'docs', 'memory', 'decisions.jsonl'), '');
  fs.writeFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'), '');
  // 런타임 산물이 관측 공백 게이트에 걸리지 않게 한다 — 실저장소와 같은 무시 규칙
  fs.writeFileSync(path.join(d, '.gitignore'), '.claude/.branch-*/\n.claude/.payload-shapes.json\n');
  fs.writeFileSync(path.join(d, 'CLAUDE.md'), '# T\n\n```yaml\nproject_name: "t"\ntest_command: ""\n```\n');
  git(d, ['init', '-q']);
  git(d, ['config', 'user.email', 't@t']); git(d, ['config', 'user.name', 't']);
  git(d, ['add', '-A']); git(d, ['commit', '-q', '-m', 'seed']);
  // CLI 를 한 번 돌려 브랜치 상태 디렉터리를 만든다 — 그래야 setState 가 CLI 와 같은 파일을 쓴다.
  //   이것 없이 심으면 CLI 는 갓 시딩한 빈 상태를 읽고, 테스트는 **엉뚱한 이유로** 초록이 된다.
  spawnSync(process.execPath, [path.join(d, '.claude', 'hooks', 'advance-phase.js'), 'status'], {
    cwd: d, encoding: 'utf8', timeout: 30000, env: { ...process.env, CLAUDE_PROJECT_DIR: d },
  });
  return d;
}
function cleanup(d) { try { fs.rmSync(d, { recursive: true, force: true }); } catch {} }

function cli(d, args) {
  const r = spawnSync(process.execPath, [path.join(d, '.claude', 'hooks', 'advance-phase.js'), ...args], {
    cwd: d, encoding: 'utf8', timeout: 30000, env: { ...process.env, CLAUDE_PROJECT_DIR: d },
  });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || '') };
}
function stateFile(d) {
  const cd = path.join(d, '.claude');
  const br = fs.readdirSync(cd).find(x => x.startsWith('.branch-'));
  return br ? path.join(cd, br, 'pipeline-state.json') : path.join(d, 'docs', 'memory', 'pipeline-state.json');
}
function readState(d) { try { return JSON.parse(fs.readFileSync(stateFile(d), 'utf8')); } catch { return {}; } }
function setState(d, patch) {
  const s = Object.assign(readState(d), patch);
  fs.mkdirSync(path.dirname(stateFile(d)), { recursive: true });
  fs.writeFileSync(stateFile(d), JSON.stringify(s, null, 2));
}

// Track C 사이클 하나를 심는다. approved=true 면 승인 기록까지.
// 승인 필드를 심을 때 **대응 감사 행도 함께** 심는다 — 근거 대조(N-02)가 상태만 있는
//   승인을 위조로 보기 때문이다. 시드가 두 장부를 같이 쓰지 않으면 정상 경로가 빨개진다.
function seedCycleC(d, { approved }) {
  const apAt = new Date().toISOString();
  if (approved) {
    try { fs.appendFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'),
      JSON.stringify({ timestamp: apAt, event: 'user-approval', target: 'prd', file: 'docs/prds/a-prd.md' }) + String.fromCharCode(10)); } catch {}
  }
  fs.writeFileSync(path.join(d, 'docs', 'prds', 'a-prd.md'), '# A PRD — 제목\n\n- **상태**: ' + (approved ? 'Approved' : 'Draft') + '\n');
  fs.writeFileSync(path.join(d, 'docs', 'plans', 'a-prd-plan.md'), '# A plan\n\n- [x] **[IMPL-01]** 안\n  - 파일: `src/a.js`\n  - 예상 공수: 1h\n');
  git(d, ['add', '-A']); git(d, ['commit', '-q', '-m', 'docs']);
  const base = git(d, ['rev-parse', '--short', 'HEAD']).trim();
  setState(d, {
    phase: 'report', track: 'C',
    activePrd: 'docs/prds/a-prd.md', activePlan: 'docs/plans/a-prd-plan.md',
    lastApproval: approved ? { prd: 'docs/prds/a-prd.md', at: apAt, via: 'user' } : null,
    cycle: { startedAt: new Date(Date.now() - 3600e3).toISOString(), headAtStart: base, touched: [], decisionsAtStart: 0, needsDecision: [], dirtyAtStart: [] },
  });
}

console.log('\n═══ AW: 승인 벽 ═══');

// ── AW-01: --force 로 중간 phase 를 건너뛴 사이클은 닫히지 않는다 (D1) ──
console.log('\n[AW-01] should_refuse_complete_when_forced_past_approval');
{
  const d = sandbox();
  seedCycleC(d, { approved: false });
  const r = cli(d, ['complete']);
  check('Draft PRD + 승인 기록 없음 → complete 거부', r.code !== 0, r.out);
  check('거부 사유가 승인을 가리킨다', /승인/.test(r.out), r.out);
  cleanup(d);
}

// ── AW-02: 무플래그 전방 점프도 같은 지점에서 닫힌다 (D1) ──────────
//   analysis → complete 직행은 plan·dev 게이트를 통째로 건너뛴다. 그래서 승인 검사가
//   그 둘 안에만 있으면 아무 비용 없이 통과했다.
console.log('\n[AW-02] should_refuse_complete_on_forward_jump_without_flags');
{
  const d = sandbox();
  seedCycleC(d, { approved: false });
  setState(d, { phase: 'analysis' });
  const r = cli(d, ['complete']);
  check('analysis → complete 직행 거부', r.code !== 0, r.out);
  check('거부 사유가 승인을 가리킨다', /승인/.test(r.out), r.out);
  cleanup(d);
}

// ── AW-03: 파일만 Approved 이고 승인 기록이 없으면 거부 ────────────
//   상태줄은 증거가 아니다(v3.5 결정 D-f89324). complete 도 같은 규칙을 따른다.
console.log('\n[AW-03] should_not_trust_status_line_at_complete');
{
  const d = sandbox();
  seedCycleC(d, { approved: false });
  fs.writeFileSync(path.join(d, 'docs', 'prds', 'a-prd.md'), '# A PRD — 제목\n\n- **상태**: Approved\n');
  const r = cli(d, ['complete']);
  check('Approved 상태줄 + 승인 기록 없음 → 거부', r.code !== 0, r.out);
  check('사유가 "상태줄은 증거가 아니다" 를 말한다', /증거/.test(r.out), r.out);
  cleanup(d);
}

// ── AW-04: 정상 경로는 열려 있다 (D2) — 벽이 사용자를 가두면 실패다 ──
console.log('\n[AW-04] should_allow_complete_on_approved_cycle');
{
  const d = sandbox();
  seedCycleC(d, { approved: true });
  const r = cli(d, ['complete']);
  check('승인 기록이 있으면 complete 통과', r.code === 0, r.out);
  const prd = fs.readFileSync(path.join(d, 'docs', 'prds', 'a-prd.md'), 'utf8');
  check('PRD 가 Completed 로 바뀐다', /^-\s*\*\*상태\*\*:\s*Completed/im.test(prd), prd.split('\n')[2]);
  cleanup(d);
}

// ── AW-05: Track A·B 는 activePrd 없이 닫힌다 (D3) ─────────────────
//   v3.1 "Track = 검토 깊이" 결정을 뒤집지 않는다.
console.log('\n[AW-05] should_not_require_prd_for_track_a_and_b');
{
  const d = sandbox();
  const base = git(d, ['rev-parse', '--short', 'HEAD']).trim();
  setState(d, { phase: 'report', track: 'A', activePrd: null, activePlan: null,
    cycle: { startedAt: new Date(Date.now() - 3600e3).toISOString(), headAtStart: base, touched: [], decisionsAtStart: 0, needsDecision: [], dirtyAtStart: [] } });
  const a = cli(d, ['complete']);
  check('Track A 는 activePrd 없이 complete 통과', a.code === 0, a.out);
  cleanup(d);
}

// ── AW-08: Track A 에서 코드를 만졌으면 재분류를 요구한다 (FR-21 · D12) ──
//   "Track A = 코드 수정 없음" 이 라벨일 뿐이라 GATES 에 분기가 하나도 없었다.
//   쓰기를 막지는 않는다(v3.1 결정) — 닫을 때 라벨을 실제와 맞추라고 요구할 뿐이다.
console.log('\n[AW-08] should_require_reclassification_when_track_a_touched_code');
{
  const d = sandbox();
  const base = git(d, ['rev-parse', '--short', 'HEAD']).trim();
  // touched 에 심는 경로는 디스크에도 있어야 한다 — complete 는 "관측했는데 사라진" 파일을 거부한다.
  const ensure = ps => { for (const q of ps) { const f = path.join(d, q); fs.mkdirSync(path.dirname(f), { recursive: true }); if (!fs.existsSync(f)) fs.writeFileSync(f, "x"); } git(d, ["add", "-A"]); git(d, ["commit", "-q", "-m", "seed touched"]); };
  const mkCycle = touched => (ensure(touched), { startedAt: new Date(Date.now() - 3600e3).toISOString(), headAtStart: git(d, ["rev-parse", "--short", "HEAD"]).trim(), touched, decisionsAtStart: 0, needsDecision: [], dirtyAtStart: [] });

  // (a) 문서성 파일만 만진 Track A 는 막지 않는다
  setState(d, { phase: 'report', track: 'A', activePrd: null, activePlan: null, cycle: mkCycle(['docs/notes.md', '.claude/skills/x/SKILL.md']) });
  check('문서만 만진 Track A 는 통과', cli(d, ['complete']).code === 0);

  // (b) 코드를 만졌으면 거부하고 재분류를 안내한다
  setState(d, { phase: 'report', track: 'A', activePrd: null, activePlan: null, cycle: mkCycle(['src/a.js']) });
  const bad = cli(d, ['complete']);
  check('코드를 만진 Track A 는 거부', bad.code !== 0, bad.out);
  check('재분류를 안내한다', /set-track/.test(bad.out), bad.out);

  // (c) 탈출로는 열려 있다 — 단 `--force complete` 는 v3.4 결정대로 **사용자 면제**를 함께 요구한다.
  //     Track A 재분류만 예외로 뚫어 주지 않는다. 게이트를 우회하는 비용은 어느 경로든 같다.
  setState(d, { phase: 'report', track: 'A', activePrd: null, activePlan: null, cycle: mkCycle(['src/a.js']) });
  const noWaiver = cli(d, ['complete', '--force', '--reason', '설정 한 줄을 함께 고쳤다']);
  check('면제 없는 --force complete 는 여전히 거부', noWaiver.code !== 0, noWaiver.out);
  setState(d, { userAck: { kind: 'force-waiver', scope: 'complete', at: new Date().toISOString() } });
  const forced = cli(d, ['complete', '--force', '--reason', '설정 한 줄을 함께 고쳤다']);
  check('면제가 있으면 탈출로가 열린다', forced.code === 0, forced.out);
  check('강제 완료 사실이 needsDecision 에 남는다',
    (readState(d).cycle.needsDecision || []).some(x => /force:|trackA/.test(x)),
    JSON.stringify(readState(d).cycle.needsDecision));
  cleanup(d);
}

// ── AW-06: approve 의 --reason 침묵 예외 (FR-05) ───────────────────
console.log('\n[AW-06] should_require_reason_for_force_on_approve');
{
  const d = sandbox();
  seedCycleC(d, { approved: false });
  const r = cli(d, ['approve', 'prd', '--force']);
  check('approve prd --force 는 --reason 없이 통과하지 않는다', r.code !== 0, r.out);
  cleanup(d);
}

// ── AW-07: set-track 이 진행 중 사이클을 리셋하지 않는다 (FR-22 · D13) ──
//   실측: `--force` 로 만든 needsDecision 을 `set-track C` 재호출 한 번이 지웠다.
//   봉투 사이클이면 cycle.charter 까지 사라져 봉투 밖으로 나간다.
console.log('\n[AW-07] should_not_reset_running_cycle_on_set_track');
{
  const d = sandbox();
  seedCycleC(d, { approved: true });
  setState(d, { phase: 'dev' });
  const s0 = readState(d);
  s0.cycle.needsDecision = ['force:analysis→dev'];
  s0.cycle.touched = ['src/a.js'];
  s0.cycle.charter = { id: 'x', node: 'N-01', path: 'docs/charters/x-charter.md' };
  fs.writeFileSync(stateFile(d), JSON.stringify(s0, null, 2));

  const r = cli(d, ['set-track', 'C']);
  const after = readState(d);
  check('set-track 재호출이 exit 0', r.code === 0, r.out);
  check('needsDecision 이 보존된다', (after.cycle.needsDecision || []).includes('force:analysis→dev'),
    JSON.stringify(after.cycle.needsDecision));
  check('touched 가 보존된다', (after.cycle.touched || []).includes('src/a.js'), JSON.stringify(after.cycle.touched));
  check('cycle.charter 가 보존된다', !!(after.cycle.charter && after.cycle.charter.node === 'N-01'), JSON.stringify(after.cycle.charter));
  check('재호출 사실이 needsDecision 에 남는다', (after.cycle.needsDecision || []).some(x => /set-track/.test(x)),
    JSON.stringify(after.cycle.needsDecision));

  // 명시 플래그일 때만 새 사이클
  const r2 = cli(d, ['set-track', 'B', '--new-cycle']);
  const after2 = readState(d);
  check('--new-cycle 이면 리셋된다', r2.code === 0 && (after2.cycle.needsDecision || []).length === 0,
    r2.out + ' ' + JSON.stringify(after2.cycle.needsDecision));
  check('새 사이클에도 롤백 포인트가 있다', !!after2.cycle.checkpointTag, JSON.stringify(after2.cycle.checkpointTag));
  cleanup(d);
}

// ── AW-09: 봉투 재승인 — 상태줄이 아니라 지문으로 판정한다 (v5/N-01) ──
//   다운스트림 sso-suite 가 v3.1.0 실사용 중 보고한 결함이다. 승인된 charter 를 개정한 뒤
//   사용자가 "봉투 승인" 이라고 말하면 **그 발화가 조용히 버려졌다** — ack 후보 선정이
//   상태줄이 문자 그대로 `Draft` 인지만 봤기 때문이다. 사람이 개정하면서 가장 정확하게 쓰는
//   표기(`Approved (개정 r4 — 재승인 대기)`)가 하필 정규식에 안 걸린다.
//
//   막힌 곳은 둘이었다 — ack 기록(session-gate)과 승인 실행(advance-phase 의 `상태줄이
//   Draft 가 아니다` 하드 거부). **하나만 고치면 사용자는 여전히 막힌다.** 그래서 여기서는
//   훅과 CLI 를 둘 다 실제로 구동해 **끝까지** 간다.
console.log('\n[AW-09] should_accept_reapproval_when_charter_revised_after_approval');
{
  const SB = require('./_sandbox.js');
  const CH = (status, extra) => `# Charter — t-reapp

- **상태**: ${status}

\`\`\`yaml
goal: 재승인 경로를 지킨다${extra ? ' ' + extra : ''}
budget_cycles: 2
expires: 2099-12-31
invariants:
  - file:exists CLAUDE.md
out_of_scope:
  - .git/**
\`\`\`

## 노드

- [ ] **[N-01]** 첫 노드
  - 파일: \`src/a.js\`
`;
  const CPATH = 'docs/charters/t-reapp-charter.md';
  const d = SB.makeSandbox({ prefix: 'aw9' });
  try {
    SB.cli(d, ['status']);
    const write = t => fs.writeFileSync(path.join(d, CPATH), t);
    const say = () => SB.hook(d, 'session-gate.js', {
      hook_event_name: 'UserPromptSubmit', session_id: 'sAW9', prompt: '봉투 승인',
    });

    // ⓐ 첫 승인 — 지금도 되는 경로다. 여기가 깨지면 뒤의 판정은 의미가 없다.
    write(CH('Draft'));
    const say1 = say();
    check('초안 charter 에 봉투 승인이 기록된다', (SB.readState(d).userAck || {}).kind === 'charter-approve', say1.out.slice(-300));
    const ap1 = SB.cli(d, ['charter', 'approve', '--charter', CPATH]);
    check('첫 승인이 성공한다', ap1.code === 0, ap1.out.slice(-300));

    // ⓑ 봉투를 개정한다. **상태줄은 Approved 그대로** — 사람이 실제로 하는 방식이다.
    const revised = CH('Approved', '— 개정 r2');
    write(revised);
    const st1 = SB.cli(d, ['charter', 'status']);
    check('개정하면 지문 불일치가 보고된다', /불일치|재승인/.test(st1.out), st1.out.slice(-300));

    // ⓒ 여기가 버그였다 — 이 발화가 버려졌다.
    const say2 = say();
    const ack2 = SB.readState(d).userAck || {};
    check('개정된 charter 에 봉투 승인이 기록된다 (상태줄이 Approved 여도)',
      ack2.kind === 'charter-approve' && !ack2.consumedAt, JSON.stringify(ack2) + ' | ' + say2.out.slice(-300));

    // ⓓ 그리고 승인 실행도 통과해야 한다. ack 만 고치면 여기서 죽는다.
    const ap2 = SB.cli(d, ['charter', 'approve', '--charter', CPATH]);
    check('재승인이 상태줄 수정 요구 없이 성공한다', ap2.code === 0, ap2.out.slice(-300));
    check('재승인 거부가 "Draft 가 아니다" 로 나오지 않는다', !/상태줄이 Draft 가 아니다/.test(ap2.out), ap2.out.slice(-200));

    // ⓔ 재승인 뒤 지문이 현재 내용과 맞아야 C1 이 통과한다 — 이것이 재승인의 본질이다.
    const st2 = SB.cli(d, ['charter', 'status']);
    check('재승인 후 지문이 일치한다', /지문 일치/.test(st2.out), st2.out.slice(-300));
    check('개정 본문이 보존된다 (하네스가 사용자의 글을 덮지 않는다)',
      /개정 r2/.test(fs.readFileSync(path.join(d, CPATH), 'utf8')), 'charter 본문에서 개정 표시가 사라졌다');

    // ⓕ 반대 방향 — 승인 상태이고 내용도 그대로면 승인할 것이 없다.
    //    거절 자체는 옳다. 거절 **사유가 무엇을 하라고 말하는가**가 이 결함의 절반이었다.
    const say3 = say();
    const ack3 = SB.readState(d).userAck || {};
    check('내용 변경이 없으면 ack 을 기록하지 않는다', !!ack3.consumedAt, JSON.stringify(ack3));
    check('거절 사유가 charter 경로를 지목한다', /t-reapp-charter\.md/.test(say3.stdout),
      (say3.stdout.split('\n').filter(l => /승인|봉투/.test(l)).join(' | ') || say3.stdout).slice(-300));
  } finally { SB.cleanup(d); }
}

// ── AW-10: 배너는 살아 있는 봉투를 보여준다 (v5/N-01) ──────────────
//   배너의 봉투 줄은 `readdirSync(...).slice(0, 2)` 였다. charter 가 셋이 되자
//   **방금 연 봉투가 배너에서 사라지고 소진된 봉투 둘이 매 프롬프트마다 남았다**
//   (실제로 이 저장소에서 harness-v5-observe 를 승인한 직후에 그렇게 됐다).
//   "둘까지만" 이라는 임의의 목록이고, 이 저장소가 아홉 번 만난 그 형태다.
//
//   자르는 것 자체는 옳다 — 배너 높이는 유한하다. 바꾸는 것은 **무엇이 남는가**다.
console.log('\n[AW-10] should_keep_live_charter_visible_in_banner');
{
  const SB = require('./_sandbox.js');
  const C = require(path.join(REPO, '.claude', 'hooks', '_charter.js'));
  const mk = (id, status, expires) => `# Charter — ${id}

- **상태**: ${status}

\`\`\`yaml
goal: ${id}
budget_cycles: 2
expires: ${expires}
invariants:
  - file:exists CLAUDE.md
out_of_scope:
  - .git/**
\`\`\`

## 노드

- [ ] **[N-01]** 노드
  - 파일: \`src/a.js\`
`;
  const d = SB.makeSandbox({ prefix: 'aw10' });
  try {
    SB.cli(d, ['status']);
    // 알파벳 앞 두 개는 **끝난 봉투**다 — 승인됐고 변경 없고 만료됐다.
    const old = { 'a-done': mk('a-done', 'Approved', '2020-01-01'), 'b-done': mk('b-done', 'Approved', '2020-01-01') };
    for (const [id, text] of Object.entries(old)) {
      fs.writeFileSync(path.join(d, 'docs', 'charters', `${id}-charter.md`), text);
    }
    // 알파벳 뒤에 있는 것이 **지금 일하는 봉투**다.
    fs.writeFileSync(path.join(d, 'docs', 'charters', 'z-live-charter.md'), mk('z-live', 'Draft', '2099-12-31'));
    SB.patchState(d, s => {
      s.charters = {};
      for (const [id, text] of Object.entries(old)) {
        s.charters[id] = { path: `docs/charters/${id}-charter.md`, approvedHash: C.contentHash(text), approvedAt: '2020-01-01T00:00:00.000Z' };
      }
      return s;
    });

    const r = SB.hook(d, 'session-gate.js', {
      hook_event_name: 'UserPromptSubmit', session_id: 'sAW10', prompt: '상태 보여줘',
    });
    const charterLines = (r.stdout || '').split('\n').filter(l => l.includes('📜'));
    check('살아 있는 봉투가 배너에 남는다', /z-live/.test(charterLines.join(' ')),
      charterLines.join(' | ').slice(0, 300) || '(봉투 줄 없음)');
    check('배너 줄 수 상한은 유지된다 (2개)', charterLines.length <= 2, String(charterLines.length));
  } finally { SB.cleanup(d); }
}



console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
