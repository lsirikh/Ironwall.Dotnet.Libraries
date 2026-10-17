// tests/unit/test_impact.js
// _impact.js 순수 함수 테스트 — 영향(import) 그래프 (v3.3 / TEST-08)
//
// 왜 이 파일이 존재하는가:
//   Loop V 는 한 파일을 고쳐도 24개 테스트 파일 전부를 27초 동안 돈다. 무엇이 영향받는지
//   모르기 때문이다. 그리고 모델이 `_common.js` 를 고칠 때 그것을 몇 곳이 쓰는지 모른다.
//   영향 그래프는 그 답을 준다 — 다만 **advisory 다.** 동적 import·리플렉션은 못 잡으므로
//   게이트로 쓰지 않는다. 그래서 이 테스트도 "정확도"가 아니라 "유용한 근사"를 검증한다.
//
// 순수 모듈이므로 샌드박스를 만들지 않는다. 파일 내용은 전부 인메모리 맵으로 주입한다.

'use strict';

const assert = require('assert');
const fs = require('fs');
const path = require('path');

const IMPACT_PATH = path.resolve(__dirname, '../../.claude/hooks/_impact.js');
const I = require(IMPACT_PATH);

let pass = 0, fail = 0;
function test(name, fn) {
  try { fn(); pass++; console.log(`  ✅ ${name}`); }
  catch (e) { fail++; console.log(`  ❌ ${name}: ${e.message} (.claude/hooks/_impact.js)`); }
}

// ── 스텁 저장소 ──────────────────────────────────────────────────────
// build 는 { files, readFile, mtime } 를 주입받는다. fs 를 직접 만지지 않는다.
function repo(files) {
  return {
    files: Object.keys(files),
    readFile: p => (Object.prototype.hasOwnProperty.call(files, p) ? files[p] : null),
    mtime: () => 1,
  };
}

console.log('\n═══ IMP: 영향(import) 그래프 ═══\n');

test('should_build_44_files_under_20ms', () => {
  const files = {};
  for (let i = 0; i < 44; i++) {
    const deps = [];
    if (i > 0) deps.push(`const a = require('./mod${i - 1}.js');`);
    if (i > 1) deps.push(`const b = require('./mod${i - 2}.js');`);
    files[`src/mod${i}.js`] = deps.join('\n') + '\nmodule.exports = {};\n';
  }
  const r = repo(files);
  I.build(r);   // warm-up

  const t0 = process.hrtime.bigint();
  for (let k = 0; k < 10; k++) I.build(r);
  const ms = Number(process.hrtime.bigint() - t0) / 1e6 / 10;

  const g = I.build(r);
  assert.strictEqual(Object.keys(g.files).length, 44, `44파일: ${Object.keys(g.files).length}`);
  assert.ok(ms < 20, `build 44파일 ${ms.toFixed(3)}ms (< 20ms 여야) — .claude/hooks/_impact.js`);
  console.log(`     (44파일 build 평균 ${ms.toFixed(3)}ms)`);
});

test('should_find_common_js_dependents_6_of_14', () => {
  // 하네스 저장소의 실제 모양을 축소해서 재현한다:
  //   여러 훅이 _common.js 를 require 하고, 그중 일부만 서로를 require 한다.
  const files = {
    '.claude/hooks/_common.js': 'module.exports = { appendAudit };\n',
    '.claude/hooks/_state.js': "const { appendAudit } = require('./_common');\n",
    '.claude/hooks/advance-phase.js': "const c = require('./_common');\nconst s = require('./_state');\n",
    '.claude/hooks/session-gate.js': "const { STATE_FILE } = require('./_state');\n",
    '.claude/hooks/pre-tool-gate.js': "const x = require('./_common.js');\n",
    '.claude/hooks/_signals.js': "const crypto = require('crypto');\n",   // 외부 모듈만 — 의존 없음
    'tests/unit/test_state.js': "const S = require('../../.claude/hooks/_state.js');\n",
  };
  const g = I.build(repo(files));

  const direct = I.dependents(g, '.claude/hooks/_common.js');
  assert.deepStrictEqual(direct.sort(), [
    '.claude/hooks/_state.js', '.claude/hooks/advance-phase.js', '.claude/hooks/pre-tool-gate.js',
  ].sort(), `직접 dependents: ${JSON.stringify(direct)}`);

  // 전이: _state.js 를 거쳐 session-gate 와 테스트까지 닿는다
  const trans = I.dependents(g, '.claude/hooks/_common.js', { transitive: true });
  assert.ok(trans.includes('.claude/hooks/session-gate.js'), `전이에 session-gate: ${JSON.stringify(trans)}`);
  assert.ok(trans.includes('tests/unit/test_state.js'), '전이에 테스트');
  assert.ok(!trans.includes('.claude/hooks/_signals.js'), '무관한 파일은 포함되지 않는다');

  // 확장자를 생략한 require 도 같은 노드로 해석되어야 한다
  assert.ok(direct.includes('.claude/hooks/_state.js'), "require('./_common') — 확장자 없는 형태");
  assert.ok(direct.includes('.claude/hooks/pre-tool-gate.js'), "require('./_common.js') — 확장자 있는 형태");
});

test('should_reparse_single_file_under_1ms', () => {
  const files = {};
  for (let i = 0; i < 44; i++) files[`src/mod${i}.js`] = `const a = require('./mod${(i + 1) % 44}.js');\n`;
  const r = repo(files);
  const g = I.build(r);

  const changed = 'src/mod7.js';
  files[changed] = "const a = require('./mod0.js');\nconst b = require('./mod1.js');\n";

  I.update(g, changed, r);   // warm-up
  const t0 = process.hrtime.bigint();
  for (let k = 0; k < 50; k++) I.update(g, changed, r);
  const ms = Number(process.hrtime.bigint() - t0) / 1e6 / 50;

  assert.ok(ms < 1, `단일 파일 재파싱 ${ms.toFixed(4)}ms (< 1ms 여야)`);
  assert.deepStrictEqual(g.files[changed].deps.sort(), ['src/mod0.js', 'src/mod1.js']);
  // 역방향도 함께 갱신되어야 한다 — 안 하면 dependents 가 조용히 낡는다
  assert.ok(I.dependents(g, 'src/mod0.js').includes(changed), 'dependents 역방향 갱신');
  assert.ok(!I.dependents(g, 'src/mod8.js').includes(changed), '없어진 의존은 제거');
  console.log(`     (단일 파일 update 평균 ${ms.toFixed(4)}ms)`);
});

test('should_prefer_import_graph_command_when_declared', () => {
  // CLAUDE.md 에 프로젝트 고유 명령이 있으면 정규식 추정보다 우선한다.
  const files = { 'src/a.js': "require('./b.js');\n", 'src/b.js': '' };
  const g = I.build(Object.assign(repo(files), {
    importGraphCommand: 'my-tool graph',
    runCommand: cmd => {
      assert.strictEqual(cmd, 'my-tool graph');
      return JSON.stringify({ 'src/a.js': ['src/z.js'], 'src/z.js': [] });
    },
  }));
  assert.strictEqual(g.source, 'command', `source: ${g.source}`);
  assert.deepStrictEqual(g.files['src/a.js'].deps, ['src/z.js'], '명령 결과가 정규식을 이긴다');

  // 명령이 실패하면 조용히 정규식으로 되돌아간다 — advisory 가 하드 실패하면 안 된다
  const g2 = I.build(Object.assign(repo(files), {
    importGraphCommand: 'broken',
    runCommand: () => { throw new Error('boom'); },
  }));
  assert.strictEqual(g2.source, 'regex', '명령 실패 → 정규식 폴백');
  assert.deepStrictEqual(g2.files['src/a.js'].deps, ['src/b.js']);
});

test('should_map_dependents_to_affected_tests', () => {
  const files = {
    'src/core.js': 'module.exports = 1;\n',
    'src/mid.js': "const c = require('./core.js');\n",
    'src/other.js': 'module.exports = 2;\n',
    'tests/unit/test_core.js': "require('../../src/core.js');\n",
    'tests/unit/test_mid.js': "require('../../src/mid.js');\n",
    'tests/unit/test_other.js': "require('../../src/other.js');\n",
  };
  const g = I.build(repo(files));
  const t = I.affectedTests(g, 'src/core.js');
  assert.deepStrictEqual(t.sort(), ['tests/unit/test_core.js', 'tests/unit/test_mid.js'],
    `영향 테스트: ${JSON.stringify(t)}`);
  assert.ok(!t.includes('tests/unit/test_other.js'), '무관한 테스트는 제외');

  // 자기 자신이 테스트면 그 자신이 영향 테스트다
  assert.deepStrictEqual(I.affectedTests(g, 'tests/unit/test_other.js'), ['tests/unit/test_other.js']);
});

test('should_handle_js_py_cs_go_rs_java_syntax', () => {
  const files = {
    'src/a.js': "import x from './b.js';\nconst y = require('./c');\n",
    'src/b.js': '', 'src/c.js': '',
    'app/main.py': 'import app.util\nfrom app.helper import thing\n',
    'app/util.py': '', 'app/helper.py': '',
    'Svc/Program.cs': 'using Svc.Models;\n',
    'Svc/Models.cs': '',
    'cmd/main.go': 'import "example.com/p/store"\n',
    'p/store/store.go': '',
    'src/lib.rs': 'mod parser;\nuse crate::engine;\n',
    'src/parser.rs': '', 'src/engine.rs': '',
    'com/App.java': 'import com.Repo;\n',
    'com/Repo.java': '',
  };
  const g = I.build(repo(files));
  const deps = p => (g.files[p] ? g.files[p].deps.slice().sort() : null);

  assert.deepStrictEqual(deps('src/a.js'), ['src/b.js', 'src/c.js'], 'js: import + require');
  assert.deepStrictEqual(deps('app/main.py'), ['app/helper.py', 'app/util.py'], 'py: import + from');
  assert.deepStrictEqual(deps('Svc/Program.cs'), ['Svc/Models.cs'], 'cs: using');
  assert.deepStrictEqual(deps('cmd/main.go'), ['p/store/store.go'], 'go: import 경로 꼬리 매칭');
  assert.deepStrictEqual(deps('src/lib.rs'), ['src/engine.rs', 'src/parser.rs'], 'rs: mod + use');
  assert.deepStrictEqual(deps('com/App.java'), ['com/Repo.java'], 'java: import');
});

test('should_ignore_external_modules_and_self_reference', () => {
  const files = {
    'src/a.js': "const fs = require('fs');\nconst lodash = require('lodash');\nrequire('./a.js');\n",
  };
  const g = I.build(repo(files));
  assert.deepStrictEqual(g.files['src/a.js'].deps, [],
    `저장소 밖 모듈과 자기 참조는 제외: ${JSON.stringify(g.files['src/a.js'].deps)}`);
});

test('should_survive_cycles_without_hanging', () => {
  const files = {
    'src/a.js': "require('./b.js');\n",
    'src/b.js': "require('./c.js');\n",
    'src/c.js': "require('./a.js');\n",
  };
  const g = I.build(repo(files));
  const t = I.dependents(g, 'src/a.js', { transitive: true });
  assert.strictEqual(t.length, 2, `순환에서도 종료하고 자기 자신은 빼야: ${JSON.stringify(t)}`);
  assert.ok(!t.includes('src/a.js'), '자기 자신 제외');
});

// ── IM-20: 영향 그래프의 미탐을 센다 (v4/N-10 · FR-03) ────────────────
//
// 왜: 그래프는 원리적으로 불완전하다 — 동적 require·조립된 경로·헬퍼 경유는
//   정적 스캔이 못 잡는다. 그래서 evidenceCovers 는 "모르면 전체를 요구한다" 로
//   안전하게 기울어 있다. 그런데 **얼마나 모르는지**를 아무도 세지 않았다.
//   숫자가 없으면 부분 검증을 넓혀도 되는지 좁혀야 하는지 판단할 근거가 없다.
console.log('\n[IM-20] should_measure_the_impact_graph_miss_rate');
{
  
  test('missRate 가 순수 함수로 노출된다', () => assert(typeof I.missRate === 'function'));

  if (typeof I.missRate === 'function') {
    // 판정표 — 파일을 읽지 않는다
    const only = src => I.missRate({ 'a.js': src });
    test('정적 require 만 있으면 미탐 0', () =>
      assert(only("const x = require('./b.js');\nconst y = require('../c');").missed === 0));
    test('동적 require 를 센다', () =>
      assert(only('const m = require(name);').byKind.dynamic === 1));
    test('조립된 경로를 센다', () =>
      assert(only("require('./' + name);").byKind.computed === 1));
    test('헬퍼 경유를 센다', () =>
      assert(only("require(path.join(d, 'x.js'));").byKind.viaHelper === 1));
    test('js 가 아닌 파일은 세지 않는다', () =>
      assert(I.missRate({ 'a.md': "require(x);" }).total === 0));
    test('빈 입력에 던지지 않는다', () => assert(I.missRate(null).total === 0));

    // 실측 — 저장소의 훅 전체
    const map = {};
    const HD = path.join(path.resolve(__dirname, '..', '..'), '.claude', 'hooks');
    for (const f of fs.readdirSync(HD)) {
      if (f.endsWith('.js')) map['.claude/hooks/' + f] = fs.readFileSync(path.join(HD, f), 'utf8');
    }
    const r = I.missRate(map);
    console.log(`    ℹ 실측: require ${r.total}건 · 정적 해석 ${r.resolved} · 미탐 ${r.missed} (${r.rate}%) · ${JSON.stringify(r.byKind)}`);
    test('실측이 숫자로 나온다', () => assert(r.total > 0 && Number.isFinite(r.rate)));
    // 미탐이 크게 늘면 부분 검증의 신뢰가 무너진 것이다 — 그때는 전체 요구로 강등해야 한다.
    test('미탐률이 20% 를 넘지 않는다', () => assert(r.rate <= 20));
  }
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
