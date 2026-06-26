// tests/unit/test_multi_session_integration.js
// _sessions.js 8세션 병렬 시뮬레이션 통합 테스트 (함수 직접 호출)
// 실행: node tests/unit/test_multi_session_integration.js

'use strict';

const assert = require('assert');
const fs = require('fs');
const path = require('path');

// ── 환경변수 설정 ─────────────────────────────────────────────────────
const PREV_ENV = process.env.CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS;
process.env.CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS = '1';

const sessions = require('../../.claude/hooks/_sessions');
const SESSIONS_FILE = sessions.SESSIONS_FILE;
const SESSIONS_LOCK = sessions.SESSIONS_LOCK;

// ── 테스트 러너 ──────────────────────────────────────────────────────
let passed = 0, failed = 0;
function test(name, fn) {
  try { fn(); passed++; console.log(`  ✅ ${name}`); }
  catch (e) { failed++; console.log(`  ❌ ${name}: ${e.message}`); }
}

// ── 유틸 ─────────────────────────────────────────────────────────────
function cleanupFiles() {
  const targets = [
    SESSIONS_FILE,
    SESSIONS_LOCK,
    SESSIONS_FILE + '.bak',
    SESSIONS_FILE + '.tmp.' + process.pid
  ];
  for (const f of targets) {
    try { fs.unlinkSync(f); } catch { /* 없으면 무시 */ }
  }
}

function setup() {
  cleanupFiles();
  const dir = path.dirname(SESSIONS_FILE);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
}

// 8개 세션 ID 생성
const SESSION_IDS = Array.from({ length: 8 }, (_, i) => `session-${String(i + 1).padStart(2, '0')}`);

// ── 테스트 케이스 ─────────────────────────────────────────────────────

console.log('\n── 8세션 Lead 선출 ──');

test('should_elect_exactly_one_lead_when_8_sessions_register', () => {
  setup();

  // 8개 세션 순차 등록
  const results = SESSION_IDS.map((sid, idx) =>
    sessions.registerSession(sid, 10000 + idx)
  );

  // isMultiSession()이 활성화되었으므로 null이 없어야 함
  const nullCount = results.filter(r => r === null).length;
  assert.strictEqual(nullCount, 0, `${nullCount}개 세션이 null 반환 — CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1 확인 필요`);

  // lead 역할인 세션 수 확인
  const allSessions = sessions.getSessions();
  const leadSessions = Object.entries(allSessions).filter(([, s]) => s.role === 'lead');
  const teammateSessions = Object.entries(allSessions).filter(([, s]) => s.role === 'teammate');

  assert.strictEqual(leadSessions.length, 1,
    `lead가 정확히 1개여야 하지만 ${leadSessions.length}개 — lead IDs: ${leadSessions.map(([id]) => id).join(', ')}`);
  assert.strictEqual(teammateSessions.length, 7,
    `teammate가 정확히 7개여야 하지만 ${teammateSessions.length}개`);

  // getLeadSession()이 유일한 lead ID와 일치하는지 확인
  const leadId = sessions.getLeadSession();
  assert.strictEqual(leadId, leadSessions[0][0],
    `getLeadSession()='${leadId}'이 실제 lead='${leadSessions[0][0]}'와 불일치`);

  // 첫 번째 등록 세션(session-01)이 lead인지 확인 (CAS 선출 원칙)
  assert.strictEqual(leadId, SESSION_IDS[0],
    `CAS 선출 원칙에 의해 첫 등록 세션(${SESSION_IDS[0]})이 lead여야 하지만 '${leadId}'`);
});

console.log('\n── 파일 소유권 분산 ──');

test('should_distribute_file_ownership_across_sessions', () => {
  setup();

  // 8개 세션 등록
  SESSION_IDS.forEach((sid, idx) => sessions.registerSession(sid, 10000 + idx));

  // 각 세션에 고유 파일 1개씩 할당
  const fileMap = {};
  SESSION_IDS.forEach((sid, idx) => {
    const filePath = `src/module-${idx + 1}/index.js`;
    fileMap[sid] = filePath;
    const result = sessions.claimFile(sid, filePath, `TASK-${idx + 1}`);
    assert(result === true, `${sid}의 claimFile 실패 (result=${result})`);
  });

  // 각 세션의 owned_files 확인
  const allSessions = sessions.getSessions();
  let totalFiles = 0;

  SESSION_IDS.forEach(sid => {
    const session = allSessions[sid];
    assert(session, `세션 ${sid}가 registry에 없음`);

    const expectedFile = fileMap[sid].replace(/\\/g, '/');
    // owned_files 배열에 할당한 파일이 포함되어 있는지 확인
    const ownedFiles = session.owned_files || [];
    const hasFile = ownedFiles.some(f => f.endsWith(expectedFile) || f.replace(/\\/g, '/').endsWith(expectedFile));
    assert(hasFile,
      `${sid}의 owned_files에 '${expectedFile}'이 없음 — owned: ${JSON.stringify(ownedFiles)}`);

    totalFiles += ownedFiles.length;
  });

  // 총 8개 파일이 분산되었는지 확인
  assert.strictEqual(totalFiles, 8,
    `총 파일 소유권이 8개여야 하지만 ${totalFiles}개`);
});

console.log('\n── 세션 해제 시 파일 소유권 반환 ──');

test('should_release_all_owned_files_when_session_unregisters', () => {
  setup();

  // 8개 세션 등록
  SESSION_IDS.forEach((sid, idx) => sessions.registerSession(sid, 10000 + idx));

  // 모든 세션에 파일 할당
  SESSION_IDS.forEach((sid, idx) => {
    sessions.claimFile(sid, `src/module-${idx + 1}/index.js`, `TASK-${idx + 1}`);
  });

  // teammate 1개 선택 (session-02: 두 번째로 등록 → teammate)
  const targetSession = SESSION_IDS[1]; // session-02
  const targetFile = `src/module-2/index.js`;

  // 해제 전 소유 파일 확인
  const beforeSessions = sessions.getSessions();
  const beforeOwned = beforeSessions[targetSession].owned_files || [];
  assert(beforeOwned.length > 0, `${targetSession}이 해제 전 소유 파일이 없음`);

  // teammate 세션 해제
  const unregResult = sessions.unregisterSession(targetSession);
  assert(unregResult === true, `unregisterSession 실패 (result=${unregResult})`);

  // 해제 후 세션 목록에서 제거되었는지 확인
  const afterSessions = sessions.getSessions();
  assert(!(targetSession in afterSessions),
    `${targetSession}이 unregister 후에도 registry에 남아있음`);

  // 다른 7개 세션은 영향 없는지 확인
  const remainingSessions = SESSION_IDS.filter(sid => sid !== targetSession);
  remainingSessions.forEach((sid, idx) => {
    const adjustedIdx = idx < 1 ? idx : idx + 1; // targetSession(idx=1) 이후는 +1 조정
    assert(sid in afterSessions,
      `${sid}가 영향을 받아 registry에서 사라짐 — 다른 세션에 영향 없어야 함`);
    const ownedFiles = afterSessions[sid].owned_files || [];
    assert(ownedFiles.length > 0,
      `${sid}의 owned_files가 비워짐 — 다른 세션에 영향 없어야 함`);
  });

  // lead(session-01)는 여전히 lead인지 확인
  const leadId = sessions.getLeadSession();
  assert.strictEqual(leadId, SESSION_IDS[0],
    `teammate 해제 후 lead가 바뀌면 안 됨 — 현재 lead: ${leadId}`);
});

console.log('\n── Lead Failover: 새 lead 선출 ──');

test('should_handle_lead_failover_and_elect_new_lead', () => {
  setup();

  // 8개 세션 등록
  SESSION_IDS.forEach((sid, idx) => sessions.registerSession(sid, 10000 + idx));

  // started_at 조작: session-02가 나머지 teammate 중 가장 이른 시각
  const rawData = JSON.parse(fs.readFileSync(SESSIONS_FILE, 'utf8'));
  const baseTime = Date.now();
  SESSION_IDS.forEach((sid, idx) => {
    if (sid === SESSION_IDS[0]) return; // lead(session-01)는 그대로
    // idx 순서대로 시간 간격 부여 (session-02가 가장 이름)
    rawData.sessions[sid].started_at = new Date(baseTime - (8 - idx) * 1000).toISOString();
  });
  fs.writeFileSync(SESSIONS_FILE, JSON.stringify(rawData, null, 2) + '\n');

  // 현재 lead 확인
  const currentLead = sessions.getLeadSession();
  assert.strictEqual(currentLead, SESSION_IDS[0],
    `failover 전 lead가 ${SESSION_IDS[0]}여야 하지만 '${currentLead}'`);

  // lead 세션 해제
  const unregResult = sessions.unregisterSession(SESSION_IDS[0]);
  assert(unregResult === true, `lead unregisterSession 실패`);

  // checkLeadFailover 명시적 호출 (unregisterSession 내부에서 자동 호출되지만 명시적으로도 검증)
  sessions.checkLeadFailover();

  // 새로운 lead 확인
  const newLead = sessions.getLeadSession();
  assert(newLead !== null,
    'lead 해제 후 새 lead가 null — failover가 실행되지 않음');
  assert(newLead !== SESSION_IDS[0],
    `새 lead가 해제된 세션(${SESSION_IDS[0]})과 동일 — failover 오류`);

  // 새 lead의 role이 'lead'인지 확인
  const afterSessions = sessions.getSessions();
  const newLeadSession = afterSessions[newLead];
  assert(newLeadSession, `새 lead(${newLead})가 sessions에 없음`);
  assert.strictEqual(newLeadSession.role, 'lead',
    `새 lead(${newLead})의 role이 'lead'여야 하지만 '${newLeadSession.role}'`);

  // started_at이 가장 이른 teammate(session-02)가 새 lead인지 확인
  // (session-02의 started_at = baseTime - 7000 으로 teammate 중 가장 이름)
  assert.strictEqual(newLead, SESSION_IDS[1],
    `started_at이 가장 이른 ${SESSION_IDS[1]}이 새 lead여야 하지만 '${newLead}'`);

  // 나머지 6개 세션은 여전히 teammate인지 확인
  const leadCount = Object.values(afterSessions).filter(s => s.role === 'lead').length;
  assert.strictEqual(leadCount, 1, `failover 후 lead가 정확히 1개여야 하지만 ${leadCount}개`);
});

// ── 정리 ─────────────────────────────────────────────────────────────
cleanupFiles();

// 환경변수 복원
if (PREV_ENV === undefined) {
  delete process.env.CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS;
} else {
  process.env.CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS = PREV_ENV;
}

console.log(`\n══ 결과: ${passed} passed, ${failed} failed ══`);
if (failed > 0) process.exit(1);
