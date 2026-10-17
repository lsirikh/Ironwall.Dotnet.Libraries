// tests/unit/test_cycle_checkpoint.js
// 새 사이클의 롤백 지점은 그 사이클의 시작이다 — charter harness-v11-cycle-truth / N-01
//
// 왜 이 파일이 존재하는가:
//   `newCycle()` 이 이전 사이클의 checkpointTag 를 복사했고, `new-cycle` 과 `complete` 뒤의 phase 전이는 태그를 만들지 않았다.
//   그래서 트랙을 바꾸지 않는 연속 노드(charter 실행의 기본 경로)에서 `status` 가 **이전 사이클 시작의 태그**로
//   `git reset --hard` 를 안내했다 — 그대로 치면 방금 끝낸 사이클의 커밋이 사라진다(v10/N-02 실사고).
//
// 무엇을 어떻게 보는가:
//   샌드박스 git 저장소에서 CLI 를 실제로 부르고, 태그가 **어느 커밋**을 가리키는지 git 에 묻는다.
//   롤백 명령은 사람이 그대로 치는 출력이라 출력 문자열도 본다.
//   CK-04 는 오탐 대조다 — 사이클 중 `checkpoint` 로 태그를 앞당기면 태그와 사이클 시작은 **다르지만 정상**이다.
//
//   "이전 사이클의 태그" 는 손으로 만든다(`checkpoint/c-old`). 태그 이름의 시각이 초 단위라,
//   같은 초에 `set-track` 과 `new-cycle` 이 각자 태그를 만들면 이름이 겹쳐 두 번째가 실패한다.

'use strict';

const fs = require('fs');
const path = require('path');
const { makeSandbox, cli, git, readState, patchState, cleanup } = require('./_sandbox');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 260) : ''}`); }
}

const OLD = 'checkpoint/c-old';

function commitFile(d, rel, content, msg) {
  fs.mkdirSync(path.dirname(path.join(d, rel)), { recursive: true });
  fs.writeFileSync(path.join(d, rel), content);
  git(d, ['add', '--', rel]);                 // 샌드박스의 상태 파일까지 담지 않는다
  git(d, ['commit', '-q', '-m', msg]);
  return git(d, ['rev-parse', '--short', 'HEAD']);
}
function tagCommit(d, tag) {
  try { return git(d, ['rev-parse', '--short', `${tag}^{commit}`]); } catch { return null; }
}

// 트랙 C 사이클 하나를 시작하고, 그 사이클의 롤백 태그를 첫 커밋의 `checkpoint/c-old` 로 둔다.
function startedCycle(prefix) {
  const d = makeSandbox({ prefix });
  const r = cli(d, ['set-track', 'C']);
  if (r.code !== 0) throw new Error('set-track C 실패: ' + r.out);
  for (const t of git(d, ['tag', '-l', 'checkpoint/*']).split('\n').filter(Boolean)) git(d, ['tag', '-d', t]);
  git(d, ['tag', '-a', OLD, '-m', 'cycle 1 start']);
  const first = git(d, ['rev-parse', '--short', 'HEAD']);
  patchState(d, s => { s.cycle = s.cycle || {}; s.cycle.checkpointTag = OLD; s.cycle.headAtStart = first; return s; });
  return { d, first };
}
// 사이클 1 을 끝낸 상태 — 작업 커밋이 태그 뒤에 있고 phase 는 complete.
function completedCycle(prefix) {
  const c = startedCycle(prefix);
  c.done = commitFile(c.d, 'src/cycle1.js', '// cycle 1\n', 'cycle 1 complete');
  patchState(c.d, s => { s.phase = 'complete'; return s; });
  return c;
}

function expectNewCycleTagged(label, c, r) {
  check(`${label} 성공`, r.code === 0, r.out);
  const st = readState(c.d) || {};
  const tag = st.cycle && st.cycle.checkpointTag;
  check('새 사이클의 롤백 태그는 이전 사이클의 것이 아니다', !!tag && tag !== OLD, `checkpointTag=${tag}`);
  check('새 태그는 새 HEAD(사이클 1 완료 커밋)를 가리킨다', !!tag && tagCommit(c.d, tag) === c.done,
    `${tag} @ ${tag && tagCommit(c.d, tag)} · HEAD=${c.done} · c-old @ ${c.first}`);
  const s = cli(c.d, ['status']);
  check('status 의 롤백 줄이 새 태그다', !!tag && tag !== OLD && s.out.includes(`rollback:   git reset --hard ${tag}`), s.out);
  check('status 가 이전 사이클의 태그로 reset 을 안내하지 않는다', !s.out.includes(`git reset --hard ${OLD}`), s.out);
}

console.log('\n[CK-01] should_tag_new_head_when_new_cycle_follows_completed_cycle');
{
  let c;
  try {
    c = completedCycle('ck01');
    expectNewCycleTagged('new-cycle', c, cli(c.d, ['new-cycle', 'next-topic']));
  } catch (e) { check('CK-01', false, e.message); } finally { if (c) cleanup(c.d); }
}

console.log('\n[CK-02] should_tag_new_head_when_phase_transition_leaves_complete');
{
  let c;
  try {
    c = completedCycle('ck02');
    expectNewCycleTagged('complete → analysis 전이', c, cli(c.d, ['analysis']));
  } catch (e) { check('CK-02', false, e.message); } finally { if (c) cleanup(c.d); }
}

console.log('\n[CK-03] should_refuse_reset_advice_when_tag_predates_cycle_start');
{
  let c;
  try {
    c = startedCycle('ck03');
    // 물려받은 옛 상태: 사이클은 뒤 커밋에서 시작했는데 태그는 그보다 앞에 있다
    const later = commitFile(c.d, 'src/prev.js', '// 이전 사이클의 작업\n', 'previous cycle complete');
    patchState(c.d, s => { s.phase = 'dev'; s.cycle.headAtStart = later; s.cycle.checkpointTag = OLD; return s; });

    const s = cli(c.d, ['status']);
    check('status 는 여전히 exit 0', s.code === 0, s.out);
    check('status 에 롤백 항목은 남는다(rollback:)', /rollback:/.test(s.out), s.out);
    check('앞선 태그로 git reset --hard 를 안내하지 않는다', !s.out.includes(`git reset --hard ${OLD}`), s.out);
    check('태그가 이 사이클 시작보다 앞이라고 말한다', /이 사이클 시작\([0-9a-f]+\)보다 앞/.test(s.out), s.out);
    check('새로 만드는 명령을 알려 준다', /advance-phase\.js checkpoint/.test(s.out), s.out);

    const rb = cli(c.d, ['rollback']);
    check('rollback 은 exit≠0', rb.code !== 0, `exit=${rb.code}`);
    check('rollback 은 git reset --hard 줄을 내지 않는다', !/git reset --hard/.test(rb.out), rb.out);
    check('rollback 도 사이클 시작보다 앞이라고 말한다', /보다 앞/.test(rb.out), rb.out);
  } catch (e) { check('CK-03', false, e.message); } finally { if (c) cleanup(c.d); }
}

console.log('\n[CK-04] should_keep_reset_advice_when_checkpoint_moved_tag_forward');
{
  let c;
  try {
    // 대조 — 사이클 중 커밋 뒤 checkpoint 로 롤백 지점을 앞당긴다. 태그 ≠ 사이클 시작이지만 정상이다.
    c = startedCycle('ck04');
    const mid = commitFile(c.d, 'src/mid.js', '// 사이클 중 커밋\n', 'mid-cycle');
    const r = cli(c.d, ['checkpoint']);
    check('checkpoint 성공', r.code === 0, r.out);
    const tag = (readState(c.d) || {}).cycle.checkpointTag;
    check('태그가 사이클 중 커밋으로 앞당겨졌다', !!tag && tag !== OLD && tagCommit(c.d, tag) === mid, `${tag} @ ${tag && tagCommit(c.d, tag)} · mid=${mid}`);
    check('사이클 시작은 그대로다', (readState(c.d) || {}).cycle.headAtStart === c.first, JSON.stringify((readState(c.d) || {}).cycle.headAtStart));
    const s = cli(c.d, ['status']);
    check('경고 없이 롤백 줄을 낸다', !!tag && s.out.includes(`rollback:   git reset --hard ${tag}`) && !/보다 앞/.test(s.out), s.out);
    const rb = cli(c.d, ['rollback']);
    check('rollback 도 reset 명령을 낸다 (exit 0)', rb.code === 0 && rb.out.includes(`git reset --hard ${tag}`), rb.out);
  } catch (e) { check('CK-04', false, e.message); } finally { if (c) cleanup(c.d); }
}

console.log('\n[CK-05] should_open_cycle_without_tag_when_not_a_git_repo');
{
  let d;
  try {
    d = makeSandbox({ prefix: 'ck05' });
    fs.rmSync(path.join(d, '.git'), { recursive: true, force: true });
    const r = cli(d, ['new-cycle', 'next-topic']);
    check('new-cycle 은 git 이 없어도 성공한다', r.code === 0, r.out);
    check('롤백 포인트를 만들지 못했다고 말한다', /롤백 포인트[^\n]{0,20}(실패|만들지 못)/.test(r.out), r.out);
    const s = cli(d, ['status']);
    check('status 에 롤백 줄이 없다', s.code === 0 && !/rollback:/.test(s.out), s.out);
  } catch (e) { check('CK-05', false, e.message); } finally { if (d) cleanup(d); }
}

console.log('\n[CK-06] should_start_new_cycle_when_force_leaves_complete');
{
  let c;
  try {
    // Loop A(v11/N-01): 새 사이클 판정(_gates isNewCycle)은 !FORCE 블록 안에서만 읽혔다. complete 에서 --force 로 나가면
    //   끝난 사이클 객체(태그·시작 모두 사이클 1 시작)가 그대로 남아 판정이 ok 가 되고, reset 이 사이클 1 커밋까지 지웠다.
    c = completedCycle('ck06');
    expectNewCycleTagged('complete → dev --force', c, cli(c.d, ['dev', '--force', '--reason', '긴급 수정']));
    const st = readState(c.d) || {};
    const cyc = st.cycle || {};
    check('새 사이클의 시작은 새 HEAD 다', cyc.headAtStart === c.done, `headAtStart=${cyc.headAtStart} · HEAD=${c.done}`);
    check('강제 전이의 흔적은 새 사이클에 남는다', !!cyc.forced && (cyc.needsDecision || []).some(x => /^force:complete→dev/.test(x)),
      JSON.stringify({ forced: cyc.forced, needsDecision: cyc.needsDecision }));
  } catch (e) { check('CK-06', false, e.message); } finally { if (c) cleanup(c.d); }
}

// ── [v12/N-04] 사이클의 기준선은 움직이지 않고, 커밋은 내 것만 담는다 ─────────────
//   set-track 은 진행 중 사이클에서도 태그를 새로 만들고 headAtStart 를 HEAD 로 덮었다 — 사이클 중 커밋이 "이 사이클의 변경" 에서 빠졌다.
//   checkpoint --commit 은 touched 가 비면 `git add -A` 로 트리 전체를 커밋했고, 사이클을 여는 안내가 바로 그 명령을 권했다.
//   대조(CK-08·CK-10)는 수정 전에도 초록이어야 한다 — 새 사이클의 태그와 touched 가 있는 커밋은 정상 경로다.
const { spawnSync } = require('child_process');
const REPO_HOOKS = path.join(__dirname, '..', '..', '.claude', 'hooks');
function sameCommit(d, a, b) {
  try { return !!a && !!b && git(d, ['rev-parse', `${a}^{commit}`]) === git(d, ['rev-parse', `${b}^{commit}`]); } catch { return false; }
}
function checkpointTags(d) { return git(d, ['tag', '-l', 'checkpoint/*']).split('\n').filter(Boolean); }

console.log('\n[CK-07] should_keep_baseline_and_tag_when_set_track_runs_mid_cycle');
{
  let c;
  try {
    c = startedCycle('ck07');
    patchState(c.d, s => { s.phase = 'dev'; return s; });
    commitFile(c.d, 'src/mid.js', '// 사이클 중 커밋\n', 'mid-cycle');
    const tagsBefore = checkpointTags(c.d);
    const r = cli(c.d, ['set-track', 'B']);
    const cyc = (readState(c.d) || {}).cycle || {};
    check('set-track 은 exit 0', r.code === 0, r.out);
    check('사이클 시작(headAtStart)이 그대로다', cyc.headAtStart === c.first, `headAtStart=${cyc.headAtStart} · first=${c.first}`);
    check('롤백 태그가 그대로다', cyc.checkpointTag === OLD, `checkpointTag=${cyc.checkpointTag}`);
    check('새 checkpoint 태그를 만들지 않는다', checkpointTags(c.d).length === tagsBefore.length,
      `before=${tagsBefore.join(',')} after=${checkpointTags(c.d).join(',')}`);
    check('롤백 포인트를 유지했다고 말하고 checkpoint 명령을 알려 준다', /롤백 포인트 유지/.test(r.out) && /advance-phase\.js checkpoint/.test(r.out), r.out);
  } catch (e) { check('CK-07', false, e.message); } finally { if (c) cleanup(c.d); }
}

console.log('\n[CK-08] should_tag_head_when_set_track_opens_cycle');
{
  let d;
  try {
    // 대조 — 사이클이 없을 때와 --new-cycle 은 지금처럼 그 자리에 태그를 만들고 기준선을 HEAD 로 둔다.
    d = makeSandbox({ prefix: 'ck08' });
    const r = cli(d, ['set-track', 'C']);
    const c1 = (readState(d) || {}).cycle || {};
    check('사이클 없는 set-track 은 exit 0', r.code === 0, r.out);
    check('태그가 HEAD 를 가리킨다', !!c1.checkpointTag && sameCommit(d, c1.checkpointTag, 'HEAD'), `checkpointTag=${c1.checkpointTag}`);
    check('headAtStart 가 HEAD 다', sameCommit(d, c1.headAtStart, 'HEAD'), `headAtStart=${c1.headAtStart}`);

    patchState(d, s => { s.phase = 'dev'; return s; });
    commitFile(d, 'src/next.js', '// 다음 작업\n', 'next');
    const r2 = cli(d, ['set-track', 'B', '--new-cycle']);
    const c2 = (readState(d) || {}).cycle || {};
    check('--new-cycle 은 exit 0', r2.code === 0, r2.out);
    check('--new-cycle 은 새 태그를 만든다', !!c2.checkpointTag && c2.checkpointTag !== c1.checkpointTag, `before=${c1.checkpointTag} after=${c2.checkpointTag}`);
    check('새 태그가 현재 HEAD 를 가리킨다', sameCommit(d, c2.checkpointTag, 'HEAD'), `checkpointTag=${c2.checkpointTag}`);
    check('새 사이클의 headAtStart 가 현재 HEAD 다', sameCommit(d, c2.headAtStart, 'HEAD'), `headAtStart=${c2.headAtStart}`);
  } catch (e) { check('CK-08', false, e.message); } finally { if (d) cleanup(d); }
}

console.log('\n[CK-09] should_refuse_checkpoint_commit_when_cycle_touched_nothing');
{
  let c;
  try {
    c = startedCycle('ck09');
    patchState(c.d, s => { s.phase = 'dev'; s.cycle.touched = []; return s; });
    fs.writeFileSync(path.join(c.d, 'src', 'other.js'), '// 다른 세션이 쓰는 중인 파일\n');
    const headBefore = git(c.d, ['rev-parse', 'HEAD']);
    const tagsBefore = checkpointTags(c.d);
    const r = cli(c.d, ['checkpoint', '--commit']);
    check('checkpoint --commit 은 exit≠0', r.code !== 0, `exit=${r.code} ${r.out}`);
    check('커밋이 생기지 않는다', git(c.d, ['rev-parse', 'HEAD']) === headBefore, git(c.d, ['log', '--oneline', '-2']));
    check('태그도 만들지 않는다', checkpointTags(c.d).length === tagsBefore.length, checkpointTags(c.d).join(','));
    check('남의 파일은 여전히 미추적이다', /\?\? src\/other\.js/.test(git(c.d, ['status', '--porcelain', '-uall', '--', 'src'])),
      git(c.d, ['status', '--porcelain', '-uall', '--', 'src']));
    check('왜 거부했는지와 대안을 말한다', /touched/.test(r.out) && /touch reconcile --mine/.test(r.out), r.out);
  } catch (e) { check('CK-09', false, e.message); } finally { if (c) cleanup(c.d); }
}

console.log('\n[CK-10] should_commit_only_touched_when_checkpoint_commit_has_touched');
{
  let c;
  try {
    // 대조 — touched 가 있으면 지금처럼 그 경로만 커밋한다.
    c = startedCycle('ck10');
    fs.writeFileSync(path.join(c.d, 'src', 'a.js'), '// 이 사이클의 작업\n');
    fs.writeFileSync(path.join(c.d, 'src', 'other.js'), '// 남의 파일\n');
    patchState(c.d, s => { s.phase = 'dev'; s.cycle.touched = ['src/a.js']; return s; });
    const r = cli(c.d, ['checkpoint', '--commit']);
    const files = git(c.d, ['log', '-1', '--name-only', '--format=']).split('\n').filter(Boolean);
    check('checkpoint --commit 은 exit 0', r.code === 0, r.out);
    check('커밋 파일이 touched 뿐이다', files.length === 1 && files[0] === 'src/a.js', files.join(','));
    check('남의 파일은 미추적으로 남는다', /\?\? src\/other\.js/.test(git(c.d, ['status', '--porcelain', '-uall', '--', 'src'])),
      git(c.d, ['status', '--porcelain', '-uall', '--', 'src']));
    // Loop A(v12/N-04, HIGH): 부분 커밋 뒤 createCheckpoint 가 dirty:[] 를 돌려줘, 남의 미커밋 변경이 남아 있어도
    //   경고 없이 `git reset --hard` 를 안내했다 — 그대로 치면 다른 세션의 편집이 사라진다.
    check('커밋 뒤 남은 미커밋 변경을 센다', /미커밋 변경 [1-9]\d*개 미포함/.test(r.out), r.out);
    check('git reset --hard 가 그 변경을 지운다고 주의한다', /reset --hard[^\n]*지웁/.test(r.out), r.out);
  } catch (e) { check('CK-10', false, e.message); } finally { if (c) cleanup(c.d); }
}

console.log('\n[CK-11] should_refuse_commit_without_files_when_create_checkpoint_called_directly');
{
  let d;
  try {
    // 두 번째 방어선 — 다른 호출자가 files 없이 commit 을 부탁해도 트리 전체를 담지 않는다.
    d = makeSandbox({ prefix: 'ck11' });
    fs.writeFileSync(path.join(d, 'src', 'x.js'), '// 미추적\n');
    const headBefore = git(d, ['rev-parse', 'HEAD']);
    const code = `const G = require(${JSON.stringify(path.join(d, '.claude', 'hooks', '_git.js'))});` +
      `process.stdout.write('\\n@@' + JSON.stringify(G.createCheckpoint({ track: 'c', commit: true, label: 'probe' })));`;
    const p = spawnSync(process.execPath, ['-e', code], { cwd: d, encoding: 'utf8', timeout: 30000, env: Object.assign({}, process.env, { CLAUDE_PROJECT_DIR: d }) });
    const m = /@@(.*)$/s.exec(p.stdout || '');
    const res = m ? JSON.parse(m[1]) : null;
    check('createCheckpoint 를 부를 수 있다', !!res, (p.stdout || '') + (p.stderr || ''));
    check('files 없는 commit 요청은 ok:false', !!res && res.ok === false, JSON.stringify(res));
    check('커밋이 생기지 않는다', git(d, ['rev-parse', 'HEAD']) === headBefore, git(d, ['log', '--oneline', '-2']));
  } catch (e) { check('CK-11', false, e.message); } finally { if (d) cleanup(d); }
}

console.log('\n[CK-12] should_not_advise_bare_checkpoint_commit_in_source');
{
  // 소스 검사 — 사이클을 여는 안내가 트리 전체를 담는 명령을 권하지 않고, `add -A` 분기가 되살아나지 않는다.
  const ap = fs.readFileSync(path.join(REPO_HOOKS, 'advance-phase.js'), 'utf8');
  const gj = fs.readFileSync(path.join(REPO_HOOKS, '_git.js'), 'utf8');
  // Loop A(v12/N-04, MEDIUM): 처음 판정은 지운 삼항식 모양(`: ['add', '-A']`)과 옛 문구 두 개만 찾았다 —
  //   `gitRun(['add', '-A'])` 한 줄 재도입이나 문구만 바꾼 권유를 놓친다. 판정을 함수로 두고 **틀린 사본**에 대 본다.
  const hasBareAddAll = src => /\[\s*'add'\s*,\s*'-A'\s*\]/.test(src);   // pathspec 이 붙은 `'add', '-A', '--', …` 는 대상이 아니다
  const bareCommitAdvice = src => src.split('\n')
    .filter(l => /console\.(log|error)\(/.test(l) && /checkpoint --commit/.test(l) && !/\[ERROR\] checkpoint --commit:/.test(l))
    .filter(l => !/--mine/.test(l));
  check('createCheckpoint 에 pathspec 없는 add -A 가 없다', !hasBareAddAll(gj));
  check('checkpoint --commit 을 권하는 출력은 모두 --mine 을 함께 안내한다', bareCommitAdvice(ap).length === 0,
    bareCommitAdvice(ap).map(l => l.trim().slice(0, 120)).join(' | '));
  const gjBad = gj.replace("gitRun(['add', '--', ...commitFiles]);", "gitRun(['add', '--', ...commitFiles]);\n      if (!commitFiles.length) gitRun(['add', '-A']);");
  check('반증: 한 줄로 재도입한 add -A 를 잡는다', gjBad !== gj && hasBareAddAll(gjBad), gjBad === gj ? '치환이 헛돌았다' : '');
  const apBad = ap + "\n  if (dirty) console.log(`  ⚠ 미커밋 변경은 advance-phase.js checkpoint --commit 으로 담으세요`);\n";
  check('반증: --mine 없이 checkpoint --commit 을 권하는 출력을 잡는다', bareCommitAdvice(apBad).length === 1);
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
