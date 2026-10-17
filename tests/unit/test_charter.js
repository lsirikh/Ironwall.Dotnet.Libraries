// tests/unit/test_charter.js
// _charter.js 순수 함수 테스트 — 승인 봉투 파서 + 판정기 (v3.5 / TEST-13)
//
// 왜 이 파일이 존재하는가:
//   charter 는 사람의 승인 벽을 **대신**하는 유일한 기제다. 판정기에 구멍이 있으면
//   사용자가 승인하지 않은 작업이 승인된 것처럼 진행된다 — v3 이전에 Draft PRD 로
//   5개 웨이브가 진행된 바로 그 상태로 돌아간다.
//   그래서 C1~C8 을 **각각 단독으로** 거부시킨다. 하나라도 안 막히면 봉투에 구멍이 있다.
//
// 순수 모듈. fs / git 없음. 시각·원장 평가기는 주입한다.

'use strict';

const assert = require('assert');
const path = require('path');

const CHARTER_PATH = path.resolve(__dirname, '../../.claude/hooks/_charter.js');
const C = require(CHARTER_PATH);

let pass = 0, fail = 0;
function test(name, fn) {
  try { fn(); pass++; console.log(`  ✅ ${name}`); }
  catch (e) { fail++; console.log(`  ❌ ${name}: ${e.message} (.claude/hooks/_charter.js)`); }
}

// ── 픽스처 ───────────────────────────────────────────────────────────
const CHARTER_MD = (status) => `# Charter — harness-v3

- **상태**: ${status}

\`\`\`yaml
goal: 하네스가 개발을 관장하게 한다
budget_cycles: 3
expires: 2026-10-07
invariants:
  - grep:.claude/hooks/pre-tool-gate.js /trackAllowsWrite/ ==0
out_of_scope:
  - install.js
  - .git/**
\`\`\`

## 노드

- [ ] **[N-01]** 의사결정 원장
  - 파일: \`.claude/hooks/_signals.js\`, \`tests/unit/test_signals.js\`
- [ ] **[N-02]** 영향 그래프
  - 파일: \`.claude/hooks/_impact.js\`, \`tests/unit/**\`
  - 의존: N-01
- [ ] **[N-03]** 결정 도달
  - 파일: \`.claude/hooks/*.js\`
  - 의존: N-01, N-02
`;

const PRD_MD = (over) => {
  const o = Object.assign({ charter: 'docs/charters/harness-v3-charter.md', node: 'N-02', files: ['.claude/hooks/_impact.js', 'tests/unit/test_impact.js'] }, over || {});
  return `# PRD — 영향 그래프

- **상태**: Draft
${o.charter === null ? '' : `- **charter**: ${o.charter}\n`}${o.node === null ? '' : `- **node**: ${o.node}\n`}${o.files === null ? '' : `- **scope_files**: ${o.files.join(', ')}\n`}
## 1. 문제
`;
};

const NOW = Date.parse('2026-09-07T00:00:00Z');
const okEval = specs => specs.map(s => ({ spec: s, status: 'ok' }));

function ctx(over) {
  const charter = C.parse(CHARTER_MD((over && over.status) || 'Approved'), 'docs/charters/harness-v3-charter.md');
  return Object.assign({
    charter,
    prd: C.extractDeclaration(PRD_MD(over && over.prd)),
    now: NOW,
    consumed: {},                    // { 'N-01': 'docs/prds/x-prd.md' }
    completedNodes: new Set(['N-01']),
    evaluateSpecs: okEval,
  }, over && over.ctx);
}

console.log('\n═══ CHR: 승인 봉투 파서 + 판정기 ═══\n');

// ══ 1. 파싱 ════════════════════════════════════════════════════════════

test('should_parse_charter_as_graph', () => {
  const ch = C.parse(CHARTER_MD('Approved'), 'docs/charters/harness-v3-charter.md');
  assert.strictEqual(ch.errors.length, 0, `파싱 오류: ${ch.errors.join(' / ')}`);
  assert.strictEqual(ch.id, 'harness-v3');
  assert.strictEqual(ch.status, 'Approved');
  assert.strictEqual(ch.budgetCycles, 3);
  assert.strictEqual(ch.expires, '2026-10-07');
  assert.deepStrictEqual(ch.invariants, ['grep:.claude/hooks/pre-tool-gate.js /trackAllowsWrite/ ==0']);
  assert.deepStrictEqual(ch.outOfScope, ['install.js', '.git/**']);
  // 노드는 _plan-graph 문법 그대로 — 파일·의존이 붙는다
  assert.deepStrictEqual(Object.keys(ch.nodes).sort(), ['N-01', 'N-02', 'N-03']);
  assert.deepStrictEqual(ch.nodes['N-02'].files, ['.claude/hooks/_impact.js', 'tests/unit/**']);
  assert.deepStrictEqual(ch.nodes['N-02'].deps, ['N-01']);
  assert.deepStrictEqual(ch.nodes['N-03'].deps, ['N-01', 'N-02']);
});

test('should_reject_toplevel_star_scope', () => {
  // 전체를 봉투에 넣으면 봉투가 아니다
  for (const bad of ['**', '*', '**/*', './**']) {
    const md = CHARTER_MD('Approved').replace('`.claude/hooks/*.js`', '`' + bad + '`');
    const ch = C.parse(md, 'docs/charters/x-charter.md');
    assert.ok(ch.errors.some(e => /N-03/.test(e) && /\*\*|최상위|전체/.test(e)),
      `'${bad}' 는 거부되어야: ${JSON.stringify(ch.errors)}`);
  }
  // 하위 경로의 ** 는 허용 (tests/unit/**)
  const ok = C.parse(CHARTER_MD('Approved'), 'docs/charters/x-charter.md');
  assert.strictEqual(ok.errors.length, 0);
});

test('should_extract_prd_declaration', () => {
  const d = C.extractDeclaration(PRD_MD());
  assert.strictEqual(d.charter, 'docs/charters/harness-v3-charter.md');
  assert.strictEqual(d.node, 'N-02');
  assert.deepStrictEqual(d.files, ['.claude/hooks/_impact.js', 'tests/unit/test_impact.js']);
  // 선언이 없으면 null — 산문에서 추측하지 않는다
  const none = C.extractDeclaration('# PRD\n\n- **상태**: Draft\n');
  assert.strictEqual(none.charter, null);
  assert.strictEqual(none.node, null);
  assert.deepStrictEqual(none.files, []);
});

// ══ 2. 판정 — 정상 통과 ═══════════════════════════════════════════════

test('should_pass_when_all_conditions_hold', () => {
  const r = C.judge(ctx());
  assert.strictEqual(r.ok, true, `실패 조건: ${JSON.stringify(r.failed)}`);
  assert.deepStrictEqual(r.passed.sort(), ['C1', 'C2', 'C3', 'C4', 'C5', 'C6', 'C7', 'C8']);
  assert.strictEqual(r.failed.length, 0);
});

// ══ 3. 판정 — 각 조건이 단독으로 거부한다 ═══════════════════════════
// 하나의 픽스처에서 조건 하나만 깨뜨린다. 다른 조건이 함께 실패하면 원인을 가린다.

test('should_judge_each_condition_independently', () => {
  const only = (r, id) => {
    assert.strictEqual(r.ok, false, `${id}: 통과하면 안 됨`);
    const ids = r.failed.map(f => f.id);
    assert.ok(ids.includes(id), `${id} 가 실패 목록에 있어야: ${JSON.stringify(ids)}`);
    assert.ok(r.failed.every(f => f.reason && f.reason.length > 5), `${id}: 사유가 있어야`);
  };

  // C1 — charter 가 Approved 가 아니다
  only(C.judge(ctx({ status: 'Draft' })), 'C1');

  // C2 — 만료
  only(C.judge(ctx({ ctx: { now: Date.parse('2026-10-08T00:00:00Z') } })), 'C2');

  // C3a — 예산 소진 (budget 3, 이미 3개 소비)
  only(C.judge(ctx({ ctx: { consumed: { 'N-01': 'a', 'N-03': 'b', 'N-09': 'c' }, completedNodes: new Set(['N-01']) } })), 'C3');
  // C3b — 같은 노드를 두 번
  only(C.judge(ctx({ ctx: { consumed: { 'N-02': 'docs/prds/earlier-prd.md' } } })), 'C3');

  // C4a — charter 선언 없음
  only(C.judge(ctx({ prd: { charter: null } })), 'C4');
  // C4b — 다른 charter 를 가리킴
  only(C.judge(ctx({ prd: { charter: 'docs/charters/other-charter.md' } })), 'C4');
  // C4c — 없는 노드
  only(C.judge(ctx({ prd: { node: 'N-99' } })), 'C4');

  // C5a — 노드 파일 밖
  only(C.judge(ctx({ prd: { files: ['.claude/hooks/_impact.js', 'install.js'] } })), 'C5');
  // C5b — scope_files 선언 자체가 없음 → 판정 불가 → 거부
  only(C.judge(ctx({ prd: { files: null } })), 'C5');

  // C6 — out_of_scope 와 겹침 (노드 파일 안이어도)
  {
    // N-03 의 파일은 .claude/hooks/*.js 인데 out_of_scope 에 .git/** 이 있다. 겹치는 파일을 선언한다.
    const md = CHARTER_MD('Approved').replace('  - .git/**', '  - .claude/hooks/_state.js');
    const charter = C.parse(md, 'docs/charters/harness-v3-charter.md');
    const r = C.judge(Object.assign(ctx(), { charter, prd: C.extractDeclaration(PRD_MD({ node: 'N-03', files: ['.claude/hooks/_state.js'] })), completedNodes: new Set(['N-01', 'N-02']) }));
    only(r, 'C6');
  }

  // C7a — 불변식 위반
  only(C.judge(ctx({ ctx: { evaluateSpecs: specs => specs.map(s => ({ spec: s, status: 'violated' })) } })), 'C7');
  // C7b — 불변식 판정 불가(insufficient) 도 실패다
  only(C.judge(ctx({ ctx: { evaluateSpecs: specs => specs.map(s => ({ spec: s, status: 'insufficient' })) } })), 'C7');
  // C7c — 평가기 자체가 없으면 판정 불가 → 거부
  only(C.judge(ctx({ ctx: { evaluateSpecs: null } })), 'C7');

  // C8 — 의존 노드 미완
  only(C.judge(ctx({ ctx: { completedNodes: new Set() } })), 'C8');
});

test('should_consume_budget_per_node_once', () => {
  // 같은 노드에 두 번째 PRD → C3. 다른 노드는 예산 안이면 통과.
  const consumed = { 'N-01': 'docs/prds/ledger-prd.md' };
  const again = C.judge(ctx({ prd: { node: 'N-01', files: ['.claude/hooks/_signals.js'] }, ctx: { consumed, completedNodes: new Set() } }));
  assert.strictEqual(again.ok, false);
  assert.ok(again.failed.some(f => f.id === 'C3' && /N-01/.test(f.reason)), JSON.stringify(again.failed));

  const other = C.judge(ctx({ ctx: { consumed } }));
  assert.strictEqual(other.ok, true, JSON.stringify(other.failed));
  assert.strictEqual(C.remainingBudget(ctx({ ctx: { consumed } })), 2, '3 - 1 = 2');
});

test('should_refuse_when_node_deps_incomplete', () => {
  // N-03 은 N-01, N-02 에 의존. N-02 가 안 끝났으면 거부 — 순서를 건너뛰지 않는다.
  const r = C.judge(ctx({ prd: { node: 'N-03', files: ['.claude/hooks/_signals.js'] }, ctx: { completedNodes: new Set(['N-01']) } }));
  assert.strictEqual(r.ok, false);
  const c8 = r.failed.find(f => f.id === 'C8');
  assert.ok(c8 && /N-02/.test(c8.reason), `미완 의존을 지목해야: ${JSON.stringify(r.failed)}`);
});

test('should_refuse_when_prd_files_exceed_node_files', () => {
  const r = C.judge(ctx({ prd: { files: ['.claude/hooks/_impact.js', 'tests/unit/test_impact.js', 'scripts/build-package.js'] } }));
  assert.strictEqual(r.ok, false);
  const c5 = r.failed.find(f => f.id === 'C5');
  assert.ok(c5 && /scripts\/build-package\.js/.test(c5.reason), `밖으로 나간 파일을 지목해야: ${JSON.stringify(r.failed)}`);
  // 글롭 매칭: tests/unit/** 이 tests/unit/test_impact.js 를 덮는다
  const ok = C.judge(ctx({ prd: { files: ['tests/unit/deep/test_x.js'] } }));
  assert.strictEqual(ok.ok, true, JSON.stringify(ok.failed));
});

test('should_fail_closed_on_malformed_charter', () => {
  // 파싱이 깨진 charter 로는 아무것도 승인되지 않는다
  const broken = C.parse('# Charter — x\n\n- **상태**: Approved\n\n```yaml\nbudget_cycles: 세개\n```\n', 'docs/charters/x-charter.md');
  assert.ok(broken.errors.length > 0, '오류가 기록되어야');
  const r = C.judge(Object.assign(ctx(), { charter: broken }));
  assert.strictEqual(r.ok, false, '깨진 charter 는 통과 불가');
});

// ── [v5/N-01] awaitingApproval — 재승인 대기 판정 ─────────────────────
// 왜 이 블록이 존재하는가:
//   "이 봉투가 사용자의 봉투 승인을 기다리는가" 를 하네스는 **두 곳에서 서로 다르게** 답했다.
//   배너는 `approvedHash !== contentHash` 로 재승인 필요를 정확히 찍고 있었는데,
//   ack 기록은 상태줄이 문자 그대로 `Draft` 인지만 봤다. 그래서 승인된 봉투를 개정한 뒤
//   사용자가 "봉투 승인" 이라고 말하면 **그 발화가 조용히 버려졌다**
//   (다운스트림 sso-suite 가 r4 개정까지 겪고 보고했다).
//
//   여기서 지키는 것은 "상태줄을 어떻게 쓰는가" 가 아니라 **판정의 근거가 실측이라는 것**이다.
//   사람이 상태줄에 무엇을 적든 — `Approved (개정 r4 — 재승인 대기)` 든 뭐든 —
//   지문이 다르면 재승인 대기이고, 같으면 아니다.
{
  const md = (status, extra) => `# Charter — harness-v5-observe

- **상태**: ${status}

\`\`\`yaml
goal: 판정을 실측으로${extra ? ' ' + extra : ''}
budget_cycles: 2
expires: 2026-12-31
invariants:
  - file:exists tests/run.js
out_of_scope:
  - .git/**
\`\`\`

## 노드

- [ ] **[N-01]** 재승인
  - 파일: \`.claude/hooks/session-gate.js\`
`;

  test('should_await_approval_when_draft_and_no_record', () => {
    const r = C.awaitingApproval(md('Draft'), null);
    assert.strictEqual(r.awaiting, true, JSON.stringify(r));
    assert.strictEqual(r.why, 'draft', JSON.stringify(r));
  });

  test('should_await_approval_when_approved_but_no_record', () => {
    // 상태줄만 손으로 Approved 로 바꾼 charter 는 하네스가 승인한 적이 없다.
    // 승인된 것으로 취급하면 봉투가 무의미해진다 — 대기로 본다.
    const r = C.awaitingApproval(md('Approved'), null);
    assert.strictEqual(r.awaiting, true, JSON.stringify(r));
    assert.strictEqual(r.why, 'no-record', JSON.stringify(r));
  });

  test('should_await_approval_when_content_changed_after_approval', () => {
    // **이 버그의 핵심.** 상태줄은 Approved 그대로인데 본문이 바뀌었다 = 재승인 대기.
    const before = md('Approved');
    const rec = { approvedHash: C.contentHash(before), approvedAt: '2026-09-09T00:00:00.000Z' };
    const after = md('Approved', '— 개정 r2');
    const r = C.awaitingApproval(after, rec);
    assert.strictEqual(r.awaiting, true, JSON.stringify(r));
    assert.strictEqual(r.why, 'revised', JSON.stringify(r));
  });

  test('should_not_await_approval_when_approved_and_unchanged', () => {
    const text = md('Approved');
    const rec = { approvedHash: C.contentHash(text), approvedAt: '2026-09-09T00:00:00.000Z' };
    const r = C.awaitingApproval(text, rec);
    assert.strictEqual(r.awaiting, false, JSON.stringify(r));
  });

  test('should_ignore_status_line_annotation_in_awaiting_verdict', () => {
    // 사람이 가장 정확하게 쓰는 표기가 판정을 바꾸면 안 된다.
    //   contentHash 가 상태줄을 제외하므로 괄호 주석은 지문에 영향이 없다.
    const plain = md('Approved');
    const rec = { approvedHash: C.contentHash(plain), approvedAt: '2026-09-09T00:00:00.000Z' };
    const annotated = md('Approved (개정 r4 — 재승인 대기)');
    const r = C.awaitingApproval(annotated, rec);
    assert.strictEqual(r.awaiting, false,
      `상태줄 주석은 내용이 아니다 — 지문이 같으면 대기가 아니다: ${JSON.stringify(r)}`);
    // 그리고 Draft 표기가 아니어도 지문이 다르면 대기다 (역방향)
    const revised = md('Approved (개정 r4 — 재승인 대기)', '— 실제 개정');
    assert.strictEqual(C.awaitingApproval(revised, rec).awaiting, true);
  });

  test('should_stay_pure_awaiting_approval', () => {
    // _charter.js 는 순수 모듈이다. 판정이 디스크를 읽기 시작하면 배너·훅·CLI 가
    // 서로 다른 시점의 파일을 보고 다른 답을 낸다 — 지금 고치는 그 버그가 되돌아온다.
    const srcTxt = require('fs').readFileSync(CHARTER_PATH, 'utf8');
    assert.ok(!/require\(['"]fs['"]\)/.test(srcTxt), '_charter.js 가 fs 를 부르면 안 된다');
    assert.strictEqual(typeof C.awaitingApproval, 'function', 'export 되어야 한다');
  });
}

// ── [v5/N-02] scopeVerdict — 파일이 승인된 그래프의 어디에 있는가 ────
// 왜 이 블록이 존재하는가:
//   범위 판정이 **이분법**이었다 — 활성 노드 안이거나 밖이거나. 그래서 같은 charter 의
//   **다른 노드에 선언된 파일**(= 사용자가 이미 승인한 파일)을 고치면 "승인된 그래프 밖"
//   으로 잡혀 사이클이 닫히지 않았다. 계층을 가로지르는 기능과 공유 계약 리팩터링이
//   구조적으로 막혔고, 다운스트림은 charter 를 r4 까지 개정하며 재승인을 반복했다.
//
//   사용자가 승인한 것은 **그래프**이지 그 분할이 아니다. 노드에 파일을 어떻게 나눌지는
//   모델의 정리 방식이고, 사용자의 안전 경계는 charter 전체와 out_of_scope 다.
//   그래서 3상태다 — in-node · other-node · outside. **막는 것은 outside 뿐이다.**
{
  const CH = C.parse(CHARTER_MD('Approved'), 'docs/charters/harness-v3-charter.md');

  test('should_call_active_node_files_in_node', () => {
    const v = C.scopeVerdict('.claude/hooks/_impact.js', CH, 'N-02');
    assert.strictEqual(v.verdict, 'in-node', JSON.stringify(v));
    assert.strictEqual(v.node, 'N-02');
  });

  test('should_call_other_declared_node_files_other_node', () => {
    // N-01 의 파일이다. 사용자가 승인한 그래프 안이므로 **범위 이탈이 아니라 순서 문제**다.
    const v = C.scopeVerdict('.claude/hooks/_signals.js', CH, 'N-02');
    assert.strictEqual(v.verdict, 'other-node', JSON.stringify(v));
    assert.strictEqual(v.node, 'N-01', '어느 노드의 파일인지 말해야 안내가 가능하다');
  });

  test('should_call_undeclared_files_outside', () => {
    const v = C.scopeVerdict('scripts/build-package.js', CH, 'N-02');
    assert.strictEqual(v.verdict, 'outside', JSON.stringify(v));
  });

  test('should_prefer_active_node_when_file_matches_both', () => {
    // tests/unit/test_signals.js 는 N-01(명시)과 N-02(tests/unit/**) 양쪽에 걸린다.
    // 활성 노드가 이기지 않으면 자기 노드의 파일이 "다른 노드 것" 으로 보고된다.
    const v = C.scopeVerdict('tests/unit/test_signals.js', CH, 'N-02');
    assert.strictEqual(v.verdict, 'in-node', JSON.stringify(v));
    assert.strictEqual(v.node, 'N-02');
  });

  test('should_report_outside_when_no_active_node_matches_nothing', () => {
    // 활성 노드가 없어도(노드 밖 맥락) 그래프 전체로는 판정할 수 있어야 한다.
    const v = C.scopeVerdict('.claude/hooks/_impact.js', CH, null);
    assert.strictEqual(v.verdict, 'other-node', JSON.stringify(v));
    assert.strictEqual(v.node, 'N-02');
  });

  test('should_pass_c5_for_other_node_files', () => {
    // **이 노드의 핵심.** 다른 노드의 파일을 선언한 PRD 가 봉투 판정을 통과해야 한다.
    const r = C.judge(ctx({ prd: { files: ['.claude/hooks/_impact.js', '.claude/hooks/_signals.js'] } }));
    assert.strictEqual(r.ok, true, `C5 가 다른 노드 파일을 막으면 안 된다: ${JSON.stringify(r.failed)}`);
  });

  test('should_still_refuse_c5_for_undeclared_files', () => {
    // 완화는 "그래프 안" 까지다. 어느 노드에도 없는 파일은 그대로 막힌다 — 진짜 사고는 이쪽이다.
    const r = C.judge(ctx({ prd: { files: ['.claude/hooks/_impact.js', 'scripts/build-package.js'] } }));
    assert.strictEqual(r.ok, false);
    const c5 = r.failed.find(f => f.id === 'C5');
    assert.ok(c5 && /scripts\/build-package\.js/.test(c5.reason), `밖으로 나간 파일을 지목해야: ${JSON.stringify(r.failed)}`);
  });

  test('should_let_out_of_scope_win_over_node_membership', () => {
    // out_of_scope 는 사용자가 **명시적으로 막은** 것이다. 노드에 있어도 이긴다.
    //   (이 우선순위가 뒤집히면 C6 이 무의미해진다 — C5 완화가 C6 을 삼키면 안 된다.)
    const md = CHARTER_MD('Approved').replace(
      '  - install.js', '  - install.js\n  - .claude/hooks/_impact.js');
    const charter = C.parse(md, 'docs/charters/harness-v3-charter.md');
    const r = C.judge(ctx({ ctx: { charter } }));
    assert.strictEqual(r.ok, false, 'out_of_scope 파일은 통과하면 안 된다');
    assert.ok(r.failed.some(f => f.id === 'C6'), `C6 이 걸려야: ${JSON.stringify(r.failed)}`);
  });
}

// ── [v5/N-07] .gitignore 는 어느 노드의 산출물도 아니지만 모두가 건드려야 한다 ──
//   다운스트림이 검증 도구 산출물(.playwright-mcp/)을 무시하려고 `.gitignore` 에 두 줄을
//   넣었더니 봉투 범위 게이트가 막았다. 그래서 무시 규칙 추가를 **포기하고** 재발이 뻔한
//   상태로 넘어갔다 — 승인이 두 줄짜리 규칙에 소모되는 것을 피하려고.
//
//   승인은 판단이 갈리는 곳에 남겨야 한다. git 이 무엇을 보는가를 정하는 파일 자체는
//   모든 노드가 건드릴 수 있어야 한다. 그리고 이 하나를 열면 관측 공백 게이트가
//   **저절로** 조용해진다 — `git status -uall` 은 이미 gitignore 를 존중한다.
//
//   **닫힌 목록이다.** git 이 저장소 루트에서 읽는 설정 파일은 이 둘뿐이고 늘지 않는다.
//   도구 경로 목록(.playwright-mcp/, TestResults/, coverage/ …)은 도구가 늘 때마다 자란다 —
//   그것이 이 봉투가 다섯 번 지운 형태이고, 그래서 그 제안은 기각했다.
{
  const CH2 = C.parse(CHARTER_MD('Approved'), 'docs/charters/harness-v3-charter.md');

  test('should_always_allow_git_configuration_files', () => {
    for (const f of ['.gitignore', '.gitattributes']) {
      const v = C.scopeVerdict(f, CH2, 'N-02');
      assert.strictEqual(v.verdict, 'always-allowed', `${f}: ${JSON.stringify(v)}`);
    }
  });

  test('should_not_widen_beyond_the_closed_list', () => {
    // 루트의 다른 파일은 그대로 outside 다. 목록이 자라기 시작하면 이 단언이 깨진다.
    for (const f of ['package.json', 'Makefile', '.env', '.npmrc', 'docs/.gitignore']) {
      assert.strictEqual(C.scopeVerdict(f, CH2, 'N-02').verdict, 'outside', f);
    }
  });

  test('should_let_out_of_scope_win_over_always_allowed', () => {
    // 사용자가 명시적으로 막았으면 그것이 이긴다 — 완화가 C6 을 삼키면 안 된다.
    const md = CHARTER_MD('Approved').replace('  - install.js', '  - install.js\n  - .gitignore');
    const charter = C.parse(md, 'docs/charters/harness-v3-charter.md');
    const r = C.judge(ctx({ ctx: { charter }, prd: { files: ['.claude/hooks/_impact.js', '.gitignore'] } }));
    assert.strictEqual(r.ok, false, 'out_of_scope 파일은 통과하면 안 된다');
    assert.ok(r.failed.some(f => f.id === 'C6'), `C6 이 걸려야: ${JSON.stringify(r.failed)}`);
  });

  test('should_pass_c5_when_prd_declares_gitignore', () => {
    const r = C.judge(ctx({ prd: { files: ['.claude/hooks/_impact.js', '.gitignore'] } }));
    assert.strictEqual(r.ok, true, `C5 가 .gitignore 를 막으면 안 된다: ${JSON.stringify(r.failed)}`);
  });
}

// ── [v6/N-01] charter 의 여러 줄 목록이 온전히 읽힌다 ───────────────
//   다운스트림이 파일 7개를 세 줄에 나눠 적었더니 **3개만 읽혔다.** 파싱 오류는 없었다.
//   사용자는 세 줄 전체를 보고 "봉투 승인" 을 했는데 하네스는 첫 줄만 승인된 것으로 다뤘다 —
//   **승인한 것과 판정하는 것이 어긋나고, 그 어긋남이 조용했다.**
//
//   `- 의존:` 도 잘렸다(리포트는 추정만 했고 실측으로 확인했다). 의존은 C8(선행 노드 완료)의
//   근거이므로, 간선이 사라지면 **미완료 노드를 완료로 보고 통과한다.**
{
  const ML = `# Charter — t-ml

- **상태**: Approved

\`\`\`yaml
goal: 여러 줄 목록
budget_cycles: 3
expires: 2099-12-31
invariants:
  - file:exists CLAUDE.md
out_of_scope:
  - .git/**
\`\`\`

## 노드

- [ ] **[N-18]** 여러 줄
  - 파일: \`src/Server/Endpoints/**\`, \`src/Agent/Ui/**\`, \`src/Agent/Assets/**\`,
    \`src/Agent/Agent.csproj\`, \`tests/Agent.Tests/**\`, \`tests/Server.Tests/**\`,
    \`docs/**/design-apply*\`
  - 의존: N-01,
    N-02

- [ ] **[N-01]** 가
  - 파일: \`src/a.js\`
- [ ] **[N-02]** 나
  - 파일: \`src/b.js\`
`;

  test('should_read_every_line_of_a_multiline_file_list', () => {
    const ch = C.parse(ML, 'docs/charters/t-ml-charter.md');
    assert.strictEqual(ch.errors.length, 0, ch.errors.join(' / '));
    const n = ch.nodes['N-18'];
    assert.ok(n, '노드 N-18 이 사라졌다 — 이어 붙이기가 과했다');
    const want = ['src/Server/Endpoints/**', 'src/Agent/Ui/**', 'src/Agent/Assets/**',
      'src/Agent/Agent.csproj', 'tests/Agent.Tests/**', 'tests/Server.Tests/**',
      'docs/**/design-apply*'];
    const missing = want.filter(w => !n.files.includes(w));
    assert.strictEqual(missing.length, 0, `사라진 경로: ${missing.join(', ')} (읽힌 것: ${n.files.join(', ')})`);
  });

  test('should_read_every_line_of_a_multiline_dependency_list', () => {
    // 간선이 사라지면 C8 이 미완료 노드를 완료로 본다.
    const ch = C.parse(ML, 'docs/charters/t-ml-charter.md');
    assert.deepStrictEqual(ch.nodes['N-18'].deps.slice().sort(), ['N-01', 'N-02']);
  });

  test('should_not_swallow_the_following_node', () => {
    // **가장 위험한 실패 방향.** 이어 붙이기가 과하면 뒤 노드가 통째로 사라진다.
    const ch = C.parse(ML, 'docs/charters/t-ml-charter.md');
    assert.ok(ch.nodes['N-01'], 'N-01 이 삼켜졌다');
    assert.ok(ch.nodes['N-02'], 'N-02 가 삼켜졌다');
    assert.deepStrictEqual(ch.nodes['N-01'].files, ['src/a.js']);
    assert.deepStrictEqual(ch.nodes['N-02'].files, ['src/b.js']);
  });

  test('should_keep_single_line_charters_identical', () => {
    // 기존 charter 3개가 전부 한 줄 형태다. **하나도 달라지면 안 된다.**
    const ch = C.parse(CHARTER_MD('Approved'), 'docs/charters/harness-v3-charter.md');
    assert.deepStrictEqual(ch.nodes['N-01'].files, ['.claude/hooks/_signals.js', 'tests/unit/test_signals.js']);
    assert.deepStrictEqual(ch.nodes['N-02'].deps, ['N-01']);
    assert.deepStrictEqual(ch.nodes['N-03'].deps.slice().sort(), ['N-01', 'N-02']);
  });
}

// ── [v6/N-02] 잘림을 그 자리에서 말한다 ────────────────────────────
//   N-01 이 연속 줄을 읽게 만들었지만, **연속으로 인정되지 않는 형태**가 남는다 —
//   빈 줄이 끼거나 들여쓰기가 없으면 마크다운 규칙상 연속이 아니다. 규칙을 넓히면
//   다음 블록을 삼킬 위험이 커지므로 넓히지 않는다. 대신 **말한다.**
//
//   판정은 목록이 아니라 관측이다: **이어 붙인 결과가 쉼표로 끝나면** 사람은 다음 줄을
//   이어 쓸 생각이었다. 정상 형태는 백틱이나 글롭으로 끝난다.
//
//   그리고 시점이 값어치다 — 지금은 PRD 승인(C5)에서야 드러나고, 그 사이 사용자는
//   charter 를 승인했고 모델은 그 범위로 계획을 세웠다.
{
  const mk = filesLine => `# Charter — t-warn

- **상태**: Approved

\`\`\`yaml
goal: 경고
budget_cycles: 2
expires: 2099-12-31
invariants:
  - file:exists CLAUDE.md
out_of_scope:
  - .git/**
\`\`\`

## 노드

- [ ] **[N-01]** 가
${filesLine}
`;

  // 빈 줄이 끼어 연속으로 인정되지 않는다 → 쉼표로 끝난 채 남는다
  const TRUNC = mk('  - 파일: `src/a.js`, `src/b.js`,\n\n    `src/c.js`');
  const CLEAN = mk('  - 파일: `src/a.js`, `src/b.js`');
  const JOINED = mk('  - 파일: `src/a.js`,\n    `src/b.js`');

  test('should_warn_when_a_file_list_still_ends_with_a_comma', () => {
    const ch = C.parse(TRUNC, 'docs/charters/t-warn-charter.md');
    assert.ok(Array.isArray(ch.warnings), 'warnings 배열이 있어야 한다');
    assert.ok(ch.warnings.some(w => /N-01/.test(w)), `경고가 없다: ${JSON.stringify(ch.warnings)}`);
  });

  test('should_not_warn_on_a_well_formed_list', () => {
    // 오탐이 잦으면 경고는 읽히지 않는다.
    for (const [label, md] of [['한 줄', CLEAN], ['이어 붙은 여러 줄', JOINED]]) {
      const ch = C.parse(md, 'docs/charters/t-warn-charter.md');
      assert.strictEqual((ch.warnings || []).length, 0, `${label}: ${JSON.stringify(ch.warnings)}`);
    }
  });

  test('should_keep_warnings_out_of_errors', () => {
    // 잘림은 문법상 유효하다. 거부할 근거가 없으므로 경고여야 한다.
    const ch = C.parse(TRUNC, 'docs/charters/t-warn-charter.md');
    assert.strictEqual(ch.errors.length, 0, `errors 에 섞였다: ${ch.errors.join(' / ')}`);
    assert.ok(ch.nodes['N-01'], '노드는 그대로 성립해야 한다');
  });

  test('should_record_the_raw_line_it_read', () => {
    // "안 적었나 못 읽었나" 를 구분할 수 있어야 한다.
    const ch = C.parse(JOINED, 'docs/charters/t-warn-charter.md');
    const raw = ch.nodes['N-01'].filesRaw;
    assert.ok(raw && /src\/a\.js/.test(raw) && /src\/b\.js/.test(raw), `filesRaw: ${raw}`);
  });

  test('should_show_the_raw_line_in_c5_failure', () => {
    const charter = C.parse(CHARTER_MD('Approved'), 'docs/charters/harness-v3-charter.md');
    const r = C.judge(ctx({ ctx: { charter }, prd: { files: ['scripts/build-package.js'] } }));
    const c5 = r.failed.find(f => f.id === 'C5');
    assert.ok(c5, 'C5 가 걸려야 한다');
    assert.ok(/읽은 원문/.test(c5.reason), `원문이 없다: ${c5.reason}`);
  });
}

// ── [v7/N-01] vendor — 하네스 설치물은 노드의 이탈이 아니다 ─────────
//   v5/N-05 가 `.claude/**` 를 추적 대상으로 바꾸자, complete 의 이탈 검사가
//   **하네스 자신의 파일을 노드의 책임으로** 세기 시작했다. 하네스를 업그레이드한
//   프로젝트는 다음 사이클을 면제 없이 닫을 수 없다(다운스트림 보고, 재현 2/2).
//
//   리포트는 `.claude/` 를 통째로 건너뛰자고 했다. **이 저장소에서는 틀리다** —
//   skill-set 의 `.claude/hooks/*.js` 는 노드의 산출물 그 자체이고, 통째로 면제하면
//   자기 훅에 대한 범위 게이트를 통째로 잃는다.
//
//   답은 **판정 순서**다. 노드 소속이 먼저고, 어느 노드도 선언하지 않은 하네스 경로만
//   `vendor` 다. 아래 ②③ 이 그 안전선이며 **이 블록에서 가장 중요한 단언**이다.
{
  const VEND = `# Charter — t-vendor

- **상태**: Approved

\`\`\`yaml
goal: 벤더 판정
budget_cycles: 2
expires: 2099-12-31
invariants:
  - file:exists CLAUDE.md
out_of_scope:
  - docs/memory/decisions.jsonl
\`\`\`

## 노드

- [ ] **[N-01]** 가
  - 파일: \`src/a.js\`, \`.claude/hooks/_charter.js\`
- [ ] **[N-02]** 나
  - 파일: \`.claude/skills/plan/SKILL.md\`
`;
  const CV = C.parse(VEND, 'docs/charters/t-vendor-charter.md');

  test('should_call_undeclared_harness_paths_vendor', () => {
    for (const f of ['.claude/hooks/_signals.js', '.claude/skills/process/SKILL.md',
      '.claude/agents/architect.md', '.claude/rules/common/security.md',
      '.claude/domain-skills/x.md', '.claude/settings.json']) {
      const v = C.scopeVerdict(f, CV, 'N-01');
      assert.strictEqual(v.verdict, 'vendor', `${f}: ${JSON.stringify(v)}`);
    }
  });

  test('should_let_node_membership_beat_vendor', () => {
    // **이 저장소의 안전선.** skill-set 의 훅은 전부 노드에 선언돼 있다 —
    //   vendor 가 노드 소속을 이기면 자기 훅에 대한 게이트를 통째로 잃는다.
    const v = C.scopeVerdict('.claude/hooks/_charter.js', CV, 'N-01');
    assert.strictEqual(v.verdict, 'in-node', JSON.stringify(v));
    assert.strictEqual(v.node, 'N-01');
  });

  test('should_let_other_node_beat_vendor', () => {
    const v = C.scopeVerdict('.claude/skills/plan/SKILL.md', CV, 'N-01');
    assert.strictEqual(v.verdict, 'other-node', JSON.stringify(v));
    assert.strictEqual(v.node, 'N-02');
  });

  test('should_keep_undeclared_source_outside', () => {
    // 완화는 하네스 경로까지다. 선언되지 않은 소스는 그대로 이탈이다 — 진짜 사고는 이쪽이다.
    for (const f of ['src/z.js', 'scripts/build-package.js', 'install.js']) {
      assert.strictEqual(C.scopeVerdict(f, CV, 'N-01').verdict, 'outside', f);
    }
  });

  test('should_not_change_always_allowed', () => {
    assert.strictEqual(C.scopeVerdict('.gitignore', CV, 'N-01').verdict, 'always-allowed');
  });

  test('should_not_turn_out_of_scope_into_an_exemption', () => {
    // out_of_scope 는 **보호**다. 면제로 바뀌면 이 저장소에서 decisions.jsonl 을 고친
    //   사이클이 조용히 닫힌다. C6 가 그대로 막아야 한다.
    const md = VEND.replace('  - docs/memory/decisions.jsonl',
      '  - docs/memory/decisions.jsonl\n  - .claude/hooks/_charter.js');
    const charter = C.parse(md, 'docs/charters/t-vendor-charter.md');
    const r = C.judge(ctx({ ctx: { charter },
      prd: { charter: 'docs/charters/t-vendor-charter.md', node: 'N-01', files: ['.claude/hooks/_charter.js'] } }));
    assert.strictEqual(r.ok, false, 'out_of_scope 파일은 통과하면 안 된다');
    assert.ok(r.failed.some(f => f.id === 'C6'), `C6 이 걸려야: ${JSON.stringify(r.failed)}`);
  });
}


// ══ Loop N — 병렬 가능 묶음과 YAML 주석 (v16/N-26) ═══════════════════
// 왜: 하네스는 병렬 가능성을 계산하지도 제안하지도 않았다. 2026-09-17 실측으로 값은
//   확인됐다 — 조사 에이전트 5개 동시 투입이 결함 4건의 근본 원인을 5분에 확정했고,
//   Loop A 3렌즈 병렬이 실재 결함 8건을 잡았다(단일 렌즈였으면 대부분 놓쳤다).
//   그런데 그 전부를 **모델이 손으로** 붙였다. 설계는 v9 봉투의 N-05 에 있었으나
//   v9 가 0/7 소비로 Superseded 되며 사라졌다.
{
  const CH = {
    id: 'test-charter',
    nodes: {
      'N-01': { id: 'N-01', title: 'a', files: ['a.js', 'shared.js'], deps: [] },
      'N-02': { id: 'N-02', title: 'b', files: ['b.js'], deps: [] },
      'N-03': { id: 'N-03', title: 'c', files: ['shared.js'], deps: [] },     // N-01 과 겹친다
      'N-04': { id: 'N-04', title: 'd', files: ['d.js'], deps: ['N-02'] },    // 의존 미완
      'N-05': { id: 'N-05', title: 'e', files: ['e.js'], deps: [] },
    },
  };
  const flat = gs => (gs || []).map(g => g.slice().sort().join('+')).sort().join(' | ');
  const groupOf = (gs, id) => (gs || []).findIndex(g => g.includes(id));

  test('should_export_parallel_groups', () => {
    assert.strictEqual(typeof C.parallelGroups, 'function',
      '_charter exports: ' + Object.keys(C).join(','));
  });

  test('should_group_together_when_files_do_not_overlap', () => {
    const gs = C.parallelGroups(CH, { done: [] });
    assert.ok(Array.isArray(gs) && gs.length >= 1, JSON.stringify(gs));
    assert.strictEqual(groupOf(gs, 'N-02'), groupOf(gs, 'N-05'),
      'b.js 와 e.js 는 겹치지 않는데 다른 묶음이다: ' + flat(gs));
  });

  test('should_split_groups_when_files_overlap', () => {
    const gs = C.parallelGroups(CH, { done: [] });
    assert.notStrictEqual(groupOf(gs, 'N-01'), groupOf(gs, 'N-03'),
      'shared.js 를 함께 쓰는데 같은 묶음이다 — 동시에 돌리면 서로를 덮는다: ' + flat(gs));
  });

  test('should_exclude_node_when_deps_unmet', () => {
    const gs = C.parallelGroups(CH, { done: [] });
    assert.strictEqual(groupOf(gs, 'N-04'), -1, 'N-02 가 안 끝났는데 N-04 가 시작 가능으로 나왔다: ' + flat(gs));
  });

  test('should_exclude_node_when_already_done', () => {
    const gs = C.parallelGroups(CH, { done: ['N-02'] });
    assert.strictEqual(groupOf(gs, 'N-02'), -1, '완료 노드가 다시 나왔다: ' + flat(gs));
    assert.ok(groupOf(gs, 'N-04') >= 0, '의존이 풀렸는데 N-04 가 안 나왔다: ' + flat(gs));
  });

  test('should_return_fixed_grouping_when_input_is_same', () => {
    // [Loop A 교정] 처음에는 `flat(A) === flat(A)` 로 썼다 — 같은 프로세스에서 같은 함수를
    //   두 번 부른 값이라 **내부에 순회 순서 의존이 있어도 항상 통과**한다. 결정성을 고정하려면
    //   기대값을 박아야 한다. 아래 값은 실제 실행으로 확인한 것이다.
    assert.strictEqual(flat(C.parallelGroups(CH, { done: [] })), 'N-01+N-02+N-05 | N-03');
    assert.strictEqual(flat(C.parallelGroups(CH, { done: ['N-02'] })), 'N-01+N-04+N-05 | N-03');
  });

  test('should_split_groups_when_glob_covers_concrete_file', () => {
    // [Loop A 교정] 겹침은 **문자열 동등이 아니다.** 봉투는 디렉터리 글롭과 그 안의 구체
    //   파일을 함께 선언한다(실제 v16 봉투의 `.claude/hooks/ui/**` 와 `.../preset-designer.js`).
    //   Set 동등 비교만 하면 **서로를 덮는 두 노드가 "동시 가능" 으로 표시된다** —
    //   Loop N 의 존재 이유를 정면으로 배신한다.
    const CH2 = {
      id: 'g', nodes: {
        'N-01': { id: 'N-01', files: ['.claude/hooks/ui/**'], deps: [] },
        'N-02': { id: 'N-02', files: ['.claude/hooks/ui/preset-designer.js'], deps: [] },
      },
    };
    const gs = C.parallelGroups(CH2, { done: [] });
    assert.notStrictEqual(groupOf(gs, 'N-01'), groupOf(gs, 'N-02'),
      '글롭이 덮는 구체 파일인데 같은 묶음이다: ' + flat(gs));
  });

  test('should_group_together_when_glob_does_not_cover', () => {
    // 대조군 — 글롭 판정이 과해 무관한 노드까지 갈라내면 그것도 틀렸다.
    const CH3 = {
      id: 'g2', nodes: {
        'N-01': { id: 'N-01', files: ['.claude/hooks/ui/**'], deps: [] },
        'N-02': { id: 'N-02', files: ['.claude/hooks/_signals.js'], deps: [] },
      },
    };
    const gs = C.parallelGroups(CH3, { done: [] });
    assert.strictEqual(groupOf(gs, 'N-01'), groupOf(gs, 'N-02'),
      '겹치지 않는데 갈라졌다 — 글롭 판정이 과하다: ' + flat(gs));
  });

  test('should_include_node_when_deps_become_met', () => {
    // [Loop A 교정] "의존 미해결이면 없다" 는 **부재 증명**이라 빈 스텁으로도 통과한다.
    //   양성 짝을 둔다 — 의존이 풀리면 실제로 나타나야 한다.
    const before = C.parallelGroups(CH, { done: [] });
    const after = C.parallelGroups(CH, { done: ['N-02'] });
    assert.strictEqual(groupOf(before, 'N-04'), -1, flat(before));
    assert.ok(groupOf(after, 'N-04') >= 0, '의존이 풀렸는데 N-04 가 안 나왔다: ' + flat(after));
  });

  test('should_return_empty_when_no_nodes', () => {
    assert.deepStrictEqual(C.parallelGroups({ id: 'x', nodes: {} }, { done: [] }), []);
    assert.deepStrictEqual(C.parallelGroups(null, { done: [] }), []);
  });

  // ── YAML 주석 — 오늘 사용자의 봉투 승인을 실제로 막았다 ────────────
  //   `_charter.js` 의 주석 제거는 `\s+#` 라 **줄 맨 앞의 `#`** 를 놓친다.
  //   그 줄은 `key: value` 도 아니라 '해석 불가' 오류가 되고, `charter approve` 가
  //   "charter 가 성립하지 않는다" 로 거부한다. 봉투에서 주석을 걷어내 급히 풀었지만
  //   원인은 파서에 있었다.
  const CHARTER_TEXT = [
    '# Charter — c-yaml',
    '',
    '- **상태**: Draft',
    '',
    '## 노드',
    '',
    '- [ ] **[N-01]** 하나',
    '  - 파일: `a.js`',
    '  - 의존: 없음',
    '',
    '```yaml',
    'charter: c-yaml',
    'budget_cycles: 3',
    'expires: 2026-12-31',
    'invariants:',
    '# 줄 전체가 주석이다 — 들여쓰기가 없다',
    '  # 들여쓴 줄 전체 주석',
    '  - grep:a.js /needle/ >=1',
    '  - grep:b.js /x#y/ >=1',
    'out_of_scope: 없음   # 인라인 주석',
    '```',
  ].join('\n');

  test('should_accept_charter_when_line_is_whole_comment', () => {
    const ch = C.parse(CHARTER_TEXT, 'docs/charters/c-yaml-charter.md');
    assert.deepStrictEqual(ch.errors || [], [], JSON.stringify(ch.errors));
  });

  test('should_strip_inline_comment_when_value_has_trailing_hash', () => {
    const ch = C.parse(CHARTER_TEXT, 'docs/charters/c-yaml-charter.md');
    assert.strictEqual(String(ch.outOfScope), '없음', JSON.stringify(ch.outOfScope));
  });

  test('should_keep_hash_when_inside_spec_value', () => {
    const ch = C.parse(CHARTER_TEXT, 'docs/charters/c-yaml-charter.md');
    const inv = ch.invariants || [];
    assert.ok(inv.some(s => s.includes('/x#y/')), '값 안의 # 가 잘렸다: ' + JSON.stringify(inv));
    assert.strictEqual(inv.length, 2, JSON.stringify(inv));
  });
}


console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
