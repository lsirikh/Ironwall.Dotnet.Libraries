// tests/unit/test_post_write_sync_worktree.js
// post-write-sync.js 소스 코드 정적 분석 테스트
// 실행: node tests/unit/test_post_write_sync_worktree.js

'use strict';

const assert = require('assert');
const fs = require('fs');
const path = require('path');

const SOURCE_FILE = path.resolve(__dirname, '../../.claude/hooks/post-write-sync.js');

// ── 테스트 러너 ──────────────────────────────────────────────────────
let passed = 0, failed = 0;
function test(name, fn) {
  try { fn(); passed++; console.log(`  ✅ ${name}`); }
  catch (e) { failed++; console.log(`  ❌ ${name}: ${e.message}`); }
}

// ── 소스 읽기 ────────────────────────────────────────────────────────
let sourceCode;
try {
  sourceCode = fs.readFileSync(SOURCE_FILE, 'utf8');
} catch (e) {
  console.error(`소스 파일 읽기 실패: ${SOURCE_FILE}\n${e.message}`);
  process.exit(1);
}

// ── 테스트 케이스 ────────────────────────────────────────────────────

console.log('\n── normalizeToMainTree: worktree 경로 정규화 함수 ──');

test('should_normalize_worktree_path_function_exists_in_post_write_sync', () => {
  // normalizeToMainTree 함수 정의가 소스에 존재하는지 확인
  const hasFunctionDecl = /function\s+normalizeToMainTree\s*\(/.test(sourceCode);
  assert(hasFunctionDecl, 'normalizeToMainTree 함수 선언이 소스에 없음');

  // worktree list 명령어를 사용해 worktree를 순회하는 로직이 있는지 확인
  const hasWorktreeList = /worktree.*list.*porcelain/.test(sourceCode);
  assert(hasWorktreeList, 'worktree list --porcelain 로직이 없음');

  // startsWith를 사용해 worktree 경로 매칭을 하는지 확인
  const hasStartsWith = /absolutePath\.startsWith\(wt\)/.test(sourceCode);
  assert(hasStartsWith, 'worktree 경로 매칭 로직(startsWith)이 없음');
});

console.log('\n── _HARNESS_ORIGIN: 재귀 방지 마커 ──');

test('should_set_harness_origin_marker_during_sync', () => {
  // _HARNESS_ORIGIN 마커 설정 로직 확인
  const hasSet = /process\.env\._HARNESS_ORIGIN\s*=\s*['"]harness['"]/.test(sourceCode);
  assert(hasSet, '_HARNESS_ORIGIN 설정 코드가 없음');

  // _HARNESS_ORIGIN 마커 삭제 로직 확인 (재귀 방지 해제)
  const hasDelete = /delete\s+process\.env\._HARNESS_ORIGIN/.test(sourceCode);
  assert(hasDelete, '_HARNESS_ORIGIN 삭제 코드가 없음');

  // 마커 설정과 삭제가 모두 존재해야 함 (설정 후 해제 패턴)
  const setCount = (sourceCode.match(/process\.env\._HARNESS_ORIGIN\s*=\s*['"]harness['"]/g) || []).length;
  const deleteCount = (sourceCode.match(/delete\s+process\.env\._HARNESS_ORIGIN/g) || []).length;
  assert(setCount >= 1, `_HARNESS_ORIGIN 설정이 ${setCount}회 — 최소 1회 필요`);
  assert(deleteCount >= 2, `_HARNESS_ORIGIN 삭제가 ${deleteCount}회 — 최소 2회 필요 (정상/오류 경로 각각)`);
});

console.log('\n── withSessionCtxLock: session-context.md 잠금 ──');

test('should_have_session_context_lock_when_writing', () => {
  // withSessionCtxLock 함수 정의 확인
  const hasFunctionDecl = /function\s+withSessionCtxLock\s*\(/.test(sourceCode);
  assert(hasFunctionDecl, 'withSessionCtxLock 함수 선언이 없음');

  // .lock 파일을 wx 플래그로 생성하는 배타 잠금 확인
  const hasWxFlag = /SESSION_CTX_LOCK.*wx/.test(sourceCode) ||
                    /wx.*SESSION_CTX_LOCK/.test(sourceCode) ||
                    /writeFileSync\s*\(\s*SESSION_CTX_LOCK[^)]+wx/.test(sourceCode);
  assert(hasWxFlag, 'SESSION_CTX_LOCK wx 플래그 잠금 코드가 없음');

  // 잠금 해제 (unlinkSync) 확인
  const hasUnlink = /unlinkSync\s*\(\s*SESSION_CTX_LOCK\s*\)/.test(sourceCode);
  assert(hasUnlink, 'SESSION_CTX_LOCK unlinkSync 해제 코드가 없음');

  // withSessionCtxLock 실제 사용 확인 (session-context 갱신 시 호출)
  const hasUsage = /withSessionCtxLock\s*\(/.test(sourceCode.replace(/^function\s+withSessionCtxLock.*$/m, ''));
  assert(hasUsage, 'withSessionCtxLock 호출부가 없음');
});

console.log('\n── withIndexLock: INDEX.md 잠금 ──');

test('should_have_index_lock_when_writing', () => {
  // withIndexLock 함수 정의 확인
  const hasFunctionDecl = /function\s+withIndexLock\s*\(/.test(sourceCode);
  assert(hasFunctionDecl, 'withIndexLock 함수 선언이 없음');

  // INDEX.md.lock 경로 상수 확인
  const hasLockConst = /INDEX_LOCK\s*=\s*path\.join\(.*INDEX\.md\.lock/.test(sourceCode);
  assert(hasLockConst, 'INDEX_LOCK 상수(INDEX.md.lock) 정의가 없음');

  // wx 플래그 배타 잠금 확인 — writeFileSync(INDEX_LOCK, ..., { flag: 'wx' }) 패턴
  // 한 줄 또는 여러 줄로 작성될 수 있으므로 INDEX_LOCK과 'wx' 각각 포함 여부를 확인
  const lockBlock = sourceCode.match(/function\s+withIndexLock[\s\S]*?^\}/m)?.[0] || '';
  const hasWxFlag = /wx/.test(lockBlock) && /INDEX_LOCK/.test(lockBlock);
  assert(hasWxFlag, 'INDEX_LOCK wx 플래그 잠금 코드가 없음');

  // 잠금 해제 확인
  const hasUnlink = /unlinkSync\s*\(\s*INDEX_LOCK\s*\)/.test(sourceCode);
  assert(hasUnlink, 'INDEX_LOCK unlinkSync 해제 코드가 없음');

  // withIndexLock 실제 사용 (INDEX.md 갱신 시 호출)
  const callCount = (sourceCode.match(/withIndexLock\s*\(/g) || []).length;
  assert(callCount >= 2, `withIndexLock 호출이 ${callCount}회 — 정의 1회 + 사용 최소 1회 필요`);
});

console.log('\n── validatePrdPath: PRD 경로 검증 ──');

test('should_have_prd_path_validation_when_checking_prd', () => {
  // validatePrdPath 함수 정의 확인
  const hasFunctionDecl = /function\s+validatePrdPath\s*\(/.test(sourceCode);
  assert(hasFunctionDecl, 'validatePrdPath 함수 선언이 없음');

  // docs/prds/ 경로를 기준으로 검증하는 로직 확인
  const hasPrdsDir = /docs['"\\\/]+prds/.test(sourceCode);
  assert(hasPrdsDir, "docs/prds/ 경로 참조가 없음");

  // path traversal 방지 — resolved path가 prdsDir로 시작하는지 확인
  const hasStartsWithCheck = /resolved\.startsWith\s*\(\s*prdsDir\s*\)/.test(sourceCode);
  assert(hasStartsWithCheck, 'PRD path traversal 방지(startsWith prdsDir) 코드가 없음');

  // .md 파일만 허용하는 검증 확인
  const hasMdCheck = /endsWith\s*\(\s*['"]\.md['"]\s*\)/.test(sourceCode);
  assert(hasMdCheck, '.md 파일 검증 코드가 없음');

  // 유효하지 않은 경로에서 Error throw 확인
  const hasThrow = /throw\s+new\s+Error\s*\(\s*['"]Invalid PRD path/.test(sourceCode);
  assert(hasThrow, "Invalid PRD path Error throw 코드가 없음");

  // validatePrdPath 실제 호출부 확인
  const callCount = (sourceCode.match(/validatePrdPath\s*\(/g) || []).length;
  assert(callCount >= 2, `validatePrdPath 호출이 ${callCount}회 — 정의 1회 + 사용 최소 1회 필요`);
});

// ── 결과 출력 ────────────────────────────────────────────────────────
console.log(`\n══ 결과: ${passed} passed, ${failed} failed ══`);
if (failed > 0) process.exit(1);
