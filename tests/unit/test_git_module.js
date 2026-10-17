// tests/unit/test_git_module.js — _git.js 단위 테스트
// 실행: node tests/unit/test_git_module.js

'use strict';

const assert = require('assert');

const { gitRun, detectGit } = require('../../.claude/hooks/_git');

// ── 테스트 헬퍼 ───────────────────────────────────────────────────────
// [v4/N-01] 결과 형식은 tests/run.js 의 공용 리포터가 만든다 —
//   형식을 만드는 쪽과 parseCounts 로 읽는 쪽이 갈라지지 않게.
const { makeReporter } = require('../run.js');
const R = makeReporter();
const test = (name, fn) => R.test(name, fn);

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
  // [v3 격리 수정] 이 테스트는 원래 cwd 를 넘기지 않아 **실제 저장소**에
  //   커밋을 남겼다. 실행할 때마다 `; echo INJECTED ...` 제목의 빈 커밋이
  //   git 히스토리에 쌓였고(관측: 6개), git 히스토리는 사용자의 멀티세션
  //   복구 기반이므로 단순 소음이 아니었다.
  //   gitRun(args, cwd) 는 cwd 를 받으므로 임시 저장소에서 검증한다.
  const fs = require('fs');
  const os = require('os');
  const path = require('path');

  const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gitrun-inj-'));
  const injectionMsg = '; echo INJECTED > /tmp/injection_proof.txt';
  const injectionFile = '/tmp/injection_proof.txt';
  const preExisting = fs.existsSync(injectionFile);

  try {
    gitRun(['init'], tmp);
    gitRun(['config', 'user.email', 'test@example.com'], tmp);
    gitRun(['config', 'user.name', 'test'], tmp);

    gitRun(['commit', '--allow-empty', '-m', injectionMsg], tmp);

    // 메시지가 **리터럴 문자열**로 기록됐는가 = 셸을 거치지 않았다는 증거
    const lastMsg = gitRun(['log', '-1', '--pretty=%s'], tmp);
    assert.strictEqual(
      lastMsg.trim(), injectionMsg,
      `커밋 메시지가 리터럴이어야 함, 실제: "${lastMsg.trim()}"`
    );

    // 주입이 성공했다면 /tmp/injection_proof.txt 가 새로 생겼을 것
    assert.ok(
      preExisting || !fs.existsSync(injectionFile),
      'Shell injection 차단 실패 — injection_proof.txt 가 생성됨'
    );
  } finally {
    try { fs.rmSync(tmp, { recursive: true, force: true }); } catch {}
  }
});

// ── 결과 출력 ─────────────────────────────────────────────────────────
R.done();
