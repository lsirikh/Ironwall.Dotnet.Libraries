// tests/unit/test_bench.js
// 기준선 측정 파서의 계약 — charter harness-v13-lean-harness / N-01
//
// 왜 이 파일이 존재하는가:
//   개선안은 "성능은 같은 조건에서 전후 측정하라, 토큰 미제공은 미측정으로" 를 요구한다.
//   측정값이 틀리면 전후 비교가 거짓이 된다. 그래서 수를 세는 순수 함수를 먼저 못박는다.
//   특히 두 가지를 조심한다: ① 합성(<synthetic>) 행을 실제 모델로 세지 않는다
//   ② 값이 없으면 0 이 아니라 null(= 미측정) 이다 — 0 은 "재서 0 이었다" 는 뜻이다.
'use strict';

const assert = require('assert');
const path = require('path');

let pass = 0, fail = 0;
function test(name, fn) {
  try { fn(); pass++; console.log(`  ✅ ${name}`); }
  catch (e) { fail++; console.log(`  ❌ ${name}: ${e.message}`); }
}

let B = null;
try { B = require(path.resolve(__dirname, '../../scripts/bench/lib.js')); } catch (e) { B = null; }

console.log('\n═══ BENCH: 기준선 측정 파서 ═══\n');

test('should_expose_bench_lib', () => {
  assert.ok(B, 'scripts/bench/lib.js 를 불러올 수 없다');
  for (const k of ['percentile', 'summarize', 'parseJsonl', 'hookPerfByHook', 'verifyDurations', 'phaseIntervals',
    'transcriptStats', 'lastEffectiveModel', 'decisionTableShare', 'costWarning', 'slugForProject', 'formatMarkdown']) {
    assert.strictEqual(typeof B[k], 'function', `${k} export 없음`);
  }
});

if (!B) {
  console.log('\n  lib 이 없어 나머지를 돌릴 수 없습니다 (Red).');
  console.log(`\n  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계\n`);
  process.exit(1);
}

test('should_compute_nearest_rank_percentiles', () => {
  assert.strictEqual(B.percentile([5, 1, 3, 2, 4], 50), 3);
  assert.strictEqual(B.percentile([5, 1, 3, 2, 4], 95), 5);
  assert.strictEqual(B.percentile([], 50), null, '빈 표본은 null(미측정)');
  const s = B.summarize([10, 20, 30]);
  assert.deepStrictEqual({ n: s.n, p50: s.p50, max: s.max, sum: s.sum }, { n: 3, p50: 20, max: 30, sum: 60 });
  assert.deepStrictEqual(B.summarize([]), { n: 0, p50: null, p95: null, max: null, sum: 0 });
});

test('should_parse_jsonl_and_count_bad_lines', () => {
  const r = B.parseJsonl('{"a":1}\nnot json\n\n{"a":2}\n');
  assert.strictEqual(r.rows.length, 2);
  assert.strictEqual(r.bad, 1);
});

test('should_summarize_hook_perf_per_hook', () => {
  const rows = [
    { event: 'hook-perf', hook: 'pre-tool-gate.js', elapsed_ms: 40 },
    { event: 'hook-perf', hook: 'pre-tool-gate.js', elapsed_ms: 60 },
    { event: 'hook-perf', hook: 'session-gate.js', elapsed_ms: 200 },
    { event: 'hook-payload', hook_event: 'PostToolUse' },
  ];
  const h = B.hookPerfByHook(rows);
  assert.strictEqual(h['pre-tool-gate.js'].n, 2);
  assert.strictEqual(h['pre-tool-gate.js'].p95, 60);
  assert.strictEqual(h['session-gate.js'].p50, 200);
  assert.ok(!h['undefined'], 'hook 이 없는 행을 세지 않는다');
});

test('should_split_full_and_filtered_verify_durations', () => {
  const rows = [
    { event: 'loopv-pass', filter: null, durationMs: 280000 },
    { event: 'loopv-pass', filter: null, durationMs: 300000 },
    { event: 'loopv-pass', filter: 'test_a', durationMs: 9000 },
  ];
  const v = B.verifyDurations(rows);
  assert.strictEqual(v.full.n, 2);
  assert.strictEqual(v.full.max, 300000);
  assert.strictEqual(v.filtered.n, 1);
});

test('should_measure_phase_intervals_only_on_chained_transitions', () => {
  const rows = [
    { event: 'phase-transition', timestamp: '2026-09-15T00:00:00Z', from: 'prd', to: 'plan' },
    { event: 'phase-transition', timestamp: '2026-09-15T00:10:00Z', from: 'plan', to: 'dev' },
    { event: 'phase-transition', timestamp: '2026-09-15T01:10:00Z', from: 'dev', to: 'report' },
    { event: 'phase-transition', timestamp: '2026-09-15T02:00:00Z', from: 'analysis', to: 'prd' },
  ];
  const p = B.phaseIntervals(rows);
  assert.strictEqual(p.plan.p50, 600000, 'plan 에 머문 10분');
  assert.strictEqual(p.dev.p50, 3600000, 'dev 에 머문 60분');
  assert.ok(!p.report, '다음 전이가 report 에서 출발하지 않으면 report 체류는 재지 않는다');
});

// 전사본 조각 — Claude Code 내부 형식(버전 고정 픽스처)
const T = [
  { type: 'assistant', sessionId: 'S1', version: '2.1.248', message: { model: 'claude-opus-5', usage: { input_tokens: 10, cache_creation_input_tokens: 100, cache_read_input_tokens: 1000, output_tokens: 50 } } },
  { type: 'assistant', sessionId: 'S1', version: '2.1.248', message: { model: '<synthetic>', usage: { input_tokens: 0, output_tokens: 0 } } },
  { type: 'system', subtype: 'compact_boundary', sessionId: 'S1', compactMetadata: { trigger: 'auto', preTokens: 900000, postTokens: 15000 } },
  { type: 'assistant', sessionId: 'S1', version: '2.1.248', message: { model: 'claude-sonnet-5', usage: { input_tokens: 20, cache_creation_input_tokens: 0, cache_read_input_tokens: 2000, output_tokens: 70 } } },
  { type: 'user', sessionId: 'S1' },
];

test('should_count_models_without_synthetic_rows', () => {
  const s = B.transcriptStats(T);
  assert.strictEqual(s.assistantTurns, 2, '합성 행은 턴으로 세지 않는다');
  assert.strictEqual(s.synthetic, 1);
  assert.deepStrictEqual(s.models, { 'claude-opus-5': 1, 'claude-sonnet-5': 1 });
  assert.deepStrictEqual(s.versions, { '2.1.248': 3 });
});

test('should_sum_usage_and_per_turn_input', () => {
  const s = B.transcriptStats(T);
  assert.deepStrictEqual(s.usage, { input: 30, cacheWrite: 100, cacheRead: 3000, output: 120 });
  assert.strictEqual(s.perTurnInput.max, 2020, '턴 입력 = input + cacheWrite + cacheRead');
  assert.strictEqual(s.perTurnOutput.sum, 120);
});

test('should_judge_session_id_continuity_across_compaction', () => {
  const s = B.transcriptStats(T);
  assert.strictEqual(s.compactBoundaries, 1);
  assert.strictEqual(s.sessionIdStableAcrossCompact, true);
  const broken = T.map((x, i) => (i === 3 ? { ...x, sessionId: 'S2' } : x));
  assert.strictEqual(B.transcriptStats(broken).sessionIdStableAcrossCompact, false, '압축 뒤 id 가 바뀌면 false');
  const none = T.filter(x => x.type !== 'system');
  assert.strictEqual(B.transcriptStats(none).sessionIdStableAcrossCompact, null, '압축이 없으면 판정 불가(null)');
});

test('should_return_last_effective_model_skipping_synthetic', () => {
  assert.strictEqual(B.lastEffectiveModel(T), 'claude-sonnet-5');
  assert.strictEqual(B.lastEffectiveModel([T[1]]), null, '합성 행뿐이면 null(확인 불가)');
});

test('should_measure_decision_table_share', () => {
  const text = 'head\n<!-- decisions-start -->\n' + 'x'.repeat(90) + '\n<!-- decisions-end -->\ntail';
  const d = B.decisionTableShare(text);
  assert.strictEqual(d.total, Buffer.byteLength(text));
  assert.ok(d.tableBytes >= 90 && d.tableBytes < d.total, `tableBytes=${d.tableBytes}`);
  assert.ok(d.share > 0.5 && d.share < 1);
  assert.strictEqual(B.decisionTableShare('no markers').tableBytes, 0);
});

test('should_warn_before_paid_calls', () => {
  const w = B.costWarning(3);
  assert.ok(/유료/.test(w) && /3/.test(w), w);
});

test('should_derive_transcript_dir_slug_like_claude_code', () => {
  // 실측(N-01 탐침): 전사본 폴더는 세션을 연 cwd 의 드라이브 문자 대소문자를 그대로 쓴다.
  //   VSCode 세션은 `c--workspace-…`, PowerShell 에서 띄운 헤드리스는 `C--Users-…` 였다.
  assert.strictEqual(B.slugForProject('C:\\workspace_python\\skill-set'), 'C--workspace-python-skill-set');
  assert.strictEqual(B.slugForProject('c:\\workspace_python\\skill-set'), 'c--workspace-python-skill-set');
  assert.strictEqual(B.slugForProject('/home/u/my_proj'), '-home-u-my-proj');
  const c = B.transcriptDirCandidates('C:\\workspace_python\\skill-set');
  assert.ok(c.includes('C--workspace-python-skill-set') && c.includes('c--workspace-python-skill-set'), JSON.stringify(c));
  assert.deepStrictEqual(B.transcriptDirCandidates('/home/u/p'), ['-home-u-p'], '드라이브가 없으면 후보 하나');
});

test('should_not_count_the_same_folder_twice_on_case_insensitive_fs', () => {
  // 실측: Windows 에서 `C--…` 와 `c--…` 는 같은 폴더다 — 두 후보를 다 읽으면 턴이 정확히 두 배가 됐다
  assert.deepStrictEqual(B.uniquePaths(['C:\\a\\C--x', 'C:\\a\\c--x'], true), ['C:\\a\\C--x']);
  assert.deepStrictEqual(B.uniquePaths(['/a/C--x', '/a/c--x'], false), ['/a/C--x', '/a/c--x'], '대소문자 구분 FS 에서는 둘 다');
});

test('should_summarize_probe_results_without_guessing', () => {
  const P = require(path.resolve(__dirname, '../../scripts/bench/runtime-probe.js'));
  const ok = P.summarizeResult({ exit: 0, json: { is_error: false, modelUsage: { 'claude-haiku-4-5': {} }, total_cost_usd: 0.07, num_turns: 1 } });
  assert.deepStrictEqual(ok.models, ['claude-haiku-4-5']);
  assert.strictEqual(ok.costUsd, 0.07);
  const broken = P.summarizeResult({ exit: 1, json: null, stderr: 'boom' });
  assert.strictEqual(broken.models, null, 'JSON 이 없으면 모델은 null(확인 불가)');
  assert.strictEqual(broken.costUsd, null);
  assert.ok(/boom/.test(broken.error));
});

test('should_render_missing_transcripts_as_unmeasured', () => {
  const Bl = require(path.resolve(__dirname, '../../scripts/bench/baseline.js'));
  const md = Bl.render({
    measuredAt: '2026-09-15T00:00:00Z', head: 'abc',
    bytes: { 'CLAUDE.md': 100, decisionTable: { total: 0, tableBytes: 0, share: null } },
    banner: { ok: false, bytes: null, note: 'x' },
    audit: { files: 0, rows: 0, hooks: {}, verify: { full: B.summarize([]), filtered: B.summarize([]) }, phases: {} },
    transcripts: { note: '전사본 폴더 없음' },
  });
  assert.ok(/미측정 — 전사본 폴더 없음/.test(md), md);
});

test('should_render_unmeasured_as_label_not_zero', () => {
  const md = B.formatMarkdown({ rows: [{ label: '토큰', value: null }, { label: '훅 p95', value: 54 }] });
  assert.ok(/미측정/.test(md), md);
  assert.ok(/54/.test(md), md);
});

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계\n`);
process.exit(fail ? 1 : 0);
