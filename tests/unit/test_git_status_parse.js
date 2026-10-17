// tests/unit/test_git_status_parse.js
// `git status --porcelain` 파서 회귀 — statusEntries()
//
// 왜 있는가: gitRun() 은 stdout 에 trim() 을 적용한다. 그래서 첫 줄만 선행 공백을 잃고,
//   관용적인 `line.slice(3)` 가 **첫 항목의 경로에서 한 글자를 잘라먹었다**
//   (`.claude/hooks/x.js` → `claude/hooks/x.js`). 그 결과 complete 의 사이클 커밋에서
//   첫 dirty 파일이 매번 "흡수하지 않음"으로 빠졌다 — 실제로 v3.1 종료 커밋에서 관측됐다.

'use strict';

const assert = require('assert');
const path = require('path');

const { statusEntries } = require(path.resolve(__dirname, '..', '..', '.claude', 'hooks', '_git.js'));

let pass = 0, fail = 0;
function test(name, fn) {
  try { fn(); pass++; console.log(`  ✅ ${name}`); }
  catch (e) { fail++; console.log(`  ❌ ${name}: ${e.message}`); }
}

console.log('\n═══ GS: git status 파서 ═══\n');

test('should_keep_leading_dot_when_first_line_is_trimmed', () => {
  // gitRun() 이 trim 한 결과를 그대로 재현 — 첫 줄에 선행 공백이 없다
  const raw = 'M .claude/hooks/advance-phase-harness.js\n M CLAUDE.md';
  const e = statusEntries(raw);
  assert.strictEqual(e.length, 2);
  assert.strictEqual(e[0].path, '.claude/hooks/advance-phase-harness.js');
  assert.strictEqual(e[1].path, 'CLAUDE.md');
});

test('should_parse_untrimmed_output_identically', () => {
  const raw = ' M .claude/hooks/a.js\n M CLAUDE.md';
  assert.deepStrictEqual(statusEntries(raw).map(e => e.path), ['.claude/hooks/a.js', 'CLAUDE.md']);
});

test('should_expose_status_code', () => {
  const e = statusEntries('?? docs/new.md\n M src/a.js\nA  src/b.js\nD  src/c.js');
  assert.deepStrictEqual(e.map(x => x.code), ['??', 'M', 'A', 'D']);
  assert.strictEqual(e[0].path, 'docs/new.md');
});

test('should_strip_quotes_from_paths_with_spaces', () => {
  const e = statusEntries(' M "docs/some file.md"');
  assert.strictEqual(e[0].path, 'docs/some file.md');
});

test('should_take_new_path_on_rename', () => {
  const e = statusEntries('R  docs/old.md -> docs/new.md');
  assert.strictEqual(e.length, 1);
  assert.strictEqual(e[0].path, 'docs/new.md');
});

test('should_ignore_blank_and_malformed_lines', () => {
  const e = statusEntries('\n\n M src/a.js\n\n쓰레기\n');
  assert.deepStrictEqual(e.map(x => x.path), ['src/a.js']);
});

test('should_return_empty_on_clean_tree', () => {
  assert.deepStrictEqual(statusEntries(''), []);
  assert.deepStrictEqual(statusEntries('   '), []);
});

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
