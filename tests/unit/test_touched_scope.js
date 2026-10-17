// tests/unit/test_touched_scope.js
// post-write-sync 가 cycle.touched 에 무엇을 넣는가
//
// 왜 있는가: 최초 구현은 `^(docs|\.claude|tests)/` 를 전부 "문서류"로 보고 제외했다.
//   하네스 저장소에서는 `.claude/hooks/*.js` 와 `tests/*.js` 가 곧 소스라서
//   cycle.touched 가 **항상 비었고**, complete 는 이 사이클의 작업을 하나도 보지 못한 채
//   문서만 커밋할 뻔했다(관측 공백 게이트가 잡아냈다).
//   제외 대상은 문서(docs/)와 런타임 상태(.claude/.branch-*, install-logs)뿐이다.

'use strict';

const assert = require('assert');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const HOOKS_SRC = path.join(REPO, '.claude', 'hooks');

let pass = 0, fail = 0;
function test(name, fn) {
  try { fn(); pass++; console.log(`  ✅ ${name}`); }
  catch (e) { fail++; console.log(`  ❌ ${name}: ${e.message}`); }
}

function sandbox() {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'touched-'));
  fs.mkdirSync(path.join(dir, '.claude', 'hooks'), { recursive: true });
  for (const f of fs.readdirSync(HOOKS_SRC)) {
    if (f.endsWith('.js')) fs.copyFileSync(path.join(HOOKS_SRC, f), path.join(dir, '.claude', 'hooks', f));
  }
  for (const d of ['docs/memory', 'docs/prds', 'docs/plans', 'src', 'tests/unit']) {
    fs.mkdirSync(path.join(dir, d), { recursive: true });
  }
  fs.writeFileSync(path.join(dir, 'CLAUDE.md'),
    '# CLAUDE.md\n```yaml\nproject_name: "sbx"\nversion: "0.0.1"\ntest_command: ""\nmax_auto_fix: 3\n```\n');
  fs.writeFileSync(path.join(dir, 'docs', 'memory', 'audit-log.jsonl'), '');
  fs.writeFileSync(path.join(dir, 'docs', 'memory', 'feedback-rules.json'), '[]\n');
  for (const c of [['init', '-q'], ['config', 'user.email', 't@t'], ['config', 'user.name', 't']]) {
    spawnSync('git', c, { cwd: dir, encoding: 'utf8' });
  }
  spawnSync('git', ['add', '-A'], { cwd: dir });
  spawnSync('git', ['commit', '-q', '-m', 'init'], { cwd: dir });
  return dir;
}

function write(dir, rel) {
  const abs = path.join(dir, rel);
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  fs.writeFileSync(abs, '// x\n');
  spawnSync(process.execPath, [path.join(dir, '.claude', 'hooks', 'post-write-sync.js')], {
    cwd: dir, encoding: 'utf8', timeout: 20000,
    env: { ...process.env, CLAUDE_PROJECT_DIR: dir },
    input: JSON.stringify({ hook_event_name: 'PostToolUse', tool_name: 'Write', tool_input: { file_path: abs } }),
  });
}

function touched(dir) {
  // 테스트가 스스로 `.claude/.branch-abc/` 를 만들므로, 이름 순 첫 번째가 아니라
  // 실제 상태 파일이 있는 브랜치 디렉터리를 찾는다.
  const cd = path.join(dir, '.claude');
  const out = [];
  for (const n of fs.readdirSync(cd)) {
    if (!n.startsWith('.branch-')) continue;
    const f = path.join(cd, n, 'pipeline-state.json');
    if (!fs.existsSync(f)) continue;
    // [v3.6/N-04] touched 는 소유권 레코드 배열이다 — 경로만 필요한 곳은 공용 헬퍼를 쓴다.
    try {
      const raw = ((JSON.parse(fs.readFileSync(f, 'utf8')).cycle) || {}).touched;
      out.push(...require(path.join(REPO, '.claude', 'hooks', '_git.js')).touchedPaths(raw));
    } catch {}
  }
  return out;
}

console.log('\n═══ TS: cycle.touched 범위 ═══\n');

const d = sandbox();
write(d, 'src/app.js');
write(d, '.claude/hooks/my-hook.js');
write(d, 'tests/unit/test_x.js');
write(d, 'docs/prds/x-prd.md');
write(d, '.claude/.branch-abc/scratch.js');
write(d, '.claude/install-logs/run.log');
const t = touched(d);

test('should_record_plain_source_file', () => assert.ok(t.includes('src/app.js'), JSON.stringify(t)));
test('should_record_harness_hook_as_source', () => assert.ok(t.includes('.claude/hooks/my-hook.js'), JSON.stringify(t)));
test('should_record_test_file_as_source', () => assert.ok(t.includes('tests/unit/test_x.js'), JSON.stringify(t)));
test('should_skip_docs', () => assert.ok(!t.includes('docs/prds/x-prd.md'), JSON.stringify(t)));
test('should_skip_branch_runtime_state', () => assert.ok(!t.some(p => p.startsWith('.claude/.branch-')), JSON.stringify(t)));
test('should_skip_install_logs', () => assert.ok(!t.some(p => p.startsWith('.claude/install-logs/')), JSON.stringify(t)));

try { fs.rmSync(d, { recursive: true, force: true }); } catch {}

// ── [v5/N-02] 봉투 범위 3상태 — 실제 CLI 로 사이클을 끝까지 ──────────
//   같은 charter 의 **다른 노드에 선언된 파일**을 고치면 "승인된 그래프 밖" 으로 잡혀
//   사이클이 닫히지 않았다. 사용자가 이미 승인한 파일인데도. 계층을 가로지르는 기능과
//   공유 계약 리팩터링이 그래서 막혔고, 다운스트림은 charter 를 r4 까지 개정했다.
//
//   여기서 지키는 것은 두 방향이다 —
//     ① 다른 노드의 파일은 **막히지 않는다** (순서 문제이지 범위 이탈이 아니다)
//     ② 어느 노드에도 없는 파일은 **여전히 막힌다** (진짜 사고는 이쪽이다)
//   완화가 ②까지 삼키면 봉투가 무의미해진다. 둘을 같은 블록에서 본다.
console.log('\n═══ TS: 봉투 범위 3상태 (v5/N-02) ═══\n');
{
  const SB = require('./_sandbox.js');
  const CH_TEXT = `# Charter — t-scope

- **상태**: Approved

\`\`\`yaml
goal: 범위 3상태
budget_cycles: 4
expires: 2099-12-31
invariants:
  - file:exists CLAUDE.md
out_of_scope:
  - .git/**
\`\`\`

## 노드

- [ ] **[N-01]** 가
  - 파일: \`src/a.js\`
- [ ] **[N-02]** 나
  - 파일: \`src/b.js\`
`;

  // 봉투 사이클 하나를 심는다. 승인 필드는 **감사 행과 함께** 심어야 근거 대조를 통과한다.
  function seed(d, opts) {
    opts = opts || {};
    const CJ = require(path.join(SB.REPO, '.claude', 'hooks', '_charter.js'));
    const at = new Date().toISOString();
    fs.mkdirSync(path.join(d, 'docs', 'charters'), { recursive: true });
    fs.writeFileSync(path.join(d, 'docs', 'charters', 't-scope-charter.md'), CH_TEXT);
    fs.writeFileSync(path.join(d, 'docs', 'prds', 'a-prd.md'),
      '# PRD — 가\n\n- **상태**: Approved\n- **charter**: docs/charters/t-scope-charter.md\n'
      + '- **node**: N-01\n- **scope_files**: `src/a.js`\n');
    fs.writeFileSync(path.join(d, 'docs', 'plans', 'a-prd-plan.md'),
      '# plan\n\n- [' + (opts.taskOpen ? ' ' : 'x') + '] **[IMPL-01]** 가\n  - 파일: `' + (opts.taskFile || 'src/a.js') + '`\n  - 예상 공수: 1h\n');
    fs.appendFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'),
      JSON.stringify({ timestamp: at, event: 'user-approval', target: 'prd', file: 'docs/prds/a-prd.md' }) + '\n');
    SB.git(d, ['add', '-A']); SB.git(d, ['commit', '-q', '-m', 'seed']);
    const base = SB.git(d, ['rev-parse', '--short', 'HEAD']).trim();
    SB.patchState(d, s => {
      s.phase = opts.phase || 'report'; s.track = 'C';
      s.activePrd = 'docs/prds/a-prd.md'; s.activePlan = 'docs/plans/a-prd-plan.md';
      s.lastApproval = { prd: 'docs/prds/a-prd.md', at, via: 'user' };
      s.charters = { 't-scope': { path: 'docs/charters/t-scope-charter.md', approvedHash: CJ.contentHash(CH_TEXT), approvedAt: at } };
      s.cycle = {
        startedAt: new Date(Date.now() - 3600e3).toISOString(), headAtStart: base,
        touched: [], decisionsAtStart: 0, needsDecision: [], dirtyAtStart: [],
        charter: { id: 't-scope', node: 'N-01', path: 'docs/charters/t-scope-charter.md' },
      };
      return s;
    });
    return base;
  }

  // ── ① 다른 노드의 파일 — 막히지 않는다 ──────────────────────────
  {
    const d = SB.makeSandbox({ prefix: 'ts5a' });
    try {
      SB.cli(d, ['status']);
      seed(d);
      fs.writeFileSync(path.join(d, 'src', 'b.js'), '// N-02 의 파일을 N-01 사이클에서 미리 고친다\n');
      const rt = SB.cli(d, ['touch', 'reconcile', '--all']);
      test('다른 노드 파일이 touched 로 흡수된다', () => {
        const t = (SB.readState(d).cycle.touched || []).map(x => (typeof x === 'string' ? x : x.path));
        assert.ok(t.includes('src/b.js'), `${rt.out.slice(-200)} | ${JSON.stringify(t)}`);
      });
      SB.seedFreshEvidenceFixture(d);
      const rc = SB.cli(d, ['complete']);
      test('다른 노드 파일을 고쳐도 면제 없이 complete 한다', () => {
        assert.strictEqual(rc.code, 0, rc.out.slice(-400));
      });
      test('complete 거부 사유가 "범위 밖" 이 아니다', () => {
        assert.ok(!/범위 밖 변경/.test(rc.out), rc.out.slice(-300));
      });
    } finally { SB.cleanup(d); }
  }

  // ── ② 어느 노드에도 없는 파일 — 여전히 막힌다 ───────────────────
  {
    const d = SB.makeSandbox({ prefix: 'ts5b' });
    try {
      SB.cli(d, ['status']);
      seed(d);
      fs.writeFileSync(path.join(d, 'src', 'z.js'), '// 어느 노드에도 선언되지 않은 파일\n');
      SB.cli(d, ['touch', 'reconcile', '--all']);
      SB.seedFreshEvidenceFixture(d);
      const rc = SB.cli(d, ['complete']);
      test('선언되지 않은 파일을 고치면 complete 가 거부한다', () => {
        assert.notStrictEqual(rc.code, 0, rc.out.slice(-400));
      });
      test('거부 사유가 그 파일을 지목한다', () => {
        assert.ok(/src\/z\.js/.test(rc.out), rc.out.slice(-300));
      });
    } finally { SB.cleanup(d); }
  }

  // ── ③ task start — 다른 노드 파일의 태스크를 거부하지 않는다 ─────
  {
    const d = SB.makeSandbox({ prefix: 'ts5c' });
    try {
      SB.cli(d, ['status']);
      seed(d, { phase: 'dev', taskOpen: true, taskFile: 'src/b.js' });
      const rs = SB.cli(d, ['task', 'IMPL-01', 'start']);
      test('다른 노드 파일의 태스크가 시작된다', () => {
        assert.strictEqual(rs.code, 0, rs.out.slice(-400));
      });
      test('시작 거부가 "범위 밖" 으로 나오지 않는다', () => {
        assert.ok(!/범위 밖입니다/.test(rs.out), rs.out.slice(-300));
      });
    } finally { SB.cleanup(d); }
  }

  // ── ④ 편집 시점 경고 — 사실에 맞는 문구인가 ─────────────────────
  //   "complete 할 수 없게 됩니다" 는 이제 거짓이다. 거짓을 말하는 경고는 경고가 아니다.
  {
    const d = SB.makeSandbox({ prefix: 'ts5d' });
    try {
      SB.cli(d, ['status']);
      seed(d, { phase: 'dev' });
      const abs = path.join(d, 'src', 'b.js');
      fs.writeFileSync(abs, '// x\n');
      const r = SB.hook(d, 'post-write-sync.js', {
        hook_event_name: 'PostToolUse', session_id: 'sTS', tool_name: 'Write',
        tool_input: { file_path: abs }, tool_response: { success: true },
      });
      test('다른 노드 파일 편집이 화면에 알려진다', () => {
        assert.ok(/봉투/.test(r.stdout), r.stdout.slice(-300) || '(출력 없음)');
      });
      test('알림이 complete 를 막는다고 말하지 않는다', () => {
        assert.ok(!/complete 할 수 없게 됩니다/.test(r.stdout), r.stdout.slice(-300));
      });
      test('scopeDrift 에 쌓이지 않는다 (막지 않으므로)', () => {
        const sd = ((SB.readState(d) || {}).cycle || {}).scopeDrift || [];
        assert.ok(!sd.includes('src/b.js'), JSON.stringify(sd));
      });
    } finally { SB.cleanup(d); }
  }
}

// ── [v5/N-07] 검증 도구의 산출물이 게이트를 네 번 만나지 않는다 ─────
//   다운스트림이 브라우저 자동화로 종단간 검증을 하자 `.playwright-mcp/` 가 생겼고,
//   한 사이클에서 게이트를 **네 번** 만났다 — 관측 안 됨 → 사라짐 → 범위 밖 → git 되돌림 차단.
//   **검증을 많이 할수록 사이클을 닫기 어려워진다.** 게이트가 막으려던 것과 반대 방향의
//   행동(자동화 검증 회피)을 유도했고, 실제로 그 선택이 일어났다.
//
//   완화의 경계를 같은 블록에서 지킨다 — **추적되던 파일의 삭제는 그대로 잡혀야 한다.**
console.log('\n═══ TS: 검증 도구 산출물 (v5/N-07) ═══\n');
{
  const SB2 = require('./_sandbox.js');
  const CJ2 = require(path.join(SB2.REPO, '.claude', 'hooks', '_charter.js'));
  const CH_T = `# Charter — t-tool

- **상태**: Approved

\`\`\`yaml
goal: 도구 산출물
budget_cycles: 4
expires: 2099-12-31
invariants:
  - file:exists CLAUDE.md
out_of_scope:
  - .git/**
\`\`\`

## 노드

- [ ] **[N-01]** 가
  - 파일: \`src/a.js\`
`;

  function seedC(d) {
    const at = new Date().toISOString();
    fs.mkdirSync(path.join(d, 'docs', 'charters'), { recursive: true });
    fs.writeFileSync(path.join(d, 'docs', 'charters', 't-tool-charter.md'), CH_T);
    fs.writeFileSync(path.join(d, 'docs', 'prds', 'a-prd.md'),
      '# PRD — 가\n\n- **상태**: Approved\n- **charter**: docs/charters/t-tool-charter.md\n'
      + '- **node**: N-01\n- **scope_files**: `src/a.js`\n');
    fs.writeFileSync(path.join(d, 'docs', 'plans', 'a-prd-plan.md'),
      '# plan\n\n- [x] **[IMPL-01]** 가\n  - 파일: `src/a.js`\n  - 예상 공수: 1h\n');
    fs.appendFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'),
      JSON.stringify({ timestamp: at, event: 'user-approval', target: 'prd', file: 'docs/prds/a-prd.md' }) + '\n');
    SB2.git(d, ['add', '-A']); SB2.git(d, ['commit', '-q', '-m', 'seed']);
    const base = SB2.git(d, ['rev-parse', '--short', 'HEAD']).trim();
    SB2.patchState(d, s => {
      s.phase = 'report'; s.track = 'C';
      s.activePrd = 'docs/prds/a-prd.md'; s.activePlan = 'docs/plans/a-prd-plan.md';
      s.lastApproval = { prd: 'docs/prds/a-prd.md', at, via: 'user' };
      s.charters = { 't-tool': { path: 'docs/charters/t-tool-charter.md', approvedHash: CJ2.contentHash(CH_T), approvedAt: at } };
      s.cycle = {
        startedAt: at, headAtStart: base, touched: [], decisionsAtStart: 0, needsDecision: [], dirtyAtStart: [],
        charter: { id: 't-tool', node: 'N-01', path: 'docs/charters/t-tool-charter.md' },
      };
      return s;
    });
    return base;
  }

  // ── ⓐ 도구 산출물: 만들고 → 흡수하고 → 지우면 **한 번에** 닫힌다 ──
  {
    const d = SB2.makeSandbox({ prefix: 'ts7a' });
    try {
      SB2.cli(d, ['status']); seedC(d);
      fs.writeFileSync(path.join(d, 'src', 'a.js'), '// 실제 작업\n');
      fs.mkdirSync(path.join(d, '.playwright-mcp'), { recursive: true });
      fs.writeFileSync(path.join(d, '.playwright-mcp', 'console-1.log'), 'log\n');
      fs.writeFileSync(path.join(d, '.playwright-mcp', 'page-1.yml'), 'a: 1\n');
      SB2.cli(d, ['touch', 'reconcile', '--all']);
      fs.rmSync(path.join(d, '.playwright-mcp'), { recursive: true, force: true });

      SB2.seedFreshEvidenceFixture(d);
      const r = SB2.cli(d, ['complete']);
      test('도구 산출물을 지운 사이클이 한 번에 닫힌다', () => {
        assert.strictEqual(r.code, 0, r.out.slice(-400));
      });
      test('"사라졌습니다" 로 두 번 부르지 않는다', () => {
        assert.ok(!/사라졌습니다/.test(r.out), r.out.slice(-300));
      });
    } finally { SB2.cleanup(d); }
  }

  // ── ⓑ 관측 공백 메시지가 .gitignore 로 가는 길을 알려 준다 ──────
  {
    const d = SB2.makeSandbox({ prefix: 'ts7b' });
    try {
      SB2.cli(d, ['status']); seedC(d);
      fs.mkdirSync(path.join(d, '.playwright-mcp'), { recursive: true });
      fs.writeFileSync(path.join(d, '.playwright-mcp', 'page-2.yml'), 'a: 1\n');
      SB2.seedFreshEvidenceFixture(d);
      const r = SB2.cli(d, ['complete']);
      test('관측 공백은 여전히 막는다', () => assert.notStrictEqual(r.code, 0, r.out.slice(-200)));
      test('무엇을 하면 되는지 알려 준다 (.gitignore)', () => {
        assert.ok(/\.gitignore/.test(r.out), r.out.slice(-400));
      });
    } finally { SB2.cleanup(d); }
  }

  // ── ⓒ .gitignore 는 봉투 범위 판정에서 항상 허용 ────────────────
  {
    const d = SB2.makeSandbox({ prefix: 'ts7c' });
    try {
      SB2.cli(d, ['status']); seedC(d);
      fs.writeFileSync(path.join(d, 'src', 'a.js'), '// 노드 안 작업\n');
      fs.appendFileSync(path.join(d, '.gitignore'), '.playwright-mcp/\n');
      SB2.cli(d, ['touch', 'reconcile', '--all']);
      SB2.seedFreshEvidenceFixture(d);
      const r = SB2.cli(d, ['complete']);
      test('.gitignore 를 고쳐도 면제 없이 닫힌다', () => assert.strictEqual(r.code, 0, r.out.slice(-400)));
      test('.gitignore 가 범위 밖으로 잡히지 않는다', () => {
        assert.ok(!/범위 밖 변경/.test(r.out), r.out.slice(-300));
      });
    } finally { SB2.cleanup(d); }
  }

  // ── ⓓ 완화의 경계 — **추적되던** 파일의 삭제는 그대로 잡힌다 ────
  //   화해 대상은 "한 번도 추적된 적 없고 지금 흔적도 없는" 경로뿐이다.
  //   추적되던 파일을 지우면 git 이 ` D` 로 잡아 이 경로에 오지 않는다.
  {
    const d = SB2.makeSandbox({ prefix: 'ts7d' });
    try {
      SB2.cli(d, ['status']); seedC(d);
      fs.writeFileSync(path.join(d, 'src', 'a.js'), '// 작업\n');
      SB2.cli(d, ['touch', 'reconcile', '--all']);
      fs.rmSync(path.join(d, 'src', 'a.js'), { force: true });   // 추적되던 파일을 지운다
      SB2.seedFreshEvidenceFixture(d);
      const r = SB2.cli(d, ['complete']);
      test('추적되던 파일의 삭제는 화해되지 않는다', () => {
        const st = SB2.readState(d) || {};
        const t = ((st.cycle || {}).touched || []).map(x => (typeof x === 'string' ? x : x.path));
        assert.ok(t.includes('src/a.js'), `touched 에서 빠지면 안 된다: ${JSON.stringify(t)} · ${r.out.slice(-200)}`);
      });
    } finally { SB2.cleanup(d); }
  }
}

// ── [v7/N-01] 하네스 업그레이드가 남의 사이클을 막지 않는다 ──────────
//   v5/N-05 가 `.claude/**` 를 추적 대상으로 바꾸자, `complete` 의 이탈 검사가 **하네스
//   자신의 파일을 노드의 책임으로** 세기 시작했다. 다운스트림은 손대지도 않은 파일 때문에
//   사이클을 면제 없이 닫을 수 없었다(재현 2/2). 예전에는 gitignore 가 가려 줬을 뿐이다.
//
//   여기서 지키는 것은 세 방향이다 —
//     ⓐ 하네스 설치물의 변경은 **막지 않는다**
//     ⓑ 그러나 **조용하지 않다** — 화면과 감사에 남는다. 면제를 조용히 주면 다음 사고가 숨는다
//     ⓒ 선언되지 않은 **소스**는 **여전히 막힌다** — 완화의 경계이자 진짜 사고가 있는 쪽
//   이 저장소는 자기 훅을 노드에 선언하므로 `vendor` 에 도달하지 않는다(그 순서는
//   test_charter.js 가 지킨다). 여기서 흉내내는 것은 **선언하지 않는 다운스트림**이다.
console.log('\n═══ TS: 하네스 업그레이드와 이탈 검사 (v7/N-01) ═══\n');
{
  const SB3 = require('./_sandbox.js');
  const CH_V = `# Charter — t-vend

- **상태**: Approved

\`\`\`yaml
goal: 하네스 설치물은 노드의 이탈이 아니다
budget_cycles: 4
expires: 2099-12-31
invariants:
  - file:exists CLAUDE.md
out_of_scope:
  - .git/**
\`\`\`

## 노드

- [ ] **[N-01]** 가
  - 파일: \`src/a.js\`
- [ ] **[N-02]** 나
  - 파일: \`src/b.js\`
`;

  // 다운스트림 모양: 노드가 `.claude/` 를 **선언하지 않는다**.
  const SKILL_REL = '.claude/skills/process/SKILL.md';
  function box(prefix) {
    return SB3.makeSandbox({ prefix, files: { [SKILL_REL]: '# process\n\n초기 내용\n' } });
  }

  function seedV(d) {
    const CJ = require(path.join(SB3.REPO, '.claude', 'hooks', '_charter.js'));
    const at = new Date().toISOString();
    fs.writeFileSync(path.join(d, 'docs', 'charters', 't-vend-charter.md'), CH_V);
    fs.writeFileSync(path.join(d, 'docs', 'prds', 'a-prd.md'),
      '# PRD — 가\n\n- **상태**: Approved\n- **charter**: docs/charters/t-vend-charter.md\n'
      + '- **node**: N-01\n- **scope_files**: `src/a.js`\n');
    fs.writeFileSync(path.join(d, 'docs', 'plans', 'a-prd-plan.md'),
      '# plan\n\n- [x] **[IMPL-01]** 가\n  - 파일: `src/a.js`\n  - 예상 공수: 1h\n');
    fs.appendFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'),
      JSON.stringify({ timestamp: at, event: 'user-approval', target: 'prd', file: 'docs/prds/a-prd.md' }) + '\n');
    SB3.git(d, ['add', '-A']); SB3.git(d, ['commit', '-q', '-m', 'seed']);
    const base = SB3.git(d, ['rev-parse', '--short', 'HEAD']).trim();
    SB3.patchState(d, s => {
      s.phase = 'report'; s.track = 'C';
      s.activePrd = 'docs/prds/a-prd.md'; s.activePlan = 'docs/plans/a-prd-plan.md';
      s.lastApproval = { prd: 'docs/prds/a-prd.md', at, via: 'user' };
      s.charters = { 't-vend': { path: 'docs/charters/t-vend-charter.md', approvedHash: CJ.contentHash(CH_V), approvedAt: at } };
      s.cycle = {
        startedAt: new Date(Date.now() - 3600e3).toISOString(), headAtStart: base,
        touched: [], decisionsAtStart: 0, needsDecision: [], dirtyAtStart: [],
        charter: { id: 't-vend', node: 'N-01', path: 'docs/charters/t-vend-charter.md' },
      };
      return s;
    });
    return base;
  }

  // 하네스 업그레이드 흉내 — 훅은 **덧붙여서** 고친다. 덮어쓰면 샌드박스의 CLI 가 죽는다.
  function upgrade(d) {
    fs.appendFileSync(path.join(d, '.claude', 'hooks', '_signals.js'), '\n// upgraded by installer\n');
    fs.writeFileSync(path.join(d, SKILL_REL), '# process\n\n새 하네스가 덮어쓴 내용\n');
  }

  // ── ⓐ 업그레이드한 사이클이 면제 없이 닫힌다 ───────────────────
  {
    const d = box('ts8a');
    try {
      SB3.cli(d, ['status']);
      seedV(d);
      fs.writeFileSync(path.join(d, 'src', 'a.js'), '// 이 사이클의 실제 작업\n');
      upgrade(d);
      SB3.cli(d, ['touch', 'reconcile', '--all']);
      SB3.seedFreshEvidenceFixture(d);
      const r = SB3.cli(d, ['complete']);
      test('하네스 설치물이 바뀌어도 complete 한다', () => {
        assert.strictEqual(r.code, 0, r.out.slice(-500));
      });
      test('거부 사유로 하네스 파일을 지목하지 않는다', () => {
        assert.ok(!/범위 밖 변경/.test(r.out), r.out.slice(-400));
      });

      // ⓑ 막지 않는 것과 모르는 척하는 것은 다르다.
      test('하네스 설치물 변경이 화면에 남는다', () => {
        assert.ok(/하네스 설치물/.test(r.out), r.out.slice(-500) || '(출력 없음)');
      });
      test('바뀐 하네스 파일을 지목한다', () => {
        assert.ok(/_signals\.js/.test(r.out), r.out.slice(-500));
      });
      test('감사에 vendor 이벤트가 남는다', () => {
        const rows = SB3.auditRows(d).filter(x => /vendor/.test(x.event || ''));
        assert.ok(rows.length >= 1, JSON.stringify(SB3.auditRows(d).slice(-6).map(x => x.event)));
      });
    } finally { SB3.cleanup(d); }
  }

  // ── ⓒ 완화의 경계 — 선언되지 않은 소스는 그대로 막힌다 ──────────
  {
    const d = box('ts8b');
    try {
      SB3.cli(d, ['status']);
      seedV(d);
      upgrade(d);
      fs.writeFileSync(path.join(d, 'src', 'z.js'), '// 어느 노드에도 선언되지 않은 소스\n');
      SB3.cli(d, ['touch', 'reconcile', '--all']);
      SB3.seedFreshEvidenceFixture(d);
      const r = SB3.cli(d, ['complete']);
      test('선언되지 않은 소스는 하네스 변경에 섞여도 막힌다', () => {
        assert.notStrictEqual(r.code, 0, r.out.slice(-500));
      });
      test('거부 사유가 그 소스를 지목한다', () => {
        assert.ok(/src\/z\.js/.test(r.out), r.out.slice(-400));
      });
      test('거부 사유가 하네스 파일을 함께 지목하지 않는다', () => {
        // 진짜 하나를 107개 사이에 묻으면 사람이 읽지 않는다.
        const m = /범위 밖 변경[\s\S]{0,400}/.exec(r.out);
        assert.ok(m && !/_signals\.js/.test(m[0]), (m && m[0].slice(0, 300)) || r.out.slice(-300));
      });
    } finally { SB3.cleanup(d); }
  }

  // ── ⓓ 편집 시점에도 같은 판정 ──────────────────────────────────
  {
    const d = box('ts8c');
    try {
      SB3.cli(d, ['status']);
      seedV(d);
      SB3.patchState(d, s => { s.phase = 'dev'; return s; });
      const abs = path.join(d, '.claude', 'hooks', '_signals.js');
      fs.appendFileSync(abs, '\n// upgraded\n');
      const r = SB3.hook(d, 'post-write-sync.js', {
        hook_event_name: 'PostToolUse', session_id: 'sTS8', tool_name: 'Write',
        tool_input: { file_path: abs }, tool_response: { success: true },
      });
      test('하네스 파일 편집이 scopeDrift 에 쌓이지 않는다', () => {
        const sd = ((SB3.readState(d) || {}).cycle || {}).scopeDrift || [];
        assert.ok(!sd.some(x => /_signals\.js/.test(String(x))), JSON.stringify(sd));
      });
      test('편집 시점 문구가 "범위 밖" 이라고 말하지 않는다', () => {
        assert.ok(!/범위 밖입니다/.test(r.stdout), r.stdout.slice(-300));
      });
    } finally { SB3.cleanup(d); }
  }
}

// ── [v12/N-04] touch reconcile 의 기본은 목록만 ─────────────────────────
//   옵션 없는 reconcile 이 사이클 기준선 이후의 새 파일을 **전부** touched 에 넣었다. 주석은 "--mine 으로 명시한 것만" 이라 했다.
//   실사고: 사용자가 다른 도구로 쓴 PRD 가 v11/N-02 사이클 커밋(37247a2)에 들어갔다 — 출력은 있었고 사람이 놓쳤다.
//   사용자 결정 D-2026-09-14-1baa90: 기본은 목록만, 흡수는 --mine <경로> 또는 --all.
//   대조: --mine·--all 은 지금처럼 넣고, 흔적 없이 사라진 내 항목은 기본 모드에서도 빠진다(complete 의 거부 안내가 이 명령을 부른다).
console.log('\n═══ TS: reconcile 기본은 목록만 (v12/N-04) ═══\n');
{
  const SB4 = require('./_sandbox.js');
  const paths = d => ((SB4.readState(d) || {}).cycle || {}).touched || [];
  const tp = d => paths(d).map(x => (typeof x === 'string' ? x : x.path));

  // 봉투 없는 Track C 사이클 — 기준선 커밋 뒤에 새 파일을 셸로 쓴 것처럼 훅 없이 만든다.
  function cycleBox(prefix, touched) {
    const d = SB4.makeSandbox({ prefix });
    SB4.cli(d, ['status']);
    const base = SB4.git(d, ['rev-parse', '--short', 'HEAD']);
    SB4.patchState(d, s => {
      s.phase = 'dev'; s.track = 'C';
      s.cycle = { startedAt: new Date(Date.now() - 3600e3).toISOString(), headAtStart: base,
        touched: touched || [], decisionsAtStart: 0, needsDecision: [], dirtyAtStart: {} };
      return s;
    });
    return d;
  }
  function twoNewFiles(d) {
    fs.writeFileSync(path.join(d, 'src', 'mine.js'), '// 내 파일\n');
    fs.writeFileSync(path.join(d, 'src', 'theirs.js'), '// 다른 세션의 파일\n');
  }

  // ── ⓐ 옵션 없는 reconcile — 보여 주기만 한다 ──
  {
    const d = cycleBox('ts12a');
    try {
      twoNewFiles(d);
      const r = SB4.cli(d, ['touch', 'reconcile']);
      test('기본 reconcile 은 exit 0', () => assert.strictEqual(r.code, 0, r.out.slice(-300)));
      test('기본 reconcile 은 touched 에 아무것도 넣지 않는다', () => {
        const t = tp(d);
        assert.ok(!t.includes('src/mine.js') && !t.includes('src/theirs.js'), JSON.stringify(t));
      });
      test('후보 두 경로를 모두 보여 준다', () => assert.ok(/src\/mine\.js/.test(r.out) && /src\/theirs\.js/.test(r.out), r.out.slice(-400)));
      test('--mine 과 --all 을 안내한다', () => assert.ok(/--mine/.test(r.out) && /--all/.test(r.out), r.out.slice(-400)));
    } finally { SB4.cleanup(d); }
  }

  // ── ⓑ 대조 — --mine 은 지정한 것만, --all 은 전부 ──
  {
    const d = cycleBox('ts12b');
    try {
      twoNewFiles(d);
      const r1 = SB4.cli(d, ['touch', 'reconcile', '--mine', 'src/mine.js']);
      const t1 = tp(d);
      test('--mine 은 지정한 경로만 넣는다', () => assert.ok(t1.includes('src/mine.js') && !t1.includes('src/theirs.js'), `${r1.out.slice(-200)} | ${JSON.stringify(t1)}`));
      const r2 = SB4.cli(d, ['touch', 'reconcile', '--all']);
      const t2 = tp(d);
      test('--all 은 전부 넣는다', () => assert.ok(t2.includes('src/mine.js') && t2.includes('src/theirs.js'), `${r2.out.slice(-200)} | ${JSON.stringify(t2)}`));
    } finally { SB4.cleanup(d); }
  }

  // ── ⓒ 기본 모드에서도 흔적 없이 사라진 내 항목은 빠진다 — 새 파일은 여전히 넣지 않는다 ──
  {
    const d = cycleBox('ts12c', ['src/ghost.js']);
    try {
      fs.writeFileSync(path.join(d, 'src', 'new.js'), '// 새 파일\n');
      const r = SB4.cli(d, ['touch', 'reconcile']);
      const t = tp(d);
      test('사라진 항목은 기본 모드에서도 목록에서 빠진다', () => assert.ok(!t.includes('src/ghost.js'), `${r.out.slice(-200)} | ${JSON.stringify(t)}`));
      test('그때도 새 파일은 흡수하지 않는다', () => assert.ok(!t.includes('src/new.js'), `${r.out.slice(-200)} | ${JSON.stringify(t)}`));
    } finally { SB4.cleanup(d); }
  }

  // ── ⓓ 인자 없는 touch 도 같다 ──
  {
    const d = cycleBox('ts12d');
    try {
      twoNewFiles(d);
      const r = SB4.cli(d, ['touch']);
      test('인자 없는 touch 는 exit 0', () => assert.strictEqual(r.code, 0, r.out.slice(-300)));
      test('인자 없는 touch 도 아무것도 넣지 않는다', () => {
        const t = tp(d);
        assert.ok(!t.includes('src/mine.js') && !t.includes('src/theirs.js'), JSON.stringify(t));
      });
    } finally { SB4.cleanup(d); }
  }
}

// ── [v15/N-01] 하네스가 스스로 쓴 파일은 reconcile 해도 범위 이탈이 아니다 ─────────────
//   HARNESS_OWNED(advance-phase.js:273)는 이 교착을 막으려고 만들어졌는데 필터가 한 곳에만 걸려 있었다.
//   새로 바뀐 파일 검사에는 있고, touch reconcile 의 흡수 경로와 기록된 scopeDrift 재판정에는 없었다.
//   실사고: 러너가 매 검증마다 갱신하는 tests/baseline/assert-counts.json 을 complete 가 "관측 못 함"으로
//   거부했고, 하네스가 시킨 대로 `touch reconcile --mine` 하자 지울 수 없는 scopeDrift 가 생겨 complete 가
//   영구히 막혔다. 하네스가 제시한 다른 선택지(.gitignore 추가)는 추적되는 기준선을 추적 해제하는 것이라 더 나쁘다.
console.log('\n═══ TS: 하네스 소유 파일은 reconcile 해도 이탈이 아니다 (v15/N-01) ═══\n');
{
  const SB5 = require('./_sandbox.js');
  const drift = d => (((SB5.readState(d) || {}).cycle || {}).scopeDrift) || [];
  const touchedOf = d => ((((SB5.readState(d) || {}).cycle || {}).touched) || []).map(x => (typeof x === 'string' ? x : x.path));

  const C5 = require('../../.claude/hooks/_charter.js');
  const CHARTER_ID = 'ts15-owned';
  const CHARTER_REL = `docs/charters/${CHARTER_ID}-charter.md`;
  // N-01 은 src/a.js 만 쥔다 — tests/baseline/ 도 src/mine.js 도 이 노드의 파일이 아니다.
  const CHARTER_TEXT = `# Charter — ${CHARTER_ID}

- **상태**: Approved

## 노드

- [ ] **[N-01]** 픽스처 노드
  - 파일: \`src/a.js\`

## 선언

\`\`\`yaml
goal: 픽스처
budget_cycles: 5
expires: 2099-12-31
invariants: []
out_of_scope: []
\`\`\`
`;

  function charterBox(prefix) {
    const d = SB5.makeSandbox({ prefix });
    SB5.cli(d, ['status']);
    const base = SB5.git(d, ['rev-parse', '--short', 'HEAD']);
    fs.writeFileSync(path.join(d, CHARTER_REL), CHARTER_TEXT);
    SB5.patchState(d, s => {
      s.phase = 'dev'; s.track = 'C';
      s.charters = { ...(s.charters || {}), [CHARTER_ID]: {
        path: CHARTER_REL, approvedHash: C5.contentHash(CHARTER_TEXT), approvedAt: '2026-01-01T00:00:00.000Z' } };
      s.cycle = { startedAt: new Date(Date.now() - 3600e3).toISOString(), headAtStart: base,
        touched: [], decisionsAtStart: 0, needsDecision: [], dirtyAtStart: {},
        charter: { id: CHARTER_ID, node: 'N-01', path: CHARTER_REL } };
      return s;
    });
    return d;
  }

  {
    const d = charterBox('ts15a');
    try {
      fs.mkdirSync(path.join(d, 'tests', 'baseline'), { recursive: true });
      fs.writeFileSync(path.join(d, 'tests', 'baseline', 'assert-counts.json'), '{"files":{}}\n');
      fs.writeFileSync(path.join(d, 'src', 'mine.js'), '// 내 파일\n');
      const r = SB5.cli(d, ['touch', 'reconcile', '--mine', 'tests/baseline/assert-counts.json']);
      test('하네스 소유 파일 reconcile 은 exit 0', () => assert.strictEqual(r.code, 0, r.out.slice(-300)));
      test('러너가 쓴 기준선은 scopeDrift 에 쌓이지 않는다', () => {
        assert.ok(!drift(d).includes('tests/baseline/assert-counts.json'), JSON.stringify(drift(d)));
      });
      // 대조: 진짜 범위 밖 파일은 여전히 scopeDrift 로 잡혀야 한다 — 필터가 전부를 통과시키면 안 된다.
      const r2 = SB5.cli(d, ['touch', 'reconcile', '--mine', 'src/mine.js']);
      test('노드 밖 소스 파일은 여전히 scopeDrift 에 남는다', () => {
        assert.ok(drift(d).includes('src/mine.js'), `${r2.out.slice(-200)} | ${JSON.stringify(drift(d))}`);
      });
      test('두 경로 모두 touched 에는 들어간다', () => {
        const t = touchedOf(d);
        assert.ok(t.includes('tests/baseline/assert-counts.json') && t.includes('src/mine.js'), JSON.stringify(t));
      });
    } finally { SB5.cleanup(d); }
  }
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
