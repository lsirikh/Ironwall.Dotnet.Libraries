// tests/unit/test_sessions_heartbeat.js — _sessions.js Heartbeat + Dead Session 감지 테스트
// 실행: node tests/unit/test_sessions_heartbeat.js

'use strict';

const assert = require('assert');
const fs = require('fs');
const path = require('path');

// ── 환경변수 설정 (isMultiSession() 활성화) ───────────────────────────
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
  for (const f of [SESSIONS_FILE, SESSIONS_LOCK, SESSIONS_FILE + '.bak', SESSIONS_FILE + '.tmp.' + process.pid]) {
    try { fs.unlinkSync(f); } catch { /* 없으면 무시 */ }
  }
}

function setup() {
  cleanupFiles();
  const dir = path.dirname(SESSIONS_FILE);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
}

/**
 * sessions.json의 특정 세션 last_heartbeat를 N초 전으로 직접 수정
 * @param {string} sessionId
 * @param {number} secondsAgo
 */
function setHeartbeatAgo(sessionId, secondsAgo) {
  const raw = JSON.parse(fs.readFileSync(SESSIONS_FILE, 'utf8'));
  if (!raw.sessions[sessionId]) throw new Error(`세션 ${sessionId} 없음`);
  raw.sessions[sessionId].last_heartbeat = new Date(Date.now() - secondsAgo * 1000).toISOString();
  fs.writeFileSync(SESSIONS_FILE, JSON.stringify(raw, null, 2) + '\n');
}

// ── 테스트 케이스 ─────────────────────────────────────────────────────

console.log('\n── Heartbeat: 갱신 확인 ──');

test('should_update_heartbeat_when_called', () => {
  setup();
  sessions.registerSession('s1', 1111);

  // 등록 직후 heartbeat 기록
  const before = JSON.parse(fs.readFileSync(SESSIONS_FILE, 'utf8'));
  const hbBefore = before.sessions['s1'].last_heartbeat;

  // 1ms 이상 간격 확보 (동일 ms 방지)
  const waitUntil = Date.now() + 10;
  while (Date.now() < waitUntil) { /* spin */ }

  const ok = sessions.updateHeartbeat('s1');
  assert.strictEqual(ok, true, 'updateHeartbeat가 true를 반환해야 함');

  const after = JSON.parse(fs.readFileSync(SESSIONS_FILE, 'utf8'));
  const hbAfter = after.sessions['s1'].last_heartbeat;

  assert(hbAfter > hbBefore, `last_heartbeat가 갱신되어야 함: before=${hbBefore}, after=${hbAfter}`);
});

console.log('\n── Dead Session: heartbeat 61초 초과 시 제거 ──');

test('should_remove_dead_session_when_heartbeat_exceeds_60s', () => {
  setup();
  sessions.registerSession('s1', 1111);

  // last_heartbeat를 61초 전으로 조작
  setHeartbeatAgo('s1', 61);

  const { removed } = sessions.cleanDeadSessions();
  assert(removed.includes('s1'), `s1이 removed 목록에 있어야 하지만: ${JSON.stringify(removed)}`);

  const remaining = sessions.getSessions();
  assert(!('s1' in remaining), `s1이 getSessions()에서 제거되어야 하지만 ${JSON.stringify(remaining)}`);
});

console.log('\n── Lead Failover: lead heartbeat 만료 시 teammate 승격 ──');

test('should_trigger_failover_when_lead_heartbeat_expires', () => {
  setup();

  // s1: lead, s2: teammate 등록
  sessions.registerSession('s1', 1111);
  sessions.registerSession('s2', 2222);

  // s2의 started_at을 명시적으로 s1보다 이르게 설정 (failover 후보 순서 보장)
  {
    const raw = JSON.parse(fs.readFileSync(SESSIONS_FILE, 'utf8'));
    raw.sessions['s2'].started_at = new Date(Date.now() - 10000).toISOString();
    fs.writeFileSync(SESSIONS_FILE, JSON.stringify(raw, null, 2) + '\n');
  }

  // s1(lead)의 last_heartbeat를 61초 전으로 조작
  setHeartbeatAgo('s1', 61);

  // cleanDeadSessions 호출 → s1 dead 감지 → failover 트리거
  const { removed } = sessions.cleanDeadSessions();
  assert(removed.includes('s1'), `s1이 dead로 감지되어야 함: ${JSON.stringify(removed)}`);

  // failover 이후 s2가 lead여야 함
  const newLead = sessions.getLeadSession();
  assert.strictEqual(newLead, 's2', `s2가 새 lead여야 하지만 '${newLead}'`);
  assert.strictEqual(sessions.isLead('s2'), true, 'isLead(s2)가 true여야 함');
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
