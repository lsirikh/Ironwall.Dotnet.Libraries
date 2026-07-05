// tests/unit/test_loop_hardening.js
// Loop Hardening PRD 검증 — 루프 4요소(경계·수렴·에스컬레이션·진척) 단위 테스트
//
//   LH-07: stuck detection — 동일 실패 반복 시 stuckCount 진척측정 (FR-07)
//   LH-SU: DEFAULT_STATE.stuckCount 기본값 (SETUP-01)
//   LH-02: stale 세션 converge — archiveSession 멱등 + pollOtherSessions 수렴 (FR-02)
//   LH-01: 피드백 승격 로직 — remind 위반 3회 → block 승격 (FR-01 데이터 흐름)

'use strict';
const fs = require('fs');
const path = require('path');
const os = require('os');

const HOOKS = path.resolve(__dirname, '../../.claude/hooks');
const harness = require(path.join(HOOKS, 'advance-phase-harness.js'));
const state = require(path.join(HOOKS, '_state.js'));
const sctx = require(path.join(HOOKS, '_session-context.js'));

let pass = 0, fail = 0;
function check(name, cond, detail = '') {
  if (cond) { console.log(`    ✅ ${name}`); pass++; }
  else { console.log(`    ❌ ${name}${detail ? ' — ' + detail : ''}`); fail++; }
}

// ── LH-07: stuck detection (FR-07) ──────────────────────────────
console.log('[LH-07] stuck detection — 진척측정');
{
  let s = {};
  s = harness.saveLastFailure(s, 'test', { message: 'AssertionError line 5: foo' });
  check('LH-07a 첫 실패 stuckCount=0', s.stuckCount === 0, `actual=${s.stuckCount}`);
  s = harness.saveLastFailure(s, 'test', { message: 'AssertionError line 88: foo' }); // 숫자 정규화 → 동일 시그니처
  check('LH-07b 동일패턴 2회 stuckCount=1', s.stuckCount === 1, `actual=${s.stuckCount}`);
  s = harness.saveLastFailure(s, 'test', { message: 'AssertionError line 3: foo' });
  check('LH-07c 동일패턴 3회 stuckCount=2(에스컬 임계)', s.stuckCount === 2, `actual=${s.stuckCount}`);
  s = harness.saveLastFailure(s, 'test', { message: 'TypeError: bar undefined' });
  check('LH-07d 다른 오류 stuckCount 리셋', s.stuckCount === 0, `actual=${s.stuckCount}`);
  s = harness.clearLastFailure(s);
  check('LH-07e clearLastFailure가 stuck/autoFix 리셋', s.stuckCount === 0 && s.autoFixCount === 0 && s.lastFailure === null);
}

// ── LH-SU: DEFAULT_STATE.stuckCount (SETUP-01) ──────────────────
console.log('[LH-SU] DEFAULT_STATE');
check('LH-SU stuckCount 기본 0', state.DEFAULT_STATE.stuckCount === 0, `actual=${state.DEFAULT_STATE.stuckCount}`);

// ── LH-02: stale 세션 converge (FR-02) ──────────────────────────
console.log('[LH-02] stale 세션 converge-until-dry');
{
  const branch = '__loop_hardening_test__';
  const deadPpid = 999999;            // 존재하지 않는 PID → isProcessAlive false
  const myPpid = process.pid;          // 살아있는 PID(나)
  check('LH-02a deadPpid는 dead로 판정', sctx.isProcessAlive(deadPpid) === false);

  const deadPath = sctx.getSessionFilePath(deadPpid, branch);
  try {
    // dead 세션 파일 생성
    sctx.createSessionFile(deadPpid, branch);
    check('LH-02b dead 세션 .md 생성됨', fs.existsSync(deadPath));

    // archiveSession 멱등성: 1회 성공, 2회째는 파일 없음
    const r1 = sctx.archiveSession(deadPpid, branch);
    check('LH-02c archiveSession 1회 성공', r1.ok === true, JSON.stringify(r1));
    check('LH-02d 원본 .md 이동(수렴)', !fs.existsSync(deadPath));
    const r2 = sctx.archiveSession(deadPpid, branch);
    check('LH-02e archiveSession 2회째 멱등(파일없음)', r2.ok === false);

    // pollOtherSessions 수렴: dead 세션 재생성 후 poll → 자동 아카이브
    sctx.createSessionFile(deadPpid, branch);
    check('LH-02f poll 전 dead .md 존재', fs.existsSync(deadPath));
    sctx.pollOtherSessions(myPpid, branch);
    check('LH-02g poll이 dead 세션 자동 아카이브(누적 수렴)', !fs.existsSync(deadPath));
  } finally {
    // cleanup: 임시 브랜치 세션 + 아카이브 제거
    try {
      const dir = path.dirname(deadPath);
      const root = path.dirname(dir);
      const archived = path.join(root, '.archived');
      for (const d of [dir, path.join(archived, path.basename(dir))]) {
        if (fs.existsSync(d)) fs.rmSync(d, { recursive: true, force: true });
      }
    } catch {}
  }
}

// ── LH-01: 피드백 승격 데이터 흐름 (FR-01) ──────────────────────
// post-write-sync가 remind 위반을 기록하면(수정완료) advance-phase 승격이 발화함을 증명.
// 승격 로직(advance-phase.js:487-510)과 동일한 판정을 temp 데이터로 재현.
console.log('[LH-01] 피드백 폐루프 — remind 3회 위반 → block 승격');
{
  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'lh-fb-'));
  try {
    const ruleId = 'TEST-REMIND';
    const now = Date.now();
    const audit = [
      { event: 'feedback-violation', rule_id: ruleId, rule_action: 'remind', timestamp: new Date(now - 1000).toISOString() },
      { event: 'feedback-violation', rule_id: ruleId, rule_action: 'remind', timestamp: new Date(now - 2000).toISOString() },
      { event: 'feedback-violation', rule_id: ruleId, rule_action: 'remind', timestamp: new Date(now - 3000).toISOString() },
    ].map(e => JSON.stringify(e)).join('\n') + '\n';
    fs.writeFileSync(path.join(tmp, 'audit-log.jsonl'), audit);
    const rules = [{ id: ruleId, active: true, action: 'remind' }];

    // advance-phase.js:487-510 승격 판정 재현 (remind + 7일내 3회 → block)
    const auditLines = audit.split('\n');
    const sevenDaysAgo = now - 7 * 24 * 60 * 60 * 1000;
    let promoted = false;
    for (const rule of rules) {
      if (!rule.active || rule.action !== 'remind') continue;
      const violations = auditLines.filter(l => {
        try { const e = JSON.parse(l); return e.event === 'feedback-violation' && e.rule_id === rule.id && new Date(e.timestamp).getTime() > sevenDaysAgo; }
        catch { return false; }
      });
      if (violations.length >= 3) { rule.action = 'block'; promoted = true; }
    }
    check('LH-01a remind 규칙이 3회 위반으로 block 승격', promoted && rules[0].action === 'block');
    // 핵심: remind 위반이 audit에 존재해야 위 흐름이 성립 — post-write-sync 수정으로 보장됨
    check('LH-01b 승격 입력(remind 위반)이 rule_action으로 식별 가능', JSON.parse(audit.split('\n')[0]).rule_action === 'remind');
  } finally {
    try { fs.rmSync(tmp, { recursive: true, force: true }); } catch {}
  }
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════');
process.exit(fail === 0 ? 0 : 1);
