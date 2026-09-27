// tests/unit/test_audit_pairing.js
// 승인 근거 대조 — charter harness-v3 / N-02 (FR-06·FR-07·FR-08·FR-26 · D8·D9·D16)
//
// 왜 이 파일이 존재하는가:
//   상태 파일이 단일 장부였다. 승인 필드(userAck·lastApproval·charters)를 손으로 심으면
//   그것이 진짜 사용자에게서 왔는지 확인할 상대가 없었다. 감사 장부를 상대로 세운다.
//
//   대조는 **상태 → 감사 단방향**이다. 감사 행이 상태보다 많은 것은 정상이고(한 발화가
//   두 행을 남긴다), 상태에 있는데 감사에 없는 것만 문제다.
//
//   그리고 이 검사는 **하네스 자신을 막을 수 있다.** 이 기능이 배포되기 전에 기록된 승인은
//   짝이 있을 수 없다. 컷오프 면제가 없으면 다음 사이클이 영원히 닫히지 않는다.

'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');

const HOOKS = path.resolve(__dirname, '..', '..', '.claude', 'hooks');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).slice(0, 200) : ''}`); }
}

let C = null;
try { C = require(path.join(HOOKS, '_common.js')); } catch { /* 아래에서 */ }

console.log('\n═══ AP: 승인 근거 대조 ═══');

console.log('\n[AP-00] should_expose_provenance_helpers');
check('_common 이 readAuditAll 을 export 한다', !!(C && typeof C.readAuditAll === 'function'));
check('_common 이 verifyApprovalProvenance 를 export 한다', !!(C && typeof C.verifyApprovalProvenance === 'function'));
check('_common 이 PROVENANCE_CUTOFF 를 export 한다', !!(C && typeof C.PROVENANCE_CUTOFF === 'string'));
if (!C || typeof C.verifyApprovalProvenance !== 'function') {
  console.log('\n  대조 함수가 없어 나머지를 돌릴 수 없습니다 (Red).');
  console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
  process.exit(1);
}

const verify = C.verifyApprovalProvenance;
const CUT = Date.parse(C.PROVENANCE_CUTOFF);
const AFTER = new Date(CUT + 86400e3).toISOString();          // 컷오프 이후
const AFTER2 = new Date(CUT + 86400e3 + 2).toISOString();     // 2ms 어긋남
const AFTER_FAR = new Date(CUT + 86400e3 + 300e3).toISOString(); // 5분 어긋남
const BEFORE = new Date(CUT - 86400e3).toISOString();          // 컷오프 이전

const row = (event, extra, ts) => Object.assign({ timestamp: ts, event }, extra || {});

// ── AP-01: 짝이 있으면 통과 ────────────────────────────────────────
console.log('\n[AP-01] should_pass_when_audit_row_pairs');
{
  const state = { prd: 'docs/prds/a-prd.md', at: AFTER, via: 'user' };
  const rows = [row('user-approval', { target: 'prd', file: 'docs/prds/a-prd.md' }, AFTER)];
  check('정확히 같은 시각', verify('lastApproval', state, rows).verdict === 'ok');
  check('2ms 어긋나도 통과', verify('lastApproval', state, [row('user-approval', { target: 'prd', file: 'docs/prds/a-prd.md' }, AFTER2)]).verdict === 'ok');
}

// ── AP-02: 짝이 없으면 거부 ───────────────────────────────────────
console.log('\n[AP-02] should_reject_when_no_pairing_row');
{
  const state = { prd: 'docs/prds/a-prd.md', at: AFTER, via: 'user' };
  const other = [row('user-approval', { target: 'prd', file: 'docs/prds/OTHER-prd.md' }, AFTER)];
  const r = verify('lastApproval', state, other);
  check('다른 파일의 승인 행으로는 통과하지 않는다', r.verdict === 'reject', r.verdict);
  const late = [row('user-approval', { target: 'prd', file: 'docs/prds/a-prd.md' }, AFTER_FAR)];
  check('5분 어긋나면 통과하지 않는다', verify('lastApproval', state, late).verdict === 'reject');
}

// ── AP-03: 컷오프 이전은 면제 — 하네스가 자기를 막지 않는다 (D16) ──
console.log('\n[AP-03] should_exempt_records_older_than_cutoff');
{
  const old = { prd: 'docs/prds/a-prd.md', at: BEFORE, via: 'user' };
  check('컷오프 이전 기록은 감사 행이 없어도 면제', verify('lastApproval', old, []).verdict === 'exempt');
  const now = { prd: 'docs/prds/a-prd.md', at: AFTER, via: 'user' };
  check('컷오프 이후 같은 형태는 거부', verify('lastApproval', now, []).verdict !== 'exempt');
}

// ── AP-04: 로그 자체가 없으면 경고까지만 ──────────────────────────
//   신규 설치·러너 복원 뒤에는 감사 로그가 비어 있을 수 있다. 그것을 위조로 보면
//   첫날부터 아무것도 닫히지 않는다.
console.log('\n[AP-04] should_warn_not_reject_when_log_is_absent');
{
  const state = { prd: 'docs/prds/a-prd.md', at: AFTER, via: 'user' };
  const r = verify('lastApproval', state, [], { logMissing: true });
  check('로그 부재는 경고', r.verdict === 'warn', r.verdict);
}

// ── AP-05: waiver 는 절대 거부하지 않는다 — 자기잠금 회로 차단 ────
//   거부하면 탈출로(--force complete)가 다시 waiver 를 요구하고 그 waiver 도 같은 대조를
//   받는다. 하네스가 영구히 닫힌다.
console.log('\n[AP-05] should_never_reject_waiver');
{
  const ack = { kind: 'force-waiver', scope: 'complete', at: AFTER };
  const r = verify('userAck', ack, []);
  check('waiver 는 짝이 없어도 거부되지 않는다', r.verdict !== 'reject', r.verdict);
  const prd = { kind: 'prd-approve', scope: 'docs/prds/a-prd.md', at: AFTER };
  check('prd ack 은 짝이 없으면 거부된다', verify('userAck', prd, []).verdict === 'reject');
}

// ── AP-06: 감사 행이 더 많은 것은 정상 (단방향) ───────────────────
console.log('\n[AP-06] should_allow_more_audit_rows_than_state');
{
  const state = { prd: 'docs/prds/a-prd.md', at: AFTER, via: 'user' };
  const rows = [
    row('prd-review-cleared', { prd: 'docs/prds/a-prd.md' }, AFTER),
    row('user-approval', { target: 'prd', file: 'docs/prds/a-prd.md' }, AFTER),
    row('phase-transition', {}, AFTER),
  ];
  check('한 발화가 남긴 여러 행이 있어도 통과', verify('lastApproval', state, rows).verdict === 'ok');
}

// ── AP-07: 레거시 절대경로 정규화 ─────────────────────────────────
//   옛 행은 file 이 Windows 절대경로 + 백슬래시다. 정규화 없이 비교하면 전부 거부된다.
console.log('\n[AP-07] should_normalize_legacy_absolute_paths');
{
  const state = { prd: 'docs/prds/a-prd.md', at: AFTER, via: 'user' };
  const legacy = [row('user-approval', { target: 'prd', file: 'C:\\workspace\\proj\\docs\\prds\\a-prd.md' }, AFTER)];
  check('백슬래시 절대경로가 상대 POSIX 경로와 매칭된다', verify('lastApproval', state, legacy).verdict === 'ok');
}

// ── AP-08: charter 승인 대조 ──────────────────────────────────────
console.log('\n[AP-08] should_pair_charter_approval');
{
  const rec = { path: 'docs/charters/x-charter.md', approvedHash: 'abc123', approvedAt: AFTER };
  const rows = [row('charter-approved', { charter: 'x', hash: 'abc123' }, AFTER)];
  check('charter 승인이 짝을 찾는다', verify('charters', rec, rows, { charterId: 'x' }).verdict === 'ok');
  check('다른 지문이면 거부', verify('charters', rec, [row('charter-approved', { charter: 'x', hash: 'zzz' }, AFTER)], { charterId: 'x' }).verdict === 'reject');
}

// ── AP-09: readAuditAll 이 아카이브 + live 를 잇는다 (D9) ─────────
//   GC 가 돌면 live 의 절반이 아카이브로 간다. 아카이브를 안 보면 그 승인이 사라진다.
console.log('\n[AP-09] should_read_archive_and_live_together');
{
  const d = fs.mkdtempSync(path.join(os.tmpdir(), 'ap-'));
  const mem = path.join(d, 'docs', 'memory');
  fs.mkdirSync(path.join(mem, 'audit-log-archive'), { recursive: true });
  fs.writeFileSync(path.join(mem, 'audit-log-archive', '2026-09-01.jsonl'), JSON.stringify(row('a', {}, BEFORE)) + '\n');
  fs.writeFileSync(path.join(mem, 'audit-log-archive', '2026-09-02.jsonl'),
    JSON.stringify(row('b', {}, BEFORE)) + '\n' + '{깨진 줄\n' + JSON.stringify(row('c', {}, BEFORE)) + '\n');
  fs.writeFileSync(path.join(mem, 'audit-log.jsonl'), JSON.stringify(row('d', {}, AFTER)) + '\n');
  const res = C.readAuditAll(d);
  check('아카이브 + live 를 한 배열로', Array.isArray(res.rows) && res.rows.length === 4, JSON.stringify((res.rows || []).map(r => r.event)));
  check('순서가 아카이브(사전순) → live', res.rows.map(r => r.event).join('') === 'abcd', res.rows.map(r => r.event).join(''));
  check('깨진 줄은 건너뛴다', !res.rows.some(r => r === null));
  check('파일이 있으면 missing 아님', res.missing === false);
  fs.rmSync(d, { recursive: true, force: true });

  const empty = fs.mkdtempSync(path.join(os.tmpdir(), 'ap2-'));
  const r2 = C.readAuditAll(empty);
  check('파일이 전혀 없으면 missing 신호', r2.missing === true && r2.rows.length === 0);
  fs.rmSync(empty, { recursive: true, force: true });
}

// ── AP-10: 어떤 입력에도 던지지 않는다 ────────────────────────────
console.log('\n[AP-10] should_never_throw');
{
  let threw = null;
  for (const bad of [null, undefined, {}, { at: 'not-a-date' }, { at: AFTER }]) {
    for (const rows of [null, undefined, [], [null], [{}]]) {
      try {
        const r = verify('lastApproval', bad, rows);
        if (!r || typeof r.verdict !== 'string') { threw = 'bad shape: ' + JSON.stringify(r); }
      } catch (e) { threw = e.message; }
    }
  }
  check('비정상 입력에도 일정한 형태 반환', threw === null, threw);
  check('알 수 없는 종류는 면제', verify('nope', { at: AFTER }, []).verdict === 'exempt');
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
