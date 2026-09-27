// tests/unit/test_package_closure.js
// 패키지 의존 폐쇄성 — 배포되는 훅이 require 하는 것은 전부 배포되어야 한다
//
// 왜 이 파일이 존재하는가:
//   훅들은 선택적 모듈을 `try { require('./x') } catch { /* 없으면 스킵 */ }` 로 부른다.
//   그 관용 덕에 모듈이 패키지에서 빠져도 **아무도 모른다** — 예외도 없고 로그도 없다.
//   실측(v3.4): `_impact.js`(v3.3 영향 그래프) · `_session-context.js` · `_session-guard.js`
//   세 개가 INCLUDE 목록에 없었다. 즉 설치 사용자에게는 그 기능들이 **아예 없었다.**
//   이 저장소에서는 전부 정상 동작했으므로 어떤 테스트도 그것을 잡지 못했다.
//
// 여기서 검사하는 것은 "이 저장소가 동작하는가"가 아니라 "설치본이 완결되는가"다.

'use strict';

const assert = require('assert');
const fs = require('fs');
const path = require('path');

const REPO = path.resolve(__dirname, '..', '..');
const HOOKS_DIR = path.join(REPO, '.claude', 'hooks');
const { INCLUDE } = require(path.join(REPO, 'scripts', 'build-package.js'));

let pass = 0, fail = 0;
function test(name, fn) {
  try { fn(); pass++; console.log(`  ✅ ${name}`); }
  catch (e) { fail++; console.log(`  ❌ ${name}: ${e.message} (scripts/build-package.js)`); }
}

const shipped = new Set(INCLUDE.filter(p => p.startsWith('.claude/hooks/')).map(p => p.split('/').pop()));
const localHooks = fs.readdirSync(HOOKS_DIR).filter(f => f.endsWith('.js'));

// 훅 하나가 require 하는 형제 모듈 목록
function siblingDeps(hookFile) {
  const src = fs.readFileSync(path.join(HOOKS_DIR, hookFile), 'utf8');
  const out = new Set();
  for (const m of src.matchAll(/require\(\s*['"]\.\/([A-Za-z0-9_.-]+)['"]\s*\)/g)) {
    out.add(m[1].endsWith('.js') ? m[1] : m[1] + '.js');
  }
  return [...out];
}

console.log('\n═══ PKG: 패키지 의존 폐쇄성 ═══\n');

test('should_ship_every_module_required_by_a_shipped_hook', () => {
  const missing = [];
  for (const hook of localHooks) {
    if (!shipped.has(hook)) continue;                 // 배포되지 않는 훅의 의존은 상관없다
    for (const dep of siblingDeps(hook)) {
      if (shipped.has(dep)) continue;
      if (!fs.existsSync(path.join(HOOKS_DIR, dep))) continue;   // 저장소에도 없으면 죽은 require
      missing.push(`${dep} ← ${hook}`);
    }
  }
  assert.strictEqual(missing.length, 0,
    `배포되는 훅이 require 하는데 패키지에 없음:\n       ${missing.join('\n       ')}\n` +
    '       → scripts/build-package.js 의 INCLUDE 에 추가하세요. ' +
    'try/catch 안에서 require 되므로 빠져도 예외가 나지 않고 기능만 조용히 사라집니다.');
});

test('should_not_list_nonexistent_files_in_include', () => {
  const ghosts = INCLUDE.filter(rel => !fs.existsSync(path.join(REPO, rel)));
  assert.strictEqual(ghosts.length, 0, `INCLUDE 에 없는 파일: ${ghosts.join(', ')}`);
});

test('should_ship_hooks_registered_in_settings_json', () => {
  // settings.json 이 실행하라고 등록한 훅은 반드시 배포되어야 한다.
  //   등록됐는데 파일이 없으면 매 이벤트마다 조용히 실패한다.
  const settings = JSON.parse(fs.readFileSync(path.join(REPO, '.claude', 'settings.json'), 'utf8'));
  const registered = new Set();
  for (const entries of Object.values(settings.hooks || {})) {
    for (const e of entries || []) {
      for (const h of (e.hooks || [])) {
        const m = String(h.command || '').match(/\.claude[\\/]hooks[\\/]([A-Za-z0-9_.-]+\.js)/);
        if (m) registered.add(m[1]);
      }
    }
  }
  assert.ok(registered.size > 0, 'settings.json 에서 훅을 하나도 못 읽었다 — 파싱이 깨졌을 수 있다');
  const notShipped = [...registered].filter(h => !shipped.has(h));
  assert.strictEqual(notShipped.length, 0,
    `settings.json 이 등록했으나 패키지에 없음: ${notShipped.join(', ')}`);
});

test('should_ship_hooks_that_install_js_registers', () => {
  // install.js 의 hookDefs 도 같은 계약이다 — 설치 시 등록해 놓고 파일을 안 넣으면
  //   그 이벤트가 매번 실패한다(사용자에게는 조용히).
  const src = fs.readFileSync(path.join(REPO, 'install.js'), 'utf8');
  const registered = new Set();
  for (const m of src.matchAll(/\.claude\/hooks\/([A-Za-z0-9_.-]+\.js)/g)) registered.add(m[1]);
  const notShipped = [...registered].filter(h => fs.existsSync(path.join(HOOKS_DIR, h)) && !shipped.has(h));
  assert.strictEqual(notShipped.length, 0,
    `install.js 가 참조하나 패키지에 없음: ${notShipped.join(', ')}`);
});

test('should_report_shipped_hook_coverage', () => {
  const unshipped = localHooks.filter(h => !shipped.has(h));
  // 배포하지 않는 훅이 있는 것 자체는 정상(개발 전용). 다만 몇 개인지는 보여준다.
  console.log(`     (훅 ${localHooks.length}개 중 배포 ${shipped.size}개` +
    (unshipped.length ? ` · 미배포: ${unshipped.join(', ')}` : '') + ')');
  assert.ok(shipped.size > 0);
});

// ── [v3.6/N-10/TEST-02] 마크다운 상호참조 폐쇄성 (FR-04) ─────────────
// 기존 폐쇄성 검사는 `.js` 의 `require()` 만 봤다. 그래서 **문서가 가리키는 하네스 파일**이
// 배포에서 빠진 것을 구조적으로 잡지 못했다 — 실제로 `LOOPS.md` 가 빠져 있었고,
// 설치본의 `CLAUDE.md` 와 `memory/SKILL.md` 가 없는 파일을 가리키고 있었다.
test('should_ship_every_harness_path_referenced_by_contract_docs', () => {
  // **계약 문서**만 본다 — 모델이 규칙으로 읽는 것들이다.
  //   `dev-journal.md`·`settings-guide.md` 같은 서술 문서는 지나간 파일을 언급하는 것이
  //   정상이므로 대상이 아니다. 계약 문서가 없는 파일을 가리키면 그것은 **끊긴 지시**다.
  const CONTRACT = /^(CLAUDE\.md|\.claude\/skills\/.*\.md|docs\/Manual\.md)$/;
  const docs = INCLUDE.filter(rel => rel.endsWith('.md') && CONTRACT.test(rel));
  assert.ok(docs.length >= 3, `계약 문서를 못 찾았다: ${docs.join(', ')}`);

  const shippedPaths = new Set(INCLUDE);
  const coveredByDir = ref => INCLUDE.some(inc => inc.endsWith('/') && ref.startsWith(inc));
  const HARNESS_PATH = /(?:^|[\s(`'"])((?:\.claude|docs|tests|scripts)\/[\w./-]+\.(?:md|js|json|jsonl))/g;
  const missing = [];
  for (const doc of docs) {
    let text = '';
    try { text = fs.readFileSync(path.join(REPO, doc), 'utf8'); } catch { continue; }
    for (const m of text.matchAll(HARNESS_PATH)) {
      const ref = m[1].replace(/[.,)]+$/, '');
      if (!fs.existsSync(path.join(REPO, ref))) continue;         // 저장소에도 없으면 죽은 링크(별건)
      if (shippedPaths.has(ref) || coveredByDir(ref)) continue;
      if (ref.startsWith('.claude/hooks/') && shipped.has(ref.split('/').pop())) continue;
      // 런타임에 생기거나 사용자가 만드는 것은 배포 대상이 아니다
      if (/^docs\/(memory\/(sessions|state-backups)|charters|analyses|prds|plans|tests|reports)\//.test(ref)) continue;
      if (/^\.claude\/\.branch-/.test(ref)) continue;
      missing.push(`${ref} ← ${doc}`);
    }
  }
  assert.strictEqual(missing.length, 0,
    `계약 문서가 가리키는데 패키지에 없음:\n       ${[...new Set(missing)].join('\n       ')}\n` +
    '       → INCLUDE 에 추가하거나 문서에서 그 참조를 지우세요.');
});

test('should_ship_the_file_that_default_test_command_points_at', () => {
  // `LOOPS.md` 는 "test_command 는 반드시 샌드박스 러너(tests/run.js)를 가리켜야 한다" 고
  //   지시한다. 그 파일이 배포에 없으면 지시를 따르는 순간 영구 fail 이 된다.
  const loops = path.join(REPO, '.claude', 'skills', 'process', 'LOOPS.md');
  if (!fs.existsSync(loops)) return;                              // 파일이 없으면 이 검사는 성립하지 않는다
  const text = fs.readFileSync(loops, 'utf8');
  for (const m of text.matchAll(/`(tests\/[\w./-]+\.js)`/g)) {
    assert.ok(shipped.has(m[1]) || INCLUDE.includes(m[1]),
      `LOOPS.md 가 지시하는 ${m[1]} 가 패키지에 없다 — 지시를 따르면 영구 fail 이다`);
  }
});


// ── PC-20: 훅 목록이 디렉터리에서 파생된다 (v4/N-12 · FR-02) ──────────
//
// 왜: `scripts/build-package.js` 가 훅 28개를 손으로 나열한다. 새 훅을 만들면 거기
//   등록해야 하고, 잊으면 설치본에서 require 가 죽는다. 이 저장소의 **아홉 번째
//   "잊을 수 있는 목록"** 이다 — N-05 에서 실제로 걸렸고 그때는 새 파일을 만들지
//   않는 것으로 우회했다. 우회는 해결이 아니다.
//
//   여기서 지키는 것은 "지금 28개가 다 있다" 가 아니라 **"파생된다"** 이다 —
//   존재하지 않는 새 훅 파일을 놓았을 때도 담기는지 본다.
console.log('\n[PC-20] should_derive_the_hook_list_from_the_directory');
{
  const HD = path.join(REPO, '.claude', 'hooks');
  const actual = fs.readdirSync(HD).filter(f => f.endsWith('.js'));

  // build-package 가 INCLUDE 를 export 하거나, 최소한 디렉터리를 읽어야 한다
  const src = fs.readFileSync(path.join(REPO, 'scripts', 'build-package.js'), 'utf8');
  const hardcoded = [...src.matchAll(/'\.claude\/hooks\/[\w.-]+\.js'/g)].length;
  test('훅 경로를 손으로 나열하지 않는다', () => assert(hardcoded === 0));
  test('디렉터리를 읽어 파생한다', () => assert(/function hookFiles/.test(src) && /readdirSync/.test(src)));

  // 실제로 파생되는가 — 임시 훅 파일을 놓고 목록을 다시 계산한다
  const probe = path.join(HD, '_pc20-probe.js');
  try {
    fs.writeFileSync(probe, '// probe\nmodule.exports = {};\n');
    delete require.cache[require.resolve(path.join(REPO, 'scripts', 'build-package.js'))];
    let listed = null;
    try {
      const bp = require(path.join(REPO, 'scripts', 'build-package.js'));
      listed = bp && (bp.INCLUDE || bp.includeList || null);
    } catch { listed = null; }
    if (Array.isArray(listed)) {
      test('새 훅 파일이 자동으로 담긴다', () => assert(listed.some(p => String(p).endsWith('_pc20-probe.js'))));
      test('설치본에도 대시보드와 실행 자산이 포함된다', () => { for (const suffix of ['dashboard-server.js', '_ui-server.js', 'process-supervisor.ps1', 'ui/']) assert(listed.some(p => String(p).endsWith(suffix)), suffix); });
    } else {
      test('build-package 가 INCLUDE 를 export 한다', () => assert(false));
      test('개발 전용 훅은 제외된다', () => assert(false));
    }
  } finally { try { fs.unlinkSync(probe); } catch { /* 임시 */ } }

  // 개발 전용 제외 목록은 짧아야 한다 — 길어지면 그것이 다시 목록이다
  const excl = (src.match(/HOOK_EXCLUDE\s*=\s*\[([\s\S]*?)\]/) || [])[1] || '';
  const exclN = (excl.match(/'[^']+'/g) || []).length;
  test('제외 목록이 짧다 (5개 이하)', () => assert(exclN <= 5));
  test('실제 훅 수와 크게 어긋나지 않는다', () => assert(actual.length >= 20));
}

console.log('\n═══════════════════════════════════════════════════════');
console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
