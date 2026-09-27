// tests/unit/test_runner_contamination_subject.js
// 오염 판정에 **주체**가 있는가 — charter harness-v16-one-graph-one-screen
//
// 왜 이 파일이 존재하는가:
//   러너의 오염 판정은 스위트 실행 전후의 `git status` 차집합이다. 그 차집합에는
//   **누가 썼는가** 가 없다. 스위트는 5분 넘게 돈다. 그 사이에 다른 창이 추적 파일을
//   한 줄 고치면 "테스트가 오염시켰다" 로 신고되고, 출력은 `git checkout --` 을 권했다.
//
//   실측(2026-09-17): 단위 테스트 92/92 통과 뒤 `.claude/hooks/_coordinator.js` 가
//   오염으로 신고됐다. 그 변경은 다른 창이 실행 중(10:00:33)에 쓴 `supersedeWindow()`
//   였다. 안내를 따랐다면 남의 작업이 사라졌다. 러너 스스로 474행에
//   *"되돌리지는 않는다 — 남의 변경을 삼킬 수 있다"* 고 적어 두었는데, 출력만
//   되돌리라고 말하고 있었다.
//
//   그리고 `exit 1` 이라 **다른 창이 일하는 동안에는 전체 스위트가 초록이 될 수 없다** —
//   다중 세션을 목표로 하는 저장소에서 검증 자체가 막힌다.
//
//   고치는 방향은 벽을 여는 것이 **아니다**(run.js:484 가 dist/ 로 그 실수를 기록해 뒀다).
//   판정 기준에 주체 신호를 더한다: 나 말고 살아 있는 대화형 창이 있으면 주체를 가릴 수
//   없으니 경고, 혼자면 여전히 실패.
//
// 검증 방식: 소스 grep 이 아니라 **임시 git 저장소에서 러너를 실제로 돌려** 판정을 본다.

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const RUNNER = path.join(REPO, 'tests', 'run.js');
const HOOKS_SRC = path.join(REPO, '.claude', 'hooks');
const { makeReporter } = require(RUNNER);

const R = makeReporter('RCS: 오염 판정 주체');

function git(dir, args) {
  const r = spawnSync('git', args, { cwd: dir, encoding: 'utf8', timeout: 30000, windowsHide: true });
  if (r.status !== 0) throw Error('git ' + args.join(' ') + ': ' + (r.stderr || '').trim());
  return (r.stdout || '').trim();
}

// 추적 파일 하나를 커밋해 둔 임시 저장소 + 러너 사본. 훅도 복사한다 —
//   러너의 주체 판정이 ROOT/.claude/hooks/_harness-store.js 를 읽는다.
function miniRepo(contaminate) {
  const dir = fs.realpathSync(fs.mkdtempSync(path.join(os.tmpdir(), 'rcs-')));
  fs.mkdirSync(path.join(dir, 'tests', 'unit'), { recursive: true });
  fs.mkdirSync(path.join(dir, 'docs', 'memory'), { recursive: true });
  fs.mkdirSync(path.join(dir, 'src'), { recursive: true });
  fs.mkdirSync(path.join(dir, '.claude', 'hooks'), { recursive: true });
  for (const f of fs.readdirSync(HOOKS_SRC)) {
    if (f.endsWith('.js')) fs.copyFileSync(path.join(HOOKS_SRC, f), path.join(dir, '.claude', 'hooks', f));
  }
  fs.copyFileSync(RUNNER, path.join(dir, 'tests', 'run.js'));
  fs.writeFileSync(path.join(dir, 'src', 'tracked.txt'), 'original\n');

  // 추적 파일을 **실행 중에** 고치는 테스트 하나 — 오염(또는 동시 쓰기)의 재현이다.
  fs.writeFileSync(path.join(dir, 'tests', 'unit', 'test_writer.js'), `
const fs = require('fs'); const path = require('path');
const { makeReporter } = require('../run.js');
const R = makeReporter('writer');
${contaminate ? `fs.writeFileSync(path.join(__dirname,'..','..','src','tracked.txt'),'changed by test\\n');` : ''}
R.check('한 건 검증', true);
R.done();
`);

  git(dir, ['init', '-q']);
  git(dir, ['add', '-A']);
  git(dir, ['-c', 'user.name=T', '-c', 'user.email=t@localhost', 'commit', '-q', '-m', 'base']);
  return dir;
}

// 사본 저장소에 살아 있는 대화형 세션 N 개를 등록한다. pid 는 이 프로세스 —
//   실제로 살아 있어야 러너의 판정이 '남이 있다' 로 간다.
function joinInteractive(dir, count) {
  const { repository } = require(path.join(dir, '.claude', 'hooks', '_harness-store.js'));
  const { Coordinator } = require(path.join(dir, '.claude', 'hooks', '_coordinator.js'));
  const repo = repository(dir);
  try {
    const c = new Coordinator(repo.store, { repo_id: repo.repo_id });
    for (let i = 0; i < count; i++) {
      c.join({
        runtime: 'claude', pid: process.pid, worker_pids: [process.pid],
        worktree: dir, kind: 'interactive', name: 'window-' + (i + 1),
      });
    }
  } finally { try { repo.store.close(); } catch { /* 임시 */ } }
}

function runSuite(dir) {
  const r = spawnSync(process.execPath, [path.join(dir, 'tests', 'run.js')], {
    cwd: dir, encoding: 'utf8', timeout: 120000,
    env: Object.assign({}, process.env, { _HARNESS_TEST_RUN: 'rcs-probe', CLAUDE_PROJECT_DIR: dir }),
  });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || '') };
}
function rm(dir) { try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ } }

// ── RCS-01 혼자일 때의 오염은 여전히 실패다 (벽을 열지 않았다) ──────────
{
  const dir = miniRepo(true);
  try {
    const { code, out } = runSuite(dir);
    R.check('혼자 · 오염 → 러너가 실패로 끝낸다', code !== 0, 'exit ' + code);
    R.check('혼자 · 오염 → [격리 위반] 로 신고한다', /격리 위반/.test(out), out.slice(-600));
    R.check('혼자 · 오염 → 오염된 파일 이름을 낸다', /src[\\/]tracked\.txt/.test(out), out.slice(-600));
  } catch (e) { R.check('RCS-01', false, e.message); } finally { rm(dir); }
}

// ── RCS-02 되돌리기 명령을 권하지 않는다 (남의 작업을 삼키는 안내) ──────
{
  const dir = miniRepo(true);
  try {
    const { out } = runSuite(dir);
    R.check('오염 안내에 git checkout 되돌리기가 없다', !/git checkout/.test(out), out.slice(-700));
    R.check('대신 내용을 먼저 보라고 안내한다 (git diff)', /git diff/.test(out), out.slice(-700));
  } catch (e) { R.check('RCS-02', false, e.message); } finally { rm(dir); }
}

// ── RCS-03 주체를 가릴 수 없으면 실패가 아니다 — 다른 창이 살아 있을 때 ──
{
  const dir = miniRepo(true);
  try {
    joinInteractive(dir, 2);            // 나 + 남 → 2개
    const { code, out } = runSuite(dir);
    R.check('동시 창 있음 · 추적 파일 변경 → 러너가 실패로 끝내지 않는다', code === 0, 'exit ' + code);
    R.check('동시 창 있음 → [격리 판정 보류] 로 알린다', /격리 판정 보류/.test(out), out.slice(-800));
    R.check('동시 창 있음 → 살아 있는 창 수를 말한다', /살아 있는 대화형 창 2개/.test(out), out.slice(-800));
    R.check('동시 창 있어도 오염 후보 파일은 그대로 낸다 (조용히 넘기지 않는다)',
      /src[\\/]tracked\.txt/.test(out), out.slice(-800));
  } catch (e) { R.check('RCS-03', false, e.message); } finally { rm(dir); }
}

// ── RCS-04 창이 하나(나 자신)뿐이면 여전히 실패다 — 등록만으로 면제되지 않는다 ──
//   반증 짝: RCS-03 이 '세션이 있으면 통과' 가 아니라 '남이 있으면 보류' 임을 고정한다.
{
  const dir = miniRepo(true);
  try {
    joinInteractive(dir, 1);            // 나 하나뿐
    const { code, out } = runSuite(dir);
    R.check('내 창 하나만 등록 · 오염 → 여전히 실패다', code !== 0, 'exit ' + code);
    R.check('내 창 하나만 등록 → 판정 보류가 아니다', !/격리 판정 보류/.test(out), out.slice(-800));
  } catch (e) { R.check('RCS-04', false, e.message); } finally { rm(dir); }
}

// ── RCS-05 판별 재료를 출력한다 — 실행 구간과 mtime ─────────────────────
{
  const dir = miniRepo(true);
  try {
    const { out } = runSuite(dir);
    R.check('스위트 실행 구간을 출력한다', /스위트 실행 구간: \d\d:\d\d:\d\d ~ \d\d:\d\d:\d\d/.test(out), out.slice(-800));
    R.check('오염 후보의 mtime 을 함께 출력한다', /\(mtime \d\d:\d\d:\d\d\)/.test(out), out.slice(-800));
  } catch (e) { R.check('RCS-05', false, e.message); } finally { rm(dir); }
}

// ── RCS-06 깨끗하면 아무 말도 하지 않는다 (오탐 없음) ───────────────────
{
  const dir = miniRepo(false);
  try {
    const { code, out } = runSuite(dir);
    R.check('추적 파일을 건드리지 않으면 통과한다', code === 0, 'exit ' + code);
    R.check('깨끗할 때 격리 문구를 내지 않는다', !/격리 위반|격리 판정 보류/.test(out), out.slice(-500));
  } catch (e) { R.check('RCS-06', false, e.message); } finally { rm(dir); }
}

R.done();
