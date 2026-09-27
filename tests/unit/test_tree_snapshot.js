// tests/unit/test_tree_snapshot.js
// 작업 트리 스냅샷 — charter harness-v3 / N-03 (FR-01·FR-04·FR-12 · D1·D2·D8)
//
// 왜 이 파일이 존재하는가:
//   관측 단위를 도구에서 **도구 호출 전후의 작업 트리 차이**로 바꾼다. 어떤 도구가 무엇을
//   쓰는지 알 필요가 없어지는 대신, 스냅샷이 실제 변경을 빠짐없이 잡아야 한다.
//
//   실측으로 기각한 대안이 둘 있다. 디렉터리 mtime 은 파일 내용 편집 후에도 부모 mtime 이
//   변하지 않아 인플레이스 쓰기(sed -i · >> · 스크립트)를 100% 놓친다 — 그것이 정확히
//   이 노드가 겨냥한 경로다. 전량 해시는 이 저장소에서는 빠르지만 2만 파일에서 p90 2.6초다.
//   그래서 status 로 후보를 좁히고 후보만 해시하는 하이브리드다.
//
//   무시 경로(상태 파일)는 git 이 원리적으로 못 본다 — 감시목록으로 직접 해시한다.

'use strict';

const { spawnSync } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');

const HOOKS = path.resolve(__dirname, '..', '..', '.claude', 'hooks');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).slice(0, 220) : ''}`); }
}

let G = null;
try { G = require(path.join(HOOKS, '_git.js')); } catch { /* 아래에서 */ }

console.log('\n═══ TS: 작업 트리 스냅샷 ═══');

console.log('\n[TS-00] should_expose_tree_snapshot');
check('_git 이 treeSnapshot 을 export 한다', !!(G && typeof G.treeSnapshot === 'function'),
  G ? 'exports: ' + Object.keys(G).join(',') : '_git 로드 실패');
check('_git 이 OBSERVE_EXCLUDE 를 export 한다', !!(G && G.OBSERVE_EXCLUDE));
if (!G || typeof G.treeSnapshot !== 'function') {
  console.log('\n  treeSnapshot 이 없어 나머지를 돌릴 수 없습니다 (Red).');
  console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
  process.exit(1);
}

function git(d, args) { return spawnSync('git', args, { cwd: d, encoding: 'utf8' }).stdout || ''; }
function repo() {
  const d = fs.mkdtempSync(path.join(os.tmpdir(), 'ts-'));
  fs.mkdirSync(path.join(d, 'src'), { recursive: true });
  fs.writeFileSync(path.join(d, 'src', 'a.js'), 'a\n');
  fs.writeFileSync(path.join(d, 'src', 'b.js'), 'b\n');
  fs.writeFileSync(path.join(d, '.gitignore'), 'runtime/\n');
  fs.mkdirSync(path.join(d, 'runtime'), { recursive: true });
  fs.writeFileSync(path.join(d, 'runtime', 'state.json'), '{"phase":"dev"}');
  git(d, ['init', '-q']); git(d, ['config', 'user.email', 't@t']); git(d, ['config', 'user.name', 't']);
  git(d, ['add', '-A']); git(d, ['commit', '-q', '-m', 'seed']);
  return d;
}
const WATCH = ['runtime/state.json'];
const snap = d => G.treeSnapshot({ cwd: d, watch: WATCH });
// 전후 차이 — 변경된 경로 집합
function diff(a, b) {
  const out = new Set();
  const all = new Set([...Object.keys(a.entries), ...Object.keys(b.entries), ...Object.keys(a.watch), ...Object.keys(b.watch)]);
  for (const p of all) {
    const x = a.entries[p] ? a.entries[p].sig : (a.watch[p] || null);
    const y = b.entries[p] ? b.entries[p].sig : (b.watch[p] || null);
    if (x !== y) out.add(p);
  }
  return out;
}

// ── TS-01: 추적 파일 수정 ──────────────────────────────────────────
console.log('\n[TS-01] should_detect_tracked_modification');
{
  const d = repo();
  const before = snap(d);
  fs.writeFileSync(path.join(d, 'src', 'a.js'), 'a changed\n');
  const after = snap(d);
  check('수정된 추적 파일이 잡힌다', diff(before, after).has('src/a.js'), [...diff(before, after)].join(','));
  check('건드리지 않은 파일은 안 잡힌다', !diff(before, after).has('src/b.js'));
  fs.rmSync(d, { recursive: true, force: true });
}

// ── TS-02: 이미 dirty 인 파일의 재편집 (charter 설계 (4)의 핵심) ──
//   status 코드는 ` M` → ` M` 으로 불변이다. 코드만 비교하면 이 변경을 못 본다.
console.log('\n[TS-02] should_detect_reedit_of_already_dirty_file');
{
  const d = repo();
  fs.writeFileSync(path.join(d, 'src', 'a.js'), 'first edit\n');
  const before = snap(d);
  fs.writeFileSync(path.join(d, 'src', 'a.js'), 'second edit\n');
  const after = snap(d);
  check('이미 dirty 인 파일의 재편집이 잡힌다', diff(before, after).has('src/a.js'), [...diff(before, after)].join(','));
  const same = snap(d);
  check('재편집하지 않으면 안 잡힌다', diff(after, same).size === 0, [...diff(after, same)].join(','));
  fs.rmSync(d, { recursive: true, force: true });
}

// ── TS-03: 미추적 신규 파일 ───────────────────────────────────────
console.log('\n[TS-03] should_detect_untracked_new_file');
{
  const d = repo();
  const before = snap(d);
  fs.writeFileSync(path.join(d, 'src', 'c.js'), 'c\n');
  const after = snap(d);
  check('신규 미추적 파일이 잡힌다', diff(before, after).has('src/c.js'), [...diff(before, after)].join(','));
  fs.rmSync(d, { recursive: true, force: true });
}

// ── TS-04: 삭제 ───────────────────────────────────────────────────
console.log('\n[TS-04] should_detect_deletion');
{
  const d = repo();
  const before = snap(d);
  fs.unlinkSync(path.join(d, 'src', 'b.js'));
  const after = snap(d);
  check('삭제된 파일이 잡힌다', diff(before, after).has('src/b.js'), [...diff(before, after)].join(','));
  fs.rmSync(d, { recursive: true, force: true });
}

// ── TS-05: 이름 변경 — 원본과 대상 둘 다 ─────────────────────────
//   statusEntries 는 새 경로만 남긴다. 원본을 잃으면 '무엇이 사라졌는지' 를 못 본다.
console.log('\n[TS-05] should_record_both_sides_of_rename');
{
  const d = repo();
  const before = snap(d);
  git(d, ['mv', 'src/b.js', 'src/renamed.js']);
  const after = snap(d);
  const ch = diff(before, after);
  check('이름 변경의 원본 경로가 잡힌다', ch.has('src/b.js'), [...ch].join(','));
  check('이름 변경의 대상 경로가 잡힌다', ch.has('src/renamed.js'), [...ch].join(','));
  fs.rmSync(d, { recursive: true, force: true });
}

// ── TS-06: mtime 만 바뀌고 내용은 같으면 변경이 아니다 ────────────
console.log('\n[TS-06] should_ignore_mtime_only_change_on_tracked_file');
{
  const d = repo();
  fs.writeFileSync(path.join(d, 'src', 'a.js'), 'dirty\n');
  const before = snap(d);
  const t = new Date(Date.now() + 60000);
  fs.utimesSync(path.join(d, 'src', 'a.js'), t, t);
  const after = snap(d);
  check('추적 파일의 mtime 만 바뀌면 변경이 아니다', !diff(before, after).has('src/a.js'), [...diff(before, after)].join(','));
  fs.rmSync(d, { recursive: true, force: true });
}

// ── TS-07: 무시 경로 감시목록 — git 이 못 보는 것 ────────────────
console.log('\n[TS-07] should_watch_ignored_paths_explicitly');
{
  const d = repo();
  const before = snap(d);
  check('감시 경로가 status 후보에는 없다', !before.entries['runtime/state.json']);
  check('감시 경로가 watch 에 있다', typeof before.watch['runtime/state.json'] === 'string', JSON.stringify(before.watch));
  fs.writeFileSync(path.join(d, 'runtime', 'state.json'), '{"phase":"test"}');
  const after = snap(d);
  check('무시 경로의 변경이 잡힌다', diff(before, after).has('runtime/state.json'), [...diff(before, after)].join(','));
  fs.rmSync(d, { recursive: true, force: true });
}

// ── TS-08: 예산 상한 — 강등하되 포기하지 않는다 (FR-04 · D8) ─────
console.log('\n[TS-08] should_degrade_not_abandon_over_budget');
{
  const d = repo();
  for (let i = 0; i < 30; i++) fs.writeFileSync(path.join(d, 'src', 'f' + i + '.js'), 'x'.repeat(100));
  const s = G.treeSnapshot({ cwd: d, watch: WATCH, maxCandidates: 5, maxBytes: 10 });
  check('강등 플래그가 선다', s.degraded === true, JSON.stringify(s.counts));
  check('강등돼도 후보는 남는다', Object.keys(s.entries).length > 0, String(Object.keys(s.entries).length));
  check('강등돼도 감시목록은 해시된다', typeof s.watch['runtime/state.json'] === 'string');
  check('강등 카운트가 보고된다', !!(s.counts && typeof s.counts.skipped === 'number'), JSON.stringify(s.counts));
  fs.rmSync(d, { recursive: true, force: true });
}

// ── TS-09: 관측 제외 목록 (FR-12 · D7) ───────────────────────────
//   이 목록이 한 항목이라도 빠지면 Track A 가 죽고 봉투가 자기잠금된다.
console.log('\n[TS-09] should_exclude_harness_runtime_paths');
{
  const ex = G.OBSERVE_EXCLUDE;
  const isEx = p => (typeof ex === 'function') ? ex(p) : ex.some(r => (r instanceof RegExp ? r.test(p) : p.startsWith(r)));
  for (const p of ['.claude/.branch-abc/pipeline-state.json', '.claude/install-logs/x.log',
    'docs/memory/sessions/s.json', '.claude/.pending-signal-violations.json',
    '.claude/settings.local.json', 'dist/_staging/x.js', 'C:/other/abs.js']) {
    check(`제외: ${p}`, isEx(p), 'not excluded');
  }
  for (const p of ['.claude/hooks/_git.js', 'src/a.js', 'docs/prds/x-prd.md', 'tests/unit/t.js']) {
    check(`제외 아님: ${p}`, !isEx(p), 'wrongly excluded');
  }
}

// ── TS-10: 어떤 상황에도 던지지 않는다 ───────────────────────────
console.log('\n[TS-10] should_never_throw');
{
  let threw = null;
  for (const opts of [undefined, {}, { cwd: '/nonexistent-path-xyz' }, { cwd: os.tmpdir() }]) {
    try {
      const s = G.treeSnapshot(opts);
      if (!s || typeof s.entries !== 'object') threw = 'bad shape: ' + JSON.stringify(s);
    } catch (e) { threw = e.message; }
  }
  check('git 저장소가 아니어도 예외 없음', threw === null, threw);
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
