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

// [v3.1/TEST-04] LH-07 삭제 — stuckCount 산술만 검사했다. 실제 상태기계
// (STUCK 즉시 진입 · EXHAUSTED 무리셋 · INFRA 카운터 불변)는 test_loop_v.js 가 검증한다.

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

// [v3.6/N-08/TEST-02] LH-01 을 **실제로** 지웠다.
//   v3.1 이 "삭제" 라고 주석에 적어 놓고 코드를 남겼다. 대상 기능(remind→block 자동 승격
//   사다리)은 `advance-phase.js` 에서 이미 사라졌는데, 테스트는 그 로직을 자기 안에 복제해
//   **자기 사본을 검증하며** 오늘까지 초록 2건을 내고 있었다 — 주석 스스로 "가짜 신뢰" 라고
//   부르던 그 상태다. 존재하지 않는 기능에 대한 초록은 커버리지가 아니라 소음이다.

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════');
process.exit(fail === 0 ? 0 : 1);
