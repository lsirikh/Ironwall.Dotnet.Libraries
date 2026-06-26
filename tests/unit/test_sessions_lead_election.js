// tests/unit/test_sessions_lead_election.js — _sessions.js CAS Lead 선출 동시성 테스트
// 실행: node tests/unit/test_sessions_lead_election.js

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
  // docs/memory 디렉토리 보장
  const dir = path.dirname(SESSIONS_FILE);
  if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
}

// ── 테스트 케이스 ─────────────────────────────────────────────────────

console.log('\n── Lead 선출: 기본 초기화 ──');

test('should_set_initialized_sessions_when_file_missing', () => {
  setup();
  // sessions.json이 없는 상태에서 getSessions() 호출
  assert(!fs.existsSync(SESSIONS_FILE), 'sessions.json이 없어야 함');
  const result = sessions.getSessions();
  assert.deepStrictEqual(result, {}, `빈 객체여야 하지만 ${JSON.stringify(result)} 반환`);
});

console.log('\n── Lead 선출: 첫 번째 세션 ──');

test('should_elect_lead_when_first_session_registers', () => {
  setup();
  const reg = sessions.registerSession('s1', 1111);
  assert(reg !== null, 'registerSession이 null을 반환하면 안 됨 (멀티세션 비활성 확인)');
  assert.strictEqual(reg.role, 'lead', `첫 세션 role은 'lead' 여야 하지만 '${reg.role}'`);
  assert.strictEqual(sessions.getLeadSession(), 's1', 'getLeadSession()이 s1을 반환해야 함');
  assert.strictEqual(sessions.isLead('s1'), true, 'isLead(s1)이 true여야 함');
});

console.log('\n── Lead 선출: 두 번째 세션 ──');

test('should_elect_teammate_when_second_session_registers', () => {
  setup();
  sessions.registerSession('s1', 1111);
  const reg2 = sessions.registerSession('s2', 2222);
  assert(reg2 !== null, 'registerSession이 null을 반환하면 안 됨');
  assert.strictEqual(reg2.role, 'teammate', `두 번째 세션 role은 'teammate' 여야 하지만 '${reg2.role}'`);
  assert.strictEqual(sessions.isLead('s2'), false, 'isLead(s2)는 false여야 함');
});

console.log('\n── Lead Failover: lead 해제 후 가장 오래된 teammate 승격 ──');

test('should_promote_oldest_teammate_when_lead_unregisters', () => {
  setup();

  // s1: lead로 등록
  sessions.registerSession('s1', 1111);

  // s2: teammate로 등록 (s1보다 늦게 started_at — 하지만 s3보다 일찍)
  // started_at을 직접 조작해서 s2 > s3 순서 역전을 만들어도 되지만,
  // 기본적으로 등록 순서 = 시간 순서이므로 s2가 s3보다 earlier
  sessions.registerSession('s2', 2222);
  sessions.registerSession('s3', 3333);

  // s2의 started_at을 s3보다 이르게 설정 (파일 직접 조작)
  const raw = JSON.parse(fs.readFileSync(SESSIONS_FILE, 'utf8'));
  const earlier = new Date(Date.now() - 5000).toISOString();
  const later   = new Date(Date.now() - 1000).toISOString();
  raw.sessions['s2'].started_at = earlier;
  raw.sessions['s3'].started_at = later;
  fs.writeFileSync(SESSIONS_FILE, JSON.stringify(raw, null, 2) + '\n');

  // s1 해제 → failover 트리거
  sessions.unregisterSession('s1');

  // failover 이후 lead가 s2인지 확인
  const newLead = sessions.getLeadSession();
  assert.strictEqual(newLead, 's2', `started_at이 가장 이른 s2가 lead가 되어야 하지만 '${newLead}'`);
});

console.log('\n── 복구: 손상된 JSON ──');

test('should_recover_sessions_when_json_corrupted', () => {
  setup();
  // 잘못된 JSON 기록
  fs.writeFileSync(SESSIONS_FILE, '{ invalid json !!!');

  // .bak도 없는 상황 → 빈 구조 반환 기대
  const result = sessions.loadWithRecovery();
  assert(typeof result === 'object', '결과가 객체여야 함');
  assert(result.lead === null || result.lead === undefined, `lead가 null이어야 하지만 ${result.lead}`);
  // sessions 필드가 빈 객체거나 없어야 함
  const sessMap = result.sessions;
  assert(typeof sessMap === 'object' && sessMap !== null, 'sessions 필드가 객체여야 함');
  assert.strictEqual(Object.keys(sessMap).length, 0, '손상 복구 후 sessions는 비어있어야 함');
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
