// tests/unit/test_install_parity.js
// 설치본이 하네스로서 동작하는가 — charter harness-v3 / N-10 (FR-01~FR-09 · D1~D10)
//
// 왜 이 파일이 존재하는가:
//   설치본을 임시 저장소에 실제로 만들어 측정했더니, 결함이 여럿인데 **전부 같은 형태**였다 —
//   *"저장소에서는 통과하지만 설치본에서는 아니다."* 그리고 그 형태를 잡는 테스트가 0건이었다.
//
//   가장 무거운 것: `.gitignore` 템플릿이 `docs/memory/` 를 통째로 무시해
//   **의사결정 원장이 설치본에서 git 에 남지 않는다.** LOOPS.md 의 "원장은 git 추적 진실"
//   이 설치본에서 거짓이었다. 하네스가 자기 설치본이 재현하지 못하는 조건에서 개발되고 있었다.
//
//   그리고 설치본 CLAUDE.md 는 하드코딩 복제본이라 저장소가 **금지한 것을 지시했다** —
//   "session-context.md 표에 한 줄". 실측하면 그 표는 애초에 없고, 적으면 하네스가 지운다.

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 260) : ''}`); }
}

function git(dir, a) {
  const r = spawnSync('git', a, { cwd: dir, encoding: 'utf8' });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || '') };
}
function node(dir, args, opts) {
  const r = spawnSync(process.execPath, args, {
    cwd: dir, encoding: 'utf8', timeout: 120000,
    env: Object.assign({}, process.env, { CLAUDE_PROJECT_DIR: dir }, (opts && opts.env) || {}),
    input: (opts && opts.input) || '',
  });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || '') };
}

// [v8/N-01] 추적 중인 릴리스 아카이브의 지문. 검증 빌드가 이것을 바꾸면 안 된다.
//   예전에는 검증 빌드가 `dist/claude-skill-harness-v<버전>.zip` — **릴리스와 같은 이름** — 에
//   썼고, 빌드가 재현 가능하지 않아 소스가 같아도 바이트가 달라졌다. 스위트를 돌릴 때마다
//   배포된 zip 과 저장소의 zip 이 다른 물건이 됐다.
function releaseZipPrints() {
  const r = spawnSync('git', ['ls-files', '--', 'dist/*.zip'], { cwd: REPO, encoding: 'utf8' });
  const out = {};
  for (const rel of String(r.stdout || '').split('\n').map(x => x.trim()).filter(Boolean)) {
    try { out[rel] = require('crypto').createHash('sha1').update(fs.readFileSync(path.join(REPO, rel))).digest('hex'); }
    catch { out[rel] = '(읽기 실패)'; }
  }
  return out;
}
const RELEASE_BEFORE = releaseZipPrints();
let RELEASE_AFTER = null;

// ── 설치본을 한 번만 만든다 (설치는 느리다) ──────────────────────────
let PROJ = null, STAGE = null, buildErr = null, VERIFY_OUT = null;
let zipFresh = false, zipWhy = null;   // [IP-30] 빌드가 이번 실행에서 zip 을 만들었는가
try {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'iparity-'));
  STAGE = path.join(root, 'stage');
  PROJ = path.join(root, 'proj');
  fs.mkdirSync(PROJ, { recursive: true });

  // 패키지를 만든다 — build-package 는 저장소를 읽어 `dist/_staging/claude-skill-harness/`
  //   아래에 배포 트리를 만든다(그 안에 install.js 가 있다).
  const buildStartedAt = Date.now();
  // [v8/N-01] 검증 빌드는 **저장소 밖**에 쓴다. 예전에는 인자 없이 불러 릴리스 zip 을 덮어썼다.
  //   `--no-exe`: IP 테스트는 exe 를 보지 않는다. 굽기만 하고 검사하지 않던 112MB 였고,
  //   `npx @yao-pkg/pkg` 로 **네트워크를 탔다**. exe 가 실패해도 이 테스트는 초록이었다.
  VERIFY_OUT = path.join(root, 'build');
  const b = spawnSync(process.execPath, [path.join(REPO, 'scripts', 'build-package.js'), '--out', VERIFY_OUT, '--no-exe'], {
    cwd: REPO, encoding: 'utf8', timeout: 180000,
  });
  RELEASE_AFTER = releaseZipPrints();
  const wrapper = path.join(VERIFY_OUT, '_staging', 'claude-skill-harness');
  if (fs.existsSync(path.join(wrapper, 'install.js'))) STAGE = wrapper;
  else buildErr = ((b.stdout || '') + (b.stderr || '')).slice(-400) || `스테이징 없음: ${wrapper}`;

  // [IP-30 · v5/N-06] **zip 이 실제로 만들어졌는가.** 예전에는 staging 만 확인해서,
  //   zip 단계가 통째로 죽어도 이 테스트가 초록이었다 — 실측으로 그런 일이 일어났다
  //   (Compress-Archive 가 폴백 없이 멈춰 릴리스 산출물이 없는 채로 스위트가 통과).
  //   빌드 **시작 이후에 쓰인** 파일인지까지 본다. 예전 zip 이 남아 있으면 통과해 버린다.
  const zipVer = (fs.readFileSync(path.join(REPO, 'CLAUDE.md'), 'utf8').match(/^version:\s*"([^"]+)"/m) || [, '0'])[1];
  const zipPath = path.join(VERIFY_OUT, `claude-skill-harness-v${zipVer}.zip`);

  try {
    const st = fs.statSync(zipPath);
    zipFresh = st.mtimeMs >= buildStartedAt && st.size > 10000;
    if (!zipFresh) zipWhy = `mtime=${new Date(st.mtimeMs).toISOString()} size=${st.size} (빌드 시작 ${new Date(buildStartedAt).toISOString()})`;
  } catch (e) { zipWhy = `없음: ${zipPath}`; }
  if (!buildErr) {
    git(PROJ, ['init', '-q']);
    git(PROJ, ['config', 'user.email', 't@t']);
    git(PROJ, ['config', 'user.name', 't']);
    const inst = path.join(STAGE, 'install.js');
    if (fs.existsSync(inst)) {
      node(PROJ, [inst, '--target', PROJ, '--yes'], {});
    } else {
      buildErr = `스테이징에 install.js 가 없다: ${STAGE}`;
    }
  }
} catch (e) { buildErr = e.message; }

console.log('\n═══ IP: 설치본 하네스 계약 ═══');

console.log('\n[IP-00] should_build_and_install');
check('패키지를 만들고 설치한다', !buildErr, buildErr);
if (buildErr) {
  console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
  process.exit(1);
}
check('설치본에 훅이 있다', fs.existsSync(path.join(PROJ, '.claude', 'hooks', 'advance-phase.js')));

// ── IP-01: 원장이 git 에 남는다 (D1/D2) ──────────────────────────────
// LOOPS.md 는 "decisions.jsonl 은 git 추적 진실" 이라고 말한다.
console.log('\n[IP-01] should_track_the_ledger_in_installed_project');
{
  const tracked = p => git(PROJ, ['check-ignore', '-q', '--', p]).code !== 0;   // exit≠0 = 무시되지 않음
  check('의사결정 원장이 추적된다', tracked('docs/memory/decisions.jsonl'),
    git(PROJ, ['check-ignore', '-v', '--', 'docs/memory/decisions.jsonl']).out);
  check('dev-journal 이 추적된다', tracked('docs/memory/dev-journal.md'));
  for (const d of ['prds', 'plans', 'tests', 'reports', 'charters', 'analyses']) {
    check(`docs/${d} 가 추적된다`, tracked(`docs/${d}/x.md`),
      git(PROJ, ['check-ignore', '-v', '--', `docs/${d}/x.md`]).out);
  }
  check('CLAUDE.md 가 추적된다', tracked('CLAUDE.md'));

  // 런타임 상태는 여전히 무시된다 — 이쪽이 무너지면 사용자 저장소가 쓰레기로 찬다
  const ignored = p => git(PROJ, ['check-ignore', '-q', '--', p]).code === 0;
  check('브랜치 런타임 상태는 무시된다', ignored('.claude/.branch-x/pipeline-state.json'));
  check('세션 파일은 무시된다', ignored('docs/memory/sessions/x/ppid-1.md'));
  check('상태 백업은 무시된다', ignored('.claude/.branch-x/state-backups/y.json'));
  check('잠금 파일은 무시된다', ignored('docs/memory/audit.lock'));
}

// ── IP-02: 배포된 문서가 가리키는 것이 실재한다 (D5) ─────────────────
console.log('\n[IP-02] should_ship_every_referenced_harness_file');
{
  const must = [
    '.claude/skills/process/LOOPS.md',
    '.claude/skills/process/SKILL.md',
    'tests/run.js',
    'tests/unit/_sandbox.js',
  ];
  for (const m of must) check(`${m} 가 설치본에 있다`, fs.existsSync(path.join(PROJ, m)));
  check('docs/charters 디렉터리가 생긴다', fs.existsSync(path.join(PROJ, 'docs', 'charters')));
}

// ── IP-03: CLAUDE.md 가 저장소와 같은 것을 지시한다 (D7) ─────────────
// 복제본은 세 세대 뒤처져 "표에 한 줄" 이라고 말했다 — 저장소가 금지한 행위다.
console.log('\n[IP-03] should_derive_claude_md_from_repository');
{
  const inst = fs.readFileSync(path.join(PROJ, 'CLAUDE.md'), 'utf8');
  const repo = fs.readFileSync(path.join(REPO, 'CLAUDE.md'), 'utf8');
  const strip = s => s.replace(/```yaml[\s\S]*?```/, '```yaml\n(yaml)\n```').replace(/\r\n/g, '\n').trim();
  check('yaml 을 뺀 산문이 저장소와 같다', strip(inst) === strip(repo),
    `설치본 ${strip(inst).length}자 / 저장소 ${strip(repo).length}자`);
  check('결정 기록을 CLI 로 지시한다', /decide/.test(inst), inst.slice(0, 200));
  check('표에 손으로 적으라고 하지 않는다', !/결정 사항[^\n]*표에 한 줄|표에 한 줄/.test(inst),
    (inst.match(/[^\n]*표에 한 줄[^\n]*/g) || []).join(' | '));
}

// ── IP-04: 설치본에서 하네스가 돈다 (D10) ────────────────────────────
console.log('\n[IP-04] should_run_the_harness_in_the_installed_project');
{
  const AP = path.join(PROJ, '.claude', 'hooks', 'advance-phase.js');
  const st = node(PROJ, [AP, 'status']);
  check('status 가 exit 0 이다', st.code === 0, st.out);
  check('상태가 시딩된다', /phase:/.test(st.out), st.out.slice(0, 300));

  const dj = path.join(PROJ, 'd.json');
  fs.writeFileSync(dj, JSON.stringify({
    decision: '설치본에서 판단을 남긴다', reason: '원장이 git 에 남아야 다음 세션이 읽는다',
    rejected: ['기록하지 않기'], invalidation: { text: '원장이 무시되면 이 결정이 틀린 것' }, files: [],
  }));
  const dec = node(PROJ, [AP, 'decide', '--json-file', dj]);
  check('decide 가 exit 0 이다', dec.code === 0, dec.out);

  const ch = node(PROJ, [AP, 'charter', 'new', 'demo']);
  check('charter new 가 exit 0 이다', ch.code === 0, ch.out);

  // 그리고 그 원장이 실제로 커밋 후보에 들어간다 — 이것이 이 노드의 핵심이다
  const add = git(PROJ, ['add', '-A', '-n']);
  check('원장이 커밋 후보에 들어간다', /docs\/memory\/decisions\.jsonl/.test(add.out), add.out.slice(0, 500));
  check('런타임 상태는 커밋 후보가 아니다', !/\.branch-/.test(add.out), add.out.slice(0, 500));
}

// ── IP-05: 설치 검증이 실제 훅 목록에서 파생된다 (D9) ────────────────
console.log('\n[IP-05] should_derive_install_verification_from_hook_defs');
{
  const src = fs.readFileSync(path.join(REPO, 'install.js'), 'utf8');
  check('hookFiles 를 손으로 나열하지 않는다', !/const hookFiles = \['session-gate\.js'/.test(src),
    (src.match(/const hookFiles[^\n]*/g) || []).join(' | ').slice(0, 200));
  // 실제로 모든 훅이 설치됐는지 본다 — 목록이 짧으면 이 단언이 잡는다
  const repoHooks = fs.readdirSync(path.join(REPO, '.claude', 'hooks')).filter(f => f.endsWith('.js'));
  const missing = repoHooks.filter(f => !fs.existsSync(path.join(PROJ, '.claude', 'hooks', f)));
  check('저장소의 훅이 전부 설치된다 (대시보드 제외)',
    missing.filter(f => !/dashboard/.test(f)).length === 0, missing.join(', '));
}

// ── IP-06: 업그레이드가 무엇을 바꾸는지 밝힌다 (D3/D4) ───────────────
console.log('\n[IP-06] should_disclose_what_upgrade_replaces');
{
  const src = fs.readFileSync(path.join(REPO, 'install.js'), 'utf8');
  // 교체 목록은 `OVERWRITTEN_DIRS` 이고 보존 목록은 `PRESERVED_FILES` 다 — 어느 쪽에 있는지가 계약이다.
  const over = (src.match(/const OVERWRITTEN_DIRS = \[([\s\S]*?)\];/) || ['', ''])[1];
  const kept = (src.match(/const PRESERVED_FILES = \[([\s\S]*?)\];/) || ['', ''])[1];
  check('CLAUDE.md 가 교체 목록에 있다', /CLAUDE\.md/.test(over), over.slice(0, 300));
  check('CLAUDE.md 가 보존 목록에 없다', !/CLAUDE\.md/.test(kept), kept.slice(0, 300));
  check('.gitignore 변경을 교체 목록이 밝힌다', /gitignore/i.test(over), over.slice(0, 300));
  check('추적 후보 증가분을 보여준다', /추적 후보|newly tracked|추적 대상/.test(src),
    (src.match(/[^\n]*추적 후보[^\n]*/g) || []).join(' | ').slice(0, 200));
}

// ── 정리 ─────────────────────────────────────────────────────────────
try { fs.rmSync(path.dirname(PROJ), { recursive: true, force: true }); } catch { /* 임시 */ }

console.log('\n[IP-30] should_actually_produce_a_zip');
check('빌드가 이번 실행에서 zip 을 만들었다', zipFresh, zipWhy || '');

// ── [v8/N-01] 검증 빌드는 릴리스 산출물을 건드리지 않는다 ─────────────
console.log('\n[IP-31] should_not_touch_release_archives');
{
  const before = RELEASE_BEFORE, after = RELEASE_AFTER || {};
  const changed = Object.keys(before).filter(k => before[k] !== after[k]);
  check(`추적 중인 릴리스 zip ${Object.keys(before).length}개가 빌드 전후로 같다`,
    Object.keys(before).length > 0 && changed.length === 0,
    changed.length ? '바뀐 것: ' + changed.join(', ') : '추적 중인 zip 이 없다');
}

console.log('\n[IP-32] should_build_outside_the_repo');
{
  // 스테이징이 저장소 안이면 검증 빌드가 저장소를 고칠 수 있다는 뜻이다.
  const inRepo = p => !!p && path.resolve(p).toLowerCase().startsWith(path.resolve(REPO).toLowerCase() + path.sep);
  check('검증 빌드의 스테이징이 저장소 밖이다', STAGE && !inRepo(STAGE), String(STAGE));
}

console.log('\n[IP-33] should_refuse_out_without_a_path');
{
  // `--out` 뒤에 경로가 없으면 조용히 dist/ 로 떨어지면 안 된다 — 그러면 이 결함이 돌아온다.
  //   `--no-exe` 를 함께 준다: 지금 코드는 `--out` 을 모르므로 전체 빌드로 떨어지는데,
  //   exe 까지 구우면 네트워크를 타고 수십 초가 걸린다.
  const r = spawnSync(process.execPath, [path.join(REPO, 'scripts', 'build-package.js'), '--out', '--no-exe'], {
    cwd: REPO, encoding: 'utf8', timeout: 180000,
  });
  const out = (r.stdout || '') + (r.stderr || '');
  check('--out 뒤에 경로가 없으면 exit≠0', r.status !== 0, `exit=${r.status} · ${out.slice(-200)}`);
  check('--out 거부 이유를 말한다', /--out/.test(out) && /경로/.test(out), out.slice(-200));
}


console.log('\n[IP-34] should_let_the_isolation_wall_guard_release_archives');
{
  // 러너의 오염 벽은 "실행 후 트리가 깨끗한가" 를 지킨다. 그 벽이 dist/ 덮어쓰기를 잡았을 때
  //   **테스트를 고치는 대신 벽을 열었다** — 면제 목록에 'dist/' 를 넣었다. 그래서 릴리스
  //   아카이브가 그 뒤로 계속 덮어써졌고 아무도 몰랐다.
  //   벽이 무언가를 잡았을 때, 벽을 여는 것은 답이 아니라 질문의 시작이다.
  const run = fs.readFileSync(path.join(REPO, 'tests', 'run.js'), 'utf8');
  const owned = (run.match(/const OWNED_BY_HARNESS = \[([\s\S]*?)\];/) || ['', ''])[1];
  const entries = owned.split('\n').map(l => l.replace(/\/\/.*$/, '').trim()).filter(Boolean).join(' ');
  check('오염 벽의 면제 목록을 찾았다', owned.length > 0, '');
  check("오염 벽이 dist/ 를 면제하지 않는다", !/['"]dist\/['"]/.test(entries), entries.slice(0, 200));
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
