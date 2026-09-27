// tests/unit/test_cli_wiring.js
// CLI·훅 배선 통합 — charter harness-v3 / N-08 (FR-05~FR-07 · D4~D8)
//
// 왜 이 파일이 존재하는가:
//   `advance-phase.js` 의 서브커맨드 13개가 **한 번도 spawn 되지 않았다.** 그중
//   `allow-git` 은 N-04 가 만든 게이트 우회로이고, `verify`·`escalate` 는 Loop V 의 CLI
//   진입점인데 순수 함수(`advance-phase-harness.js`)만 검증돼 있었다 —
//   **인자 파싱·상태 쓰기·audit 배선이 통째로 미검증**이었다.
//   순수 로직이 맞아도 CLI 가 상태를 안 쓰면 아무도 모른다.
//
//   그리고 `pre-compact.js` 는 부수효과 단언이 0건이었다. 128줄짜리 훅의 유일한 일
//   (스냅샷 기록)을 확인하는 단언이 없어 스냅샷이 빈 객체가 되어도 초록이었다.
//
//   판정 기준은 **exit code 가 아니라 결과 상태**다 — 상태 파일 필드·원장 행·audit 이벤트.

'use strict';

const fs = require('fs');
const path = require('path');
const SB = require('./_sandbox.js');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 220) : ''}`); }
}

// 테스트가 통과/실패를 고를 수 있는 test_command 를 가진 샌드박스
const PASS_CMD = 'node tools/pass.js';
const FAIL_CMD = 'node tools/fail.js';
// test_command 는 yaml 한 줄이라 따옴표를 품을 수 없다 — 스크립트 파일로 뺀다.
const TOOLS = {
  'tools/pass.js': 'console.log("  ok");\n',
  'tools/fail.js': 'console.log("\\n  ❌ FAIL tests/unit/test_x.js");\nprocess.exit(1);\n',
};
function claudeMd(testCmd) {
  return '# CLAUDE.md\n```yaml\nproject_name: "sbx"\nlanguage: "JavaScript"\nversion: "0.0.1"\n'
    + `test_command: "${testCmd}"\n` + 'lint_command: ""\nmax_auto_fix: 3\n```\n';
}
const PLAN = `# 플랜

## Wave 1

- [ ] **[IMPL-01]** 🟢 첫 태스크
  - 파일: \`src/a.js\`
  - 예상 공수: 1h
- [ ] **[IMPL-02]** 🟢 둘째 태스크
  - 파일: \`src/b.js\`
  - 예상 공수: 1h
  - 의존: IMPL-01
`;
const PRD = '# P PRD — 제목\n\n- **상태**: Approved\n';

console.log('\n═══ CW: CLI·훅 배선 ═══');

// ── CW-01: verify 가 상태를 나른다 (D5) ──────────────────────────────
console.log('\n[CW-01] should_carry_verify_result_into_state');
{
  const d = SB.makeSandbox({ prefix: 'cw1', claudeMd: claudeMd(PASS_CMD), files: TOOLS });
  try {
    SB.cli(d, ['status']);
    const r = SB.cli(d, ['verify']);
    check('verify 가 통과로 끝난다', r.code === 0, r.out);
    const st = SB.readState(d);
    const ev = st && st.lastEvidence;
    check('lastEvidence 가 기록된다', !!ev, JSON.stringify(st && Object.keys(st)));
    check('scope 가 full 이다', !!ev && ev.scope === 'full', JSON.stringify(ev));
    check('treeSig 가 담긴다', !!ev && typeof ev.treeSig === 'string' && ev.treeSig.length > 0, JSON.stringify(ev));
    check('audit 에 loopv-pass 가 남는다', /"event":"loopv-pass"/.test(SB.audit(d)), SB.audit(d).slice(-300));
    check('lastVerify 의 exit 이 0 이다', !!(st.lastVerify && st.lastVerify.exit === 0), JSON.stringify(st.lastVerify));
  } catch (e) { check('CW-01', false, e.message); } finally { SB.cleanup(d); }
}

// ── CW-02: 실패가 Loop V 상태기계를 움직인다 (D5) ────────────────────
console.log('\n[CW-02] should_drive_loop_v_state_machine_on_failure');
{
  const d = SB.makeSandbox({ prefix: 'cw2', claudeMd: claudeMd(FAIL_CMD), files: TOOLS });
  try {
    SB.cli(d, ['status']);
    const r1 = SB.cli(d, ['verify']);
    check('실패는 exit 1 이다', r1.code === 1, r1.out);
    check('loopV 가 FAILING 이 된다', (SB.readState(d).loopV || {}).state === 'FAILING',
      JSON.stringify(SB.readState(d).loopV));

    // 같은 실패 반복 → STUCK
    SB.cli(d, ['verify']);
    const lv = SB.readState(d).loopV || {};
    check('반복 실패가 STUCK 으로 간다', lv.state === 'STUCK', JSON.stringify(lv));

    // STUCK 에서는 재시도가 거부된다
    const r3 = SB.cli(d, ['verify']);
    check('STUCK 에서 verify 가 거부된다', r3.code === 1 && /STUCK|LOOP V/.test(r3.out), r3.out);

    // escalate 가 탈출로다 — 가설 2개 미만은 거부
    const bad = SB.cli(d, ['escalate', '가설 하나뿐']);
    check('가설 1개는 거부된다', bad.code !== 0, bad.out);

    const esc = SB.cli(d, ['escalate', '가설1 무언가; 가설2 다른 무언가']);
    check('가설 2개면 escalate 성공', esc.code === 0, esc.out);
    const st2 = SB.readState(d);
    check('loopV 가 ESCALATED 가 된다', (st2.loopV || {}).state === 'ESCALATED', JSON.stringify(st2.loopV));
    check('가설이 상태에 남는다', ((st2.loopV || {}).hypotheses || []).length === 2, JSON.stringify(st2.loopV));
    check('escalate 가 needsDecision 에 적재된다',
      ((st2.cycle || {}).needsDecision || []).some(x => /escalate/.test(x)),
      JSON.stringify((st2.cycle || {}).needsDecision));
  } catch (e) { check('CW-02', false, e.message); } finally { SB.cleanup(d); }
}

// ── CW-03: allow-git — 게이트 우회로 (D4) ────────────────────────────
// N-04 가 만든 탈출로인데 회귀 테스트가 0건이었다.
console.log('\n[CW-03] should_open_git_revert_with_allow_git');
{
  const d = SB.makeSandbox({ prefix: 'cw3' });
  try {
    SB.cli(d, ['status']);
    SB.patchState(d, s => {
      s.phase = 'dev'; s.track = 'C';
      s.cycle = Object.assign({}, s.cycle, { touched: [{ path: 'src/a.js', by: { session_id: 'sA' }, at: new Date().toISOString() }] });
      return s;
    });
    const gate = cmd => SB.hook(d, 'pre-tool-gate.js', {
      hook_event_name: 'PreToolUse', tool_name: 'Bash', tool_input: { command: cmd },
    }).stdout || '';

    const before = gate('git checkout -- src/a.js');
    check('touched 와 교차하는 되돌림이 차단된다', /"decision":"block"/.test(before), before.slice(0, 200));

    const allow = SB.cli(d, ['allow-git', '--reason', '체크포인트로 되돌린다']);
    check('allow-git 이 성공한다', allow.code === 0, allow.out);
    check('상태에 허용 기록이 남는다', /gitAllowance/.test(JSON.stringify(SB.readState(d))),
      JSON.stringify(SB.readState(d)).slice(0, 300));

    const after = gate('git checkout -- src/a.js');
    check('허용 후에는 통과한다', !/"decision":"block"/.test(after), after.slice(0, 200));
    check('audit 에 허용이 남는다', /git-allowance-granted/.test(SB.audit(d)), SB.audit(d).slice(-300));
  } catch (e) { check('CW-03', false, e.message); } finally { SB.cleanup(d); }
}

// ── CW-04: plan-load · task skip/block 이 상태를 바꾼다 (D6) ─────────
console.log('\n[CW-04] should_wire_plan_load_and_task_transitions');
{
  const d = SB.makeSandbox({
    prefix: 'cw4', claudeMd: claudeMd(PASS_CMD),
    files: Object.assign({ 'docs/plans/p-prd-plan.md': PLAN, 'docs/prds/p-prd.md': PRD }, TOOLS),
  });
  try {
    SB.cli(d, ['status']);
    SB.patchState(d, s => { s.phase = 'dev'; s.track = 'C'; s.activePrd = 'docs/prds/p-prd.md'; return s; });

    const pl = SB.cli(d, ['plan-load', 'docs/plans/p-prd-plan.md']);
    check('plan-load 가 성공한다', pl.code === 0, pl.out);
    check('activePlan 이 상태에 기록된다', SB.readState(d).activePlan === 'docs/plans/p-prd-plan.md',
      String(SB.readState(d).activePlan));

    const start = SB.cli(d, ['task', 'IMPL-01', 'start']);
    check('task start 가 성공한다', start.code === 0, start.out);
    const body1 = fs.readFileSync(path.join(d, 'docs', 'plans', 'p-prd-plan.md'), 'utf8');
    check('plan 체크박스가 [~] 가 된다', /- \[~\] \*\*\[IMPL-01\]\*\*/.test(body1),
      (body1.match(/- \[.\] \*\*\[IMPL-01\]\*\*/) || [''])[0]);
    const t1 = SB.readState(d).tasks || {};
    check('상태에 태스크 레코드가 남는다', !!t1['docs/plans/p-prd-plan.md#IMPL-01'], JSON.stringify(Object.keys(t1)));
    check('audit 에 task-start 가 남는다', /"event":"task-start"/.test(SB.audit(d)), SB.audit(d).slice(-300));

    const skipNoReason = SB.cli(d, ['task', 'IMPL-02', 'skip']);
    check('사유 없는 skip 은 거부된다', skipNoReason.code !== 0, skipNoReason.out);

    const skip = SB.cli(d, ['task', 'IMPL-02', 'skip', '--reason', '이번 사이클 범위 밖']);
    check('사유 있는 skip 은 성공한다', skip.code === 0, skip.out);
    const body2 = fs.readFileSync(path.join(d, 'docs', 'plans', 'p-prd-plan.md'), 'utf8');
    check('plan 에 [-] 와 사유가 남는다', /- \[-\] \*\*\[IMPL-02\]\*\*/.test(body2) && /이번 사이클 범위 밖/.test(body2),
      body2.slice(0, 400));

    const block = SB.cli(d, ['task', 'IMPL-01', 'block', '--reason', '외부 의존 대기']);
    check('block 이 성공한다', block.code === 0, block.out);
    const body3 = fs.readFileSync(path.join(d, 'docs', 'plans', 'p-prd-plan.md'), 'utf8');
    check('plan 에 [!] 가 남는다', /- \[!\] \*\*\[IMPL-01\]\*\*/.test(body3),
      (body3.match(/- \[.\] \*\*\[IMPL-01\]\*\*/) || [''])[0]);
  } catch (e) { check('CW-04', false, e.message); } finally { SB.cleanup(d); }
}

// ── CW-05: pre-compact 의 부수효과 (D8) ──────────────────────────────
// 128줄짜리 훅인데 초록 3건이 전부 exit code 였다.
console.log('\n[CW-05] should_record_compact_snapshot');
{
  const d = SB.makeSandbox({ prefix: 'cw5' });
  try {
    SB.cli(d, ['status']);
    const before = (() => { try { return fs.readFileSync(path.join(d, 'docs', 'memory', 'compact-log.jsonl'), 'utf8'); } catch { return ''; } })();
    const r = SB.hook(d, 'pre-compact.js', { hook_event_name: 'PreCompact', session_id: 'sA', trigger: 'auto' });
    check('훅이 정상 종료한다', r.code === 0, r.out);
    const after = (() => { try { return fs.readFileSync(path.join(d, 'docs', 'memory', 'compact-log.jsonl'), 'utf8'); } catch { return ''; } })();
    check('compact-log 에 행이 늘어난다', after.length > before.length, `before=${before.length} after=${after.length}`);
    const rows = after.split('\n').filter(l => l.trim().startsWith('{')).map(l => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean);
    const last = rows[rows.length - 1];
    check('스냅샷이 빈 객체가 아니다', !!last && Object.keys(last).length >= 3, JSON.stringify(last));
    check('phase 를 담는다', !!last && ('phase' in last || 'state' in last || 'snapshot' in last), JSON.stringify(last && Object.keys(last)));
  } catch (e) { check('CW-05', false, e.message); } finally { SB.cleanup(d); }
}

// ── CW-06: 전 사이클 E2E (D7) ────────────────────────────────────────
// approve prd → plan-load → task start → verify → task done → decide → complete.
// 판정은 exit code 가 아니라 상태·원장·audit·커밋 집합이다.
console.log('\n[CW-06] should_close_a_full_cycle_end_to_end');
{
  const d = SB.makeSandbox({
    prefix: 'cw6', claudeMd: claudeMd(PASS_CMD),
    files: Object.assign({
      'docs/prds/p-prd.md': '# P PRD — 제목\n\n- **상태**: Draft\n',
      'docs/plans/p-prd-plan.md': PLAN,
      // [v10/N-03] 버전 사이트 — complete 의 자동 버전 올림이 고치는 파일
      'README.md': '# Skill-Set v0.0.1\n\n- **버전**: v0.0.1\n',
      'docs/Manual.md': '# 매뉴얼\n\n- **버전**: 0.0.1\n',
    }, TOOLS),
  });
  try {
    SB.cli(d, ['status']);
    SB.git(d, ['add', '-A']); SB.git(d, ['commit', '-q', '-m', 'artifacts']);

    // 1. 승인 — userAck 없이는 거부된다
    SB.patchState(d, s => { s.phase = 'prd'; s.track = 'C'; s.activePrd = 'docs/prds/p-prd.md'; return s; });
    const noAck = SB.cli(d, ['approve', 'prd', '검토함']);
    check('E2E: userAck 없는 승인은 거부된다', noAck.code !== 0, noAck.out);

    // N-02 의 이중 장부 — 상태의 승인 필드는 ±1초 안에 짝이 되는 감사 행을 가져야 한다.
    //   실제 발화 경로가 남기는 이벤트명은 `prd-review-cleared` 다.
    const at = new Date().toISOString();
    fs.appendFileSync(path.join(d, 'docs', 'memory', 'audit-log.jsonl'),
      JSON.stringify({ timestamp: at, event: 'prd-review-cleared', target: 'prd', file: 'docs/prds/p-prd.md', scope: 'docs/prds/p-prd.md' }) + '\n');
    SB.patchState(d, s => { s.userAck = { kind: 'prd-approve', scope: 'docs/prds/p-prd.md', at }; return s; });
    const ap = SB.cli(d, ['approve', 'prd', '검토함']);
    check('E2E: userAck 가 있으면 승인된다', ap.code === 0, ap.out);
    check('E2E: PRD 본문이 Approved 가 된다',
      /- \*\*상태\*\*:\s*Approved/.test(fs.readFileSync(path.join(d, 'docs', 'prds', 'p-prd.md'), 'utf8')));
    check('E2E: lastApproval 이 상태에 남는다',
      (SB.readState(d).lastApproval || {}).prd === 'docs/prds/p-prd.md',
      JSON.stringify(SB.readState(d).lastApproval));

    // 2. plan → dev → 태스크
    SB.cli(d, ['plan']);
    const pl = SB.cli(d, ['plan-load', 'docs/plans/p-prd-plan.md']);
    check('E2E: plan-load 성공', pl.code === 0, pl.out);
    SB.cli(d, ['dev']);
    SB.cli(d, ['task', 'IMPL-01', 'start']);

    // 3. 증거 없이 닫으면 거부된다
    const noEv = SB.cli(d, ['task', 'IMPL-01', 'done']);
    check('E2E: 검증 증거 없이 task done 이 거부된다', noEv.code !== 0, noEv.out);

    // 4. verify 통과 — 그런데 통과 증거만으로는 아직 닫히지 않는다 (v4/N-03 반증 짝)
    const v = SB.cli(d, ['verify']);
    check('E2E: verify 통과', v.code === 0, v.out);

    // [v4/N-03] 통과 증거만 있으면 거부된다 — "테스트가 통과했다" 위에
    //   "그 테스트가 실패할 수 있었다" 가 필요하다.
    const noPair = SB.cli(d, ['task', 'IMPL-01', 'done']);
    check('E2E: 통과 증거만으로는 IMPL 태스크가 닫히지 않는다', noPair.code !== 0, noPair.out.slice(0, 200));
    check('E2E: 거부 메시지가 --expect-fail 을 안내한다', /--expect-fail/.test(noPair.out), noPair.out.slice(0, 200));

    // 실제 Red → Green 을 흉내낸다: 실패하는 test_command 로 Red 를 찍고,
    //   소스를 고친 뒤(지문이 바뀐다) 통과하는 명령으로 되돌려 재검증한다.
    //   Red 는 **고치기 전** 상태에서 찍혀야 하므로 이 순서가 계약 그 자체다.
    const cmdPath = path.join(d, 'CLAUDE.md');
    fs.writeFileSync(cmdPath, claudeMd(FAIL_CMD));
    const red = SB.cli(d, ['verify', '--expect-fail', '--task', 'IMPL-01']);
    check('E2E: Red 기대 검증이 실패를 확인한다', /Red 기대/.test(red.out), red.out.slice(0, 200));
    check('E2E: Red 증거가 태스크별로 남는다',
      !!((SB.readState(d).redEvidence || {})['IMPL-01']),
      JSON.stringify(SB.readState(d).redEvidence));

    fs.appendFileSync(path.join(d, 'src', 'a.js'), '// fixed\n');   // 지문을 바꾼다
    fs.writeFileSync(cmdPath, claudeMd(PASS_CMD));
    const v2 = SB.cli(d, ['verify']);
    check('E2E: Red 이후 재검증 통과', v2.code === 0, v2.out.slice(0, 200));

    // 픽스처가 훅 밖에서 소스를 고쳤으므로 관측을 보정한다 — 실제 개발에서는
    //   PostToolUse 가 자동으로 흡수하지만, 여기서는 정식 보정 경로를 쓴다.
    SB.cli(d, ['touch', 'reconcile', '--all']);   // [v12/N-04] 기본은 목록만 — `--yes` 는 없는 플래그였다
    const done = SB.cli(d, ['task', 'IMPL-01', 'done']);
    check('E2E: 반증 짝이 있으면 task done 이 통과한다', done.code === 0, done.out.slice(0, 300));
    check('E2E: 태스크에 evidenceRef 가 붙는다',
      !!((SB.readState(d).tasks || {})['docs/plans/p-prd-plan.md#IMPL-01'] || {}).evidenceRef,
      JSON.stringify(SB.readState(d).tasks));
    SB.cli(d, ['task', 'IMPL-02', 'skip', '--reason', '범위 밖']);

    // 5. 원장 없이 닫으려 하면 — 트리거가 있을 때만 거부된다
    SB.patchState(d, s => {
      s.cycle = Object.assign({}, s.cycle, { needsDecision: ['backward:report→dev'] });
      return s;
    });
    const noLedger = SB.cli(d, ['complete']);
    check('E2E: 트리거가 있는데 원장이 없으면 complete 거부', noLedger.code !== 0, noLedger.out);

    const dec = SB.cli(d, ['decide', '--json', JSON.stringify({
      decision: '이 사이클의 판단을 적는다', reason: '게이트가 요구했다',
      rejected: ['그냥 넘어가기'], invalidation: { text: '되돌아오면 회귀' }, files: ['src/a.js'],
    })]);
    check('E2E: decide 성공', dec.code === 0, dec.out);
    check('E2E: 모델 결정은 agent 출처로 기록된다',
      SB.ledgerRows(d).some(r => r.source === 'agent'), JSON.stringify(SB.ledgerRows(d).map(r => r.source)));

    // [v10/N-03] complete 직전 — 사이클과 무관한 스테이징 하나 · 사용자 편집으로 dirty 한 버전 사이트 하나.
    //   README.md 는 깨끗하다: 버전 올림이 고치면 그것은 complete 가 한 일이다.
    //   무관한 파일을 **문서**로 두는 이유: 관측되지 않은 코드 파일이면 관측 공백 게이트가 complete 자체를 막는다(그건 다른 계약이다).
    //   인덱스를 휩쓴 실사고도 게이트가 코드로 세지 않는 파일(v8 의 릴리스 zip)이었다.
    fs.mkdirSync(path.join(d, 'docs', 'notes'), { recursive: true });
    fs.writeFileSync(path.join(d, 'docs', 'notes', 'unrelated.md'), '다른 작업\n');
    SB.git(d, ['add', 'docs/notes/unrelated.md']);
    fs.appendFileSync(path.join(d, 'docs', 'Manual.md'), '\n사용자가 쓰던 문단\n');

    // 6. complete
    const comp = SB.cli(d, ['complete']);
    check('E2E: complete 통과', comp.code === 0, comp.out);
    check('E2E: phase 가 complete 다', SB.readState(d).phase === 'complete', String(SB.readState(d).phase));
    check('E2E: 사이클 커밋이 만들어진다', /사이클 커밋/.test(comp.out), comp.out.slice(-400));

    // [v10/N-03] 커밋 집합과 화면이 실제로 일어난 일과 같다
    const headFiles = SB.git(d, ['show', '--name-only', '--format=', 'HEAD']).split('\n').map(x => x.trim()).filter(Boolean);
    const warnLines = (comp.out.match(/⚠[^\n]*/g) || []).join(' | ');
    check('E2E: 미리 스테이징된 무관한 파일은 사이클 커밋에 없다', !headFiles.includes('docs/notes/unrelated.md'), headFiles.join(', '));
    check('E2E: 그 파일은 스테이징된 채 남는다',
      SB.git(d, ['diff', '--cached', '--name-only']).split('\n').map(x => x.trim()).includes('docs/notes/unrelated.md'), SB.git(d, ['status', '--short']));
    check('E2E: 화면이 경로 밖 스테이징을 그 파일 이름과 함께 따로 말한다',
      /스테이징된 파일 1개는 이 사이클 밖[^\n]*docs\/notes\/unrelated\.md/.test(comp.out), warnLines);
    check('E2E: "흡수하지 않음" 줄에 그 파일이 섞이지 않는다', !/흡수하지 않음[^\n]*docs\/notes\/unrelated\.md/.test(comp.out), warnLines);
    check('E2E: 버전 올림이 일어났다 (아래 두 단언의 전제)', /CHANGELOG: \S+ → \S+/.test(comp.out), (comp.out.match(/CHANGELOG[^\n]*/) || ['(CHANGELOG 줄 없음)'])[0]);
    check('E2E: 올림 직전 깨끗했던 README.md 는 사이클 커밋에 들어간다', headFiles.includes('README.md'), headFiles.join(', '));
    check('E2E: 사용자 편집이 있던 docs/Manual.md 는 커밋에 없고 이유가 나온다',
      !headFiles.includes('docs/Manual.md') && /버전 올림이 docs\/Manual\.md 를 고쳤지만/.test(comp.out), warnLines);
    check('E2E: audit 에 phase-transition 이 남는다',
      SB.auditRows(d).some(r => /transition|phase/.test(String(r.event))),
      JSON.stringify(SB.auditRows(d).map(r => r.event).slice(-8)));

    // 7. 산출물 문서가 커밋에 들어갔는가 (N-07 이 고친 것)
    const tracked = SB.git(d, ['ls-files', 'docs/plans/p-prd-plan.md']);
    check('E2E: plan 이 추적된다', tracked.includes('p-prd-plan.md'), tracked);
  } catch (e) { check('CW-06', false, e.message); } finally { SB.cleanup(d); }
}

// ── CW-20: decide --json-file 은 **무엇을 읽었는지** 말한다 ──────────
//   다운스트림 보고(2026-09-09): Windows + Git Bash 에서 POSIX 경로가 도구마다 다르게
//   풀린다. `/tmp/dec.json` 이 인자로는 `C:\Users\…\Temp\dec.json` 이 되고 스크립트
//   안쪽 문자열로는 `C:\tmp\dec.json` 이 된다. 후자에 **완전하고 유효한** 옛 결정 파일이
//   있으면 게이트를 통과하고 **어제의 결정이 오늘 사이클의 행으로 기록된다.**
//
//   원장의 존재 이유는 "다음 세션이 같은 논의를 반복하지 않게" 하는 것이고, 그 전제는
//   각 행이 **그 사이클에 실제로 내려진 결정**이라는 것이다. 경로가 조용히 어긋나면
//   그 전제가 깨지는데, 그런 행은 문법이 완벽해서 눈에 띄지도 않는다.
//
//   막을 일은 아니다 — 미리 써 둔 결정 파일을 쓰는 정당한 흐름이 있다.
//   **말해 주면 된다.** 무엇을 읽었고 그것이 언제 쓰인 것인지.
console.log('\n[CW-20] should_report_which_file_decide_read');
{
  const os = require('os');
  const d = SB.makeSandbox({ prefix: 'cw20' });
  const outside = fs.mkdtempSync(path.join(os.tmpdir(), 'cw20-src-'));
  try {
    SB.cli(d, ['status']);
    const DEC = {
      decision: '판정을 실측으로 옮긴다',
      reason: '이름표가 아니라 관측된 사실로 판정해야 두 곳의 답이 어긋나지 않는다',
      rejected: ['목록을 늘린다 — 열두 번째 항목을 또 잊는다'],
      invalidation: { text: '판정이 두 벌이 되면 되돌아온다', specs: ['file:exists CLAUDE.md'] },
      files: ['src/a.js'],
    };
    const good = path.join(outside, 'decision.json');
    fs.writeFileSync(good, JSON.stringify(DEC));

    const r = SB.cli(d, ['decide', '--json-file', good]);
    check('기록에 성공한다', r.code === 0, r.out.slice(-300));
    check('읽은 파일의 경로를 출력한다', /원본:/.test(r.out), r.out.slice(-300));
    const norm = s => String(s).replace(/\\/g, '/').toLowerCase();
    check('그 경로가 실제로 읽은 절대 경로다',
      norm(r.out).includes(norm(path.resolve(good))), r.out.slice(-300));

    // 거절될 때도 경로를 말해야 한다 — 내 파일이 잘못된 건지 다른 파일을 읽은 건지
    //   구분할 방법이 없으면 왕복이 늘어난다(리포트가 "대여섯 번" 이라 적었다).
    const bad = path.join(outside, 'broken.json');
    fs.writeFileSync(bad, JSON.stringify({ decision: 'x' }));
    const rb = SB.cli(d, ['decide', '--json-file', bad]);
    check('거절된다', rb.code !== 0, rb.out.slice(-200));
    check('거절 메시지도 경로를 말한다', norm(rb.out).includes(norm(path.resolve(bad))), rb.out.slice(-300));

    // 이 사이클보다 오래된 파일이면 경고한다. **막지는 않는다** —
    //   미리 써 둔 결정 파일을 쓰는 흐름은 정당하다.
    const old = path.join(outside, 'old.json');
    fs.writeFileSync(old, JSON.stringify(Object.assign({}, DEC, { decision: '어제의 결정' })));
    const past = new Date(Date.now() - 36 * 3600e3);
    fs.utimesSync(old, past, past);
    SB.patchState(d, s => {
      s.cycle = Object.assign({}, s.cycle, { startedAt: new Date(Date.now() - 3600e3).toISOString() });
      return s;
    });
    const ro = SB.cli(d, ['decide', '--json-file', old]);
    check('오래된 파일이어도 기록은 된다 (막지 않는다)', ro.code === 0, ro.out.slice(-300));
    check('사이클보다 오래된 파일이면 경고한다', /오래|이전에 쓰인|⚠/.test(ro.out), ro.out.slice(-300));
  } finally {
    SB.cleanup(d);
    try { fs.rmSync(outside, { recursive: true, force: true }); } catch {}
  }
}

// ── CW-21: charter 승인이 잘림을 말하되 **막지 않는다** (v6/N-02) ───
//   잘림은 문법상 유효하다 — 거부할 근거가 없다. 그러나 침묵하면 사용자가 승인한 범위와
//   하네스가 판정하는 범위가 어긋난 채 PRD 승인까지 가고, 그 사이 계획이 잘못된 범위 위에
//   세워진다. **경고의 값어치는 시점이다.**
//
//   여기서 두 방향을 같은 블록에서 지킨다 — 말하는가, 그리고 **통과하는가.**
console.log('\n[CW-21] should_warn_about_truncation_without_blocking');
{
  const os2 = require('os');
  const mk = filesLine => `# Charter — t-warn

- **상태**: Draft

\`\`\`yaml
goal: 경고
budget_cycles: 2
expires: 2099-12-31
invariants:
  - file:exists CLAUDE.md
out_of_scope:
  - .git/**
\`\`\`

## 노드

- [ ] **[N-01]** 가
${filesLine}
`;

  const run = (label, filesLine, wantWarn) => {
    const d = SB.makeSandbox({ prefix: 'cw21' });
    try {
      SB.cli(d, ['status']);
      const rel = 'docs/charters/t-warn-charter.md';
      fs.mkdirSync(path.join(d, 'docs', 'charters'), { recursive: true });
      fs.writeFileSync(path.join(d, rel), mk(filesLine));
      const say = SB.hook(d, 'session-gate.js', {
        hook_event_name: 'UserPromptSubmit', session_id: 'sCW21', prompt: '봉투 승인',
      });
      const r = SB.cli(d, ['charter', 'approve', '--charter', rel]);
      check(`${label}: 승인이 통과한다 (경고는 막지 않는다)`, r.code === 0,
        (r.out + ' | ' + say.out).slice(-300));
      const warned = /쉼표로 끝납니다/.test(r.out);
      check(`${label}: 경고 ${wantWarn ? '있음' : '없음'}`, warned === wantWarn, r.out.slice(-300));
    } finally { SB.cleanup(d); }
  };

  // 빈 줄이 끼어 연속으로 인정되지 않는다 → 쉼표로 끝난 채 남는다
  run('잘린 목록', '  - 파일: `src/a.js`, `src/b.js`,\n\n    `src/c.js`', true);
  // 정상 두 형태 — 오탐이 잦으면 경고는 읽히지 않는다
  run('한 줄 목록', '  - 파일: `src/a.js`, `src/b.js`', false);
  run('이어 붙은 여러 줄', '  - 파일: `src/a.js`,\n    `src/b.js`', false);
}


// ── CW-N24: 좁힌 검증과 락 판정 (v16/N-24) ───────────────────────────
// 왜: 2026-09-17~18 실측에서 사이클당 60~90분 중 **전체 verify 7분41초 × 3~4회 = 25~30분**이
//   최대 비용이었다. 좁힌 검증은 3,900배 빠른데(test_charter.js 119ms vs 461,000ms)
//   **쓸 수가 없었다** — 필터 토큰을 파일로 해석할 때 `.js` 확장자를 안 붙이고 existsSync 해서
//   항상 실패했고, 그러면 ev.files 가 null 이 되어 evidenceCovers 가 "전부 누락" 으로 읽는다.
//   러너(tests/run.js)의 실제 매칭은 `f.includes(tok)` 로 **확장자와 무관**하다.
//   같은 규칙이 두 파일에 따로 구현돼 있다가 한쪽이 어긋난 것이다 — 규칙을 한 벌로 모은다.
console.log('\n[CW-N24] should_resolve_filter_like_the_runner_does');
{
  const H = require(path.resolve(__dirname, '..', '..', '.claude', 'hooks', 'advance-phase-harness.js'));
  const MISSING = { __missingFn: true };   // 함수가 없으면 대조군까지 정직하게 깨진다
  const UNITS = [
    'test_charter.js', 'test_signals.js', 'test_cli_wiring.js',
    'learning.test.js', 'test_learning.js', '_sandbox.js',
  ];
  const R = (f) => (typeof H.resolveFilterFiles !== 'function' ? MISSING : H.resolveFilterFiles(f, UNITS));

  check('advance-phase-harness 가 resolveFilterFiles 를 export 한다',
    typeof H.resolveFilterFiles === 'function', Object.keys(H).join(','));

  // Red — 오늘 실제로 막힌 형태
  check('확장자 없는 토큰이 실재 파일로 해석된다',
    JSON.stringify(R('test_charter')) === JSON.stringify(['tests/unit/test_charter.js']),
    JSON.stringify(R('test_charter')));

  check('대조군: 확장자 있는 토큰도 그대로 해석된다',
    JSON.stringify(R('test_charter.js')) === JSON.stringify(['tests/unit/test_charter.js']),
    JSON.stringify(R('test_charter.js')));

  // Red — 러너는 부분 문자열이라 한 토큰이 여러 파일을 돌린다. 증거도 그만큼 담아야 한다.
  check('여러 파일에 매치되는 토큰은 전부 담긴다 — 좁게 잡아 증거를 부풀리지 않는다',
    JSON.stringify(R('learning')) === JSON.stringify(['tests/unit/learning.test.js', 'tests/unit/test_learning.js']),
    JSON.stringify(R('learning')));

  check('대조군: 실재하지 않는 토큰은 해석 실패다 (전수 요구로 안전하게 떨어진다)',
    R('test_nope') === null, JSON.stringify(R('test_nope')));

  check('대조군: 토큰이 여럿이면 합집합이다',
    JSON.stringify(R('test_charter test_signals')) ===
      JSON.stringify(['tests/unit/test_charter.js', 'tests/unit/test_signals.js']),
    JSON.stringify(R('test_charter test_signals')));

  check('대조군: 하나라도 해석 못 하면 전체가 실패다 — 부분 증거로 태스크를 닫으면 안 된다',
    R('test_charter test_nope') === null, JSON.stringify(R('test_charter test_nope')));

  check('대조군: 러너가 건너뛰는 _ 접두 파일은 매치하지 않는다',
    R('_sandbox') === null, JSON.stringify(R('_sandbox')));

  check('대조군: 빈 필터는 해석 대상이 아니다', R('') === null && R(null) === null, '');
}

// ── CW-N24b: 락 판정은 이미 옳다 — 흔들리지 않게 고정한다 ────────────
// 2026-09-18 실측으로 내 주장 절반이 틀렸음을 확인했다. "죽은 PID 락을 15분 기다린다" 고
//   적었으나 verifyLockState 는 이미 dead-pid 를 즉시 stale 로 판정한다. 21분을 기다린 것은
//   **내가 락 파일 존재를 폴링한 탓**이다 — 회수는 지연 회수라 누군가 verify 를 시도해야 한다.
//   그래서 회수 규칙은 건드리지 않고, 아래 세 판정을 대조군으로 못박아 표시를 붙이다가
//   규칙이 흔들리지 않게 한다.
console.log('\n[CW-N24b] should_keep_verify_lock_judgment_unchanged');
{
  const C = require(path.resolve(__dirname, '..', '..', '.claude', 'hooks', '_common.js'));
  const now = Date.now();
  const iso = ms => new Date(ms).toISOString();

  check('대조군: 죽은 PID 는 나이와 무관하게 즉시 stale 이다',
    C.verifyLockState({ pid: 999999, acquired_at: iso(now) }, now).reason === 'dead-pid',
    JSON.stringify(C.verifyLockState({ pid: 999999, acquired_at: iso(now) }, now)));

  check('대조군: 살아 있는 PID 의 최근 락은 보존된다 — 남의 검증을 죽이면 안 된다',
    C.verifyLockState({ pid: process.pid, acquired_at: iso(now) }, now).stale === false,
    JSON.stringify(C.verifyLockState({ pid: process.pid, acquired_at: iso(now) }, now)));

  check('대조군: 살아 있어도 나이를 넘기면 stale 이다',
    C.verifyLockState({ pid: process.pid, acquired_at: iso(now - 20 * 60000) }, now).reason === 'age',
    JSON.stringify(C.verifyLockState({ pid: process.pid, acquired_at: iso(now - 20 * 60000) }, now)));

  check('대조군: 읽을 수 없는 락은 stale 이다',
    C.verifyLockState(null, now).reason === 'unreadable', '');
}



console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
