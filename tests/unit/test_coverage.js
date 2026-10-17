// tests/unit/test_coverage.js
// 커버리지 수집·기준선·`cover:` 스펙 — charter harness-v4-quality / N-05
//
// 왜 이 파일이 존재하는가:
//   N-01 은 단언 수의 하락을, N-03 은 실패할 수 없는 테스트를 막는다.
//   그런데 **코드는 느는데 검증이 안 느는 것**을 보는 눈이 없었다 —
//   새 파일에 아무것도 재지 않는 테스트 하나를 붙이면 두 벽을 다 통과한다.
//   `pre-tool-gate` 규칙 9 가 v3.2 이후 커버리지 0 인 채 초록이었던 것이 그 형태다.
//
// 변환·비교는 **순수 함수**라 표로 단언한다. 파일도 env 도 읽지 않는다.

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { makeReporter } = require('../run.js');

const REPO = path.resolve(__dirname, '..', '..');
// 커버리지는 _signals 가 소유한다 — 스펙 평가의 io 경계가 거기 있고,
//   새 훅 파일을 만들면 build-package 의 손으로 쓴 INCLUDE 목록을 늘려야 한다.
const S = require(path.join(REPO, '.claude', 'hooks', '_signals.js'));
const C = S;

const R = makeReporter('CV: 커버리지');

// V8 산출물 한 항목을 만든다 — url 은 file:// 절대경로
const entry = (rel, ranges) => ({
  url: 'file:///' + path.join(REPO, rel).replace(/\\/g, '/').replace(/^\//, ''),
  functions: [{ functionName: '', ranges, isBlockCoverage: true }],
});

// ── CV-01: V8 산출물 → 파일별 비율 (FR-01) ───────────────────────────
console.log('\n[CV-01] should_convert_v8_output_to_per_file_ratios');
{
  // 전체 100바이트 중 0~100 이 1번 돌았다 → 100%
  const full = C.ratiosFromV8([entry('src/a.js', [{ startOffset: 0, endOffset: 100, count: 1 }])]);
  R.check('전부 돌면 100', full['src/a.js'] === 100, JSON.stringify(full));

  // 전체 100 중 안쪽 50~100 이 0번 → 50%
  const half = C.ratiosFromV8([entry('src/a.js', [
    { startOffset: 0, endOffset: 100, count: 1 },
    { startOffset: 50, endOffset: 100, count: 0 },
  ])]);
  R.check('절반만 돌면 50', half['src/a.js'] === 50, JSON.stringify(half));

  // 아무것도 안 돌면 0
  const none = C.ratiosFromV8([entry('src/a.js', [{ startOffset: 0, endOffset: 100, count: 0 }])]);
  R.check('아무것도 안 돌면 0', none['src/a.js'] === 0, JSON.stringify(none));

  // 안 돈 구간 안에 돈 구간이 있으면 그만큼 회복된다
  const nested = C.ratiosFromV8([entry('src/a.js', [
    { startOffset: 0, endOffset: 100, count: 1 },
    { startOffset: 0, endOffset: 100, count: 0 },
    { startOffset: 0, endOffset: 40, count: 1 },
  ])]);
  R.check('안 돈 구간 속의 돈 구간을 회복한다', nested['src/a.js'] === 40, JSON.stringify(nested));
}

// ── CV-02: 측정 대상이 아닌 것은 뺀다 (FR-01) ────────────────────────
console.log('\n[CV-02] should_exclude_non_targets');
{
  const r = C.ratiosFromV8([
    entry('tests/unit/test_x.js', [{ startOffset: 0, endOffset: 10, count: 1 }]),
    entry('node_modules/x/i.js', [{ startOffset: 0, endOffset: 10, count: 1 }]),
    entry('dist/x.js', [{ startOffset: 0, endOffset: 10, count: 1 }]),
    entry('.claude/hooks/_signals.js', [{ startOffset: 0, endOffset: 10, count: 1 }]),
  ]);
  R.check('테스트 파일은 대상이 아니다', !('tests/unit/test_x.js' in r), JSON.stringify(r));
  R.check('node_modules 는 대상이 아니다', !('node_modules/x/i.js' in r), JSON.stringify(r));
  R.check('dist 는 대상이 아니다', !('dist/x.js' in r), JSON.stringify(r));
  R.check('훅 소스는 대상이다', r['.claude/hooks/_signals.js'] === 100, JSON.stringify(r));

  // 저장소 밖은 무시한다
  const outside = C.ratiosFromV8([{ url: 'file:///tmp/elsewhere/x.js', functions: [{ ranges: [{ startOffset: 0, endOffset: 10, count: 1 }] }] }]);
  R.check('저장소 밖 파일은 무시한다', Object.keys(outside).length === 0, JSON.stringify(outside));
}

// ── CV-03: 기준선 — 자동 등록·상승, 하락은 판정 대상 (FR-02) ─────────
console.log('\n[CV-03] should_treat_baseline_as_an_observation_record');
{
  const base = { 'a.js': 80, 'b.js': 60 };
  const now = { 'a.js': 90, 'b.js': 40, 'c.js': 70 };
  const cmp = C.compareBaseline(base, now);

  R.check('신규 파일은 자동 등록된다', cmp.added.includes('c.js') && cmp.merged['c.js'] === 70, JSON.stringify(cmp));
  R.check('상승은 자동 반영된다', cmp.merged['a.js'] === 90, JSON.stringify(cmp.merged));
  R.check('하락이 잡힌다', cmp.drops.length === 1 && cmp.drops[0].file === 'b.js', JSON.stringify(cmp.drops));
  R.check('하락해도 기준선은 내려가지 않는다', cmp.merged['b.js'] === 60, JSON.stringify(cmp.merged));
  R.check('하락에 이전·현재 값이 붙는다',
    cmp.drops[0].from === 60 && cmp.drops[0].to === 40, JSON.stringify(cmp.drops[0]));

  // 기준선 파일 왕복
  const tmp = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'cvb-')), 'coverage.json');
  C.writeBaseline({ 'x.js': 55 }, tmp);
  R.check('기준선을 쓰고 다시 읽는다', C.readBaseline(tmp)['x.js'] === 55, JSON.stringify(C.readBaseline(tmp)));
  R.check('없는 기준선은 빈 객체다', Object.keys(C.readBaseline(tmp + '.none')).length === 0, '');
  try { fs.rmSync(path.dirname(tmp), { recursive: true, force: true }); } catch { /* 임시 */ }
}

// ── CV-04: `cover:` 스펙 문법 (FR-03) ────────────────────────────────
console.log('\n[CV-04] should_parse_and_evaluate_the_cover_spec');
{
  const p1 = S.parseSpec('cover:.claude/hooks/_signals.js nodrop');
  R.check('nodrop 형태를 파싱한다', p1.kind === 'cover' && p1.parsed && p1.parsed.mode === 'nodrop',
    JSON.stringify(p1));
  const p2 = S.parseSpec('cover:.claude/hooks/*.js >= 70');
  R.check('하한 형태를 파싱한다',
    p2.kind === 'cover' && p2.parsed && p2.parsed.mode === 'min' && p2.parsed.n === 70,
    JSON.stringify(p2));

  const evalWith = (spec, coverage, baseline) => S.evaluate({
    rows: [{ id: 'x', decision: 'd', reason: 'r', rejected: ['x'], invalidation: { text: 't', specs: [spec] } }],
    readFile: () => null, exists: () => true, listFiles: () => ['a.js', 'b.js'],
    git: () => null, auditLines: [], now: Date.parse('2026-09-08T00:00:00Z'),
    coverage, coverageBaseline: baseline,
  }).results[0];

  R.check('표본이 없으면 insufficient',
    evalWith('cover:a.js nodrop', null, null).status === 'insufficient',
    JSON.stringify(evalWith('cover:a.js nodrop', null, null)));
  R.check('하락하면 violated',
    evalWith('cover:a.js nodrop', { 'a.js': 40 }, { 'a.js': 60 }).status === 'violated',
    JSON.stringify(evalWith('cover:a.js nodrop', { 'a.js': 40 }, { 'a.js': 60 })));
  R.check('유지·상승이면 ok',
    evalWith('cover:a.js nodrop', { 'a.js': 60 }, { 'a.js': 60 }).status === 'ok', '');
  R.check('하한 미달이면 violated',
    evalWith('cover:a.js >= 70', { 'a.js': 50 }, null).status === 'violated', '');
  R.check('하한 충족이면 ok',
    evalWith('cover:a.js >= 70', { 'a.js': 90 }, null).status === 'ok', '');
  R.check('글롭 대상 — 하나라도 미달이면 violated',
    evalWith('cover:*.js >= 70', { 'a.js': 90, 'b.js': 10 }, null).status === 'violated', '');
}

// ── CV-05: 실제 수집이 동작한다 (FR-01) ──────────────────────────────
// 소스 문자열 검사가 아니라 **실제 V8 산출물**로 확인한다.
console.log('\n[CV-05] should_collect_real_coverage_without_external_deps');
{
  const { spawnSync } = require('child_process');
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'cov-'));
  try {
    const r = spawnSync(process.execPath,
      ['-e', "require('./.claude/hooks/_signals.js').parseSpec('file:exists x')"],
      { cwd: REPO, encoding: 'utf8', timeout: 60000,
        env: Object.assign({}, process.env, { NODE_V8_COVERAGE: dir }) });
    R.check('커버리지 실행이 성공한다', r.status === 0, (r.stderr || '').slice(0, 200));
    const ratios = C.ratiosFromDir(dir, { root: REPO });
    R.check('산출물에서 비율을 얻는다', Object.keys(ratios).length > 0, Object.keys(ratios).slice(0, 3).join(','));
    R.check('_signals.js 가 측정된다', typeof ratios['.claude/hooks/_signals.js'] === 'number',
      JSON.stringify(Object.keys(ratios).slice(0, 5)));
    const v = ratios['.claude/hooks/_signals.js'];
    R.check('비율이 0~100 범위다', v >= 0 && v <= 100, String(v));
  } finally { try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ } }
}

R.done();
