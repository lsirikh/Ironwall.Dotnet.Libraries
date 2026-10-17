// tests/unit/test_pre_tool_gate_ownership.js
// TEST-05: pre-tool-gate.js 파일 소유권 관련 테스트

const assert = require('assert');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

let passed = 0, failed = 0;

function test(name, fn) {
  try { fn(); passed++; console.log(`  ✅ ${name}`); }
  catch (e) { failed++; console.log(`  ❌ ${name}: ${e.message}`); }
}

const GATE_JS = path.resolve(__dirname, '../../.claude/hooks/pre-tool-gate.js');

console.log('\n=== TEST-05: pre-tool-gate.js 파일 소유권 테스트 ===\n');

const src = fs.readFileSync(GATE_JS, 'utf8');

// ── TC-1: isMultiSession 분기 존재 확인 ──────────────────────────────
test('should_have_file_ownership_check_when_multi_session', () => {
  assert.ok(
    src.includes('isMultiSession()'),
    'isMultiSession() 분기가 pre-tool-gate.js에 없음'
  );
  // 파일 소유권 확인 로직도 함께 존재해야 함
  assert.ok(
    src.includes('owned_files'),
    '소유권 확인 로직(owned_files) 없음'
  );
});

// ── TC-2: file_conflict_policy block 처리 로직 존재 확인 ───────────────
test('should_respect_block_policy_when_file_owned_by_other_session', () => {
  assert.ok(
    src.includes('file_conflict_policy'),
    'file_conflict_policy 환경변수 참조 없음'
  );
  assert.ok(
    src.includes("policy === 'block'"),
    "block 정책 처리 로직(policy === 'block') 없음"
  );
  // block 정책 시 exit(1) 또는 차단 처리
  assert.ok(
    src.includes('GATE BLOCKED') || src.includes('process.exit(1)'),
    'block 정책 적용 시 차단 처리 없음'
  );
});

// ── TC-3: isMultiSession() false시 기존 동작 유지 확인 ────────────────
test('should_allow_edit_when_no_multi_session_flag', () => {
  // 멀티세션 소유권 블록이 isMultiSession() 조건으로 감싸져 있는지 확인
  // owned_files 체크가 isMultiSession() if 블록 안에 있어야 함
  const multiSessionBlockMatch = src.match(/if\s*\(\s*isMultiSession\(\)\s*\)[\s\S]*?owned_files/);
  assert.ok(
    multiSessionBlockMatch !== null,
    '소유권 체크(owned_files)가 isMultiSession() 조건 블록 안에 없음 — 싱글세션에서도 실행될 수 있음'
  );
});

// ── TC-4: _sessions require 존재 확인 + syntax 검사 ────────────────────
test('should_have_session_require_when_loaded', () => {
  // require('./_sessions') 존재 확인
  assert.ok(
    src.includes("require('./_sessions')"),
    "require('./_sessions')가 pre-tool-gate.js에 없음"
  );

  // node --check으로 문법 오류 검사
  try {
    execSync(`node --check "${GATE_JS}"`, { encoding: 'utf8', stdio: 'pipe' });
  } catch (e) {
    throw new Error(`pre-tool-gate.js 문법 오류: ${e.stderr || e.message}`);
  }
});

// ── 결과 출력 ────────────────────────────────────────────────────────
console.log(`\n결과: ${passed} passed, ${failed} failed\n`);
if (failed > 0) process.exit(1);
