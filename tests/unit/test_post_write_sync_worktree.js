// tests/unit/test_post_write_sync_worktree.js
// post-write-sync 의 계약을 **실행으로** 검증한다 — charter harness-v3 / N-08 (FR-09 · D9)
//
// 왜 다시 썼는가:
//   이 파일은 훅을 한 번도 실행하지 않고 소스 텍스트에 정규식 22개를 걸어 5개 테스트를
//   통과시키고 있었다. 실측으로 반증됐다 — **함수를 통째로 주석 처리해도 5건이 전부 초록**
//   이었다. `post-write-sync` 는 `session-context.md`·`INDEX.md`·`cycle.touched` 를 쓰는
//   훅인데, 그 잠금·경로검증 계약은 한 번도 실행 검증된 적이 없었다.
//
//   이제 샌드박스에서 훅을 실제로 구동하고 **결과 상태**를 단언한다.
//   함수를 지우거나 주석 처리하면 이 파일은 실패한다.

'use strict';

const fs = require('fs');
const path = require('path');
const SB = require('./_sandbox.js');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`  ✅ ${label}`); }
  else { fail++; console.log(`  ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 200) : ''}`); }
}

const write = (dir, rel, content) => {
  const abs = path.join(dir, rel);
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  fs.writeFileSync(abs, content);
  return abs;
};
const drive = (dir, rel, content) => SB.hook(dir, 'post-write-sync.js', {
  hook_event_name: 'PostToolUse', tool_name: 'Write', session_id: 'sPW',
  tool_input: { file_path: path.join(dir, rel), content },
});

console.log('\n═══ PW: post-write-sync 실행 계약 ═══');

// ── PW-01: 소스 쓰기가 cycle.touched 에 소유권과 함께 남는다 ──────────
console.log('\n[PW-01] should_record_source_write_with_ownership');
{
  const d = SB.makeSandbox({ prefix: 'pw1' });
  try {
    SB.cli(d, ['status']);
    SB.patchState(d, s => {
      s.phase = 'dev'; s.track = 'C';
      s.cycle = { startedAt: new Date().toISOString(), touched: [], touchedDocs: [], decisionsAtStart: 0, needsDecision: [] };
      return s;
    });
    write(d, 'src/b.js', '// b\n');
    const r = drive(d, 'src/b.js', '// b\n');
    check('훅이 정상 종료한다', r.code === 0, r.out);

    const st = SB.readState(d);
    const touched = (st.cycle || {}).touched || [];
    check('cycle.touched 에 기록된다', touched.some(t => String((t && t.path) || t).includes('src/b.js')),
      JSON.stringify(touched));
    const rec = touched.find(t => t && t.path && t.path.includes('src/b.js'));
    check('소유권 레코드가 붙는다', !!(rec && rec.by && rec.by.session_id === 'sPW'), JSON.stringify(rec));
    check('lastSourceWrite 가 갱신된다', !!(st.lastSourceWrite && /src\/b\.js/.test(st.lastSourceWrite.path)),
      JSON.stringify(st.lastSourceWrite));
    check('audit 에 tool-write 가 남는다', SB.auditRows(d).some(x => x.event === 'tool-write' && /src\/b\.js/.test(String(x.file))),
      JSON.stringify(SB.auditRows(d).map(x => x.event)));
  } catch (e) { check('PW-01', false, e.message); } finally { SB.cleanup(d); }
}

// ── PW-02: 산출물 문서는 touchedDocs 로 간다 (N-07 이 고친 것) ────────
console.log('\n[PW-02] should_route_cycle_output_docs_to_touched_docs');
{
  const d = SB.makeSandbox({ prefix: 'pw2' });
  try {
    SB.cli(d, ['status']);
    SB.patchState(d, s => {
      s.phase = 'dev'; s.track = 'C';
      s.cycle = { startedAt: new Date().toISOString(), touched: [], touchedDocs: [], decisionsAtStart: 0, needsDecision: [] };
      return s;
    });
    write(d, 'docs/reports/x-report.md', '# 보고\n');
    drive(d, 'docs/reports/x-report.md', '# 보고\n');
    const c = (SB.readState(d).cycle) || {};
    check('touchedDocs 에 들어간다', (c.touchedDocs || []).some(x => String(x).includes('x-report.md')),
      JSON.stringify(c.touchedDocs));
    check('touched(코드) 에는 안 들어간다',
      !(c.touched || []).some(t => String((t && t.path) || t).includes('x-report.md')),
      JSON.stringify(c.touched));

    // 산출물이 아닌 docs 는 어느 쪽에도 들어가지 않는다
    write(d, 'docs/memory/scratch.md', 'x\n');
    drive(d, 'docs/memory/scratch.md', 'x\n');
    const c2 = (SB.readState(d).cycle) || {};
    check('산출물이 아닌 docs 는 제외된다', !(c2.touchedDocs || []).some(x => String(x).includes('scratch.md')),
      JSON.stringify(c2.touchedDocs));
  } catch (e) { check('PW-02', false, e.message); } finally { SB.cleanup(d); }
}

// ── PW-03: INDEX.md 에 새 문서를 실제로 등재한다 ─────────────────────
// 훅은 INDEX.md 가 **이미 있을 때만** 갱신한다(없는 프로젝트에 파일을 만들지 않는다).
console.log('\n[PW-03] should_register_new_doc_in_index');
{
  const d = SB.makeSandbox({ prefix: 'pw3' });
  try {
    SB.cli(d, ['status']);
    // 섹션 표제는 훅이 찾는 문자열과 정확히 같아야 한다 — 그래야 행이 삽입된다.
    write(d, 'docs/INDEX.md',
      '# 문서 인덱스\n\n- **마지막 갱신**: 2026-01-01\n\n## 분석 (docs/analyses/)\n\n| 문서 | 설명 |\n|---|---|\n');
    write(d, 'docs/analyses/probe-analysis.md', '# 프로브 분석\n\n내용\n');
    drive(d, 'docs/analyses/probe-analysis.md', '# 프로브 분석\n\n내용\n');
    const idx = fs.readFileSync(path.join(d, 'docs', 'INDEX.md'), 'utf8');
    check('새 문서가 INDEX 에 실린다', /probe-analysis/.test(idx), idx.slice(0, 500));
    check('마지막 갱신 날짜가 바뀐다', !/2026-01-01/.test(idx), idx.slice(0, 200));
    check('INDEX 잠금이 남지 않는다', !fs.existsSync(path.join(d, 'docs', 'INDEX.md.lock')));
  } catch (e) { check('PW-03', false, e.message); } finally { SB.cleanup(d); }
}

// ── PW-04: 저장소 밖 경로는 흡수하지 않는다 ─────────────────────────
// 경로 검증이 죽으면 다른 저장소의 파일이 이 사이클의 touched 로 들어온다.
console.log('\n[PW-04] should_not_absorb_paths_outside_project');
{
  const d = SB.makeSandbox({ prefix: 'pw4' });
  const other = SB.makeSandbox({ prefix: 'pw4other' });
  try {
    SB.cli(d, ['status']);
    SB.patchState(d, s => {
      s.phase = 'dev'; s.track = 'C';
      s.cycle = { startedAt: new Date().toISOString(), touched: [], touchedDocs: [], decisionsAtStart: 0, needsDecision: [] };
      return s;
    });
    const foreign = path.join(other, 'src', 'a.js');
    const r = SB.hook(d, 'post-write-sync.js', {
      hook_event_name: 'PostToolUse', tool_name: 'Write', session_id: 'sPW',
      tool_input: { file_path: foreign, content: '// 남의 저장소\n' },
    });
    check('훅이 정상 종료한다', r.code === 0, r.out);
    const c = (SB.readState(d).cycle) || {};
    const all = JSON.stringify([...(c.touched || []), ...(c.touchedDocs || [])]);
    check('남의 저장소 경로가 흡수되지 않는다', !all.includes(other.replace(/\\/g, '/')) && !/pw4other/.test(all), all.slice(0, 300));
  } catch (e) { check('PW-04', false, e.message); } finally { SB.cleanup(d); SB.cleanup(other); }
}

// ── PW-05: 남의 이벤트로 불리면 아무것도 하지 않는다 ─────────────────
console.log('\n[PW-05] should_ignore_foreign_hook_event');
{
  const d = SB.makeSandbox({ prefix: 'pw5' });
  try {
    SB.cli(d, ['status']);
    SB.patchState(d, s => {
      s.phase = 'dev'; s.cycle = { startedAt: new Date().toISOString(), touched: [], touchedDocs: [], decisionsAtStart: 0, needsDecision: [] };
      return s;
    });
    write(d, 'src/c.js', '// c\n');
    const r = SB.hook(d, 'post-write-sync.js', {
      hook_event_name: 'SomeOtherEvent', tool_name: 'Write', session_id: 'sPW',
      tool_input: { file_path: path.join(d, 'src/c.js'), content: '// c\n' },
    });
    check('exit 0 으로 조용히 끝난다', r.code === 0, r.out);
    const c = (SB.readState(d).cycle) || {};
    check('touched 가 비어 있다', (c.touched || []).length === 0, JSON.stringify(c.touched));
  } catch (e) { check('PW-05', false, e.message); } finally { SB.cleanup(d); }
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
