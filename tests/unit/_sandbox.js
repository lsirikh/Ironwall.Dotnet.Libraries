// tests/unit/_sandbox.js — 공용 샌드박스 헬퍼
//
// [v3.6/N-08/IMPL-03] 왜 이 파일이 존재하는가:
//   `test_seed_gate`·`test_loop_ac`·`test_runner_isolation`·`test_task_evidence` 가
//   **같은 60줄을 네 번 복제**하고 있었다. 복제가 나쁜 이유는 중복이 아니라 **드리프트**다 —
//   N-03 이 브랜치 상태 경로를 바꿨을 때 네 사본이 각자 다른 시점에 따라왔다.
//
//   그리고 격리는 `CLAUDE_PROJECT_DIR` 이 아니라 **훅 파일 복사**에서 나온다(N-07 실측):
//   훅 20개는 전부 `path.resolve(__dirname,'..','..')` 로 루트를 계산한다. 그래서 이 헬퍼는
//   훅을 tmpdir 로 복사한다 — env 를 심는 것만으로는 실 저장소를 건드리게 된다.
//
//   파일명이 `_` 로 시작하므로 러너가 테스트로 실행하지 않는다.

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
const HOOKS_SRC = path.join(REPO, '.claude', 'hooks');

const DEFAULT_CLAUDE_MD = `# CLAUDE.md
\`\`\`yaml
project_name: "sbx"
language: "JavaScript"
version: "0.0.1"
test_command: ""
lint_command: ""
max_auto_fix: 3
\`\`\`
`;

function git(dir, args) {
  const r = spawnSync('git', args, { cwd: dir, encoding: 'utf8' });
  if (r.status !== 0) throw new Error(`git ${args.join(' ')}: ${r.stderr}`);
  return (r.stdout || '').trim();
}

// opts: { prefix?, branch?, claudeMd?, files?: {rel: content}, dirs?: string[] }
function makeSandbox(opts) {
  opts = opts || {};
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), (opts.prefix || 'sbx') + '-'));
  fs.mkdirSync(path.join(dir, '.claude', 'hooks'), { recursive: true });
  for (const f of fs.readdirSync(HOOKS_SRC)) {
    if (f.endsWith('.js')) fs.copyFileSync(path.join(HOOKS_SRC, f), path.join(dir, '.claude', 'hooks', f));
  }
  for (const d of ['docs/memory', 'docs/prds', 'docs/plans', 'docs/charters', 'src', ...(opts.dirs || [])]) {
    fs.mkdirSync(path.join(dir, d), { recursive: true });
  }
  fs.writeFileSync(path.join(dir, 'CLAUDE.md'), opts.claudeMd || DEFAULT_CLAUDE_MD);
  fs.writeFileSync(path.join(dir, 'docs', 'memory', 'audit-log.jsonl'), '');
  fs.writeFileSync(path.join(dir, 'docs', 'memory', 'session-context.md'),
    '# 세션 컨텍스트\n\n## 다음 할 일\n\n없음\n\n## 중요 기술 결정 사항\n\n| 날짜 | 결정 | 이유 |\n|------|------|------|\n');
  fs.writeFileSync(path.join(dir, 'CHANGELOG.md'), '# Changelog\n\n<!-- changelog-entries-start -->\n');
  fs.writeFileSync(path.join(dir, 'src', 'a.js'), '// a\n');
  for (const [rel, content] of Object.entries(opts.files || {})) {
    const abs = path.join(dir, rel);
    fs.mkdirSync(path.dirname(abs), { recursive: true });
    fs.writeFileSync(abs, content);
  }
  git(dir, ['init', '-q']);
  git(dir, ['config', 'user.email', 't@t']);
  git(dir, ['config', 'user.name', 't']);
  git(dir, ['config', 'core.autocrlf', 'false']);
  git(dir, ['add', '-A']);
  git(dir, ['commit', '-q', '-m', 'init']);
  git(dir, ['checkout', '-q', '-b', opts.branch || 'feat/sbx']);
  return dir;
}

function cli(dir, args, env) {
  const r = spawnSync(process.execPath, [path.join(dir, '.claude', 'hooks', 'advance-phase.js'), ...args], {
    cwd: dir, encoding: 'utf8', timeout: 60000,
    env: Object.assign({}, process.env, { CLAUDE_PROJECT_DIR: dir }, env || {}),
  });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || ''), stdout: r.stdout || '' };
}

function hook(dir, name, payload, env) {
  const r = spawnSync(process.execPath, [path.join(dir, '.claude', 'hooks', name)], {
    cwd: dir, encoding: 'utf8', timeout: 60000, input: JSON.stringify(payload),
    env: Object.assign({}, process.env, { CLAUDE_PROJECT_DIR: dir }, env || {}),
  });
  return { code: r.status, out: (r.stdout || '') + (r.stderr || ''), stdout: r.stdout || '' };
}

// [v16/N-05] 두 배치를 모두 안다. 세션 소유 상태가 켜지면 상태는 `session-<id>/` 안에 산다.
//   브랜치 파일만 보던 동안에는 켠 순간 `readState` 가 null 이고 `patchState` 가 throw 했다 —
//   샌드박스가 거짓말을 하면 그 위의 모든 테스트가 조용히 빈손으로 통과한다.
function stateFile(dir) {
  const base = path.join(dir, '.claude');
  let d = null;
  try { d = fs.readdirSync(base).find(x => x.startsWith('.branch-')); } catch { /* 아직 없음 */ }
  if (!d) return null;
  const branchFile = path.join(base, d, 'pipeline-state.json');
  // 세션 파일이 있으면 **그것이 상태다.** 둘 다 있으면 세션 쪽이 이긴다(브랜치 파일은 유산).
  try {
    // readdirSync 는 순서를 보장하지 않는다 — 정렬해야 테스트가 결정적이다.
    for (const sd of fs.readdirSync(path.join(base, d)).slice().sort()) {
      if (!sd.startsWith('session-')) continue;
      const f = path.join(base, d, sd, 'pipeline-state.json');
      if (fs.existsSync(f)) return f;
    }
  } catch { /* 세션 디렉터리 없음 — 기본값(OFF)에서 정상 */ }
  return branchFile;
}
function readState(dir) {
  const f = stateFile(dir);
  return f && fs.existsSync(f) ? JSON.parse(fs.readFileSync(f, 'utf8')) : null;
}
function patchState(dir, fn) {
  const f = stateFile(dir);
  if (!f) throw new Error('브랜치 상태가 아직 없다 — cli(dir, ["status"]) 를 먼저 부르세요');
  const s = JSON.parse(fs.readFileSync(f, 'utf8'));
  fs.writeFileSync(f, JSON.stringify(fn(s) || s, null, 2) + '\n');
}
function audit(dir) {
  try { return fs.readFileSync(path.join(dir, 'docs', 'memory', 'audit-log.jsonl'), 'utf8'); } catch { return ''; }
}
function auditRows(dir) {
  return audit(dir).split('\n').filter(l => l.trim().startsWith('{')).map(l => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean);
}
function ledger(dir) {
  try { return fs.readFileSync(path.join(dir, 'docs', 'memory', 'decisions.jsonl'), 'utf8'); } catch { return ''; }
}
function ledgerRows(dir) {
  return ledger(dir).split('\n').filter(l => l.trim().startsWith('{')).map(l => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean);
}
function cleanup(dir) { try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ } }

// Seed only the prerequisite for tests of unrelated completion gates.
// Completion/evidence tests separately exercise missing, stale and failed evidence.
function seedFreshEvidenceFixture(dir) {
  const modulePath = path.join(dir, '.claude', 'hooks', 'advance-phase-harness.js');
  const result = spawnSync(process.execPath, ['-e', 'process.stdout.write(require(process.argv[1]).treeSig())', modulePath], { cwd: dir, encoding: 'utf8' });
  if (result.status !== 0 || !result.stdout.trim() || result.stdout.trim() === 'unknown') throw Error('Fixture tree signature unavailable: ' + result.stderr);
  patchState(dir, s => { s.lastEvidence = { ref: 'unit-fixture', at: new Date().toISOString(), scope: 'full', files: null, treeSig: result.stdout.trim() }; });
}

module.exports = {
  seedFreshEvidenceFixture,
  REPO, HOOKS_SRC, DEFAULT_CLAUDE_MD,
  makeSandbox, cli, hook, git,
  stateFile, readState, patchState,
  audit, auditRows, ledger, ledgerRows,
  cleanup,
};
