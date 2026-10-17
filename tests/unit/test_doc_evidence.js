// tests/unit/test_doc_evidence.js
// 문서의 ✅ 가 실재하는 테스트를 가리키는가 — charter harness-v3 / N-08 (FR-12·FR-13 · D13)
//
// 왜 이 파일이 존재하는가:
//   `docs/tests/*.md` 의 완료 기준 표는 **검증 기록이 아니라 서사**였다.
//   N-04 의 12개 DoD 중 7개가 테스트 id 없이 ✅ 로 닫혔고(그중 D5 `allow-git` 은 게이트
//   우회로인데 회귀 테스트가 0건이었다), 문서 23개 전반에 같은 유형이 16건 있었다 —
//   저장소에 없는 파일을 근거로 든 것, "통과 예정" 을 통과로 계상한 것,
//   그리고 문서는 "차단 ✅" 인데 테스트는 `allowed === true` 를 단언하던 것까지.
//
//   기능을 지워도 문서는 계속 통과를 주장했다. **자동 대조가 없었기 때문**이다.
//
// 왜 baseline 인가:
//   기존 위반을 실패로 만들면 이 테스트는 첫날부터 빨갛고, 빨간 테스트는 꺼진다.
//   정정은 N-11(문서 동기화)의 일이다. 그래서 **기존 위반은 기록된 목록으로 세고
//   새 위반만 실패시킨다** — 미래를 고치되 과거에 대해 거짓말하지 않는다.
//   목록이 줄어드는 것이 N-11 의 진척이고, 이 테스트가 그것을 센다.

'use strict';

const fs = require('fs');
const path = require('path');

const REPO = path.resolve(__dirname, '..', '..');
const DOCS_TESTS = path.join(REPO, 'docs', 'tests');
const UNIT_DIR = path.join(REPO, 'tests', 'unit');
const BASELINE = path.join(REPO, 'tests', 'baseline', 'doc-evidence.json');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`  ✅ ${label}`); }
  else { fail++; console.log(`  ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 400) : ''}`); }
}

// ── 테스트 스위트에 실재하는 케이스 id 를 모은다 ─────────────────────
// 관례는 두 가지다: `console.log('[GG-01] should_…')` 와 `test('UT-31: …')`.
function collectTestIds() {
  const ids = new Set();
  let files = [];
  try { files = fs.readdirSync(UNIT_DIR).filter(f => f.endsWith('.js')); } catch { return ids; }
  for (const f of files) {
    let src = '';
    try { src = fs.readFileSync(path.join(UNIT_DIR, f), 'utf8'); } catch { continue; }
    for (const m of src.matchAll(/\[([A-Z]{2,4}-\d{1,3})\]/g)) ids.add(m[1]);
    for (const m of src.matchAll(/^\s*test\(\s*['"`]([A-Z]{2,4}-\d{1,3})[:\s]/gm)) ids.add(m[1]);
    ids.add('FILE:' + f);
  }
  return ids;
}

// ── 문서의 DoD 표에서 근거 셀을 뽑는다 ───────────────────────────────
// `| D8 | 기준 | ✅ 감사 한 줄 |` 형태. 마지막 칸이 근거다.
const ID_RE = /\b([A-Z]{2,4}-\d{1,3})\b/g;
const PLAN_TASK_RE = /^(IMPL|TEST|DOC|RISK)-\d+$/;
function scanDoc(rel, ids) {
  const violations = [];
  let text = '';
  try { text = fs.readFileSync(path.join(REPO, rel), 'utf8'); } catch { return violations; }
  for (const line of text.split('\n')) {
    // DoD 행: `| D<n> | … | … |`
    const m = line.match(/^\|\s*(D\d{1,2})\s*\|(.*)\|\s*$/);
    if (!m) continue;
    const cells = m[2].split('|').map(c => c.trim());
    const evidence = cells[cells.length - 1] || '';
    if (!/[✅✔]/.test(evidence) && !/통과|PASS/i.test(evidence)) continue;   // 미완 항목은 대상이 아니다

    if (/통과 예정|예정|TODO/.test(evidence)) {
      violations.push({ doc: rel, dod: m[1], why: 'future-tense', cell: evidence.slice(0, 60) });
      continue;
    }
    const cited = [...evidence.matchAll(ID_RE)].map(x => x[1]);
    const files = [...evidence.matchAll(/([\w.-]+\.js)/g)].map(x => x[1]);
    const planOnly = cited.length > 0 && cited.every(c => PLAN_TASK_RE.test(c));

    if (!cited.length && !files.length) {
      violations.push({ doc: rel, dod: m[1], why: 'no-citation', cell: evidence.slice(0, 60) });
      continue;
    }
    if (planOnly && !files.length) {
      violations.push({ doc: rel, dod: m[1], why: 'plan-task-id-only', cell: evidence.slice(0, 60) });
      continue;
    }
    const missingIds = cited.filter(c => !PLAN_TASK_RE.test(c) && !ids.has(c));
    const missingFiles = files.filter(f => !ids.has('FILE:' + f));
    if (missingIds.length || missingFiles.length) {
      violations.push({
        doc: rel, dod: m[1], why: 'citation-not-found',
        cell: evidence.slice(0, 60), missing: [...missingIds, ...missingFiles],
      });
    }
  }
  return violations;
}

console.log('\n═══ DE: 문서 ✅ 와 테스트 실재 대조 ═══\n');

const ids = collectTestIds();
check('테스트 스위트에서 케이스 id 를 수집한다', ids.size > 50, `수집 ${ids.size}개`);

let docs = [];
try { docs = fs.readdirSync(DOCS_TESTS).filter(f => f.endsWith('.md')).map(f => 'docs/tests/' + f); } catch { /* 없으면 0건 */ }
check('docs/tests 문서를 읽는다', docs.length > 0, `문서 ${docs.length}개`);

const all = [];
for (const d of docs) all.push(...scanDoc(d, ids));

// ── baseline 대조 ────────────────────────────────────────────────────
let baseline = { known: [], note: '' };
try { baseline = JSON.parse(fs.readFileSync(BASELINE, 'utf8')); } catch { /* 첫 실행 */ }
const key = v => `${v.doc}#${v.dod}`;
const known = new Set((baseline.known || []).map(String));
const fresh = all.filter(v => !known.has(key(v)));
const fixed = [...known].filter(k => !all.some(v => key(v) === k));

console.log(`  기존 위반 ${known.size}건 · 현재 위반 ${all.length}건 · 신규 ${fresh.length}건 · 정정됨 ${fixed.length}건`);
for (const v of all.slice(0, 6)) console.log(`     · ${v.doc}#${v.dod} — ${v.why}${v.missing ? ' (' + v.missing.join(', ') + ')' : ''}`);
if (all.length > 6) console.log(`     · … +${all.length - 6}건`);

check('새 위반이 없다 — 문서의 ✅ 는 실재하는 테스트를 가리켜야 한다', fresh.length === 0,
  fresh.map(v => `${v.doc}#${v.dod}(${v.why})`).join(' / '));

// 정정된 것은 baseline 에서 빼라고 알린다 — 목록이 면죄부가 되지 않게
if (fixed.length) {
  console.log(`  ℹ baseline 에서 뺄 수 있는 항목 ${fixed.length}건: ${fixed.slice(0, 4).join(', ')}`);
}

// N-05 이후 문서는 위반이 없어야 한다 — 이 하네스가 스스로 지킨 구간이다
const recent = all.filter(v => /harness-v3-n0[5-9]|harness-v3-n1\d/.test(v.doc));
check('N-05 이후 문서에는 위반이 없다', recent.length === 0,
  recent.map(v => `${v.doc}#${v.dod}(${v.why})`).join(' / '));

// ── DE-10: 문서만 만드는 태스크는 코드 증거를 요구받지 않는다 (v5/N-03) ──
//   `verify` 는 `test_command` 가 비면 skip 하고 **skip 은 증거를 발급하지 않는다.**
//   그래서 산출물이 문서뿐인 노드에서는 증거 게이트가 **영원히 열리지 않았다**
//   (다운스트림 sso-suite 가 스펙 집필 노드에서 보고).
//
//   여기서 두 방향을 같은 블록에서 지킨다 —
//     ① 문서 전용 태스크는 `--no-test` 없이 닫힌다
//     ② 코드 태스크는 **여전히** 증거 없이 닫히지 않는다
//   완화가 ②까지 삼키면 이 하네스의 벽이 통째로 사라진다.
console.log('\n[DE-10] should_not_require_code_evidence_for_doc_only_tasks');
{
  const SB = require('./_sandbox.js');
  const d = SB.makeSandbox({ prefix: 'de10' });
  try {
    SB.cli(d, ['status']);
    const at = new Date().toISOString();
    fs.writeFileSync(path.join(d, 'docs', 'prds', 'a-prd.md'),
      '# PRD — 스펙 집필\n\n- **상태**: Approved\n');
    fs.writeFileSync(path.join(d, 'docs', 'plans', 'a-prd-plan.md'), [
      '# plan',
      '',
      '- [ ] **[IMPL-01]** 스펙 절 집필',
      '  - 파일: `docs/specs/protocol.md`',
      '  - 예상 공수: 1h',
      '',
      '- [ ] **[IMPL-02]** 코드 구현',
      '  - 파일: `src/a.js`',
      '  - 예상 공수: 1h',
      '',
    ].join('\n'));
    fs.appendFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'),
      JSON.stringify({ timestamp: at, event: 'user-approval', target: 'prd', file: 'docs/prds/a-prd.md' }) + '\n');
    SB.git(d, ['add', '-A']); SB.git(d, ['commit', '-q', '-m', 'seed']);
    const base = SB.git(d, ['rev-parse', '--short', 'HEAD']).trim();
    SB.patchState(d, s => {
      s.phase = 'dev'; s.track = 'C';
      s.activePrd = 'docs/prds/a-prd.md'; s.activePlan = 'docs/plans/a-prd-plan.md';
      s.lastApproval = { prd: 'docs/prds/a-prd.md', at, via: 'user' };
      s.cycle = {
        startedAt: new Date(Date.now() - 3600e3).toISOString(), headAtStart: base,
        touched: [], decisionsAtStart: 0, needsDecision: [], dirtyAtStart: [],
      };
      return s;
    });

    // ① 문서 전용 — `--no-test` 없이 닫힌다
    const rDoc = SB.cli(d, ['task', 'IMPL-01', 'done']);
    check('문서 전용 태스크가 증거 없이 닫힌다', rDoc.code === 0, rDoc.out.slice(-400));
    check('판정 사실이 화면에 남는다', /문서/.test(rDoc.out), rDoc.out.slice(-300));

    // ② 코드 태스크 — 여전히 막힌다. 완화가 여기까지 오면 벽이 사라진 것이다.
    const rCode = SB.cli(d, ['task', 'IMPL-02', 'done']);
    check('코드 태스크는 여전히 증거 없이 닫히지 않는다', rCode.code !== 0, rCode.out.slice(-400));
    check('거부가 증거 또는 반증을 가리킨다', /증거|Red|반증/.test(rCode.out), rCode.out.slice(-300));
  } finally { SB.cleanup(d); }
}


console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
