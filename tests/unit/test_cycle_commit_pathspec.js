// tests/unit/test_cycle_commit_pathspec.js
// 사이클 커밋은 사이클의 경로만 커밋한다 — charter harness-v10-upgrade-integrity / N-03
//
// 왜 이 파일이 존재하는가:
//   complete 의 사이클 커밋은 `add -A -- <경로>` 뒤 `commit` 을 **경로 없이** 불렀다. 인덱스 전체가 커밋돼
//   미리 스테이징된 무관한 파일이 "cycle complete" 커밋에 들어갔고, 화면은 그 파일을 "흡수하지 않음" 이라 말했다
//   (v8 마감에서 릴리스 zip 으로 실측). 주석의 "의도 밖 파일을 흡수하지 않는다" 는 인덱스가 비어 있을 때만 참이었다.
//
// 무엇을 어떻게 보는가:
//   커밋 명령은 charter 설계대로 advance-phase 의 커밋 블록에 있다(`add -A -- <plan.add>` → `commit -m … -- <plan.commit>`).
//   `_git` 은 **무엇을 담을지**만 정한다 — cycleCommitPlan(스테이징된 이름 변경의 옛 경로 · 이미 인덱스에서 빠진 경로 ·
//   경로 밖 스테이징) · statusEntries(경로 풀기) · splitBumpedFiles(버전 올림이 실제로 고친 파일).
//   여기서는 임시 git 저장소에서 계획을 만들고 **advance-phase 와 같은 두 명령**을 돌려 git 의 실제 결과를 본다.
//   두 명령이 advance-phase 에 그대로 있는지는 CCP-09 가 소스로 확인한다.

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');
const G = require('../../.claude/hooks/_git');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 220) : ''}`); }
}

function git(dir, args) {
  const r = spawnSync('git', args, { cwd: dir, encoding: 'utf8' });
  if (r.status !== 0) throw new Error(`git ${args.join(' ')}: ${r.stderr}`);
  return (r.stdout || '').trim();
}
function repo(files) {
  const d = fs.mkdtempSync(path.join(os.tmpdir(), 'ccp-'));
  git(d, ['init', '-q']);
  git(d, ['config', 'user.email', 't@t']);
  git(d, ['config', 'user.name', 't']);
  git(d, ['config', 'core.autocrlf', 'false']);
  for (const [rel, content] of Object.entries(files)) fs.writeFileSync(path.join(d, rel), content);
  git(d, ['add', '-A']);
  git(d, ['commit', '-q', '-m', 'init']);
  return d;
}
const cleanup = d => { try { fs.rmSync(d, { recursive: true, force: true }); } catch { /* 임시 */ } };
const lines = s => String(s).split('\n').map(x => x.trim()).filter(Boolean);
const hasPlan = typeof G.cycleCommitPlan === 'function';

// advance-phase 커밋 블록과 **같은 두 명령**. add 목록이 비면 add 를 부르지 않는다 — 빈 pathspec 의 `add -A --` 는 트리 전체다.
function commitLikeAdvancePhase(d, paths, msg) {
  if (!hasPlan) throw new Error('cycleCommitPlan 이 없다');
  const plan = G.cycleCommitPlan(paths, { cwd: d });
  if (plan.add.length) git(d, ['add', '-A', '--', ...plan.add]);
  git(d, ['commit', '-q', '-m', msg, '--', ...plan.commit]);
  return plan;
}

console.log('\n═══ CCP: 사이클 커밋 경로 명시 (v10/N-03) ═══');

console.log('\n[CCP-01] should_leave_prestaged_files_outside_the_cycle_staged_and_out_of_the_commit');
{
  const d = repo({ 'a.txt': 'a\n', 'unrelated.txt': 'u\n' });
  try {
    fs.writeFileSync(path.join(d, 'a.txt'), 'a changed\n');          // 사이클이 고친 파일
    fs.writeFileSync(path.join(d, 'cycle.txt'), 'new in cycle\n');   // 사이클이 만든 파일
    fs.writeFileSync(path.join(d, 'unrelated.txt'), 'u changed\n');  // 사이클과 무관한 작업
    git(d, ['add', 'unrelated.txt']);                                 // 사용자가 미리 스테이징
    check('cycleCommitPlan 이 있다', hasPlan, typeof G.cycleCommitPlan);
    const plan = commitLikeAdvancePhase(d, ['a.txt', 'cycle.txt'], 'feat: cycle');
    const inCommit = lines(git(d, ['show', '--name-only', '--format=', 'HEAD']));
    check('사이클 경로가 커밋에 들어간다', inCommit.includes('a.txt') && inCommit.includes('cycle.txt'), inCommit.join(', '));
    check('미리 스테이징된 무관한 파일은 커밋에 없다', !inCommit.includes('unrelated.txt'), inCommit.join(', '));
    check('그 파일은 스테이징된 채 남는다', lines(git(d, ['diff', '--cached', '--name-only'])).includes('unrelated.txt'), git(d, ['status', '--short']));
    check('경로 밖 스테이징을 그 목록 그대로 돌려준다', JSON.stringify(plan.prestagedOutside) === JSON.stringify(['unrelated.txt']), JSON.stringify(plan));
  } catch (e) { check('CCP-01', false, e.message); } finally { cleanup(d); }
}

console.log('\n[CCP-02] should_still_commit_deletions_and_new_files');
{
  // -A 는 삭제를 담으려고 넣었다(v3.6/N-09) — 경로 명시 커밋이 그것을 되돌리면 안 된다
  const d = repo({ 'gone.txt': 'g\n', 'keep.txt': 'k\n' });
  try {
    fs.unlinkSync(path.join(d, 'gone.txt'));
    fs.writeFileSync(path.join(d, 'born.txt'), 'b\n');
    const plan = commitLikeAdvancePhase(d, ['gone.txt', 'born.txt'], 'feat: del+new');
    const st = lines(git(d, ['show', '--name-status', '--format=', 'HEAD']));
    check('삭제가 커밋된다', st.some(l => /^D\s+gone\.txt$/.test(l)), st.join(' | '));
    check('새 파일이 커밋된다', st.some(l => /^A\s+born\.txt$/.test(l)), st.join(' | '));
    check('작업 트리가 깨끗하다', git(d, ['status', '--porcelain']) === '', git(d, ['status', '--short']));
    check('경로 밖 스테이징 없음', plan.prestagedOutside.length === 0, JSON.stringify(plan));
  } catch (e) { check('CCP-02', false, e.message); } finally { cleanup(d); }
}

console.log('\n[CCP-03] should_not_count_a_prestaged_cycle_path_as_outside');
{
  // 사용자가 사이클 파일 자체를 미리 스테이징했다면 그것은 사이클 밖이 아니다
  const d = repo({ 'a.txt': 'a\n' });
  try {
    fs.writeFileSync(path.join(d, 'a.txt'), 'a changed\n');
    git(d, ['add', 'a.txt']);
    const plan = commitLikeAdvancePhase(d, ['a.txt'], 'feat: prestaged cycle path');
    check('사이클 경로는 경로 밖 목록에 없다', plan.prestagedOutside.length === 0, JSON.stringify(plan));
    // HEAD 가 이 커밋인지부터 본다 — 아니면 init 커밋의 a.txt 로 공허 통과한다
    check('그 경로가 이 커밋에 들어간다', git(d, ['log', '-1', '--format=%s']) === 'feat: prestaged cycle path' &&
      lines(git(d, ['show', '--name-only', '--format=', 'HEAD'])).includes('a.txt'), git(d, ['log', '-1', '--format=%s']));
  } catch (e) { check('CCP-03', false, e.message); } finally { cleanup(d); }
}

console.log('\n[CCP-04] should_keep_the_version_site_list_equal_to_the_changelog_table');
{
  // complete 가 스스로 고친 버전 사이트를 커밋에 담으려면 목록이 필요하다. changelog 모듈은 그 표를 export 하지 않아
  //   advance-phase 에 복제한다 — 두 벌이 갈라지면 여기서 잡는다. (표는 README.md 를 패턴 둘로 담는다 — 파일 단위로 비교)
  const ROOT = path.resolve(__dirname, '..', '..');
  const ap = fs.readFileSync(path.join(ROOT, '.claude', 'hooks', 'advance-phase.js'), 'utf8');
  const cl = fs.readFileSync(path.join(ROOT, '.claude', 'hooks', 'advance-phase-changelog.js'), 'utf8');
  const apList = (((ap.match(/const VERSION_SITE_FILES = \[([^\]]*)\]/) || [])[1] || '').match(/'[^']+'/g) || []);
  const clBlock = (cl.match(/const VERSION_SITES = \[([\s\S]*?)\n\];/) || [])[1] || '';
  const clList = (clBlock.match(/file: '[^']+'/g) || []).map(x => x.slice(6));
  const norm = a => Array.from(new Set(a.map(x => x.replace(/'/g, '')))).sort().join(',');
  check('changelog 표에서 파일 목록을 읽었다 (공허 아님)', clList.length >= 3, JSON.stringify(clList));
  check('advance-phase 의 버전 사이트 목록이 changelog 표와 같다', apList.length > 0 && norm(apList) === norm(clList),
    `advance-phase=${norm(apList) || '(없음)'} changelog=${norm(clList)}`);
}

// ══ Loop A 지적 반영 ═══════════════════════════════════════════════════════════
console.log('\n[CCP-05] should_commit_both_sides_of_a_staged_rename');
{
  // `git mv` 로 스테이징된 이름 변경. status 는 새 경로만 보이고, `commit -- <새 경로>` 는 옛 경로의 삭제를 남긴다(실측).
  //   그렇다고 옛 경로를 `add -A --` 에 주면 "pathspec did not match any files" 로 실패한다(실측) — commit 에만 담는다.
  const d = repo({ 'a.txt': 'a\n', 'keep.txt': 'k\n' });
  try {
    git(d, ['mv', 'a.txt', 'b.txt']);
    const plan = commitLikeAdvancePhase(d, ['b.txt'], 'feat: rename');
    check('commit 목록이 옛 경로를 담는다 · add 목록은 담지 않는다',
      plan.commit.includes('a.txt') && plan.commit.includes('b.txt') && !plan.add.includes('a.txt'), JSON.stringify(plan));
    check('커밋 뒤 인덱스·작업 트리에 남은 것이 없다 (옛 경로의 삭제가 남지 않는다)', git(d, ['status', '--porcelain']) === '', git(d, ['status', '--short']));
    const st = lines(git(d, ['show', '--name-status', '--format=', 'HEAD']));
    check('HEAD 에서 옛 경로가 사라졌다', st.some(l => /^R\d*\s+a\.txt\s+b\.txt$/.test(l)) || (st.some(l => /^D\s+a\.txt$/.test(l)) && st.some(l => /^A\s+b\.txt$/.test(l))), st.join(' | '));
    check('이름 변경의 옛 경로는 사이클 밖으로 세지 않는다', plan.prestagedOutside.length === 0, JSON.stringify(plan));
  } catch (e) { check('CCP-05', false, e.message); } finally { cleanup(d); }
}

console.log('\n[CCP-06] should_commit_a_non_ascii_path_with_spaces');
{
  // git status 는 기본 설정에서 비ASCII 경로를 "\353\254\270…" 로 이스케이프한다(실측). 풀지 않으면 add·commit 이 경로를 못 찾는다.
  const NAME = '문서 파일.txt';
  const d = repo({ [NAME]: 'k\n' });
  try {
    fs.appendFileSync(path.join(d, NAME), 'more\n');
    const entries = G.statusEntries(G.gitRun(['status', '--porcelain', '-uall'], d));
    check('statusEntries 가 원래 경로를 돌려준다', entries.some(e => e.path === NAME), JSON.stringify(entries));
    const p = (entries.find(e => e.path === NAME) || entries[0] || {}).path;
    commitLikeAdvancePhase(d, [p], 'feat: korean path');
    check('그 경로가 커밋된다 — 트리가 깨끗하고 HEAD 가 이 커밋이다',
      git(d, ['status', '--porcelain']) === '' && git(d, ['log', '-1', '--format=%s']) === 'feat: korean path', git(d, ['status', '--short']));
  } catch (e) { check('CCP-06', false, e.message); } finally { cleanup(d); }
}

console.log('\n[CCP-07] should_unquote_git_status_paths');
{
  const raw = ' M "\\353\\254\\270\\354\\204\\234 \\355\\214\\214\\354\\235\\274.txt"\n' +
    'R  "old name.txt" -> "new name.txt"\n' +
    '?? plain.txt\n' +
    ' M "tab\\there.txt"\n';
  const e = G.statusEntries(raw);
  check('8진 이스케이프를 UTF-8 로 푼다', !!e[0] && e[0].path === '문서 파일.txt', JSON.stringify(e[0]));
  check('이름 변경은 새 경로 · 옛 경로를 from 으로', !!e[1] && e[1].path === 'new name.txt' && e[1].from === 'old name.txt', JSON.stringify(e[1]));
  check('따옴표 없는 경로는 그대로', !!e[2] && e[2].path === 'plain.txt' && !('from' in e[2]), JSON.stringify(e[2]));
  check('\\t 같은 C 이스케이프도 푼다', !!e[3] && e[3].path === 'tab\there.txt', JSON.stringify(e[3]));
}

console.log('\n[CCP-08] should_split_version_sites_by_what_the_bump_actually_changed');
{
  // 버전 올림은 정규식이 안 맞거나 이미 같은 값이면 파일을 건드리지 않는다. "고쳤지만" 은 내용이 실제로 바뀐 파일에만 말한다.
  const has = typeof G.splitBumpedFiles === 'function';
  const before = {
    'README.md': { clean: true, content: 'v1' },          // 깨끗 + 바뀜 → 담는다
    'docs/Manual.md': { clean: false, content: 'v1+편집' }, // dirty + 바뀜 → 보류(이유)
    'install.js': { clean: false, content: '버전 없음' },   // dirty + 안 바뀜 → 둘 다 아님
    'x.md': { clean: true, content: 'same' },             // 깨끗 + 안 바뀜 → 둘 다 아님
  };
  const after = { 'README.md': 'v2', 'docs/Manual.md': 'v2+편집', 'install.js': '버전 없음', 'x.md': 'same' };
  const s = has ? G.splitBumpedFiles(before, after) : { absorb: null, held: null };
  check('splitBumpedFiles 가 있다', has, typeof G.splitBumpedFiles);
  check('깨끗했고 올림이 바꾼 파일만 담는다', JSON.stringify(s.absorb) === JSON.stringify(['README.md']), JSON.stringify(s));
  check('dirty 였고 올림이 바꾼 파일만 보류한다 — 안 바뀐 dirty 파일에 "고쳤지만" 을 말하지 않는다', JSON.stringify(s.held) === JSON.stringify(['docs/Manual.md']), JSON.stringify(s));
  const empty = has ? G.splitBumpedFiles(null, null) : null;
  check('기억이 없으면(git 없음 등) 아무것도 담지 않는다', !!empty && empty.absorb.length === 0 && empty.held.length === 0, JSON.stringify(empty));
}

console.log('\n[CCP-09] should_keep_the_commit_commands_in_advance_phase_and_the_recovery_hint_path_limited');
{
  // charter N-03 설계: 커밋 명령은 advance-phase 의 커밋 블록에 — add 와 commit 이 모두 경로를 명시한다.
  const ROOT = path.resolve(__dirname, '..', '..');
  const ap = fs.readFileSync(path.join(ROOT, '.claude', 'hooks', 'advance-phase.js'), 'utf8');
  const start = ap.indexOf("if (targetPhase === 'complete' && detectGit())");
  const block = start >= 0 ? ap.slice(start, start + 12000) : '';
  check('커밋 블록을 찾았다 (공허 아님)', block.length > 0, '');
  check('계획을 cycleCommitPlan 으로 만든다', /cycleCommitPlan\(toAdd\)/.test(block), '');
  check('add 는 계획의 add 목록으로 경로를 명시하고, 목록이 비면 부르지 않는다',
    /if \(plan\.add\.length\) gitRun\(\['add', '-A', '--', \.\.\.plan\.add\]\)/.test(block), '');
  check('commit 은 계획의 commit 목록으로 경로를 명시한다', /gitRun\(\['commit', '-m',[\s\S]{0,600}?'--', \.\.\.plan\.commit\]\)/.test(block), '');
  // 커밋 실패 안내가 경로 없는 명령을 권하면, 그대로 따른 사용자가 이 결함을 손으로 재현한다
  check('실패 안내가 경로 없는 git add -A && git commit 을 권하지 않는다', !/git add -A && git commit/.test(ap), (ap.match(/손으로 커밋[^\n]*/) || [''])[0]);
}

console.log('\n[CCP-10] should_commit_a_staged_deletion_without_adding_anything_else');
{
  // `git rm` 으로 이미 스테이징된 삭제 — 인덱스에 경로가 없어 `add -A -- <그 경로>` 는 실패한다(실측).
  //   add 목록이 비면 add 를 부르지 않아야 한다: 빈 pathspec 의 `add -A --` 는 추적 안 된 파일까지 전부 스테이징한다.
  const d = repo({ 'gone.txt': 'g\n', 'keep.txt': 'k\n' });
  try {
    git(d, ['rm', '-q', 'gone.txt']);
    fs.writeFileSync(path.join(d, 'extra.txt'), '사용자의 추적 안 된 파일\n');
    const plan = commitLikeAdvancePhase(d, ['gone.txt'], 'feat: staged delete');
    check('add 목록에 이미 빠진 경로가 없다', !plan.add.includes('gone.txt') && plan.commit.includes('gone.txt'), JSON.stringify(plan));
    const st = lines(git(d, ['show', '--name-status', '--format=', 'HEAD']));
    check('삭제가 커밋된다', git(d, ['log', '-1', '--format=%s']) === 'feat: staged delete' && st.some(l => /^D\s+gone\.txt$/.test(l)), st.join(' | '));
    check('추적 안 된 사용자 파일은 스테이징되지도 커밋되지도 않는다',
      !st.some(l => /extra\.txt/.test(l)) && /\?\? extra\.txt/.test(git(d, ['status', '--porcelain'])), git(d, ['status', '--short']));
  } catch (e) { check('CCP-10', false, e.message); } finally { cleanup(d); }
}

// ══ Loop A 재검 반영 ═══════════════════════════════════════════════════════════
console.log('\n[CCP-11] should_add_a_rename_source_that_was_recreated_on_disk');
{
  // `git mv a.txt b.txt` 뒤 옛 이름으로 새 파일을 만든다 — 리팩터링 뒤 같은 이름의 스텁을 남기는 흔한 모양.
  //   재검 리뷰는 "옛 경로가 add 목록에서 빠져 `commit -- a.txt` 가 실패한다" 고 짚었다. **실측으로 반증했다**:
  //   a.txt 는 HEAD 에 있어 commit 의 pathspec 이 맞고, `commit --only` 가 디스크의 새 a.txt 를 함께 담는다.
  //   그래서 계획은 바꾸지 않았다. 이 단언은 그 동작이 앞으로도 참인지 지키는 **회귀 방지**다(구현 세부는 단언하지 않는다).
  const d = repo({ 'a.txt': 'a\n' });
  try {
    git(d, ['mv', 'a.txt', 'b.txt']);
    fs.writeFileSync(path.join(d, 'a.txt'), 'new a\n');
    commitLikeAdvancePhase(d, ['a.txt', 'b.txt'], 'feat: recreated');
    check('커밋이 되고 트리가 깨끗하다', git(d, ['log', '-1', '--format=%s']) === 'feat: recreated' && git(d, ['status', '--porcelain']) === '', git(d, ['status', '--short']));
    check('HEAD 에 b.txt 가 있고 a.txt 는 새 내용이다', git(d, ['show', 'HEAD:b.txt']) === 'a' && git(d, ['show', 'HEAD:a.txt']) === 'new a', lines(git(d, ['show', '--name-status', '--format=', 'HEAD'])).join(' | '));
  } catch (e) { check('CCP-11', false, e.message); } finally { cleanup(d); }
}

console.log('\n[CCP-12] should_decode_non_ascii_paths_in_tree_snapshots');
{
  // Bash 로 만든·고친 파일은 treeSnapshot 이 관측해 cycle.touched 에 넣는다. 재검 리뷰는 "경로가 이스케이프된 채 남는다" 고
  //   짚었는데, 실측은 **더 나빴다**: 이스케이프된 경로 `\353\254…` 의 역슬래시가 관측 제외 판정에서 `/353/…` 로 바뀌어
  //   파일이 스냅샷에서 **통째로 빠졌다**(후보 1개 — ASCII 파일만). 비ASCII 파일은 Bash 관측에 보이지 않았다.
  const NAME = '문서 파일.txt';
  const d = repo({ [NAME]: 'k\n' });
  try {
    fs.appendFileSync(path.join(d, NAME), 'bash 로 고침\n');
    const snap = G.treeSnapshot({ cwd: d, watch: [] });
    const keys = Object.keys((snap && snap.entries) || {});
    check('treeSnapshot 이 변경을 관측했다 (공허 아님)', keys.length >= 1, JSON.stringify(snap && snap.entries));
    check('treeSnapshot 의 경로가 statusEntries 와 같은 원래 경로다', keys.includes(NAME), JSON.stringify(keys));
  } catch (e) { check('CCP-12', false, e.message); } finally { cleanup(d); }
}

console.log('\n[CCP-13] should_unquote_every_git_diff_name_list_in_advance_phase');
{
  // `git diff --name-only` 도 기본 설정에서 비ASCII 경로를 "\353…" 로 낸다(실측). 같은 뿌리 — 호출부마다 풀어야 한다.
  const ROOT = path.resolve(__dirname, '..', '..');
  const ap = fs.readFileSync(path.join(ROOT, '.claude', 'hooks', 'advance-phase.js'), 'utf8');
  const sites = ap.split('\n').filter(l => /'diff', '--name-only'/.test(l));
  check('git diff --name-only 호출을 찾았다 (공허 아님)', sites.length >= 4, `${sites.length}곳`);
  check('모든 호출이 경로를 unquoteGitPath 로 푼다', sites.length > 0 && sites.every(l => /unquoteGitPath/.test(l)),
    sites.filter(l => !/unquoteGitPath/.test(l)).map(l => l.trim().slice(0, 90)).join(' | '));
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
process.exit(fail > 0 ? 1 : 0);
