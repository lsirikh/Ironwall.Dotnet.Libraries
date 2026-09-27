// tests/unit/test_gates.js
// phase 게이트 — 문서 요구의 크기 판정 (charter harness-v16 / N-25)
//
// 왜 이 파일이 존재하는가:
//   2026-09-17~18 실측: 노드 6개를 돌며 **PRD + Plan + Report 세 문서**에 사이클당
//   15~20분이 들었다. N-24 가 검증 비용을 25~30분에서 3분으로 줄인 뒤로는
//   **문서가 남은 최대 비용**이다.
//
//   값을 한 것과 못 한 것이 섞여 있었다. 문제 진술(실측 수치)·완료 조건·기각안·반증 짝은
//   Loop A 가 실재 결함 15건을 찾은 근거였다. 반면 Plan 의 "순서를 이렇게 잡는 이유" 는
//   PRD 완료 조건의 순서와 거의 같았고, Report 1~3절은 PRD 문제 진술의 과거형이었다 —
//   **같은 내용을 세 번 다시 쓴 것**이다.
//
//   그렇다고 Plan 을 없애지는 않는다. N-23(5파일·5태스크)·N-24(4파일·4태스크)에서는
//   순서 결정이 실제로 값을 했다. **크기로 가른다** — 그 경계가 여기서 검증된다.
//
//   판정은 **하네스가 이미 아는 값**(PRD 의 scope_files 수·태스크 수)으로 한다.
//   모델의 자기 신고를 믿지 않는다. 그리고 **승인 벽은 한 줄도 바뀌지 않는다** —
//   줄이는 것은 문서의 양이지 승인의 수가 아니다. 대조군이 그것을 지킨다.

'use strict';

const fs = require('fs');
const path = require('path');
const SB = require('./_sandbox.js');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 220) : ''}`); }
}

// 봉투 하나 + 그 노드를 선언한 PRD 를 심는다. files/tasks 개수를 인자로 조절한다.
function seed(d, { files, tasks, status = 'Approved' }) {
  const fileList = Array.from({ length: files }, (_, i) => `\`src/f${i + 1}.js\``).join(', ');
  const charter = [
    '# Charter — c-lean', '', '- **상태**: Approved', '', '## 노드', '',
    '- [ ] **[N-01]** 작은 노드', `  - 파일: ${fileList}`, '  - 의존: 없음', '',
    '```yaml', 'charter: c-lean', 'budget_cycles: 5', 'expires: 2026-12-31', 'out_of_scope: 없음', '```',
  ].join('\n');
  fs.mkdirSync(path.join(d, 'docs', 'charters'), { recursive: true });
  fs.writeFileSync(path.join(d, 'docs', 'charters', 'c-lean-charter.md'), charter);

  const taskLines = Array.from({ length: tasks }, (_, i) =>
    `- [ ] **[IMPL-0${i + 1}]** 일 ${i + 1}\n  - 파일: \`src/f1.js\`\n  - 예상 공수: 10m\n`).join('\n');
  const prd = [
    '# PRD — 작은 노드', '',
    `- **상태**: ${status}`,
    '- **charter**: `docs/charters/c-lean-charter.md`',
    '- **node**: N-01',
    `- **scope_files**: ${fileList}`, '',
    '## 문제', '작다.', '',
    '## 태스크', '', taskLines,
  ].join('\n');
  fs.mkdirSync(path.join(d, 'docs', 'prds'), { recursive: true });
  fs.writeFileSync(path.join(d, 'docs', 'prds', 'lean-prd.md'), prd);

  SB.cli(d, ['status']);
  SB.patchState(d, s => {
    s.phase = 'plan'; s.track = 'C';
    s.activePrd = 'docs/prds/lean-prd.md';
    s.activePlan = null;
    s.lastApproval = { prd: 'docs/prds/lean-prd.md', at: new Date().toISOString() };
    return s;
  });
}

console.log('\n═══ GT: 문서 요구의 크기 판정 ═══');

// ── GT-01: 작은 노드는 Plan 없이 dev 로 간다 ─────────────────────────
console.log('\n[GT-01] should_adopt_prd_as_plan_when_node_is_small');
{
  const d = SB.makeSandbox({ prefix: 'gt1' });
  try {
    seed(d, { files: 3, tasks: 3 });
    const r = SB.cli(d, ['dev']);
    check('작은 노드(파일 3 · 태스크 3)가 Plan 없이 dev 로 간다', r.code === 0, r.out);
    check('무엇을 했는지 화면에 말한다 — 조용히 규칙을 바꾸지 않는다',
      /PRD 를 plan 으로 씁니다/.test(r.out), r.out);
    const st = SB.readState(d);
    check('activePlan 이 PRD 를 가리킨다', st.activePlan === 'docs/prds/lean-prd.md', JSON.stringify(st.activePlan));
  } catch (e) { check('GT-01', false, e.message); } finally { SB.cleanup(d); }
}

// ── GT-02: 대조군 — 큰 노드는 여전히 Plan 을 요구한다 ────────────────
//   완화가 과하면 이것이 통과해 버린다. 그러면 크기로 가른다는 설계가 무너진다.
console.log('\n[GT-02] should_still_require_plan_when_node_is_large');
{
  const d = SB.makeSandbox({ prefix: 'gt2' });
  try {
    seed(d, { files: 5, tasks: 3 });
    const r = SB.cli(d, ['dev']);
    check('대조군: 파일이 많으면 Plan 을 요구한다', r.code !== 0 && /activePlan 없음/.test(r.out), r.out);
  } catch (e) { check('GT-02', false, e.message); } finally { SB.cleanup(d); }
}

console.log('\n[GT-03] should_still_require_plan_when_tasks_are_many');
{
  const d = SB.makeSandbox({ prefix: 'gt3' });
  try {
    seed(d, { files: 2, tasks: 5 });
    const r = SB.cli(d, ['dev']);
    check('대조군: 태스크가 많으면 Plan 을 요구한다', r.code !== 0 && /activePlan 없음/.test(r.out), r.out);
  } catch (e) { check('GT-03', false, e.message); } finally { SB.cleanup(d); }
}

// ── GT-04: 대조군 — 승인 벽은 그대로다 ───────────────────────────────
//   이 노드가 줄이는 것은 **문서의 양**이지 **승인의 수**가 아니다.
//   작다는 이유로 승인 안 된 PRD 가 통과하면 봉투 전체가 무의미해진다.
console.log('\n[GT-04] should_keep_approval_wall_when_node_is_small');
{
  const d = SB.makeSandbox({ prefix: 'gt4' });
  try {
    seed(d, { files: 3, tasks: 3, status: 'Draft' });
    SB.patchState(d, s => { s.lastApproval = null; return s; });
    const r = SB.cli(d, ['dev']);
    check('대조군: 작아도 승인 안 된 PRD 는 여전히 막힌다',
      r.code !== 0 && /Approved 아님|승인 기록 없음/.test(r.out), r.out);
  } catch (e) { check('GT-04', false, e.message); } finally { SB.cleanup(d); }
}

// ── GT-05: 대조군 — 태스크 없는 PRD 는 plan 이 될 수 없다 ────────────
console.log('\n[GT-05] should_not_adopt_prd_when_it_has_no_tasks');
{
  const d = SB.makeSandbox({ prefix: 'gt5' });
  try {
    seed(d, { files: 3, tasks: 0 });
    const r = SB.cli(d, ['dev']);
    check('대조군: 태스크가 없는 PRD 는 plan 으로 채택되지 않는다',
      r.code !== 0 && /activePlan 없음/.test(r.out), r.out);
  } catch (e) { check('GT-05', false, e.message); } finally { SB.cleanup(d); }
}
// ── GT-06: 태스크 앵커가 절 밖에 있으면 채택하지 않는다 ─────────────
//   [Loop A 지적] PRD 를 plan 으로 쓰면 `complete` 가 **본문 전체**에서 미완료를 센다.
//   완료 조건이나 검증 절에 `- [ ] **[AC-1]**` 같은 장식용 체크박스를 쓰면 그것이 영원히
//   "미완료 태스크" 로 잡혀 **사이클이 영구히 닫히지 않는다** — 빠져나올 길이 없는 덫이다.
//   별도 Plan 문서를 쓰던 방식에는 없던 위험이므로, 채택 자체를 fail-closed 로 막는다.
console.log('\n[GT-06] should_refuse_adoption_when_task_anchor_is_outside_section');
{
  const d = SB.makeSandbox({ prefix: 'gt6' });
  try {
    seed(d, { files: 3, tasks: 2 });
    // 완료 조건 절에 장식용 체크박스를 하나 심는다 — 덫의 씨앗
    const prdPath = path.join(d, 'docs', 'prds', 'lean-prd.md');
    const body = fs.readFileSync(prdPath, 'utf8')
      .replace('## 문제', '## 완료 조건\n\n- [ ] **[AC-1]** 장식용 체크박스\n\n## 문제');
    fs.writeFileSync(prdPath, body);
    const r = SB.cli(d, ['dev']);
    check('절 밖 앵커가 있으면 PRD 를 plan 으로 채택하지 않는다',
      r.code !== 0 && /activePlan 없음/.test(r.out), r.out);
    check('왜 거부했는지 말한다 — 조용히 떨어지지 않는다',
      /태스크. 절|절에 있습니다/.test(r.out), r.out);
    const st = SB.readState(d);
    check('거부 시 activePlan 잔재가 남지 않는다', !st.activePlan, JSON.stringify(st.activePlan));
  } catch (e) { check('GT-06', false, e.message); } finally { SB.cleanup(d); }
}

// ── GT-07: 대조군 — 승인 전에는 채택 자체를 하지 않는다 (잔재 방지) ──
//   [Loop A 지적] 채택이 게이트보다 먼저 실행되므로, 승인 조건을 먼저 보지 않으면
//   게이트가 거부해도 activePlan 과 감사 로그에 "일어나지 않은 일" 이 남는다.
console.log('\n[GT-07] should_not_adopt_before_approval_is_confirmed');
{
  const d = SB.makeSandbox({ prefix: 'gt7' });
  try {
    seed(d, { files: 3, tasks: 3, status: 'Draft' });
    SB.patchState(d, s => { s.lastApproval = null; return s; });
    SB.cli(d, ['dev']);
    const st = SB.readState(d);
    check('대조군: 미승인 PRD 는 채택되지 않아 activePlan 잔재가 없다',
      !st.activePlan, JSON.stringify(st.activePlan));
    const rows = SB.auditRows(d).filter(r => r && r.event === 'lean-plan-adopted');
    check('대조군: 일어나지 않은 채택이 감사에 남지 않는다', rows.length === 0, JSON.stringify(rows));
  } catch (e) { check('GT-07', false, e.message); } finally { SB.cleanup(d); }
}



console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
