// tests/unit/test_assert_counts.js
// 러너가 **몇 건을 검증했는지** 아는가 — charter harness-v4-quality / N-01 (FR-01·FR-03·FR-04)
//
// 왜 이 파일이 존재하는가:
//   `39/39 통과` 는 파일 개수였다. N-08 이 그것을 단언 수로 바꿨지만 **형식을 못 읽는
//   파일은 여전히 exit code 하나로만 판정**됐다. 실측(2026-09-08): 44개 중 10개가 그 상태였고
//   단언 195건이 집계 밖이었다. 그 10개 중 하나가 조용히 0단언으로 퇴화해도 러너는 ✅ 를
//   찍고 집계 숫자는 움직이지 않는다 — 원래 0이었으니까. 탐지 수단이 존재하지 않았다.
//
//   이것은 이 charter 가 세울 벽(N-03 반증 짝 · N-04 변이)의 **눈**이다.
//   측정이 거짓이면 강제도 거짓이다.
//
// 검증 방식: 임시 테스트 디렉터리를 만들어 러너를 실제로 돌린다. 소스 문자열 grep 이 아니라
//   **러너의 실제 판정**을 본다(N-08 이 지운 grep-only 테스트로 돌아가지 않는다).

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const RUNNER = path.join(REPO, 'tests', 'run.js');
const { makeReporter } = require(RUNNER);

const R = makeReporter('AC: 단언 계수');

// ── 임시 스위트 하나를 만들어 러너를 돌린다 ──────────────────────────
// 러너는 `tests/unit/` 를 훑으므로, 실 저장소를 건드리지 않으려면 저장소 사본이 필요하다.
// 훅과 달리 러너는 `__dirname` 기준으로 자기 unit 디렉터리를 읽으므로, tests/ 만 복사하면 된다.
function miniSuite(files) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'ac-'));
  fs.mkdirSync(path.join(dir, 'tests', 'unit'), { recursive: true });
  fs.mkdirSync(path.join(dir, 'docs', 'memory'), { recursive: true });
  fs.copyFileSync(RUNNER, path.join(dir, 'tests', 'run.js'));
  for (const [name, body] of Object.entries(files)) {
    fs.writeFileSync(path.join(dir, 'tests', 'unit', name), body);
  }
  return dir;
}
function runSuite(dir) {
  const r = spawnSync(process.execPath, [path.join(dir, 'tests', 'run.js'), '--no-sandbox'], {
    cwd: dir, encoding: 'utf8', timeout: 120000,
    env: Object.assign({}, process.env, { _HARNESS_TEST_RUN: 'ac-probe' }),
  });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || '') };
}
function cleanup(dir) { try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ } }

// 정규 형식으로 보고하는 파일
const GOOD = (n) => `
const { makeReporter } = require('../run.js');
const R = makeReporter('good');
for (let i = 0; i < ${n}; i++) R.check('c' + i, true);
R.done();
`;
// exit 0 인데 아무것도 보고하지 않는 파일 — 이 노드가 겨냥하는 바로 그 형태
const SILENT = `
console.log('  ✅ 뭔가 한 것처럼 보이는 줄');
process.exit(0);
`;
// 예전 소문자 형식 — parseCounts 가 못 읽는다
const LOWER = `
let passed = 3, failed = 0;
console.log('  ✅ a'); console.log('  ✅ b'); console.log('  ✅ c');
console.log(\`\\n══ 결과: \${passed} passed, \${failed} failed ══\`);
if (failed > 0) process.exit(1);
`;

// ── AC-01: makeReporter 가 정규 형식을 낸다 (FR-01) ──────────────────
console.log('\n[AC-01] should_emit_the_canonical_result_format');
{
  const dir = miniSuite({ 'test_good.js': GOOD(4) });
  try {
    const r = runSuite(dir);
    R.check('makeReporter 를 쓴 파일이 통과한다', r.code === 0, r.out.slice(-300));
    R.check('그 단언 수가 집계에 들어간다', /단언 4\b/.test(r.out),
      (r.out.match(/[^\n]*단언[^\n]*/g) || []).join(' | '));
    R.check('미보고로 세지 않는다', !/단언 미보고/.test(r.out),
      (r.out.match(/[^\n]*미보고[^\n]*/g) || []).join(' | '));
  } finally { cleanup(dir); }
}

// ── AC-02: 침묵은 통과가 아니다 (FR-03) ──────────────────────────────
// 이 단언이 이 파일의 존재 이유다. 지금은 실패해야 한다(IMPL-03 미구현).
console.log('\n[AC-02] should_fail_a_file_that_reports_nothing');
{
  const dir = miniSuite({ 'test_silent.js': SILENT });
  try {
    const r = runSuite(dir);
    R.check('보고하지 않는 파일이 있으면 러너가 실패한다', r.code !== 0,
      `code=${r.code} ` + r.out.slice(-300));
    R.check('실패 메시지가 makeReporter 를 안내한다', /makeReporter/.test(r.out),
      r.out.slice(-300));
  } finally { cleanup(dir); }
}

// ── AC-03: 옛 소문자 형식도 통과가 아니다 (FR-03) ────────────────────
// 형식 목록을 늘려 이것을 읽는 것이 아니라 — 계약은 "정규 형식으로 보고하라" 하나다.
console.log('\n[AC-03] should_not_accept_the_legacy_lowercase_format');
{
  const dir = miniSuite({ 'test_lower.js': LOWER });
  try {
    const r = runSuite(dir);
    R.check('소문자 형식 파일이 있으면 러너가 실패한다', r.code !== 0,
      `code=${r.code} ` + r.out.slice(-300));
  } finally { cleanup(dir); }
}

// ── AC-04: 단언 감소가 실패다 (FR-04) ────────────────────────────────
console.log('\n[AC-04] should_fail_when_assertion_count_drops');
{
  const dir = miniSuite({ 'test_good.js': GOOD(5) });
  try {
    const first = runSuite(dir);
    R.check('첫 실행이 기준선을 만든다', first.code === 0, first.out.slice(-200));
    const base = path.join(dir, 'tests', 'baseline', 'assert-counts.json');
    R.check('기준선 파일이 생긴다', fs.existsSync(base), base);
    if (fs.existsSync(base)) {
      let j = null; try { j = JSON.parse(fs.readFileSync(base, 'utf8')); } catch { /* 아래 단언이 잡는다 */ }
      R.check('기준선이 파일별 단언 수를 담는다',
        !!j && (j['test_good.js'] === 5 || (j.files && j.files['test_good.js'] === 5)),
        JSON.stringify(j));
    }

    // 단언을 하나 줄인다 — 이것이 '조용한 퇴화' 의 최소 재현이다
    fs.writeFileSync(path.join(dir, 'tests', 'unit', 'test_good.js'), GOOD(4));
    const second = runSuite(dir);
    R.check('단언이 줄면 러너가 실패한다', second.code !== 0,
      `code=${second.code} ` + second.out.slice(-300));
    R.check('무엇이 줄었는지 말한다', /test_good\.js/.test(second.out) && /5/.test(second.out),
      second.out.slice(-300));
  } finally { cleanup(dir); }
}

// ── AC-05: 감소를 인정하려면 이유가 필요하다 (FR-04) ─────────────────
console.log('\n[AC-05] should_require_a_reason_to_accept_a_drop');
{
  const dir = miniSuite({ 'test_good.js': GOOD(5) });
  try {
    runSuite(dir);
    fs.writeFileSync(path.join(dir, 'tests', 'unit', 'test_good.js'), GOOD(4));
    const noReason = spawnSync(process.execPath,
      [path.join(dir, 'tests', 'run.js'), '--no-sandbox', '--accept-drop'],
      { cwd: dir, encoding: 'utf8', timeout: 120000 });
    R.check('이유 없는 --accept-drop 은 통과하지 않는다', noReason.status !== 0,
      `code=${noReason.status} ` + ((noReason.stdout || '') + (noReason.stderr || '')).slice(-250));

    const withReason = spawnSync(process.execPath,
      [path.join(dir, 'tests', 'run.js'), '--no-sandbox', '--accept-drop', '--reason', '중복 단언 정리'],
      { cwd: dir, encoding: 'utf8', timeout: 120000 });
    const wOut = (withReason.stdout || '') + (withReason.stderr || '');
    R.check('이유가 있으면 통과한다', withReason.status === 0, `code=${withReason.status} ` + wOut.slice(-250));
    // 이유 문자열만 찾으면 **에러 메시지가 그것을 되울릴 때** 통과한다(실측으로 그랬다).
    //   통과했다는 사실과 인정 표지를 함께 요구한다 — 반증 가능하게.
    R.check('감소를 인정했다는 사실이 출력에 남는다',
      withReason.status === 0 && /감소 인정/.test(wOut) && /중복 단언 정리/.test(wOut),
      wOut.slice(-250));
  } finally { cleanup(dir); }
}

// ── AC-06: 실 저장소가 계약을 지킨다 (FR-02·FR-03) ───────────────────
console.log('\n[AC-06] should_hold_for_the_real_suite');
{
  const files = fs.readdirSync(path.join(REPO, 'tests', 'unit'))
    .filter(f => f.endsWith('.js') && !f.startsWith('_'));
  const legacy = files.filter(f => {
    const src = fs.readFileSync(path.join(REPO, 'tests', 'unit', f), 'utf8');
    return /결과:\s*\$\{[^}]*\}\s*passed/.test(src);
  });
  R.check('옛 소문자 형식을 쓰는 파일이 없다', legacy.length === 0, legacy.join(', '));
  R.check('러너가 makeReporter 를 export 한다', typeof makeReporter === 'function', typeof makeReporter);
}

R.done();
