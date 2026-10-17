// tests/unit/test_banner_ops.js
// 배너·운용 위생 — charter harness-v3 / N-09 (FR-01~FR-06 · D1~D8)
//
// 왜 이 파일이 존재하는가:
//   앞선 사이클들이 **실제로 걸린** 운용 결함 셋이 있다.
//   ① N-08 이 죽은 스크립트 7개를 지우자 `complete` 가 `git add: pathspec did not match
//      any files` 로 끝났다 — 상태는 complete 인데 커밋이 없었다. 파일을 지우는 모든
//      사이클이 같은 벽에 걸린다.
//   ② `test_timeout_ms` 기본값 60초 < 전수 실행 108.6초 — 환경 변수 없이 `verify` 를
//      돌리면 매번 INFRA 다. Loop V 가 첫날부터 죽는다.
//   ③ 배너 푸터가 "session-context.md 에 한 줄" 이라고 지시하는데, CLAUDE.md 는 그 파일이
//      하네스가 렌더링하는 파생 투영이고 **손으로 고치면 덮어쓴다**고 말한다.

'use strict';

const fs = require('fs');
const path = require('path');
const SB = require('./_sandbox.js');

const REPO = path.resolve(__dirname, '..', '..');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 220) : ''}`); }
}

console.log('\n═══ BO: 배너·운용 위생 ═══');

// ── BO-01: 삭제된 파일이 든 사이클이 커밋된다 (D1/D2) ────────────────
console.log('\n[BO-01] should_commit_cycle_that_deletes_files');
{
  const d = SB.makeSandbox({
    prefix: 'bo1',
    files: {
      'docs/prds/p-prd.md': '# P PRD — 제목\n\n- **상태**: Approved\n',
      'docs/plans/p-prd-plan.md': '# 플랜\n\n- [x] **[IMPL-01]** 하나\n  - 파일: `src/a.js`\n  - 예상 공수: 1h\n',
      'src/doomed.js': '// 곧 지워진다\n',
    },
  });
  try {
    SB.cli(d, ['status']);
    SB.git(d, ['add', '-A']); SB.git(d, ['commit', '-q', '-m', 'artifacts']);
    const base = SB.git(d, ['rev-parse', '--short', 'HEAD']);

    // 파일을 지운다 — N-08 이 실제로 한 일이다
    fs.unlinkSync(path.join(d, 'src', 'doomed.js'));
    fs.writeFileSync(path.join(d, 'src', 'a.js'), '// 고쳐졌다\n');

    const at = new Date(Date.now() - 60_000).toISOString();
    fs.appendFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'),
      JSON.stringify({ timestamp: at, event: 'user-approval', target: 'prd', file: 'docs/prds/p-prd.md' }) + '\n');
    SB.patchState(d, s => {
      s.phase = 'report'; s.track = 'C';
      s.activePrd = 'docs/prds/p-prd.md'; s.activePlan = 'docs/plans/p-prd-plan.md';
      s.lastApproval = { prd: 'docs/prds/p-prd.md', at, via: 'user' };
      s.cycle = {
        startedAt: new Date(Date.now() - 3600_000).toISOString(), headAtStart: base,
        touched: ['src/a.js', 'src/doomed.js'], touchedDocs: [], decisionsAtStart: 0, needsDecision: [],
      };
      return s;
    });

    SB.seedFreshEvidenceFixture(d);
    const r = SB.cli(d, ['complete']);
    check('complete 가 성공한다', r.code === 0, r.out);
    check('커밋 실패 경고가 없다', !/커밋 실패/.test(r.out), r.out.slice(-400));
    check('새 커밋이 만들어진다', SB.git(d, ['rev-parse', '--short', 'HEAD']) !== base,
      SB.git(d, ['log', '--oneline', '-2']));
    const show = SB.git(d, ['show', '--name-status', '--format=', 'HEAD']);
    check('삭제가 커밋에 담긴다', /^D\s+src\/doomed\.js/m.test(show), show);
    check('수정도 함께 담긴다', /^M\s+src\/a\.js/m.test(show), show);
  } catch (e) { check('BO-01', false, e.message); } finally { SB.cleanup(d); }
}

// ── BO-02: 커밋 실패는 audit 에 남는다 (D2) ──────────────────────────
// 지금은 경고 한 줄로 흘러가서, 나중에 "왜 이 사이클은 커밋이 없나" 를 알 수 없다.
console.log('\n[BO-02] should_audit_commit_failure');
{
  const src = fs.readFileSync(path.join(REPO, '.claude', 'hooks', 'advance-phase.js'), 'utf8');
  check('커밋 실패에 audit 이벤트가 있다', /cycle-commit-failed/.test(src),
    (src.match(/사이클 커밋 실패[^\n]*/g) || []).join(' | ').slice(0, 200));
}

// ── BO-03: 기본 테스트 타임아웃 (D3) ────────────────────────────────
console.log('\n[BO-03] should_default_test_timeout_above_actual_runtime');
{
  const cfg = require(path.join(REPO, '.claude', 'hooks', '_config.js'));
  const t = cfg.getConfigValue('thresholds.test_timeout_ms', 0);
  // 전수 실행 실측 108.6초. 기본값이 그보다 작으면 verify 가 매번 INFRA 로 끝난다.
  check('기본 타임아웃이 실측 실행시간보다 크다', t >= 180000, `현재 ${t}ms`);
}

// ── BO-04: test_command 미설정 알림 (D4/D5) ──────────────────────────
console.log('\n[BO-04] should_surface_missing_test_command');
{
  const d = SB.makeSandbox({ prefix: 'bo4' });   // 기본 CLAUDE.md 는 test_command: ""
  try {
    const st = SB.cli(d, ['status']);
    check('status 가 미설정을 알린다', /test_command|수동 테스트/.test(st.out), st.out.slice(0, 600));
    check('무엇을 하면 되는지 안내한다', /CLAUDE\.md|설정/.test(st.out), st.out.slice(0, 600));

    // 설정하면 사라진다
    fs.writeFileSync(path.join(d, 'CLAUDE.md'),
      SB.DEFAULT_CLAUDE_MD.replace('test_command: ""', 'test_command: "node tools/pass.js"'));
    const st2 = SB.cli(d, ['status']);
    check('설정하면 알림이 사라진다', !/수동 테스트 모드/.test(st2.out), st2.out.slice(0, 600));
  } catch (e) { check('BO-04', false, e.message); } finally { SB.cleanup(d); }
}

// ── BO-05: 배너 푸터가 CLAUDE.md 와 일치한다 (D6) ────────────────────
// 매 턴 손으로 고치라고 지시하고, 고치면 덮어쓰는 상태였다.
console.log('\n[BO-05] should_not_instruct_hand_editing_derived_file');
{
  const src = fs.readFileSync(path.join(REPO, '.claude', 'hooks', 'session-gate.js'), 'utf8');
  const footer = (src.match(/▶ 응답 후:[^\n]*/g) || []).join(' | ');
  check('푸터가 session-context.md 손편집을 지시하지 않는다',
    !/응답 후[^\n]*session-context\.md[^\n]*한 줄/.test(src), footer);
  check('푸터가 decide CLI 를 안내한다', /응답 후[^\n]*decide/.test(src), footer);
}

// ── BO-06: decide 직후 결정 표가 갱신된다 (D7) ───────────────────────
console.log('\n[BO-06] should_render_decision_table_right_after_decide');
{
  const d = SB.makeSandbox({ prefix: 'bo6' });
  try {
    SB.cli(d, ['status']);
    const before = fs.readFileSync(path.join(d, 'docs', 'memory', 'session-context.md'), 'utf8');
    const r = SB.cli(d, ['decide', '--json', JSON.stringify({
      decision: '배너가 말하는 것을 사실로 만든다', reason: '푸터가 CLAUDE.md 와 모순됐다',
      rejected: ['그대로 두기'], invalidation: { text: '다시 손편집을 지시하면 회귀' }, files: ['src/a.js'],
    })]);
    check('decide 가 성공한다', r.code === 0, r.out);
    const after = fs.readFileSync(path.join(d, 'docs', 'memory', 'session-context.md'), 'utf8');
    check('session-context 의 결정 표가 갱신된다', after !== before && /배너가 말하는 것을 사실로/.test(after),
      after.slice(-400));
    check('decide 출력이 렌더링을 알린다', /결정 표/.test(r.out), r.out.slice(-300));
  } catch (e) { check('BO-06', false, e.message); } finally { SB.cleanup(d); }
}

// ── BO-07: 박스 폭이 모든 줄에서 같다 (D8) ───────────────────────────
// 한국어는 표시 폭이 2다 — `.length` 로 자르면 더 어긋난다.
console.log('\n[BO-07] should_pad_every_banner_line_to_same_display_width');
{
  const gateSrc = fs.readFileSync(path.join(REPO, '.claude', 'hooks', 'session-gate.js'), 'utf8');
  check('표시 폭 계산 함수가 있다', /function\s+displayWidth|const\s+displayWidth/.test(gateSrc),
    (gateSrc.match(/displayWidth[^\n]*/g) || []).slice(0, 2).join(' | '));
  check('박스 줄을 맞추는 함수가 있다', /function\s+boxLine|const\s+boxLine/.test(gateSrc),
    (gateSrc.match(/boxLine[^\n]*/g) || []).slice(0, 2).join(' | '));

  // 실제 배너를 뽑아 폭을 잰다
  const d = SB.makeSandbox({ prefix: 'bo7' });
  try {
    SB.cli(d, ['status']);
    const r = SB.hook(d, 'session-gate.js', {
      hook_event_name: 'UserPromptSubmit', session_id: 'sBO', prompt: '테스트 프롬프트',
    });
    const lines = (r.stdout || '').split('\n').filter(l => l.startsWith('║'));
    check('배너가 출력된다', lines.length > 3, String(lines.length));
    if (lines.length > 3) {
      // session-gate 를 require 하면 훅 본문이 실행된다 — 폭 계산은 여기서 재현한다.
      const W = s => [...s].reduce((n, c) => {
        const cp = c.codePointAt(0);
        const wide = (cp >= 0x1100 && cp <= 0x115F) || (cp >= 0x2E80 && cp <= 0xA4CF)
          || (cp >= 0xAC00 && cp <= 0xD7A3) || (cp >= 0xF900 && cp <= 0xFAFF)
          || (cp >= 0xFE30 && cp <= 0xFE6F) || (cp >= 0xFF00 && cp <= 0xFF60)
          || (cp >= 0xFFE0 && cp <= 0xFFE6) || (cp >= 0x1F300 && cp <= 0x1FAFF)
          || (cp >= 0x2600 && cp <= 0x27BF);
        return n + (wide ? 2 : 1);
      }, 0);
      const widths = lines.map(l => W(l));
      const uniq = [...new Set(widths)];
      check('모든 박스 줄의 표시 폭이 같다', uniq.length === 1,
        `폭 ${uniq.join(',')} · 예: ${lines.filter((l, i) => widths[i] !== widths[0]).slice(0, 2).join(' ⏐ ')}`);
    }
  } catch (e) { check('BO-07', false, e.message); } finally { SB.cleanup(d); }
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
