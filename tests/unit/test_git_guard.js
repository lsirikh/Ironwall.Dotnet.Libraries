// tests/unit/test_git_guard.js
// git 되돌림 거부 — charter harness-v3 / N-04 (FR-01~FR-05 · D1~D5)
//
// 왜 이 파일이 존재하는가:
//   N-01 이 명령 판정기를 세우고 N-03 이 관측을 완성했는데, **관측된 변경을 지우는
//   명령에는 규칙이 하나도 없었다.** 26개 git 형태를 판정기에 넣어 확인했더니
//   checkout -- · reset --hard · clean -fd · stash 가 전부 통과했다.
//
//   거부가 과하면 사람이 우회로를 만든다. 그래서 조건을 좁혔다 — 되돌림이 **이 사이클의
//   touched 와 교차할 때만** 거부한다. 아래 두 목록은 같은 비중이다:
//   차단 목록이 벽을 지키고, 통과 목록이 벽이 장애물이 되지 않게 지킨다.

'use strict';

const fs = require('fs');
const path = require('path');
const HOOKS = path.resolve(__dirname, '..', '..', '.claude', 'hooks');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).slice(0, 200) : ''}`); }
}

let G = null;
try { G = require(path.join(HOOKS, '_git.js')); } catch { /* 아래에서 */ }

console.log('\n═══ GG: git 되돌림 판정 ═══');

console.log('\n[GG-00] should_expose_revert_judge');
check('_git 이 judgeGitCommand 를 export 한다', !!(G && typeof G.judgeGitCommand === 'function'),
  G ? 'exports: ' + Object.keys(G).join(',') : '_git 로드 실패');
if (!G || typeof G.judgeGitCommand !== 'function') {
  console.log('\n  judgeGitCommand 가 없어 나머지를 돌릴 수 없습니다 (Red).');
  console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
  process.exit(1);
}
const J = G.judgeGitCommand;

// ── GG-01: 되돌림으로 판정되어야 하는 것 (D1) ─────────────────────
console.log('\n[GG-01] should_classify_destructive_git_commands');
const DESTRUCTIVE = [
  ['작업 트리 되돌리기 (경로 지정)', 'git checkout -- src/a.js', ['src/a.js']],
  ['작업 트리 되돌리기 (전체)', 'git checkout .', null],
  ['restore', 'git restore src/a.js', ['src/a.js']],
  ['reset --hard', 'git reset --hard HEAD', null],
  ['stash push', 'git stash', null],
  ['stash push (경로)', 'git stash push -- src/a.js', ['src/a.js']],
  ['clean -f', 'git clean -fd', null],
];
for (const [label, cmd, paths] of DESTRUCTIVE) {
  const r = J(cmd);
  check(label + ' → 되돌림', !!(r && r.kind === 'revert'), JSON.stringify(r));
  if (r && r.kind === 'revert') {
    const want = paths === null ? true : (Array.isArray(r.paths) && paths.every(p => r.paths.includes(p)));
    check(`   대상 = ${paths === null ? '전체' : paths.join(',')}`,
      paths === null ? r.all === true : want, JSON.stringify(r));
  }
}

// ── GG-02: 막으면 안 되는 것 (D2) — 벽이 장애물이 되지 않게 ───────
console.log('\n[GG-02] should_never_classify_safe_commands_as_destructive');
const SAFE = [
  'git status', 'git status --porcelain', 'git log --oneline -5', 'git diff', 'git diff HEAD',
  'git show HEAD', 'git stash list', 'git clean -n', 'git clean --dry-run',
  'git restore --staged src/a.js', 'git reset --soft HEAD~1', 'git reset HEAD src/a.js',
  'git revert HEAD', 'git add -A', 'git commit -m x', 'git rev-parse HEAD', 'git branch --list',
];
for (const cmd of SAFE) {
  const r = J(cmd);
  check(`통과: ${cmd}`, !r || r.kind !== 'revert', JSON.stringify(r));
}

// ── GG-03: 브랜치 전환·이름 변경 (D4) ─────────────────────────────
//   상태 디렉터리가 브랜치명으로 결정되므로, 사이클 중 전환하면 스냅샷과 기록이 갈린다.
console.log('\n[GG-03] should_classify_branch_switch');
for (const [label, cmd] of [
  ['checkout <branch>', 'git checkout main'],
  ['switch', 'git switch main'],
  ['branch -m', 'git branch -m newname'],
  ['checkout -b', 'git checkout -b feature/x'],
]) {
  const r = J(cmd);
  check(label + ' → 브랜치 전환', !!(r && r.kind === 'branch'), JSON.stringify(r));
}
for (const cmd of ['git branch --list', 'git branch -a', 'git branch']) {
  const r = J(cmd);
  check(`통과: ${cmd}`, !r || r.kind !== 'branch', JSON.stringify(r));
}

// ── GG-04: git 이 아닌 명령은 판정 대상이 아니다 ──────────────────
console.log('\n[GG-04] should_ignore_non_git_commands');
for (const cmd of ['node tests/run.js', 'echo git reset --hard', 'ls -la', '']) {
  check(`판정 없음: ${cmd.slice(0, 30)}`, J(cmd) === null, JSON.stringify(J(cmd)));
}

// ── GG-05: 어떤 입력에도 던지지 않는다 ────────────────────────────
console.log('\n[GG-05] should_never_throw');
{
  let threw = null;
  for (const bad of [null, undefined, 123, {}, [], 'git ' + 'x'.repeat(5000), 'git "unclosed']) {
    try {
      const r = J(bad);
      if (!(r === null || (r && typeof r.kind === 'string'))) { threw = 'bad shape: ' + JSON.stringify(r); break; }
    } catch (e) { threw = e.message; break; }
  }
  check('비정상 입력에도 일정한 형태', threw === null, threw);
}

// ── GG-06: 교차 판정 — 되돌림이 touched 와 겹칠 때만 위험하다 (D3) ─
console.log('\n[GG-06] should_intersect_with_touched_only');
{
  const inter = G.revertIntersects;
  check('_git 이 revertIntersects 를 export 한다', typeof inter === 'function');
  if (typeof inter === 'function') {
    const touched = ['src/a.js', 'docs/x.md'];
    check('교차하는 경로 되돌리기 → 위험', inter(J('git checkout -- src/a.js'), touched) === true);
    check('교차하지 않는 경로 되돌리기 → 안전', inter(J('git checkout -- other/b.js'), touched) === false);
    check('전체 되돌리기 + touched 있음 → 위험', inter(J('git reset --hard'), touched) === true);
    check('전체 되돌리기 + touched 없음 → 안전', inter(J('git reset --hard'), []) === false);
    check('디렉터리 지정이 하위 파일과 교차', inter(J('git checkout -- src'), touched) === true);
    check('되돌림이 아니면 언제나 안전', inter(J('git status'), touched) === false);
    check('레코드 형태 touched 도 읽는다',
      inter(J('git checkout -- src/a.js'), [{ path: 'src/a.js', by: { tool: 'Bash' } }]) === true);
  }
}

// ── GG-07: 무시 경로까지 지우는 clean 은 치명이다 ─────────────────
//   실측: `git clean -fdx` 가 `.claude/.branch-*/` 전체를 지웠다 —
//   상태·기준선·관측 스냅샷·영향 그래프가 한 번에 죽는다. touched 와 무관하다.
console.log('\n[GG-07] should_mark_clean_x_as_fatal');
{
  for (const cmd of ['git clean -fdx', 'git clean -fX', 'git clean -xdf']) {
    const r = J(cmd);
    check(`치명: ${cmd}`, !!(r && r.kind === 'revert' && r.fatal === true), JSON.stringify(r));
  }
  check('-x 없는 clean 은 치명이 아니다', !(J('git clean -fd') || {}).fatal, JSON.stringify(J('git clean -fd')));
}

// ── GG-08: 하네스 장부는 되돌림에서 무조건 보호 ───────────────────
//   감사 장부를 되돌리면 승인 근거 대조(N-02)가 짝을 잃어 complete 가 영구 거부된다 —
//   하네스가 자기를 잠그는 경로다. touched 교차 여부와 무관하게 막아야 한다.
console.log('\n[GG-08] should_protect_harness_ledgers_from_revert');
{
  const P = G.REVERT_PROTECTED;
  check('_git 이 REVERT_PROTECTED 를 export 한다', !!P);
  if (P) {
    for (const p of ['docs/memory/audit-log.jsonl', 'docs/memory/decisions.jsonl',
      'docs/memory/dev-journal.md', 'docs/memory/session-context.md',
      'docs/memory/audit-log-archive/2026-09-07.jsonl', '.claude/.branch-x/pipeline-state.json']) {
      check(`보호: ${p}`, P.test(p), 'not protected');
    }
    for (const p of ['src/a.js', 'docs/prds/x-prd.md', 'tests/unit/t.js', 'docs/INDEX.md']) {
      check(`보호 아님: ${p}`, !P.test(p), 'wrongly protected');
    }
  }
}

// ── GG-09: 하네스가 안내하는 체크포인트 롤백은 막지 않는다 ────────
//   `status`·`checkpoint`·`rollback` 이 사용자에게 `git reset --hard checkpoint/…` 를 출력한다.
//   그것까지 막으면 하네스가 자기 탈출로를 막고, 사용자는 우회로를 만든다.
console.log('\n[GG-09] should_allow_harness_own_checkpoint_rollback');
{
  const iscp = G.isCheckpointRollback;
  check('_git 이 isCheckpointRollback 를 export 한다', typeof iscp === 'function');
  if (typeof iscp === 'function') {
    const cmd = 'git reset --hard checkpoint/c-20260907051645';
    check('체크포인트 태그 롤백은 예외', iscp(J(cmd), cmd) === true);
    check('일반 sha 롤백은 예외가 아니다', iscp(J('git reset --hard abc1234'), 'git reset --hard abc1234') === false);
    check('되돌림이 아니면 예외 판정도 false', iscp(J('git status'), 'git status') === false);
  }
}

// ── GG-10: MCP git 도구도 같은 판정기를 지난다 ────────────────────
console.log('\n[GG-10] should_route_mcp_git_through_same_judge');
{
  // MCP 합성은 pre-tool-gate 안에 있다(_common.js 는 이 노드의 봉투 범위 밖).
  //   훅 소스에서 합성표를 읽어 같은 판정을 재현한다.
  const src = fs.readFileSync(path.join(HOOKS, 'pre-tool-gate.js'), 'utf8');
  // 훅이 그 도구를 합성표에 갖고 있는지 소스로 확인한 뒤, 같은 합성을 재현해 판정한다.
  const norm = (name, ti) => {
    if (!src.includes(name + ':')) return null;
    if (String(ti.repo_path || '').startsWith('/some/other')) return null;
    if (name === 'mcp__git__git_checkout') return 'git checkout ' + (ti.branch_name || '');
    if (name === 'mcp__git__git_create_branch') return 'git checkout -b ' + (ti.branch_name || '');
    if (name === 'mcp__git__git_branch') return 'git branch --list';
    return null;
  };
  const co = norm('mcp__git__git_checkout', { repo_path: '.', branch_name: 'main' });
  check('mcp git_checkout → 명령 문자열', typeof co === 'string' && /checkout/.test(co), String(co));
  check('그 문자열이 브랜치 전환으로 판정', (J(co) || {}).kind === 'branch', JSON.stringify(J(co)));
  const cb = norm('mcp__git__git_create_branch', { repo_path: '.', branch_name: 'feature/x' });
  check('mcp create_branch → 브랜치', (J(cb) || {}).kind === 'branch', JSON.stringify(J(cb)));
  check('mcp git_branch 는 판정 대상 아님', !J(norm('mcp__git__git_branch', { repo_path: '.' })));
  check('다른 저장소를 가리키면 판정 대상 아님',
    norm('mcp__git__git_checkout', { repo_path: '/some/other/repo', branch_name: 'x' }) === null);
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
