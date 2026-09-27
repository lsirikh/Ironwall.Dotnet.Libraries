// tests/unit/test_signals.js
// _signals.js 순수 함수 테스트 — 의사결정 원장 파서 + 재발방지 신호 평가기 (v3.2 / TEST-06)
//
// 왜 이 파일이 존재하는가:
//   Loop D 는 98일간 0행이었다. 규율에만 맡긴 루프는 조용히 죽는다는 것이 측정됐고,
//   Loop V 가 상태기계로 배선되자 살아난 것이 반례다. 원장도 같은 처방을 받는다 —
//   행을 기계가 읽고, 재발방지(invalidation)를 기계가 **평가**한다.
//   그 평가기가 틀리면 원장은 "있는데 아무것도 안 하는" 상태로 되돌아가므로,
//   평가기는 I/O 를 전부 주입받는 순수 함수여야 하고 여기서 단독 검증된다.
//
// 이 파일은 fs / spawnSync / git 을 절대 쓰지 않는다. 샌드박스도 만들지 않는다.
//   (test_seed_gate 11초 · test_touched_scope 0.75초의 원인이 sandbox+git init 이고,
//    Loop V 의 유효 타임아웃은 _config.js 의 test_timeout_ms=60s 뿐이다.)

'use strict';

const assert = require('assert');
const path = require('path');

const SIGNALS_PATH = path.resolve(__dirname, '../../.claude/hooks/_signals.js');
const S = require(SIGNALS_PATH);

let pass = 0, fail = 0;
function test(name, fn) {
  try { fn(); pass++; console.log(`  ✅ ${name}`); }
  catch (e) { fail++; console.log(`  ❌ ${name}: ${e.message} (.claude/hooks/_signals.js)`); }
}

// ── 스텁 I/O ─────────────────────────────────────────────────────────
// evaluate 는 { rows, auditLines, readFile, exists, listFiles, git, now } 를 받는다.
// 전부 인메모리. 스텁이 호출되지 않은 것도 검증할 수 있게 호출 기록을 남긴다.
function stubs(over) {
  const files = (over && over.files) || {};
  const calls = { readFile: [], exists: [], git: [], listFiles: [] };
  return {
    calls,
    auditLines: (over && over.auditLines) || [],
    now: (over && over.now) || Date.parse('2026-09-06T00:00:00Z'),
    readFile: p => { calls.readFile.push(p); return Object.prototype.hasOwnProperty.call(files, p) ? files[p] : null; },
    exists: p => { calls.exists.push(p); return Object.prototype.hasOwnProperty.call(files, p); },
    listFiles: g => { calls.listFiles.push(g); return (over && over.listFiles) ? over.listFiles(g) : Object.keys(files); },
    git: a => { calls.git.push(a); return (over && over.git) ? over.git(a) : null; },
  };
}

const auditLine = (event, at, extra) =>
  JSON.stringify(Object.assign({ timestamp: new Date(at).toISOString(), event }, extra || {}));

const row = o => Object.assign({
  id: 'D-test-000000', date: '2026-09-01', decision: 'd', reason: 'r',
  rejected: ['alt'], invalidation: { text: 't', specs: [] },
  files: [], tasks: [], supersedes: null, source: 'user',
}, o);

// ── 상태 상수 ────────────────────────────────────────────────────────
const OK = 'ok', VIOLATED = 'violated', INSUFFICIENT = 'insufficient';

console.log('\n═══ SIG: 의사결정 원장 파서 + 신호 평가기 ═══\n');

// ══ 1. 파싱 — JSONL 과 레거시 마크다운 표 ══════════════════════════════

test('should_parse_legacy_3col_and_5col_ledger', () => {
  // session-context.md 실측: 3열 21행 + 5열 6행이 한 표에 섞여 있다.
  // 5열 이유 칸에는 이스케이프 안 된 리터럴 '|' 가 실재한다(`.claude|tests`) — split('|') 이 6필드를 낸다.
  const md = [
    '## 중요 기술 결정 사항',
    '',
    '| 날짜 | 결정 | 이유 |',
    '|------|------|------|',
    '| 2026-04-10 | 3-Track 구조 | 복잡도별 유연한 경로 |',
    '| 2026-05-28 | CAS Lead 선출 | wx flag + dead PID 감지 |',
    '| 2026-09-04 | touched 제외는 docs/ 와 런타임뿐 | 최초 규칙이 .claude|tests 를 문서류로 제외 | 기각: .claude/ 계속 제외 | 재발방지: test_touched_scope.js |',
    '| 2026-09-04 | statusEntries 일원화 | gitRun 이 trim 해 첫 줄이 잘린다 | 기각: gitRun trim 제거 | 재발방지: grep slice(3) 이 남으면 회귀 |',
    '',
    '## 다음 섹션',
  ].join('\n');

  const { rows, errors } = S.parseLedger(md);
  assert.strictEqual(rows.length, 4, `4행이어야: ${rows.length}`);

  const legacy = rows.filter(r => r.legacy);
  assert.strictEqual(legacy.length, 2, '3열 행은 legacy=true 로 표시');
  assert.deepStrictEqual(legacy[0].rejected, [], '3열에는 기각안이 없다 — 지어내면 안 된다');
  assert.strictEqual(legacy[0].invalidation.text, '', '3열에는 재발방지가 없다');
  assert.strictEqual(legacy[0].date, '2026-04-10');
  assert.strictEqual(legacy[0].decision, '3-Track 구조');

  const five = rows.filter(r => !r.legacy);
  assert.strictEqual(five.length, 2, '5열 행 2개');
  assert.ok(five[0].reason.includes('.claude|tests'),
    `이유 칸의 리터럴 파이프가 보존되어야: ${JSON.stringify(five[0].reason)}`);
  assert.ok(five[0].rejected.length >= 1, '5열은 기각안을 갖는다');
  assert.ok(/test_touched_scope/.test(five[0].invalidation.text), '재발방지 텍스트 보존');

  assert.ok(Array.isArray(errors), 'errors 는 배열');
});

test('should_parse_jsonl_ledger_and_reject_malformed_rows', () => {
  const jsonl = [
    JSON.stringify({
      id: 'D-src-gate', date: '2026-08-08', decision: 'phase 기반 src/ 차단 삭제',
      reason: '도구 사용을 막는 대신 상태 전이만 거부', rejected: ['차단 부활'],
      invalidation: { text: 'trackAllowsWrite 가 되살아나면 회귀', specs: ['grep:.claude/hooks/pre-tool-gate.js /trackAllowsWrite/ ==0'] },
      files: ['.claude/hooks/pre-tool-gate.js'], tasks: [], supersedes: null, source: 'commit',
    }),
    '',
    '{ 이건 JSON 이 아니다',
    JSON.stringify({ id: 'D-2', date: '2026-09-01', decision: 'x', reason: 'y', rejected: [], invalidation: { text: '', specs: [] }, files: [], source: 'user' }),
  ].join('\n');

  const { rows, errors } = S.parseLedger(jsonl);
  assert.strictEqual(rows.length, 2, `파싱 가능한 2행: ${rows.length}`);
  assert.strictEqual(errors.length, 1, '깨진 줄 1건이 errors 로');
  assert.ok(/3/.test(String(errors[0])), `errors 는 줄 번호를 담아야: ${errors[0]}`);
  assert.strictEqual(rows[0].id, 'D-src-gate');
  assert.strictEqual(rows[0].invalidation.specs.length, 1);
  assert.strictEqual(rows[0].legacy, false, 'JSONL 행은 legacy 가 아니다');
});

test('should_reject_row_without_rejected_or_invalidation_on_validate', () => {
  // decide CLI 가 쓰는 검증. 원장 스키마의 보증은 여기 하나에 달려 있다.
  assert.strictEqual(S.validateRow(row()).ok, true, '완전한 행은 통과');

  const noRejected = S.validateRow(row({ rejected: [] }));
  assert.strictEqual(noRejected.ok, false, 'rejected 가 비면 거부');
  assert.ok(/rejected/i.test(noRejected.reason), noRejected.reason);

  const noInval = S.validateRow(row({ invalidation: { text: '', specs: [] } }));
  assert.strictEqual(noInval.ok, false, 'invalidation 이 비면 거부');

  const noDecision = S.validateRow(row({ decision: '' }));
  assert.strictEqual(noDecision.ok, false, 'decision 이 비면 거부');

  // legacy 이관 행은 rejected 요구에서 면제된다 — 없는 기각안을 지어내는 것이 더 나쁘다.
  const legacy = S.validateRow(row({ rejected: [], invalidation: { text: '', specs: [] }, legacy: true }));
  assert.strictEqual(legacy.ok, true, 'legacy:true 행은 rejected/invalidation 요구 면제');
});

test('should_derive_stable_id_from_decision_text', () => {
  const a = S.rowId('2026-09-06', 'phase 기반 src/ 차단 삭제');
  const b = S.rowId('2026-09-06', 'phase 기반 src/ 차단 삭제');
  const c = S.rowId('2026-09-06', '다른 결정');
  assert.strictEqual(a, b, '같은 입력은 같은 id');
  assert.notStrictEqual(a, c, '다른 결정은 다른 id');
  assert.ok(/^D-2026-09-06-[0-9a-f]{6}$/.test(a), `id 형식: ${a}`);
});

// ══ 2. specs 문법 ══════════════════════════════════════════════════════

test('should_extract_specs_grammar_all_7_kinds', () => {
  const rows = [row({
    id: 'D-all', invalidation: {
      text: '전 문법',
      specs: [
        'perf:session-gate.js p90 <= 400ms/7d',
        'audit:state-seeded >= 1/30d',
        'file:absent docs/memory/observations.jsonl',
        'grep:.claude/hooks/pre-tool-gate.js /trackAllowsWrite/ ==0',
        'json:docs/memory/feedback-rules.json length == 0',
        "git:log --since=2026-08-08 /add -A/ == 0",
        'test:signals',
      ],
    },
  })];

  const sigs = S.extractSignals(rows);
  assert.strictEqual(sigs.length, 7, `7종 전부 추출: ${sigs.length}`);
  assert.deepStrictEqual(sigs.map(s => s.kind), ['perf', 'audit', 'file', 'grep', 'json', 'git', 'test']);
  assert.ok(sigs.every(s => s.rowId === 'D-all'), '모든 신호가 원 행을 가리킨다');
  assert.ok(sigs.every(s => !s.error), `파싱 오류 없어야: ${JSON.stringify(sigs.filter(s => s.error))}`);

  const grep = sigs.find(s => s.kind === 'grep');
  assert.strictEqual(grep.parsed.target, '.claude/hooks/pre-tool-gate.js');
  assert.strictEqual(grep.parsed.re, 'trackAllowsWrite');
  assert.strictEqual(grep.parsed.op, '==');
  assert.strictEqual(grep.parsed.n, 0);

  const audit = sigs.find(s => s.kind === 'audit');
  assert.strictEqual(audit.parsed.event, 'state-seeded');
  assert.strictEqual(audit.parsed.op, '>=');
  assert.strictEqual(audit.parsed.n, 1);
  assert.strictEqual(audit.parsed.days, 30);

  const perf = sigs.find(s => s.kind === 'perf');
  assert.strictEqual(perf.parsed.hook, 'session-gate.js');
  assert.strictEqual(perf.parsed.stat, 'p90');
  assert.strictEqual(perf.parsed.op, '<=', '연산자를 연다 — 문법 전체가 "성립해야 할 단언"으로 통일');
  assert.strictEqual(perf.parsed.ms, 400);
});

test('should_flag_unparseable_spec_instead_of_silently_dropping', () => {
  const sigs = S.extractSignals([row({ invalidation: { text: 't', specs: ['이건 문법이 아니다', 'grep:x /y/ ~~ 3'] } })]);
  assert.strictEqual(sigs.length, 2, '해석 못 해도 신호로 남긴다 — 조용히 사라지면 원장이 거짓말을 한다');
  assert.ok(sigs.every(s => s.error), '둘 다 error 를 갖는다');
  assert.strictEqual(sigs[0].kind, 'unknown');
});

// ══ 3. 평가 ════════════════════════════════════════════════════════════

test('should_fire_checkpoint_absent_signal', () => {
  // "checkpoint 서브커맨드는 약속만 있고 구현이 없다" 를 잡던 신호.
  // 있어야 할 것이 없으면 violated.
  const rows = [row({
    id: 'D-checkpoint',
    decision: 'checkpoint 서브커맨드 구현',
    invalidation: { text: '구현이 사라지면 회귀', specs: ["grep:.claude/hooks/advance-phase.js /targetPhase === 'checkpoint'/ >=1"] },
  })];

  const missing = stubs({ files: { '.claude/hooks/advance-phase.js': '// 아직 checkpoint 없음\n' } });
  const r1 = S.evaluate(Object.assign({ rows }, missing));
  assert.strictEqual(r1.results.length, 1);
  assert.strictEqual(r1.results[0].status, VIOLATED, `구현 부재는 violated: ${JSON.stringify(r1.results[0])}`);
  assert.strictEqual(r1.violations.length, 1);
  assert.strictEqual(r1.results[0].actual, 0, '실측 매치 수를 보고한다');

  const present = stubs({ files: { '.claude/hooks/advance-phase.js': "if (targetPhase === 'checkpoint') {\n  doIt();\n}\n" } });
  const r2 = S.evaluate(Object.assign({ rows }, present));
  assert.strictEqual(r2.results[0].status, OK, '구현이 있으면 ok');
  assert.strictEqual(r2.violations.length, 0);
});

test('should_fire_skill_md_346_signal', () => {
  // "물리적으로 차단" 서술이 남아 있으면 v3.1 거버넌스 결정과 모순 → violated.
  const rows = [row({
    id: 'D-no-physical-block',
    decision: '도구 차단이 아니라 상태 전이만 거부',
    invalidation: { text: '문서에 물리적 차단 서술이 되살아나면 회귀', specs: ['grep:.claude/skills/process/SKILL.md /물리적으로 차단/ ==0'] },
  })];

  const dirty = stubs({ files: { '.claude/skills/process/SKILL.md': '...src/ 쓰기를 물리적으로 차단한다...\n또 물리적으로 차단.\n' } });
  const r1 = S.evaluate(Object.assign({ rows }, dirty));
  assert.strictEqual(r1.results[0].status, VIOLATED);
  assert.strictEqual(r1.results[0].actual, 2, '전역 매치 수를 센다 (첫 매치에서 멈추면 안 됨)');

  const clean = stubs({ files: { '.claude/skills/process/SKILL.md': '상태 전이만 거부한다.\n' } });
  assert.strictEqual(S.evaluate(Object.assign({ rows }, clean)).results[0].status, OK);
});

test('should_report_insufficient_when_target_file_is_missing', () => {
  // 파일이 없으면 "만족"이 아니라 "확인 불가"다. ==0 을 공짜로 통과시키면 낡은 스펙이 영원히 초록이 된다.
  const rows = [row({ invalidation: { text: 't', specs: ['grep:.claude/hooks/gone.js /x/ ==0'] } })];
  const r = S.evaluate(Object.assign({ rows }, stubs({ files: {} })));
  assert.strictEqual(r.results[0].status, INSUFFICIENT, '대상 파일 부재 → insufficient');
  assert.strictEqual(r.violations.length, 0, 'insufficient 는 violation 이 아니다');
  assert.strictEqual(r.insufficient.length, 1, '따로 집계된다');
});

test('should_report_insufficient_when_perf_sample_is_empty', () => {
  // D-B: hook-perf 는 elapsed>100ms 만 기록되고 session_id 도 없다.
  //   훅이 실제로 빨라지면 표본이 0건이 되는데, 그때 'ok' 를 주면 성공했기 때문에 신호가 죽는다.
  const rows = [row({ invalidation: { text: 't', specs: ['perf:session-gate.js p90 <= 400ms/7d'] } })];

  const empty = S.evaluate(Object.assign({ rows }, stubs({ auditLines: [] })));
  assert.strictEqual(empty.results[0].status, INSUFFICIENT, '표본 0건은 ok 가 아니다');

  const slow = stubs({
    auditLines: [
      auditLine('hook-perf', Date.parse('2026-09-05T00:00:00Z'), { hook: 'session-gate.js', elapsed_ms: 900 }),
      auditLine('hook-perf', Date.parse('2026-09-05T01:00:00Z'), { hook: 'session-gate.js', elapsed_ms: 800 }),
      auditLine('hook-perf', Date.parse('2026-09-05T02:00:00Z'), { hook: 'session-gate.js', elapsed_ms: 950 }),
    ],
  });
  assert.strictEqual(S.evaluate(Object.assign({ rows }, slow)).results[0].status, VIOLATED, 'p90 이 상한을 넘으면 단언이 깨진다 → violated');
});

test('should_evaluate_audit_file_json_and_git_specs', () => {
  const now = Date.parse('2026-09-06T00:00:00Z');
  const day = 86400000;

  // audit: 창 밖의 행은 세지 않는다
  const auditRows = row({
    id: 'D-audit',
    invalidation: { text: 't', specs: ['audit:state-seeded >= 2/7d'] },
  });
  const auditCtx = stubs({
    now,
    auditLines: [
      auditLine('state-seeded', now - 1 * day),
      auditLine('state-seeded', now - 2 * day),
      auditLine('state-seeded', now - 40 * day), // 창 밖
      auditLine('other-event', now - 1 * day),
    ],
  });
  const a = S.evaluate(Object.assign({ rows: [auditRows] }, auditCtx));
  assert.strictEqual(a.results[0].actual, 2, `7일 창 안의 state-seeded 는 2건: ${a.results[0].actual}`);
  assert.strictEqual(a.results[0].status, OK);

  // file:absent
  const fileRows = [row({ invalidation: { text: 't', specs: ['file:absent docs/memory/observations.jsonl'] } })];
  assert.strictEqual(S.evaluate(Object.assign({ rows: fileRows }, stubs({ files: {} }))).results[0].status, OK);
  assert.strictEqual(
    S.evaluate(Object.assign({ rows: fileRows }, stubs({ files: { 'docs/memory/observations.jsonl': '' } }))).results[0].status,
    VIOLATED, '없어야 할 파일이 있으면 violated');

  // json:
  const jsonRows = [row({ invalidation: { text: 't', specs: ['json:docs/memory/feedback-rules.json blockCount == 0'] } })];
  const jOk = stubs({ files: { 'docs/memory/feedback-rules.json': JSON.stringify({ blockCount: 0 }) } });
  assert.strictEqual(S.evaluate(Object.assign({ rows: jsonRows }, jOk)).results[0].status, OK);
  const jBad = stubs({ files: { 'docs/memory/feedback-rules.json': JSON.stringify({ blockCount: 3 }) } });
  assert.strictEqual(S.evaluate(Object.assign({ rows: jsonRows }, jBad)).results[0].status, VIOLATED);
  const jBroken = stubs({ files: { 'docs/memory/feedback-rules.json': '{{{' } });
  assert.strictEqual(S.evaluate(Object.assign({ rows: jsonRows }, jBroken)).results[0].status, INSUFFICIENT, 'JSON 파싱 실패는 insufficient');

  // git:log
  const gitRows = [row({ invalidation: { text: 't', specs: ['git:log --since=2026-08-08 /chore: phase/ == 0'] } })];
  const gOk = stubs({ git: () => 'abc feat: x\ndef fix: y' });
  assert.strictEqual(S.evaluate(Object.assign({ rows: gitRows }, gOk)).results[0].status, OK);
  const gBad = stubs({ git: () => 'abc chore: phase dev → test\ndef feat: x' });
  assert.strictEqual(S.evaluate(Object.assign({ rows: gitRows }, gBad)).results[0].status, VIOLATED);
  const gDead = stubs({ git: () => null });
  assert.strictEqual(S.evaluate(Object.assign({ rows: gitRows }, gDead)).results[0].status, INSUFFICIENT, 'git 실패는 insufficient');
});

test('should_defer_test_spec_outside_complete', () => {
  const rows = [row({ invalidation: { text: 't', specs: ['test:signals'] } })];
  const off = S.evaluate(Object.assign({ rows }, stubs()));
  assert.strictEqual(off.results[0].status, INSUFFICIENT, 'test: 는 complete 밖에서 평가하지 않는다');
  assert.ok(/complete/.test(off.results[0].detail || ''), `사유를 밝혀야: ${off.results[0].detail}`);
});

test('should_not_fire_when_superseded_row_exists', () => {
  // 뒤집힌 결정의 신호가 계속 울리면 원장이 스스로를 반박한다.
  const rows = [
    row({
      id: 'D-old', date: '2026-08-01', decision: 'phase 기반 src/ 차단',
      invalidation: { text: '차단이 사라지면 회귀', specs: ['grep:.claude/hooks/pre-tool-gate.js /trackAllowsWrite/ >=1'] },
    }),
    row({
      id: 'D-new', date: '2026-08-08', decision: 'phase 기반 src/ 차단 삭제', supersedes: 'D-old',
      invalidation: { text: '차단이 되살아나면 회귀', specs: ['grep:.claude/hooks/pre-tool-gate.js /trackAllowsWrite/ ==0'] },
    }),
  ];
  const ctx = stubs({ files: { '.claude/hooks/pre-tool-gate.js': '// 차단 없음\n' } });
  const r = S.evaluate(Object.assign({ rows }, ctx));

  assert.strictEqual(r.violations.length, 0, `뒤집힌 D-old 는 울리면 안 된다: ${JSON.stringify(r.violations)}`);
  assert.strictEqual(r.results.length, 1, '평가 대상은 D-new 하나');
  assert.strictEqual(r.results[0].rowId, 'D-new');
  assert.strictEqual(r.skipped.length, 1, '건너뛴 사실은 남긴다');
  assert.strictEqual(r.skipped[0].rowId, 'D-old');
  assert.ok(/supersede/i.test(r.skipped[0].why), r.skipped[0].why);
});

test('should_skip_settled_rows', () => {
  // [v3.6/N-06] 계약이 바뀌었다. 예전에는 "settled 는 렌더링에서 접는다는 뜻이지
  //   검사를 그만둔다가 아니다" 였는데, 그러면 **끝난 논쟁의 스펙이 매 세션 울린다** —
  //   그리고 정착 상태로 들어가는 문이 없어서(쓰는 코드 0곳) 은퇴 수단이 supersedes 뿐이었다.
  //   위반이 complete 를 막게 되는 지금, "고치는 것보다 뒤집는 게 싸다" 를 피하려면
  //   정직한 은퇴 경로가 필요하다. 정착 행의 스펙은 평가하지 않는다.
  const rows = [row({ id: 'D-settled', settled: true, invalidation: { text: 't', specs: ['file:absent gone.js'] } })];
  const r = S.evaluate(Object.assign({ rows }, stubs({ files: { 'gone.js': 'x' } })));
  assert.strictEqual(r.violations.length, 0, '정착 행의 스펙이 여전히 평가된다');
  assert.ok(r.skipped.some(s => s.rowId === 'D-settled' && /settled|정착/.test(s.why)), JSON.stringify(r.skipped));
});

// ══ 4. 성능 ════════════════════════════════════════════════════════════

test('should_evaluate_under_5ms_on_2000_audit_lines', () => {
  const now = Date.parse('2026-09-06T00:00:00Z');
  const auditLines = [];
  for (let i = 0; i < 2000; i++) {
    auditLines.push(auditLine(i % 7 === 0 ? 'state-seeded' : 'hook-perf', now - i * 60000,
      { hook: 'session-gate.js', elapsed_ms: 100 + (i % 500) }));
  }
  const rows = [];
  for (let i = 0; i < 20; i++) {
    rows.push(row({
      id: 'D-perf-' + i,
      invalidation: { text: 't', specs: ['audit:state-seeded >= 1/30d', 'perf:session-gate.js p90 <= 400ms/7d'] },
    }));
  }
  const ctx = stubs({ now, auditLines });
  const arg = Object.assign({ rows }, ctx);

  S.evaluate(arg); // warm-up
  // [v13/N-01] 평균이 아니라 **중앙값**으로 판정한다. 평균은 GC·다른 프로세스가 한 번 튄 값에 끌려
  //   전체 검증이 간헐적으로 실패했다(O-11 — v12 에서는 원문이 잘려 원인을 몰랐고, N-08 의 실패 원문
  //   로그로 처음 확인: 평균 5.329ms). 기준(5ms)은 그대로다 — 정말 느려지면 중앙값도 넘는다.
  const samples = [];
  for (let k = 0; k < 15; k++) {
    const t0 = process.hrtime.bigint();
    S.evaluate(arg);
    samples.push(Number(process.hrtime.bigint() - t0) / 1e6);
  }
  // [v16/N-28] 중앙값도 부족했다. 중앙값은 **간헐적** 스파이크에 강하지만, 전수 스위트처럼
  //   기계 전체가 바쁜 **지속적** 부하에서는 15개 표본이 전부 느려진다 — 이 세션에서만
  //   7.148ms · 5.267ms 두 번 실패했고 둘 다 코드 회귀가 아니었다(단독 실행 2.7ms).
  //   마이크로벤치의 올바른 통계는 **최솟값**이다: 부하는 시간을 더하기만 하고 빼지 않으므로
  //   가장 빠른 관측이 진짜 비용에 가장 가깝다. **기준(5ms)은 그대로다** — 코드가 정말
  //   느려지면 최솟값도 넘는다. 벽을 낮춘 것이 아니라 재는 방식을 고친 것이다.
  samples.sort((a, b) => a - b);
  const ms = samples[0];

  assert.ok(ms < 5, `evaluate 2000줄·40신호 ${ms.toFixed(3)}ms (< 5ms 여야) — .claude/hooks/_signals.js`);
  console.log(`     (2000줄 audit · 40신호 evaluate 최솟값 ${ms.toFixed(3)}ms)`);
});

// ══ 5. glob 매칭 — session-gate 의 '📌 관련 결정' 이 쓴다 ══════════════

test('should_match_decision_files_against_task_paths', () => {
  assert.strictEqual(S.matchGlob('.claude/hooks/*.js', '.claude/hooks/session-gate.js'), true);
  assert.strictEqual(S.matchGlob('.claude/hooks/*.js', '.claude/hooks/sub/deep.js'), false, '* 는 경로 구분자를 넘지 않는다');
  assert.strictEqual(S.matchGlob('.claude/**/*.md', '.claude/skills/process/SKILL.md'), true, '** 는 넘는다');
  assert.strictEqual(S.matchGlob('docs/memory/decisions.jsonl', 'docs/memory/decisions.jsonl'), true);
  assert.strictEqual(S.matchGlob('docs/memory/decisions.jsonl', 'docs/memory/decisions.jsonl.bak'), false);
  assert.strictEqual(S.matchGlob('.claude/hooks/session-gate.js', '.claude\\hooks\\session-gate.js'), true, 'Windows 경로 구분자 허용');
  // [v3.5] `..` 순회 (레드팀 #21·25·34): 접지 않으면 .claude/hooks/../../install.js 가 .claude/hooks/** 안으로 판정된다
  assert.strictEqual(S.matchGlob('.claude/hooks/**', '.claude/hooks/../../install.js'), false, '`..` 로 봉투 밖 파일을 안으로 들이지 못한다');
  assert.strictEqual(S.matchGlob('install.js', '.claude/hooks/../../install.js'), true, '접힌 경로는 실제 파일과 맞는다');
  assert.strictEqual(S.matchGlob('src/**', 'src/./a/../b.js'), true, '`.` 와 되돌아오는 `..` 는 접힌다');
  assert.strictEqual(S.matchGlob('**/x.js', '../x.js'), false, '루트 밖은 어떤 글롭과도 맞지 않는다');
});

// ══ 5.5 topics — 행위 스코프 (v3.4 / TEST-11) ════════════════════════
// 왜: `📌 관련 결정` 은 files[] 경로 교집합으로만 뜬다. 그런데 교훈성 결정은 특정 파일이
//   아니라 **행위**에 붙는다 — "spec 을 쓸 때", "DoD 를 쓸 때". 실측으로 활성 19행 중
//   PRD 작성 시 매칭되는 행이 0건이었고, 그래서 v3.3 의 DoD 가 원장이 이미 기록한
//   함정에 그대로 빠졌다. 파일 스코프만으로는 영영 안 닿는다.

test('should_parse_and_roundtrip_topics', () => {
  const line = JSON.stringify({
    id: 'D-t', date: '2026-09-07', decision: 'x', reason: 'y', rejected: ['z'],
    invalidation: { text: 't', specs: [] }, files: [], topics: ['spec-authoring', 'prd'], source: 'user',
  });
  const { rows } = S.parseLedger(line);
  assert.deepStrictEqual(rows[0].topics, ['spec-authoring', 'prd'], `topics 왕복: ${JSON.stringify(rows[0].topics)}`);

  // 없으면 빈 배열 — undefined 로 두면 소비자가 매번 방어해야 한다
  const bare = S.parseLedger(JSON.stringify({ id: 'D-u', decision: 'a', reason: 'b', rejected: ['c'], invalidation: { text: 'd', specs: [] } }));
  assert.deepStrictEqual(bare.rows[0].topics, []);

  // topics 는 **선택**이다. 요구하면 기존 40행이 전부 무효가 된다.
  assert.strictEqual(S.validateRow(row()).ok, true, 'topics 없어도 통과');
  // 어휘 밖 topic 은 거부가 아니라 경고 — 어휘는 늘어날 수 있다
  const v = S.validateRow(row({ topics: ['made-up-topic'] }));
  assert.strictEqual(v.ok, true, '어휘 밖이어도 거부하지 않는다');
  assert.ok(v.warnings.some(w => /made-up-topic/.test(w)), `경고는 남긴다: ${JSON.stringify(v.warnings)}`);
});

test('should_match_rows_by_topic', () => {
  const rows = [
    row({ id: 'D-1', topics: ['spec-authoring'] }),
    row({ id: 'D-2', topics: ['prd', 'plan'] }),
    row({ id: 'D-3' }),
    row({ id: 'D-old', topics: ['prd'] }),
    row({ id: 'D-new', topics: ['prd'], supersedes: 'D-old' }),
    row({ id: 'D-settled', topics: ['prd'], settled: true }),
  ];
  const ids = t => S.matchTopics(rows, t).map(r => r.id);

  assert.deepStrictEqual(ids('spec-authoring'), ['D-1']);
  assert.deepStrictEqual(ids('plan'), ['D-2']);
  assert.deepStrictEqual(ids('없는토픽'), []);

  const prd = ids('prd');
  assert.ok(prd.includes('D-2') && prd.includes('D-new'), `prd 매칭: ${JSON.stringify(prd)}`);
  assert.ok(!prd.includes('D-old'), '뒤집힌 결정은 뜨지 않는다');
  assert.ok(!prd.includes('D-settled'), '정착된 결정은 뜨지 않는다 — 논쟁 대상이 아니다');
});

test('should_infer_topic_from_doc_path', () => {
  assert.strictEqual(S.topicForPath('docs/prds/x-prd.md'), 'prd');
  assert.strictEqual(S.topicForPath('docs/plans/x-prd-plan.md'), 'plan');
  assert.strictEqual(S.topicForPath('tests/unit/test_x.js'), 'testing');
  assert.strictEqual(S.topicForPath('.claude\\hooks\\pre-tool-gate.js'), 'gate', 'Windows 구분자 + 게이트 코드');
  assert.strictEqual(S.topicForPath('src/app.js'), null, '해당 없으면 null');
  assert.ok(Array.isArray(S.TOPICS) && S.TOPICS.length === 5, `어휘 5종 고정: ${JSON.stringify(S.TOPICS)}`);
});

// [v8/N-02] 실물 원장 판정 — "쓰이는 중" 과 "깨졌다" 를 구분한다.
//   **read 를 인자로 받는다** — 쓰이는 중인 상태를 가짜 읽기로 결정적으로 만들 수 있게.
//
//   작성기는 `appendFileSync(LEDGER, JSON.stringify(row) + '\n')` 하나다. 그러니
//   **완결된 원장은 항상 개행으로 끝난다.** 개행 없이 끝나는 꼬리만 "쓰이는 중일 수 있다".
//   그때만 한 번 더 읽는다(sleep 없이 — testing 규칙):
//     달라졌다 → 파일이 움직이는 중. 마지막 개행까지만 판정한다. 꼬리는 보류.
//     같다     → 멈춘 채 깨진 꼬리다. **영구 손상이므로 위반이다.**
//   중간 줄이 깨졌거나, 개행으로 끝나는 줄이 깨졌으면 여전히 위반이다 —
//   완화는 "지금 쓰이는 마지막 한 줄" 뿐이다.
function judgeLiveLedger(read) {
  const a = read();
  if (a === null || a === undefined) return { verdict: 'absent' };
  const pa = S.parseLedger(a);
  if (!pa.errors.length) return { verdict: 'ok', rows: pa.rows };

  if (!a.endsWith('\n')) {
    const b = read();
    if (b !== a) {
      const prefix = a.slice(0, a.lastIndexOf('\n') + 1);
      if (!prefix) return { verdict: 'unjudgeable' };            // 완결 행이 하나도 없다
      const pp = S.parseLedger(prefix);
      if (pp.errors.length) return { verdict: 'violated', errors: pp.errors, rows: pp.rows };
      return { verdict: 'ok', rows: pp.rows, pendingTail: true };
    }
  }
  return { verdict: 'violated', errors: pa.errors, rows: pa.rows };
}

// ══ 6. 회귀 가드 — 실제 원장이 스스로 성립하는가 (TEST-07) ═══════════
// 원장은 append-only 라 한 번 깨진 행이 들어가면 영구히 남는다. 그리고 스펙이
// 해석 불가면 evaluate 가 조용히 insufficient 로 넘겨 아무도 모른다.
// 여기서는 **저장소의 실제 원장**을 읽어 형식만 검사한다(평가는 하지 않는다 —
// 평가 결과는 코드 상태에 따라 정당하게 변하지만, 형식은 항상 성립해야 한다).
test('should_keep_live_ledger_parseable_and_schema_valid', () => {
  const fs = require('fs');
  const ledgerPath = path.resolve(__dirname, '../../docs/memory/decisions.jsonl');
  if (!fs.existsSync(ledgerPath)) return;   // 신규 설치에는 없다

  const j = judgeLiveLedger(() => { try { return fs.readFileSync(ledgerPath, 'utf8'); } catch { return null; } });
  if (j.verdict === 'absent') return;
  if (j.verdict === 'unjudgeable') { console.log('     (원장이 쓰이는 중 — 완결 행이 없어 판정 보류)'); return; }
  // 실패 메시지는 원장 경로를 **담아야 한다** — failingSet 이 그 경로를 잡아야 다음 진단이 선다.
  assert.strictEqual(j.verdict, 'ok', `원장 파싱 오류: ${(j.errors || []).join(' / ')} — docs/memory/decisions.jsonl`);
  const rows = j.rows;
  if (j.pendingTail) console.log('     (원장 꼬리가 쓰이는 중 — 마지막 개행까지만 판정)');
  assert.ok(rows.length > 0, '원장이 비어 있다');

  const ids = rows.map(r => r.id);
  assert.strictEqual(new Set(ids).size, ids.length,
    `중복 id: ${ids.filter((v, i) => ids.indexOf(v) !== i).join(', ')}`);

  // supersedes 는 실재하는 행을 가리켜야 한다 — 허공을 가리키면 그 결정은 영원히 살아 울린다
  const idSet = new Set(ids);
  for (const r of rows) {
    if (r.supersedes) assert.ok(idSet.has(r.supersedes), `${r.id} 의 supersedes 대상 없음: ${r.supersedes}`);
  }

  // legacy 가 아닌 행은 기각안과 재발방지를 갖는다 (decide 게이트의 사후 확인)
  for (const r of rows) {
    if (r.legacy) continue;
    assert.ok(r.rejected.length >= 1, `${r.id}: 기각안 없음 (legacy 아님)`);
    assert.ok(r.invalidation.text || r.invalidation.specs.length, `${r.id}: 재발방지 없음`);
  }

  // 살아 있는 행의 spec 은 전부 해석 가능해야 한다 — 해석 불가 스펙은 영원히 평가되지 않는다.
  //   [v16/N-23] **살아 있는 행만** 본다. evaluate 가 이미 그렇게 한다(partitionLedger) —
  //   뒤집히거나 정착된 행의 스펙은 애초에 평가 대상이 아니므로 파싱을 요구할 이유가 없었다.
  //   전수를 요구하던 동안 원장은 잘못 쓴 스펙 한 줄에서 **빠져나올 길이 없었다**(append-only +
  //   pipeline-state-write 차단 + 복구 커맨드 부재). 이제 `supersedes` 가 복구 경로다.
  //   새로 쓰는 쪽은 validateRow 가 거부하므로 여기가 느슨해져도 덫은 놓이지 않는다.
  const live = S.partitionLedger(rows).live;
  const bad = S.extractSignals(live).filter(s => s.error);
  assert.strictEqual(bad.length, 0,
    `해석 불가 spec: ${bad.map(b => b.spec).join(' / ')} — docs/memory/decisions.jsonl` +
    '\n  뒤집힌 행이라면 supersedes 로, 새로 쓰는 행이라면 문법을 고치세요.');

  console.log(`     (원장 ${rows.length}행 · legacy ${rows.filter(r => r.legacy).length} · settled ${rows.filter(r => r.settled).length} · 신호 ${S.extractSignals(rows).length})`);
});

// ══ 7. 잘못 쓴 스펙에서 빠져나올 길이 있는가 (v16/N-23) ══════════════
// 2026-09-17 실측으로 드러난 덫: `decide` 는 해석 불가 스펙을 **경고만 하고 받아들였고**,
// 위 회귀 가드는 원장의 **모든** 행이 파싱되기를 하드 실패로 요구했으며, 원장은
// append-only 에 `pipeline-state-write` 가 직접 수정을 막는다. 복구 서브커맨드는 없다.
// 결과: 스펙 한 줄을 잘못 쓰면 자기 테스트 스위트가 **영구히** 빨간불이 되고
// 하네스가 주는 어떤 경로로도 되돌릴 수 없었다. 실제로 이 창의 사이클을 막았다.
//
// 문을 양쪽에서 닫는다.
//   쓰는 쪽 — `decide` 의 유일한 관문(validateRow)이 해석 불가 스펙을 **거부한다**. 덫이 놓이지 않는다.
//   읽는 쪽 — 회귀 가드는 **살아 있는 행**만 본다. evaluate 가 이미 그렇게 한다(partitionLedger).
//             뒤집힌 행의 스펙은 애초에 평가되지 않으므로 파싱을 요구할 이유가 없다.
//             그래서 `supersedes` 가 실제로 복구 경로가 된다 — 이미 놓인 덫도 풀린다.
{
  const ROW = over => Object.assign({
    id: 'D-2026-09-17-aaaaaa', date: '2026-09-17', decision: 'd', reason: 'r',
    rejected: ['x'], invalidation: { specs: [] },
  }, over || {});
  const BAD = 'grep:.claude/hooks/pre-tool-gate.js prd-self-approval >= 1';   // /슬래시/ 누락 — 실제로 원장에 들어갔던 형태
  const GOOD = 'grep:.claude/hooks/pre-tool-gate.js /prd-self-approval/ >= 1';

  test('decide 는 해석 불가 스펙을 거부한다 (덫을 놓지 못하게)', () => {
    const v = S.validateRow(ROW({ invalidation: { specs: [BAD] } }));
    assert.strictEqual(v.ok, false, `받아들였다 — warnings=${JSON.stringify(v.warnings)}`);
    assert.ok(/해석 불가|spec/.test(v.reason || ''), `거부 사유가 스펙을 가리키지 않는다: ${v.reason}`);
    assert.ok(String(v.reason).includes(BAD), `거부 사유에 문제의 스펙이 없다: ${v.reason}`);
  });

  test('대조군: 해석 가능한 스펙은 그대로 통과한다', () => {
    const v = S.validateRow(ROW({ invalidation: { specs: [GOOD] } }));
    assert.strictEqual(v.ok, true, `막혔다: ${v.reason}`);
  });

  test('대조군: 스펙 없는 행(text 만)은 여전히 통과한다', () => {
    const v = S.validateRow(ROW({ invalidation: { text: '이 결정이 틀렸다면 …', specs: [] } }));
    assert.strictEqual(v.ok, true, `막혔다: ${v.reason}`);
  });

  test('뒤집힌 행의 해석 불가 스펙은 회귀 가드를 막지 않는다 (supersedes = 복구 경로)', () => {
    const rows = [
      ROW({ id: 'D-1', invalidation: { specs: [BAD] } }),
      ROW({ id: 'D-2', supersedes: 'D-1', invalidation: { specs: [GOOD] } }),
    ];
    const live = S.partitionLedger(rows).live;
    const bad = S.extractSignals(live).filter(s => s.error);
    assert.strictEqual(bad.length, 0, `살아 있는 신호에 오류가 남아 있다: ${bad.map(b => b.spec).join(' / ')}`);
  });

  test('대조군: 살아 있는 행의 해석 불가 스펙은 여전히 잡힌다', () => {
    const rows = [ROW({ id: 'D-1', invalidation: { specs: [BAD] } })];
    const live = S.partitionLedger(rows).live;
    const bad = S.extractSignals(live).filter(s => s.error);
    assert.strictEqual(bad.length, 1, '살아 있는 잘못된 스펙을 놓쳤다 — 가드가 무력해졌다');
  });

  test('대조군: 정착된 행의 해석 불가 스펙도 회귀 가드를 막지 않는다', () => {
    const rows = [
      ROW({ id: 'D-1', invalidation: { specs: [BAD] } }),
      ROW({ id: 'D-2', settles: 'D-1', invalidation: { text: '끝난 논쟁' } }),
    ];
    const live = S.partitionLedger(rows).live;
    const bad = S.extractSignals(live).filter(s => s.error);
    assert.strictEqual(bad.length, 0, `정착된 행의 스펙이 여전히 가드를 막는다: ${bad.map(b => b.spec).join(' / ')}`);
  });
}

// ── [v8/N-02] 쓰이는 중인 원장과 깨진 원장을 구분한다 ─────────────────
//   위 단언은 실물 원장을 읽는다. 그 파일은 **다른 프로세스가 쓴다** — 러너 스스로
//   "테스트가 훅을 spawn 하면 실제 원장에 행이 남는다" 고 적어 두었고, 사용자는 같은
//   저장소에서 세션을 여럿 돌린다. 쓰는 순간에 읽으면 마지막 줄이 잘린다.
//   **그것은 원장이 깨진 것이 아니라 읽는 순간의 사고다.**
//
//   작성기는 `appendFileSync(LEDGER, JSON.stringify(row) + '\n')` 하나다 — 한 행 = 한 줄이고
//   매 행이 개행으로 끝난다. 그러니 **완결된 원장은 항상 개행으로 끝난다.**
//   완화는 "지금 쓰이는 마지막 한 줄" 뿐이다. 멈춘 꼬리·중간 줄·완결된 깨진 줄은 여전히 위반이다.
{
  const ROW = id => JSON.stringify({ id, date: '2026-09-10', decision: 'd', reason: 'r',
    rejected: ['x'], invalidation: { text: 't', specs: [] } }) + '\n';
  const WHOLE = ROW('D-2026-09-10-aaaaaa') + ROW('D-2026-09-10-bbbbbb');
  const TORN = WHOLE + ROW('D-2026-09-10-cccccc').slice(0, 40);   // 개행 없이 끊긴 꼬리
  const seq = (...xs) => { let i = 0; return () => xs[Math.min(i++, xs.length - 1)]; };

  test('should_accept_a_whole_ledger', () => {
    assert.strictEqual(judgeLiveLedger(seq(WHOLE)).verdict, 'ok');
  });

  test('should_not_call_a_tail_being_written_a_violation', () => {
    // 첫 읽기는 잘렸고, 다시 읽으니 완결됐다 — 쓰는 중이었다.
    const j = judgeLiveLedger(seq(TORN, WHOLE + ROW('D-2026-09-10-cccccc')));
    assert.strictEqual(j.verdict, 'ok', JSON.stringify(j.errors || j));
    assert.strictEqual(j.rows.length, 2, '판정은 마지막 개행까지만');
  });

  test('should_call_a_stuck_torn_tail_a_violation', () => {
    // 다시 읽어도 같다 — 쓰는 중이 아니라 **멈춘 채 깨졌다**. 영구 손상이다.
    assert.strictEqual(judgeLiveLedger(seq(TORN, TORN)).verdict, 'violated');
  });

  test('should_still_catch_a_broken_middle_line_while_the_tail_moves', () => {
    // 꼬리가 움직여도 접두부의 깨진 줄은 잡힌다 — 완화가 그것까지 삼키면 안 된다.
    const mid = ROW('D-2026-09-10-aaaaaa') + '{"id":"broken\n' + ROW('D-2026-09-10-bbbbbb');
    const j = judgeLiveLedger(seq(mid + '{"id":"D-2026-09-10-cc', mid + ROW('D-2026-09-10-cccccc')));
    assert.strictEqual(j.verdict, 'violated', JSON.stringify(j));
  });

  test('should_catch_a_broken_last_line_that_ends_with_a_newline', () => {
    // 개행으로 끝났다 = 작성기가 끝까지 썼다. 그런데 깨졌다면 쓰는 중이 아니다.
    assert.strictEqual(judgeLiveLedger(seq(WHOLE + '{"id":"broken"\n')).verdict, 'violated');
  });

  test('should_keep_the_ledger_path_in_the_live_failure_message', () => {
    // failingSet 은 ❌ 줄의 경로를 긁는다. 원장 경로가 빠지면 다음 진단이 흐려진다.
    const src = require('fs').readFileSync(__filename, 'utf8');
    const live = src.slice(src.indexOf('should_keep_live_ledger_parseable_and_schema_valid'));
    assert.ok(/judgeLiveLedger\(/.test(live.slice(0, 1200)), '실물 단언이 판정 함수를 쓰지 않는다');
    assert.ok(/원장 파싱 오류[^\n]*docs\/memory\/decisions\.jsonl/.test(live.slice(0, 1500)), '실패 메시지에 원장 경로가 없다');
  });
}

// ── N-06: 정착 · 와일드카드 · test: · 호출부 배선 ───────────────────
// 왜: 이 세 기능은 문법 표에는 있는데 코드에서 죽어 있었다. 와일드카드 grep 은 네 호출부
//   **전부**가 listFiles 를 주지 않아 무조건 insufficient 였고(원장에 와일드카드 스펙이
//   0건인 것은 건강 신호가 아니라 쓸 수 없었다는 신호다), test: 는 allowTest:true 에서도
//   insufficient 를 돌려줬으며, settled 행 21건은 evaluate 가 건너뛰지도 않았다.

test('settled 행의 스펙은 평가에서 빠진다 (N-06/FR-13)', () => {
  const rows = [
    { id: 'D-1', decision: 'a', reason: 'r', rejected: ['x'], invalidation: { specs: ['file:absent nope.txt'] } },
    { id: 'D-2', decision: 'b', reason: 'r', rejected: ['x'], invalidation: { specs: ['file:absent nope.txt'] }, settled: true },
  ];
  const io = { rows, readFile: () => null, exists: () => false, listFiles: () => [], git: () => null };
  const res = S.evaluate(io);
  assert.strictEqual(res.results.length, 1, '정착 행의 스펙이 여전히 평가된다: ' + JSON.stringify(res.results.map(r => r.rowId)));
  assert.strictEqual(res.results[0].rowId, 'D-1');
  assert.ok(res.skipped.some(s => s.rowId === 'D-2' && /settled|정착/.test(s.why)),
    'skipped 에 정착 사유가 없다: ' + JSON.stringify(res.skipped));
});

test('와일드카드 grep 은 listFiles 가 있으면 ok 다 (N-06/FR-10)', () => {
  const files = { 'a/x.js': 'foo\nbar\n', 'a/y.js': 'foo\n' };
  const rows = [{ id: 'D-1', decision: 'a', reason: 'r', rejected: ['x'], invalidation: { specs: ['grep:a/*.js /foo/ >=2'] } }];
  const io = {
    rows, readFile: p => files[p] || null, exists: p => p in files,
    listFiles: () => Object.keys(files), git: () => null,
  };
  const res = S.evaluate(io);
  assert.strictEqual(res.results[0].status, S.OK, JSON.stringify(res.results[0]));
  // listFiles 없으면 insufficient — 그것이 지금까지의 상태였다
  const res2 = S.evaluate({ ...io, listFiles: null });
  assert.strictEqual(res2.results[0].status, S.INSUFFICIENT, JSON.stringify(res2.results[0]));
});

test('test: 스펙이 lastEvidence 를 근거로 평가된다 (N-06/FR-19)', () => {
  const rows = [{ id: 'D-1', decision: 'a', reason: 'r', rejected: ['x'], invalidation: { specs: ['test:tests/unit/test_signals.js'] } }];
  const base = { rows, readFile: () => null, exists: () => true, listFiles: () => [], git: () => null };

  // complete 밖에서는 평가하지 않는다 (Stop 훅 5ms 예산)
  assert.strictEqual(S.evaluate(base).results[0].status, S.INSUFFICIENT);

  // 전체 실행 증거는 어떤 test: 스펙도 만족시킨다
  const full = S.evaluate({ ...base, allowTest: true, evidence: { scope: 'full', files: null, treeSig: 't' } });
  assert.strictEqual(full.results[0].status, S.OK, JSON.stringify(full.results[0]));

  // 좁힌 증거가 그 파일을 안 돌렸으면 위반이다
  const narrow = S.evaluate({
    ...base, allowTest: true,
    evidence: { scope: 'affected', files: ['tests/unit/test_other.js'], treeSig: 't' },
  });
  assert.strictEqual(narrow.results[0].status, S.VIOLATED, JSON.stringify(narrow.results[0]));

  // 증거 자체가 없으면 판정 불가 — 위반이 아니다
  const none = S.evaluate({ ...base, allowTest: true, evidence: null });
  assert.strictEqual(none.results[0].status, S.INSUFFICIENT, JSON.stringify(none.results[0]));
});

test('ledgerTail·countHumanAdded 가 정착 행에 흔들리지 않는다', () => {
  const rows = [{ id: 'D-1', source: 'user' }, { id: 'D-2', source: 'charter' }, { id: 'D-3', source: 'user', settled: true }];
  assert.strictEqual(S.countHumanAdded(rows, 0), 2);
  assert.strictEqual(S.ledgerTail(rows).lastId, 'D-3');
});

// ══ 8. 사이클 커밋 집합 — 선언은 관측을 이긴다 (v16/N-23) ══
// 2026-09-17 실측 사고: N-21 의 헤드라인 변경(`docs/memory/` 아래 선언된 scope 파일)이
//   사이클 커밋에서 조용히 빠져 **HEAD 가 자기 테스트에 실패하는 상태**가 됐다.
//   커밋 집합은 `touched ∪ artifacts` 였고, 그 파일은 어느 원천에도 속하지 못했다.
//   감사 로그는 경고를 냈지만 5개만 보여 주고 … 로 잘랐고, 노드의 핵심 파일이 그 잘린 자리에 있었다.
//
// 처방: **선언은 관측을 이긴다.** 활성 PRD 가 `scope_files` 로 선언한 파일은
//   하네스 영역에 있든 관측에 안 잡혔든 커밋 집합에 든다. 노드가 "내가 고치겠다" 고
//   적은 파일이 커밋에서 빠지는 것은 정상일 수 없다.
//
// 목록을 공유하는 방향도 시도했다가 되돌렸다 — `HARNESS_OWNED` 를 `_git.js` 로 옮기자
//   TE-21 과 원장 스펙 4건이 "그 상수는 advance-phase.js 에 있다" 를 못박고 있었고,
//   그것이 옷다. 커밋 유실은 목록 공유가 아니라 **선언이 관측을 이기게** 해서 고쳐야 한다.
{
  const G = require(path.resolve(__dirname, '../../.claude/hooks/_git.js'));

  test('선언된 scope 파일은 관측에 안 잡혔도 커밋 집합에 든다 (N-21 유실 재현)', () => {
    assert.strictEqual(typeof G.cycleCommitSet, 'function',
      '_git exports: ' + Object.keys(G).join(','));
    const r = G.cycleCommitSet({
      touched: ['.claude/hooks/session-gate.js'],
      touchedDocs: ['docs/prds/p.md'],
      artifacts: ['CHANGELOG.md'],
      scopeFiles: ['docs/memory/feedback-rules.json', '.claude/hooks/session-gate.js'],
      dirty: ['.claude/hooks/session-gate.js', 'docs/prds/p.md', 'CHANGELOG.md',
              'docs/memory/feedback-rules.json', 'docs/memory/audit-log.jsonl'],
    });
    assert.ok(r.want.has('docs/memory/feedback-rules.json'),
      '선언된 scope 파일이 커밋 집합에 없다 — N-21 사고가 그대로 재발한다');
    assert.deepStrictEqual(r.declaredMissing, [], JSON.stringify(r.declaredMissing));
  });

  test('대조군: 선언되지 않은 파일은 여전히 커밋 집합 밖이다', () => {
    if (typeof G.cycleCommitSet !== 'function') throw new Error('cycleCommitSet 없음');
    const r = G.cycleCommitSet({
      touched: [], touchedDocs: [], artifacts: [],
      scopeFiles: ['docs/memory/feedback-rules.json'],
      dirty: ['docs/memory/feedback-rules.json', 'docs/memory/observations.jsonl', 'docs/memory/verify.lock'],
    });
    assert.ok(!r.want.has('docs/memory/observations.jsonl'), '관측 로그가 커밋에 섞인다');
    assert.ok(!r.want.has('docs/memory/verify.lock'), '런타임 락이 커밋에 섞인다');
  });

  test('대조군: 선언됐는데 바뀜지 않은 파일은 declaredMissing 으로 보고된다', () => {
    if (typeof G.cycleCommitSet !== 'function') throw new Error('cycleCommitSet 없음');
    const r = G.cycleCommitSet({
      touched: [], touchedDocs: [], artifacts: [],
      scopeFiles: ['docs/memory/feedback-rules.json', '.claude/hooks/x.js'],
      dirty: ['.claude/hooks/x.js'],
    });
    assert.deepStrictEqual(r.declaredMissing, ['docs/memory/feedback-rules.json']);
    assert.ok(!r.want.has('docs/memory/feedback-rules.json'), '바뀌지도 않은 파일을 커밋에 넣는다');
  });

  test('대조군: 세 원천(touched·touchedDocs·artifacts)은 그대로 담긴다', () => {
    if (typeof G.cycleCommitSet !== 'function') throw new Error('cycleCommitSet 없음');
    const r = G.cycleCommitSet({
      touched: ['a.js'], touchedDocs: ['docs/reports/r.md'], artifacts: ['CHANGELOG.md'],
      scopeFiles: [], dirty: ['a.js', 'docs/reports/r.md', 'CHANGELOG.md'],
    });
    for (const p of ['a.js', 'docs/reports/r.md', 'CHANGELOG.md']) {
      assert.ok(r.want.has(p), `기존 원천이 빠졌다: ${p}`);
    }
  });
}

// ══ 9. 소유 판정 — 남의 결정이 내 사이클을 막지 않는다 (v16/N-23) ═════
// 2026-09-17 실측으로 이 창이 두 번 막혔다.
//   ① `GATES.complete` 의 Loop C 가 `SIG.evaluate()` 를 부르는데, evaluate 는 원장의
//      **살아 있는 행 전부**를 평가한다. ctx 에 사이클·봉투 개념이 아예 없다. 그래서
//      **다른 봉투의 결정이 발화하면 내 사이클이 닫히지 않았다.**
//   ② `supersedes` 검증이 보는 것은 "대상 id 가 존재하는가" 뿐이다. 작성 주체를 전혀
//      보지 않아, 이 창이 **다른 세션의 결정 행을 권한 없이 뒤집었다.**
//
// 경계를 어디에 긋는가:
//   - **charter 단위**로만 자른다. 같은 봉투의 다른 노드가 쓴 공용 인프라 결정은 내
//     사이클에도 마땅히 적용되어야 한다 — 노드 단위로 자르면 그것까지 걸러진다.
//   - **소유자 불명(charter 없음) 행은 계속 평가한다.** 원장 195행 중 66%가 그렇다.
//     모른다고 남의 것으로 접으면 "판정 불가는 거부" 원칙을 뒤집고 Loop C 를 무력화한다.
{
  const CY = { charter: { id: 'harness-v16-one-graph-one-screen', node: 'N-23' } };
  const failingSpec = 'grep:a.js /needle/ >= 1';   // a.js 에 needle 이 없으므로 위반
  const evalWith = (rows, cycle) => {
    const io = stubs({ files: { 'a.js': 'no-match-here\n' } });
    return S.evaluate({
      rows, auditLines: io.auditLines, now: io.now,
      readFile: io.readFile, exists: io.exists, listFiles: io.listFiles, git: io.git,
      cycle: cycle || undefined,
    });
  };

  test('다른 봉투 행의 위반은 내 사이클을 막지 않는다', () => {
    const r = row({ id: 'D-foreign', charter: 'harness-v99-other', node: 'N-05',
      invalidation: { text: 't', specs: [failingSpec] } });
    const res = evalWith([r], CY);
    assert.strictEqual(res.violations.length, 0,
      '남의 봉투 위반이 남아 있다: ' + JSON.stringify(res.violations));
    assert.ok((res.skipped || []).some(s => s.rowId === 'D-foreign' && /foreign/.test(s.why)),
      '건너뛴 사실이 사유와 함께 남지 않았다 — 조용히 넘기면 안 된다: ' + JSON.stringify(res.skipped));
  });

  test('대조군: 내 봉투 행의 위반은 여전히 막는다', () => {
    const r = row({ id: 'D-mine', charter: 'harness-v16-one-graph-one-screen', node: 'N-21',
      invalidation: { text: 't', specs: [failingSpec] } });
    const res = evalWith([r], CY);
    assert.strictEqual(res.violations.length, 1, '내 봉투 위반이 사라졌다 — 과하게 눈멀었다');
  });

  test('대조군: charter 없는 행은 계속 평가된다 (판정 불가는 거부)', () => {
    const r = row({ id: 'D-legacy', charter: null, node: null,
      invalidation: { text: 't', specs: [failingSpec] } });
    const res = evalWith([r], CY);
    assert.strictEqual(res.violations.length, 1,
      '소유자 불명 행을 자동으로 남의 것으로 접었다 — 원장 66%가 이 상태다');
  });

  test('대조군: cycle 을 안 주면 기존 동작 그대로 (하위호환)', () => {
    const r = row({ id: 'D-foreign', charter: 'harness-v99-other',
      invalidation: { text: 't', specs: [failingSpec] } });
    const res = evalWith([r], null);
    assert.strictEqual(res.violations.length, 1, 'cycle 없는 기존 호출부 4곳의 동작이 바뀌었다');
  });

  // ── supersedes 소유 ────────────────────────────────────────────────
  const CUT = '2026-09-17';
  const mk = o => row(Object.assign({ date: '2026-09-18' }, o));

  test('남의 원장 행을 말없이 뒤집을 수 없다', () => {
    assert.strictEqual(typeof S.assertSupersedeAllowed, 'function',
      '_signals exports: ' + Object.keys(S).join(','));
    const prev = mk({ id: 'D-a', charter: 'harness-v99-other', node: 'N-01', source: 'agent' });
    const next = mk({ id: 'D-b', supersedes: 'D-a', charter: 'harness-v16-one-graph-one-screen', node: 'N-23', source: 'agent' });
    const v = S.assertSupersedeAllowed(prev, next, { cutover: CUT });
    assert.strictEqual(v.ok, false, '남의 봉투 행이 그대로 뒤집혔다');
    assert.ok(/남의|다른/.test(String(v.reason)), v.reason);
  });

  test('대조군: 같은 charter+node 의 supersedes 는 통과한다', () => {
    if (typeof S.assertSupersedeAllowed !== 'function') throw new Error('없음');
    const prev = mk({ id: 'D-a', charter: 'harness-v16-one-graph-one-screen', node: 'N-23' });
    const next = mk({ id: 'D-b', supersedes: 'D-a', charter: 'harness-v16-one-graph-one-screen', node: 'N-23' });
    assert.strictEqual(S.assertSupersedeAllowed(prev, next, { cutover: CUT }).ok, true);
  });

  test('대조군: 사용자 명시 지시(source:user + source_ref)는 통과한다', () => {
    if (typeof S.assertSupersedeAllowed !== 'function') throw new Error('없음');
    const prev = mk({ id: 'D-a', charter: 'harness-v99-other', node: 'N-01' });
    const next = mk({ id: 'D-b', supersedes: 'D-a', source: 'user', source_ref: '사용자가 직접 되돌리라고 지시' });
    assert.strictEqual(S.assertSupersedeAllowed(prev, next, { cutover: CUT }).ok, true);
  });

  test('대조군: 대상 스펙이 전부 해석 불가면 문법 교정으로 통과한다', () => {
    if (typeof S.assertSupersedeAllowed !== 'function') throw new Error('없음');
    const prev = mk({ id: 'D-a', charter: 'harness-v99-other',
      invalidation: { text: 't', specs: ['grep-이건-문법이-아니다'] } });
    const next = mk({ id: 'D-b', supersedes: 'D-a' });
    assert.strictEqual(S.assertSupersedeAllowed(prev, next, { cutover: CUT }).ok, true);
  });

  test('대조군: 컷오버 이전 행은 거부가 아니라 경고다 (소급 금지)', () => {
    if (typeof S.assertSupersedeAllowed !== 'function') throw new Error('없음');
    const prev = row({ id: 'D-a', date: '2026-08-01', charter: 'harness-v99-other' });
    const next = mk({ id: 'D-b', supersedes: 'D-a', charter: 'harness-v16-one-graph-one-screen' });
    const v = S.assertSupersedeAllowed(prev, next, { cutover: CUT });
    assert.strictEqual(v.ok, true, '소유를 판정할 데이터가 없던 시기의 행까지 막았다');
    assert.ok(/legacy|컷오버|이전/.test(String(v.via || v.warn || '')), JSON.stringify(v));
  });

  // ── Loop A 교정 (2026-09-17 적대 검증) ──────────────────────────────
  //   적대 검증이 세 건을 잡았고 셋 다 실재했다. 여기서 못박는다.
  test('대조군: 아무도 소유를 주장하지 않는 행은 막지 않는다 (원장의 66%)', () => {
    // 처음 구현은 `p.charter && n.charter && 같음` 이라, 둘 다 null 인 **정당한 자기 수정**을
    // 거부했다. evaluate 는 "소유자 불명은 접지 않는다" 인데 supersede 만 정반대였다.
    const prev = mk({ id: 'D-a', charter: null, node: null });
    const next = mk({ id: 'D-b', supersedes: 'D-a', charter: null, node: null });
    const v = S.assertSupersedeAllowed(prev, next, { cutover: CUT });
    assert.strictEqual(v.ok, true, '주인 없는 행을 남의 것으로 취급했다: ' + JSON.stringify(v));
    assert.strictEqual(v.via, 'unowned-target', JSON.stringify(v));
  });

  test('대조군: 소유를 주장한 남의 행은 여전히 막는다 (과교정 방지)', () => {
    // 위 완화가 과하면 이것이 통과해 버린다 — 그러면 문을 도로 연 것이다.
    const prev = mk({ id: 'D-a', charter: 'harness-v99-other', node: 'N-01' });
    const next = mk({ id: 'D-b', supersedes: 'D-a', charter: null, node: null });
    assert.strictEqual(S.assertSupersedeAllowed(prev, next, { cutover: CUT }).ok, false,
      '남이 소유를 주장한 행이 뒤집혔다');
  });

  test('경계: 컷오버 당일 행은 면제되지 않는다 (엄격 미만 비교)', () => {
    const prev = row({ id: 'D-a', date: CUT, charter: 'harness-v99-other' });
    const next = mk({ id: 'D-b', supersedes: 'D-a', charter: 'harness-v16-one-graph-one-screen' });
    const v = S.assertSupersedeAllowed(prev, next, { cutover: CUT });
    assert.strictEqual(v.ok, false, '컷오버 당일이 legacy 로 새어 나갔다: ' + JSON.stringify(v));
  });
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
