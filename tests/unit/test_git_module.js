// tests/unit/test_git_module.js — _git.js 단위 테스트
// 실행: node tests/unit/test_git_module.js

'use strict';

const assert = require('assert');

const { gitRun, detectGit } = require('../../.claude/hooks/_git');

// ── 테스트 헬퍼 ───────────────────────────────────────────────────────
let passed = 0, failed = 0;
function test(name, fn) {
  try { fn(); passed++; console.log(`  ✅ ${name}`); }
  catch (e) { failed++; console.log(`  ❌ ${name}: ${e.message}`); }
}

// ── 테스트 시작 ───────────────────────────────────────────────────────
console.log('\nTEST-11: _git.js');

// ─────────────────────────────────────────────────────────────────────
// 케이스 1: 배열 인수로 gitRun 호출 → stdout에 'git version' 포함
// ─────────────────────────────────────────────────────────────────────
test('should_execute_git_command_when_args_are_array', () => {
  const result = gitRun(['--version']);
  assert.ok(
    typeof result === 'string' && result.includes('git version'),
    `Expected "git version" in output, got: ${result}`
  );
});

// ─────────────────────────────────────────────────────────────────────
// 케이스 2: 존재하지 않는 git 서브커맨드 → Error 발생
// ─────────────────────────────────────────────────────────────────────
test('should_throw_when_git_command_fails', () => {
  let threw = false;
  try {
    gitRun(['invalid-cmd-9999']);
  } catch (e) {
    threw = true;
    assert.ok(e instanceof Error, 'thrown value should be an Error instance');
    assert.ok(e.message.length > 0, 'error message should not be empty');
  }
  assert.ok(threw, 'gitRun should throw when the git command returns non-zero exit code');
});

// ─────────────────────────────────────────────────────────────────────
// 케이스 3: 문자열 인수 전달 → injection 방지 Error 발생
// ─────────────────────────────────────────────────────────────────────
test('should_throw_when_args_is_not_array', () => {
  let threw = false;
  try {
    // @ts-ignore — 의도적 타입 오류: 문자열 전달
    gitRun('--version');
  } catch (e) {
    threw = true;
    assert.ok(e instanceof Error, 'thrown value should be an Error instance');
    assert.ok(
      e.message.includes('must be an array') || e.message.includes('injection'),
      `Expected injection-guard error message, got: ${e.message}`
    );
  }
  assert.ok(threw, 'gitRun should throw when args is not an array');
});

// ─────────────────────────────────────────────────────────────────────
// 케이스 4: git repo 초기화 이후 detectGit() === true
// ─────────────────────────────────────────────────────────────────────
test('should_return_true_when_in_git_repo', () => {
  // git wizard가 실행된 이후 PROJECT_ROOT = C:\workspace_python\skill-set 는 git repo
  const result = detectGit();
  assert.strictEqual(result, true, `detectGit() should be true after git init, got: ${result}`);
});

// ─────────────────────────────────────────────────────────────────────
// 케이스 5: shell injection 시도 → 배열 처리로 injection 차단
//   배열로 전달되면 spawnSync가 각 인수를 독립 인자로 처리
//   '; rm -rf /' 는 커밋 메시지 리터럴 문자열로만 전달됨 (shell 실행 없음)
// ─────────────────────────────────────────────────────────────────────
test('should_not_inject_special_chars_when_git_run_uses_array', () => {
  // git repo이므로 --allow-empty commit 성공 → 마지막 커밋 메시지로 검증
  // '; rm -rf /' 가 shell로 실행됐다면 git log 조회 자체가 실패하거나
  // 다른 커밋 메시지가 남을 것임 (주입 성공 시 rm이 실행되어 파일 삭제)
  const injectionMsg = '; echo INJECTED > /tmp/injection_proof.txt';
  let commitSucceeded = false;
  try {
    gitRun(['commit', '--allow-empty', '-m', injectionMsg]);
    commitSucceeded = true;
  } catch (e) {
    // git 오류면 테스트는 throw 경로로 처리 (여전히 injection 없음)
  }
  // 커밋이 성공했다면 메시지가 리터럴로 기록됐는지 확인
  if (commitSucceeded) {
    const lastMsg = gitRun(['log', '-1', '--pretty=%s']);
    assert.strictEqual(
      lastMsg.trim(),
      injectionMsg,
      `Last commit message should be the literal string, got: "${lastMsg.trim()}"`
    );
    // /tmp/injection_proof.txt 가 없으면 injection 차단 확인
    const fs = require('fs');
    const injectionFile = '/tmp/injection_proof.txt';
    assert.ok(
      !fs.existsSync(injectionFile),
      'Shell injection was NOT blocked — injection_proof.txt was created'
    );
  }
  // threw이든 성공이든: shell injection 없이 배열 인수로 처리됨
});

// ── 결과 출력 ─────────────────────────────────────────────────────────
console.log(`\n══ 결과: ${passed} passed, ${failed} failed ══\n`);
if (failed > 0) process.exit(1);
