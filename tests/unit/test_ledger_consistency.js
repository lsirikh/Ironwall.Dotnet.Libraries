// tests/unit/test_ledger_consistency.js
// 원장을 읽는 곳들이 **같은 판정**을 쓰는가 — v3.7 / C2·C3·C4
//
// 왜 이 파일이 존재하는가:
//   하네스가 이 charter 내내 반복한 실패는 "잊을 수 있는 목록" 이고, 그 쌍둥이가
//   **판정 규칙이 두 벌** 이다. 원장에는 그런 규칙이 둘 있다.
//
//   ① "이 사이클에 사람의 결정이 있었는가" — complete 게이트는 자동 행(charter·loopV·
//      migration)을 빼고 세는데 status 는 날 행수 차이로 셌다. 그래서 status 가
//      "(기록됨)" 이라 말하는 바로 그 순간 complete 가 거부했다.
//   ② "이 행은 정착됐는가" — evaluate·matchTopics 는 `settles:<id>` 가 가리키는 대상도
//      정착으로 치는데, 결정 표 렌더러와 pre-tool-gate 의 enforce 필터는 몰랐다.
//
//   이 테스트는 문자열이 아니라 **두 경로의 일치**를 단언한다 — 어느 쪽을 고쳐도
//   다른 쪽이 따라오지 않으면 실패한다.

'use strict';

const fs = require('fs');
const path = require('path');
const SB = require('./_sandbox');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 300) : ''}`); }
}

function row(o) {
  return JSON.stringify(Object.assign({
    id: 'D-TEST-' + Math.abs(String(o.decision || '').split('').reduce((a, c) => a * 31 + c.charCodeAt(0) | 0, 7)).toString(16),
    date: '2026-09-08',
    decision: 'x', reason: 'y',
    rejected: ['z'],
    invalidation: { text: 'w', specs: [] },
  }, o));
}

function writeLedger(dir, rows) {
  fs.writeFileSync(path.join(dir, 'docs', 'memory', 'decisions.jsonl'), rows.join('\n') + '\n');
}

console.log('\n═══ LC: 원장 판정 일치 ═══');

// ── LC-01: status 와 complete 가 같은 셈법을 쓴다 (C3) ────────────────
console.log('\n[LC-01] should_agree_between_status_and_complete_on_human_decisions');
{
  const dir = SB.makeSandbox({ prefix: 'lc01' });
  try {
    SB.cli(dir, ['status']);
    SB.patchState(dir, s => {
      s.track = 'B'; s.phase = 'test';
      s.cycle = Object.assign({}, s.cycle, {
        startedAt: '2020-01-01T00:00:00.000Z',
        decisionsAtStart: 0,
        needsDecision: ['task-force:IMPL-01'],
      });
      return s;
    });
    // Track B 의 앞선 게이트(저널 엔트리)를 만족시켜야 원장 게이트까지 내려간다
    fs.writeFileSync(path.join(dir, 'docs', 'memory', 'dev-journal.md'),
      ['# 개발 저널', '', '### [2026-09-08 12:00] C3 일치', '- **대상**: src/a.js', '- 완료', ''].join('\n'));

    // ① 자동 행만 있는 상태 — 사람의 판단은 0
    writeLedger(dir, [row({ decision: 'charter auto', source: 'charter' })]);
    const st1 = SB.cli(dir, ['status']);
    const line1 = (st1.out.match(/결정 필요:[^\n]*/) || [''])[0];
    check('자동 행만 있으면 status 가 "기록됨" 이라 하지 않는다',
      /complete 가 거부됩니다/.test(line1) && !/기록됨/.test(line1), line1 || st1.out.slice(0, 200));
    check('원장 줄이 사람·모델·자동을 나눠 보여준다', /사람 0 · 모델 0 · 자동 1/.test(st1.out),
      (st1.out.match(/원장:[^\n]*/) || [''])[0]);

    const cp1 = SB.cli(dir, ['complete']);
    check('같은 상태에서 complete 가 거부한다', /결정 기록이 필요합니다/.test(cp1.out),
      cp1.out.slice(0, 300));

    // ② 사람 행이 하나 붙으면 — 양쪽 다 통과 판정
    writeLedger(dir, [
      row({ decision: 'charter auto', source: 'charter' }),
      row({ decision: 'human call', source: 'user' }),
    ]);
    const st2 = SB.cli(dir, ['status']);
    const line2 = (st2.out.match(/결정 필요:[^\n]*/) || [''])[0];
    check('사람 행이 생기면 status 가 "기록됨" 이라 한다',
      /기록됨/.test(line2) && !/complete 가 거부됩니다/.test(line2), line2 || st2.out.slice(0, 200));

    const cp2 = SB.cli(dir, ['complete']);
    check('같은 상태에서 complete 는 원장을 이유로 거부하지 않는다',
      !/결정 기록이 필요합니다/.test(cp2.out), cp2.out.slice(0, 300));

    // ③ source 미기재는 과거의 사람 행이다 — 소급 무효화하지 않는다
    writeLedger(dir, [row({ decision: 'legacy human' })]);
    const st3 = SB.cli(dir, ['status']);
    check('source 미기재 행은 사람으로 센다',
      /기록됨/.test((st3.out.match(/결정 필요:[^\n]*/) || [''])[0]),
      (st3.out.match(/결정 필요:[^\n]*/) || [''])[0]);
  } finally { SB.cleanup(dir); }
}

// ── LC-02: 정착 판정이 원장을 읽는 모든 곳에서 같다 (C2 · C4) ─────────
console.log('\n[LC-02] should_treat_settles_rows_as_retired_everywhere');
{
  const S = require(path.join(SB.REPO, '.claude', 'hooks', '_signals.js'));
  const raw = [
    { id: 'D-A', date: '2026-09-01', decision: 'A', reason: 'r', rejected: ['x'],
      invalidation: { text: 't', specs: ['file:exists nope-A.txt'] }, topics: ['testing'],
      enforce: 'grep:src/a.js /FORBID/ ==0' },
    { id: 'D-B', date: '2026-09-02', decision: 'B', reason: 'r', rejected: ['x'],
      invalidation: { text: 't', specs: [] }, topics: ['testing'], settles: 'D-A' },
    { id: 'D-C', date: '2026-09-03', decision: 'C', reason: 'r', rejected: ['x'],
      invalidation: { text: 't', specs: [] }, topics: ['testing'] },
  ];
  const part = S.partitionLedger(raw);
  check('partitionLedger 가 settles 대상을 정착으로 친다', part.settled.has('D-A'),
    [...part.settled].join(','));
  check('정착 대상은 live 에서 빠진다', !part.live.some(r => r.id === 'D-A'),
    part.live.map(r => r.id).join(','));
  check('정착되지 않은 행은 live 에 남는다', part.live.some(r => r.id === 'D-C'),
    part.live.map(r => r.id).join(','));
  check('isRetired 가 같은 답을 준다',
    S.isRetired(part.all.find(r => r.id === 'D-A'), part) === true &&
    S.isRetired(part.all.find(r => r.id === 'D-C'), part) === false);

  const ev = S.evaluate({
    rows: raw, auditLines: [], now: Date.parse('2026-09-08T00:00:00Z'),
    readFile: () => null, exists: () => false, listFiles: null, git: () => null,
  });
  check('evaluate 가 정착된 행의 스펙을 건너뛴다',
    ev.skipped.some(s => s.rowId === 'D-A' && /settled/.test(s.why)) &&
    !ev.results.some(r => /nope-A/.test(r.spec)),
    JSON.stringify(ev.results.map(r => r.spec)));

  const hits = S.matchTopics(raw, 'testing').map(r => r.id);
  check('matchTopics 가 정착된 행을 내놓지 않는다', !hits.includes('D-A') && hits.includes('D-C'),
    hits.join(','));
}

// ── LC-03: 결정 표가 정착을 접는다 (C2) ───────────────────────────────
console.log('\n[LC-03] should_fold_settled_rows_in_the_rendered_table');
{
  const dir = SB.makeSandbox({ prefix: 'lc03' });
  try {
    SB.cli(dir, ['status']);
    writeLedger(dir, [
      row({ id: 'D-OPEN', decision: '열린 논쟁 OPENMARK', source: 'user' }),
      row({ id: 'D-SET', decision: '끝난 논쟁 SETTLEDMARK', source: 'user' }),
      row({ id: 'D-DECL', decision: 'D-SET 를 정착시킨다', source: 'user', settles: 'D-SET' }),
    ]);
    const heal = require(path.join(dir, '.claude', 'hooks', 'advance-phase-heal.js'));
    const r = heal.renderDecisionTable();
    check('렌더링이 성공한다', r && r.ok !== false, JSON.stringify(r));
    const ctx = fs.readFileSync(path.join(dir, 'docs', 'memory', 'session-context.md'), 'utf8');
    const tableRows = ctx.split('\n').filter(l => /^\| 2026-/.test(l)).join('\n');
    check('열린 결정은 표 행으로 남는다', /OPENMARK/.test(tableRows), tableRows);
    check('정착된 결정은 표 행에서 빠진다', !/SETTLEDMARK/.test(tableRows), tableRows);
    check('정착된 결정은 접힌 목록에 나타난다', /정착된 아키텍처[\s\S]*SETTLEDMARK/.test(ctx),
      (ctx.match(/정착된 아키텍처[^\n]*/) || [''])[0]);
  } finally { SB.cleanup(dir); }
}

// ── LC-04: 정착시키면 enforce 가 은퇴한다 (C4) ────────────────────────
console.log('\n[LC-04] should_retire_enforce_when_the_decision_is_settled');
{
  const dir = SB.makeSandbox({ prefix: 'lc04' });
  try {
    SB.cli(dir, ['status']);
    const enforceRow = { id: 'D-ENF', decision: 'FORBID 금지', source: 'user',
      enforce: 'grep:src/a.js /FORBID/ ==0' };
    const payload = {
      tool_name: 'Write', session_id: 'lc04',
      tool_input: { file_path: path.join(dir, 'src', 'a.js'), content: '// FORBID\n' },
    };

    writeLedger(dir, [row(enforceRow)]);
    const blocked = SB.hook(dir, 'pre-tool-gate.js', payload);
    check('살아 있는 enforce 가 편집을 막는다', /"decision"s*:s*"block"/.test(blocked.out) && /강제 조건/.test(blocked.out),
      'code=' + blocked.code + ' ' + blocked.out.slice(0, 200));

    writeLedger(dir, [
      row(enforceRow),
      row({ id: 'D-ENF-S', decision: 'D-ENF 를 정착시킨다', source: 'user', settles: 'D-ENF' }),
    ]);
    const allowed = SB.hook(dir, 'pre-tool-gate.js', payload);
    check('정착시키면 같은 편집이 통과한다', !/"decision"s*:s*"block"/.test(allowed.out),
      'code=' + allowed.code + ' ' + allowed.out.slice(0, 300));
  } finally { SB.cleanup(dir); }
}

// ── LC-05: 허용 경로는 차단으로 기록되지 않는다 (C1) ───────────────────
console.log('\n[LC-05] should_not_record_allowed_paths_as_gate_blocks');
{
  const gate = fs.readFileSync(path.join(SB.REPO, '.claude', 'hooks', 'pre-tool-gate.js'), 'utf8');
  check('봉투 자동 승인은 auditAllow 로 기록한다',
    /auditAllow\(\s*'prd-approve-via-charter'/.test(gate),
    (gate.match(/[^\n]*prd-approve-via-charter[^\n]*/g) || []).slice(0, 2).join(' | '));
  check('차단 래퍼와 허용 래퍼가 서로 다른 이벤트를 쓴다',
    /auditEvent\('gate-block'/.test(gate) && /auditEvent\('gate-allow'/.test(gate),
    (gate.match(/auditEvent\('[\w-]+'/g) || []).join(' '));

  // 생성기가 둘을 갈라 본다 — 통과 이름이 차단 규칙 목록에 실리면 안 된다
  const gen = require(path.join(SB.REPO, 'scripts', 'gen-manual-tables.js'));
  const rules = gen.collectGateRules();
  const events = gen.collectAuditEvents().fixed;
  check('규칙 목록에 통과 이름이 없다', !rules.includes('prd-approve-via-charter'), rules.join(','));
  check('이벤트 목록에 gate-allow 가 있다', events.includes('gate-allow'), '');
  check('이벤트 목록에 gate-block 이 있다', events.includes('gate-block'), '');
  check('이벤트 목록이 규칙 이름을 담지 않는다',
    !rules.some(r => events.includes(r)), rules.filter(r => events.includes(r)).join(','));

  // 생성기는 require 만으로 저장소를 고치지 않는다
  const before = fs.readFileSync(path.join(SB.REPO, 'docs', 'Manual.md'), 'utf8');
  delete require.cache[require.resolve(path.join(SB.REPO, 'scripts', 'gen-manual-tables.js'))];
  require(path.join(SB.REPO, 'scripts', 'gen-manual-tables.js'));
  const after = fs.readFileSync(path.join(SB.REPO, 'docs', 'Manual.md'), 'utf8');
  check('require 가 Manual 을 덮어쓰지 않는다', before === after, '길이 ' + before.length + ' → ' + after.length);
}

// ── LC-06: 관측된 문서는 디렉터리와 무관하게 사이클 커밋에 담긴다 (C6) ──
console.log('\n[LC-06] should_commit_every_observed_document');
{
  const dir = SB.makeSandbox({ prefix: 'lc06' });
  try {
    SB.cli(dir, ['status']);
    SB.patchState(dir, s => {
      s.track = 'B'; s.phase = 'test';
      s.cycle = Object.assign({}, s.cycle, {
        startedAt: '2020-01-01T00:00:00.000Z',
        decisionsAtStart: 0, needsDecision: [],
        // 관측 결과: 산출물 디렉터리 안 하나, 밖 하나
        touchedDocs: ['docs/reports/r.md', 'docs/Manual.md'],
      });
      return s;
    });
    fs.writeFileSync(path.join(dir, 'docs', 'memory', 'dev-journal.md'),
      ['# 개발 저널', '', '### [2026-09-08 12:00] C6', '- **대상**: docs/Manual.md', '- 완료', ''].join('\n'));
    fs.mkdirSync(path.join(dir, 'docs', 'reports'), { recursive: true });
    fs.writeFileSync(path.join(dir, 'docs', 'reports', 'r.md'), '# r\n');
    fs.writeFileSync(path.join(dir, 'docs', 'Manual.md'), '# manual\n');

    const out = SB.cli(dir, ['complete']);
    check('complete 가 통과한다', /phase: test → complete/.test(out.out), out.out.slice(-400));
    const committed = SB.git(dir, ['show', '--name-only', '--format=', 'HEAD']).split('\n').map(s => s.trim()).filter(Boolean);
    check('산출물 디렉터리 안의 문서가 담긴다', committed.includes('docs/reports/r.md'), committed.join(' '));
    check('산출물 디렉터리 **밖**의 문서도 담긴다', committed.includes('docs/Manual.md'), committed.join(' '));
    check('leftover 경고에 그 문서가 없다', !/touched 밖[^\n]*Manual/.test(out.out),
      (out.out.match(/[^\n]*touched 밖[^\n]*/) || [''])[0]);
  } finally { SB.cleanup(dir); }
}

// ── LC-07: 감사 기록이 테스트 실행 태그를 달고 나온다 (C7) ─────────────
// 러너의 "자기 흔적만 되돌린다" 는 이 태그가 유일한 근거다. 태그가 없으면
// 테스트가 만든 행이 '남의 기록' 으로 보존돼 저장소에 영구히 쌓인다.
console.log('\n[LC-07] should_tag_audit_rows_with_the_test_run_id');
{
  const dir = SB.makeSandbox({ prefix: 'lc07' });
  try {
    SB.cli(dir, ['status']);
    const RUN = 'lc07-run';
    const before = SB.auditRows(dir).length;   // 태그 없이 돈 시딩 행은 대상이 아니다
    SB.hook(dir, 'post-write-sync.js', {
      hook_event_name: 'PostToolUse',
      session_id: 'lc07', tool_use_id: 'tu-lc07',
      tool_name: 'Write',
      tool_input: { file_path: path.join(dir, 'src', 'a.js'), content: '// x\n' },
    }, { _HARNESS_TEST_RUN: RUN });

    const rows = SB.auditRows(dir).slice(before);
    check('post-write-sync 가 감사 행을 남긴다', rows.length > 0, String(rows.length));
    const untagged = rows.filter(r => r.run !== RUN);
    check('모든 감사 행에 실행 태그가 붙는다', untagged.length === 0,
      untagged.map(r => r.event || r.tool || '(?)').join(','));
  } finally { SB.cleanup(dir); }

  // 소스 수준 — 감사 경로가 하나로 모였는가 (사본이 다시 생기면 태그가 또 샌다)
  const pws = fs.readFileSync(path.join(SB.REPO, '.claude', 'hooks', 'post-write-sync.js'), 'utf8');
  const rawAudit = (pws.match(/fs\.appendFileSync\([^\n]*audit-log/g) || []);
  check('post-write-sync 에 날 감사 쓰기가 헬퍼 안에만 있다', rawAudit.length <= 1,
    rawAudit.join(' | '));
  check('post-write-sync 가 auditRow 헬퍼를 쓴다', /function auditRow\(/.test(pws) && /auditRow\(\{/.test(pws), '');
  const pc = fs.readFileSync(path.join(SB.REPO, '.claude', 'hooks', 'pre-compact.js'), 'utf8');
  check('pre-compact 가 appendAudit 를 거친다', /appendAudit\(\{/.test(pc), '');
}

// ── LC-08: 무결성 1차 방어선이 실제 훅에서 산다 (v4/N-02 · FR-05) ─────
// 판정기 단위 테스트(test_command_judge)만으로는 **배선이 끊겨도 초록**이다.
// 여기서는 샌드박스에 실제 pre-tool-gate.js 를 구동해 판정이 도구까지 닿는지 본다.
console.log('\n[LC-08] should_seal_integrity_through_the_real_hook');
{
  const dir = SB.makeSandbox({ prefix: 'lc08' });
  try {
    const L = 'docs/memory/' + 'decisions.jsonl';
    const A = 'docs/memory/' + 'audit-log.jsonl';
    const judge = cmd => {
      const r = SB.hook(dir, 'pre-tool-gate.js', {
        hook_event_name: 'PreToolUse', session_id: 'lc08', tool_use_id: 'tu',
        tool_name: 'Bash', tool_input: { command: cmd },
      }, { _HARNESS_TEST_RUN: 'lc08' });
      return /"decision"\s*:\s*"block"/.test(r.out);
    };

    const CASES = [
      // [명령, 차단해야 하는가, 라벨]
      [`node -e "xs.map(f => read('${A}'))"`, false, '읽기 전용 화살표 함수는 통과한다'],
      [`echo x > ${A}`, true, '진짜 리다이렉트는 차단된다'],
      [`python -c "open('${L}','w').write('x')"`, true, 'python 원장 쓰기가 차단된다'],
      [`ruby -e "File.write('${L}','x')"`, true, 'ruby 원장 쓰기가 차단된다'],
      ['rm -rf docs/memory', true, '보호 구역 디렉터리 삭제가 차단된다'],
      ['rm -rf .claude/.branch-x', false, '브랜치 상태 청소는 통과한다'],
      [`cat ${L}`, false, '평범한 읽기는 통과한다'],
    ];

    for (const [cmd, shouldBlock, label] of CASES) {
      const blocked = judge(cmd);
      check(label, blocked === shouldBlock,
        `${blocked ? '차단' : '통과'} · 기대=${shouldBlock ? '차단' : '통과'} · ${cmd.slice(0, 60)}`);
    }
  } finally { SB.cleanup(dir); }
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
