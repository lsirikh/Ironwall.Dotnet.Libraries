// tests/unit/test_mailbox.js — _mailbox.js 단위 테스트
// 실행: node tests/unit/test_mailbox.js

'use strict';

const assert = require('assert');
const fs     = require('fs');
const path   = require('path');

// ── 환경 설정: AGENT_TEAMS 활성화 ────────────────────────────────────
process.env.CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS = '1';

const {
  sendMessage,
  readMessages,
  purgeRead,
  broadcastToAll,
  MAILBOX_DIR
} = require('../../.claude/hooks/_mailbox');

// ── 테스트 헬퍼 ───────────────────────────────────────────────────────
let passed = 0, failed = 0;
function test(name, fn) {
  try { fn(); passed++; console.log(`  ✅ ${name}`); }
  catch (e) { failed++; console.log(`  ❌ ${name}: ${e.message}`); }
}

/** mailbox 디렉토리의 특정 세션 파일 삭제 */
function cleanupSession(sessionId) {
  const filePath = path.join(MAILBOX_DIR, `${sessionId}.json`);
  if (fs.existsSync(filePath)) fs.unlinkSync(filePath);
}

/** mailbox 디렉토리의 파일 목록에서 특정 prefix를 가진 파일 모두 삭제 */
function cleanupSessions(...sessionIds) {
  for (const id of sessionIds) cleanupSession(id);
}

// ── 테스트 시작 ───────────────────────────────────────────────────────
console.log('\nTEST-03: _mailbox.js');

// ─────────────────────────────────────────────────────────────────────
// 케이스 1: sendMessage → 세션 mailbox 파일 생성 확인
// ─────────────────────────────────────────────────────────────────────
test('should_deliver_message_when_sent_to_session_mailbox', () => {
  const sessionId = 'test03_s1';
  cleanupSession(sessionId);

  const msg = sendMessage(sessionId, 'info', { text: 'hello' }, 's2');

  // 반환값 형태 확인
  assert.ok(msg !== null, 'sendMessage should return a message object');
  assert.strictEqual(msg.to, sessionId);
  assert.strictEqual(msg.type, 'info');
  assert.deepStrictEqual(msg.payload, { text: 'hello' });
  assert.strictEqual(msg.read, false);

  // 파일 생성 확인
  const filePath = path.join(MAILBOX_DIR, `${sessionId}.json`);
  assert.ok(fs.existsSync(filePath), `mailbox file should exist: ${filePath}`);

  // 파일 내용 확인
  const saved = JSON.parse(fs.readFileSync(filePath, 'utf8'));
  assert.ok(Array.isArray(saved), 'file content should be an array');
  assert.strictEqual(saved.length, 1);
  assert.strictEqual(saved[0].to, sessionId);

  cleanupSession(sessionId);
});

// ─────────────────────────────────────────────────────────────────────
// 케이스 2: readMessages → 반환 후 read: true 로 마킹 저장 확인
// ─────────────────────────────────────────────────────────────────────
test('should_mark_messages_read_when_consumed', () => {
  const sessionId = 'test03_s2';
  cleanupSession(sessionId);

  sendMessage(sessionId, 'info', { text: 'mark me' }, 's_sender');

  const unread = readMessages(sessionId);
  assert.ok(Array.isArray(unread), 'readMessages should return an array');
  assert.strictEqual(unread.length, 1, 'should return 1 unread message');
  // 반환된 메시지 자체는 read: false 상태 (읽기 전 값)
  assert.strictEqual(unread[0].read, false, 'returned message should have read: false');

  // 파일에는 read: true 로 저장되었는지 확인
  const filePath = path.join(MAILBOX_DIR, `${sessionId}.json`);
  const saved = JSON.parse(fs.readFileSync(filePath, 'utf8'));
  assert.strictEqual(saved[0].read, true, 'file should have read: true after readMessages');

  // 두 번째 readMessages는 빈 배열 반환 (이미 읽음)
  const second = readMessages(sessionId);
  assert.strictEqual(second.length, 0, 'second readMessages should return empty array');

  cleanupSession(sessionId);
});

// ─────────────────────────────────────────────────────────────────────
// 케이스 3: broadcastToAll → 발신자 제외한 모든 세션에 전달
// ─────────────────────────────────────────────────────────────────────
test('should_broadcast_to_all_active_sessions', () => {
  const targets   = ['test03_b1', 'test03_b2', 'test03_b3'];
  const sender    = 'test03_s_from';
  const allSessions = [...targets, sender];

  cleanupSessions(...allSessions);

  const sent = broadcastToAll('phase-change', { phase: 'dev' }, sender, allSessions);

  // 3개 세션에 전송 (발신자 제외)
  assert.strictEqual(sent, 3, `should have sent 3 messages, got ${sent}`);

  // 수신 세션 각각에 메시지 있는지 확인
  for (const id of targets) {
    const filePath = path.join(MAILBOX_DIR, `${id}.json`);
    assert.ok(fs.existsSync(filePath), `mailbox file should exist for ${id}`);
    const msgs = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    assert.strictEqual(msgs.length, 1, `${id} should have 1 message`);
    assert.strictEqual(msgs[0].type, 'phase-change');
  }

  // 발신자 세션에는 파일이 없어야 함
  const senderFile = path.join(MAILBOX_DIR, `${sender}.json`);
  assert.ok(!fs.existsSync(senderFile), 'sender should not receive its own broadcast');

  cleanupSessions(...allSessions);
});

// ─────────────────────────────────────────────────────────────────────
// 케이스 4: 경로 순회 문자 포함 sessionId → 예외 발생
// ─────────────────────────────────────────────────────────────────────
test('should_throw_when_session_id_has_path_traversal', () => {
  let threw = false;
  try {
    sendMessage('../evil', 'info', {}, 's1');
  } catch (e) {
    threw = true;
    assert.ok(
      e.message.includes('unsafe characters') || e.message.includes('Invalid sessionId'),
      `Expected security error, got: ${e.message}`
    );
  }
  assert.ok(threw, 'sendMessage should throw for path traversal sessionId');
});

// ─────────────────────────────────────────────────────────────────────
// 케이스 5: purgeRead → 50개 초과 읽음 메시지 정리
// ─────────────────────────────────────────────────────────────────────
test('should_purge_read_messages_beyond_50', () => {
  const sessionId = 'test03_purge';
  cleanupSession(sessionId);

  // 60개 메시지 전송
  for (let i = 0; i < 60; i++) {
    sendMessage(sessionId, 'info', { index: i }, 's_sender');
  }

  // 전부 읽음 처리 (read: true 로 마킹)
  const unread = readMessages(sessionId);
  assert.strictEqual(unread.length, 60, 'should have 60 unread messages before purge');

  // purgeRead 실행 — read: true 중 50개 초과분 제거
  const removed = purgeRead(sessionId);
  assert.ok(removed > 0, `purgeRead should remove some messages, removed=${removed}`);
  assert.strictEqual(removed, 10, `should remove exactly 10 (60-50), got ${removed}`);

  // 파일에 남은 메시지 ≤ 50 확인
  const filePath = path.join(MAILBOX_DIR, `${sessionId}.json`);
  const remaining = JSON.parse(fs.readFileSync(filePath, 'utf8'));
  assert.ok(remaining.length <= 50, `remaining messages should be ≤ 50, got ${remaining.length}`);

  cleanupSession(sessionId);
});

// ── 결과 출력 ─────────────────────────────────────────────────────────
console.log(`\n══ 결과: ${passed} passed, ${failed} failed ══\n`);
if (failed > 0) process.exit(1);
