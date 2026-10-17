// tests/unit/test_docs_sync.js
// 문서가 코드와 같은 하네스를 말하는가 — charter harness-v3 / N-11 (FR-09 · D1~D10)
//
// 왜 이 파일이 존재하는가:
//   문서와 코드가 갈라졌고, 그중 둘은 사용자를 **반대로 행동하게** 만들었다.
//   ① Manual 이 "모든 검증 catch 는 block 방향, 안전 우선" 이라고 적었는데 코드는
//      오류를 기록하고 **통과시킨다** — 이 문장을 믿는 보안 검토자는 훅 크래시를
//      안전하다고 결론낸다. 실제로는 판정기를 throw 시키면 모든 규칙이 우회된다.
//   ② 문서 최상단이 "Track A 는 OS 레벨에서 차단된다" 고 말했는데, 같은 파일이
//      239줄 뒤에서 그것을 부정하고 `pre-tool-gate` 는 track 을 한 번도 읽지 않는다.
//
//   손으로 고치기만 하면 다음 사이클에 또 갈라진다 — 이 charter 가 열 번 고친 것이
//   전부 그 형태였다. **지운 서술이 돌아오지 않는지**를 테스트가 지킨다.
//
// 원장의 교훈(D-2026-09-07-5fc264)을 따른다: 낱말 하나를 금지하면 그 이유를 적은 문장이
//   스스로를 위반한다. 그래서 **문장 패턴**을 겨냥하고, 정정 이력을 적은 인용 블록은 센 뒤 뺀다.

'use strict';

const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const REPO = path.resolve(__dirname, '..', '..');
let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 240) : ''}`); }
}
const read = rel => { try { return fs.readFileSync(path.join(REPO, rel), 'utf8'); } catch { return ''; } };

// 인용(`>`)·코드 블록은 "지금 그렇다" 가 아니라 "예전엔 그랬다" 를 적는 자리다.
//   그 안의 문장까지 금지하면 정정 이력 자체를 쓸 수 없다.
function livePraise(text) {
  const out = [];
  let inFence = false;
  for (const line of String(text).split('\n')) {
    if (/^\s*```/.test(line)) { inFence = !inFence; continue; }
    if (inFence) continue;
    if (/^\s*>/.test(line)) continue;
    out.push(line);
  }
  return out.join('\n');
}

console.log('\n═══ DS: 문서 동기화 ═══');

const MANUAL = read('docs/Manual.md');
const README = read('README.md');
const CLAUDE = read('CLAUDE.md');
const TESTSKILL = read('.claude/skills/test/SKILL.md');
const PROCSKILL = read('.claude/skills/process/SKILL.md');
const PRDSKILL = read('.claude/skills/prd/SKILL.md');
const MEMSKILL = read('.claude/skills/memory/SKILL.md');

// ── DS-01: 삭제된 동작이 살아 있는 서술로 돌아오지 않는다 (D1) ────────
console.log('\n[DS-01] should_not_describe_removed_behaviour');
{
  const live = livePraise(MANUAL);
  check('Track A 가 OS 레벨에서 차단한다고 말하지 않는다',
    !/Track A[^\n]*OS 레벨에서 차단/.test(live),
    (live.match(/[^\n]*OS 레벨에서 차단[^\n]*/g) || []).join(' | '));
  check('테스트 실패 시 dev 자동 복귀를 말하지 않는다',
    !/실패[^\n]*dev[^\n]*자동[^\n]*복귀|자동으로 dev 복귀/.test(live),
    (live.match(/[^\n]*자동[^\n]*복귀[^\n]*/g) || []).join(' | '));
  check('폴백 방향을 block 이라고 말하지 않는다',
    !/폴백 방향[^\n]*block/i.test(live),
    (live.match(/[^\n]*폴백 방향[^\n]*/g) || []).join(' | '));
  check('72시간 자동 만료를 약속하지 않는다',
    !/72시간\)? 초과 시[^\n]*자동 삭제/.test(live),
    (live.match(/[^\n]*72시간[^\n]*/g) || []).join(' | '));
}

// ── DS-02: 삭제된 파일을 실행하라고 하지 않는다 (D2) ─────────────────
console.log('\n[DS-02] should_not_point_at_deleted_paths');
{
  for (const [name, text] of [['Manual', MANUAL], ['test/SKILL', TESTSKILL], ['README', README]]) {
    check(`${name} 이 tests/manual 을 가리키지 않는다`, !/tests\/manual/.test(livePraise(text)),
      (text.match(/[^\n]*tests\/manual[^\n]*/g) || []).slice(0, 2).join(' | '));
  }
  check('삭제된 경로가 실제로 없다', !fs.existsSync(path.join(REPO, 'tests', 'manual')));
}

// ── DS-03: 상태 파일 경로 (D3) ───────────────────────────────────────
console.log('\n[DS-03] should_describe_branch_scoped_state');
{
  const live = livePraise(PROCSKILL);
  check('process/SKILL 이 브랜치별 상태를 말한다', /\.branch-/.test(live),
    (live.match(/[^\n]*pipeline-state[^\n]*/g) || []).slice(0, 2).join(' | '));
  check('process/SKILL 이 그 파일을 기계 상태라고 하지 않는다',
    !/docs\/memory\/pipeline-state\.json`? \| \*\*기계적 상태/.test(live));
}

// ── DS-04: v3 의 일상 작업 경로가 문서에 있다 (D4) ───────────────────
console.log('\n[DS-04] should_document_the_daily_v3_path');
for (const term of ['verify', 'task ', 'decide', 'charter', 'escalate', 'Loop V']) {
  check(`Manual 이 ${term.trim()} 를 다룬다`, MANUAL.includes(term),
    `문서 길이 ${MANUAL.length}`);
}

// ── DS-05: verify.lock (D5) ──────────────────────────────────────────
console.log('\n[DS-05] should_document_verify_lock');
check('verify.lock 이 사용자 문서에 있다', /verify\.lock/.test(MANUAL) || /verify\.lock/.test(read('.claude/skills/process/LOOPS.md')),
  'Manual·LOOPS 둘 다 0건');

// ── DS-06: 봉투 선언 절 (D6) ─────────────────────────────────────────
// 이것이 없으면 CLAUDE.md 가 약속한 자동 승인 경로를 PRD 작성자가 밟을 수 없다.
console.log('\n[DS-06] should_teach_the_envelope_declaration');
// PRD 머리말의 실제 표기는 `- **charter**: …` 다 — 그 형태를 가르쳐야 작성자가 따라 쓴다.
for (const key of ['charter', 'node', 'scope_files']) {
  check(`prd/SKILL 이 ${key} 선언을 가르친다`, new RegExp(`\\*\\*${key}\\*\\*`).test(PRDSKILL), null);
}
check('C1~C8 판정을 설명한다', /C1[\s\S]{0,400}C8|C5/.test(PRDSKILL), null);

// ── DS-07: frontmatter 가 실재하는 절을 가리킨다 (D7) ────────────────
console.log('\n[DS-07] should_cite_sections_that_exist');
{
  const heads = (CLAUDE.match(/^#{1,4} .+$/gm) || []).join('\n');
  const cited = [...MEMSKILL.matchAll(/CLAUDE\.md\s*의?\s*"([^"]+)"/g)].map(m => m[1]);
  check('memory/SKILL 이 CLAUDE.md 절을 인용한다', cited.length > 0, MEMSKILL.slice(0, 200));
  for (const c of cited) {
    check(`인용한 절이 실재한다: "${c}"`, heads.includes(c), heads.slice(0, 300));
  }
}

// ── DS-08: 하드 차단 수가 코드와 맞는다 (D8) ─────────────────────────
console.log('\n[DS-08] should_count_hard_blocks_from_code');
{
  const gen = require(path.join(REPO, 'scripts', 'gen-manual-tables.js'));
  const rules = gen.collectGateRules();
  check('코드에서 규칙 이름을 뽑는다', rules.length >= 10, `추출 ${rules.length}종`);
  const live = livePraise(PROCSKILL);
  check('process/SKILL 이 "다섯 지점뿐" 이라고 말하지 않는다',
    !/하드 차단은 다섯 지점뿐/.test(live),
    (live.match(/[^\n]*하드 차단[^\n]*/g) || []).join(' | '));
  check('그 밖은 전부 advisory 라고 말하지 않는다',
    !/그 밖의 모든 것은 \*\*advisory\*\*/.test(live),
    (live.match(/[^\n]*advisory[^\n]*/g) || []).slice(0, 2).join(' | '));
}

// ── DS-09: 생성 블록이 소스와 일치한다 (D9) ──────────────────────────
console.log('\n[DS-09] should_keep_generated_blocks_in_sync');
{
  const r = spawnSync(process.execPath, [path.join(REPO, 'scripts', 'gen-manual-tables.js'), '--check'], {
    cwd: REPO, encoding: 'utf8', timeout: 60000,
  });
  check('생성 블록이 소스와 일치한다', r.status === 0, (r.stdout || '') + (r.stderr || ''));
  for (const m of ['gen:subcommands', 'gen:audit-events', 'gen:gate-rules']) {
    check(`Manual 에 ${m} 마커가 있다`, MANUAL.includes(`<!-- ${m} -->`), null);
  }
  check('생성기가 조건 열을 만들지 않는다고 밝힌다', /이름 열만 생성/.test(MANUAL), null);
}

// ── DS-10: 버전 (D10) ────────────────────────────────────────────────
console.log('\n[DS-10] should_match_the_declared_version');
{
  const v = (CLAUDE.match(/version:\s*"([^"]+)"/) || [])[1];
  check('CLAUDE.md 에서 버전을 읽는다', !!v, CLAUDE.slice(0, 200));
  check(`Manual 버전이 ${v} 다`, MANUAL.includes(`**버전**: ${v}`),
    (MANUAL.match(/\*\*버전\*\*:[^\n]*/) || [''])[0]);
  check(`README 버전이 ${v} 다`, README.includes(v),
    (README.match(/v\d+\.\d+\.\d+/g) || []).slice(0, 3).join(' | '));
}

// ── DS-11: 훅 수치 ───────────────────────────────────────────────────
console.log('\n[DS-11] should_not_state_a_stale_hook_count');
{
  const gen = require(path.join(REPO, 'scripts', 'gen-manual-tables.js'));
  const hooks = gen.collectHooks();
  const scripts = new Set(hooks.map(h => h.script));
  check('settings.json 에서 훅을 센다', scripts.size >= 6, `${scripts.size}종`);
  check('README 가 "4개 Hook" 이라고 말하지 않는다', !/4개 Hook|4 Hook/.test(livePraise(README)),
    (README.match(/[^\n]*Hook[^\n]*/g) || []).slice(0, 2).join(' | '));
}


// ── DS-12: 버전이 사는 모든 곳이 한 함수로 갱신된다 (v4/N-03) ─────────
// 왜: `syncInstallVersion` 은 install.js 하나만 봤고 주석은 "두 곳에 산다" 고 적혀 있었다.
//   N-11 이 문서 벽을 세우며 네 곳이 됐을 때 함수가 따라오지 않았고,
//   complete 의 자동 범프가 **하네스 자신의 스위트를 깨뜨렸다**(2026-09-08 실측).
//   DS-10 이 값의 일치를 보므로, 이 절은 그 일치를 **만드는 경로**를 본다.
console.log('\n[DS-12] should_sync_every_version_site_from_one_place');
{
  const os = require('os');
  const src = read('.claude/hooks/advance-phase-changelog.js');
  check('대상이 표로 선언돼 있다', /VERSION_SITES\s*=\s*\[/.test(src), '');
  for (const f of ['install.js', 'docs/Manual.md', 'README.md']) {
    check('표가 ' + f + ' 를 담는다', src.includes("file: '" + f + "'"), '');
  }

  // 실제로 작동하는가 — 훅을 임시 저장소로 복사해 PROJECT_ROOT 가 거기로 잡히게 한다
  //   (_sandbox.js 와 같은 방식: 훅은 path.resolve(__dirname,'..','..') 로 루트를 계산한다)
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'ver-'));
  try {
    fs.mkdirSync(path.join(dir, 'docs'), { recursive: true });
    fs.mkdirSync(path.join(dir, '.claude', 'hooks'), { recursive: true });
    const HS = path.join(REPO, '.claude', 'hooks');
    for (const f of fs.readdirSync(HS)) {
      if (f.endsWith('.js')) fs.copyFileSync(path.join(HS, f), path.join(dir, '.claude', 'hooks', f));
    }
    fs.writeFileSync(path.join(dir, 'CLAUDE.md'),
      ['```yaml', 'version: "9.9.9"', 'last_updated: "2020-01-01"', '```', ''].join('\n'));
    fs.writeFileSync(path.join(dir, 'install.js'), "const HARNESS_VERSION = '0.0.1';\n");
    fs.writeFileSync(path.join(dir, 'docs', 'Manual.md'), '- **버전**: 0.0.1\n');
    fs.writeFileSync(path.join(dir, 'README.md'), '# Skill-Set v0.0.1\n\n- **버전**: v0.0.1\n');

    const r = spawnSync(process.execPath, ['-e',
      "require('./.claude/hooks/advance-phase-changelog.js').writeClaudeMdVersion('9.9.9','2026-09-08')"],
      { cwd: dir, encoding: 'utf8', timeout: 60000 });
    check('동기화 호출이 예외로 죽지 않는다', r.status === 0, (r.stderr || '').slice(0, 200));

    const got = f => { try { return fs.readFileSync(path.join(dir, f), 'utf8'); } catch { return ''; } };
    const SITES = [
      ['install.js', /HARNESS_VERSION = '9\.9\.9'/],
      ['docs/Manual.md', /\*\*버전\*\*: 9\.9\.9/],
      ['README.md', /# Skill-Set v9\.9\.9/],
      ['README.md', /\*\*버전\*\*: v9\.9\.9/],
    ];
    for (const [f, re] of SITES) {
      check(f + ' 가 갱신된다', re.test(got(f)), got(f).slice(0, 80));
    }
  } finally { try { fs.rmSync(dir, { recursive: true, force: true }); } catch { /* 임시 */ } }
}


// ── DS-13: Manual 이 v4 의 네 계약을 말한다 (v4/N-12 · FR-01·FR-03) ───
// 왜: 이 charter 가 하네스의 계약을 넷 바꿨는데 사용자가 읽는 문서는 하나도 몰랐다.
//   v3/N-11 이 세운 원칙 그대로 — 손으로 고치기만 하면 다음 사이클에 또 갈라진다.
console.log('\n[DS-13] should_document_the_v4_quality_contracts');
{
  const live = livePraise(MANUAL);
  for (const [what, re] of [
    ['반증 짝', /반증 짝/],
    ['--expect-fail 사용법', /--expect-fail/],
    ['--no-red 탈출로', /--no-red/],
    ['cover: 스펙 문법', /cover:<glob>|cover:.*nodrop/],
    ['오염 탐지', /오염/],
    ['단언 기준선', /assert-counts\.json/],
    ['--accept-drop', /--accept-drop/],
  ]) {
    check(`Manual 이 ${what} 를 다룬다`, re.test(live), `문서 길이 ${MANUAL.length}`);
  }
  // 강제가 아니라 기울기라는 것 — 이 설계 판단이 지워지면 다음 사람이 벽으로 오해한다
  check('탈출로가 원장을 요구한다고 밝힌다', /needsDecision|결정 행 없이/.test(live), '');
}

console.log('\n[DS-14] should_document_the_v7_blast_radius_contracts');
{
  // v7 을 부른 두 결함은 **둘 다 문서의 침묵과 짝이었다.**
  //   ① Manual 은 "업그레이드하면 훅이 커밋 후보로 뜬다" 까지만 말하고, 그것이
  //      **열려 있는 사이클**에 무슨 뜻인지는 말한 적이 없다 — 다운스트림이 겪고서야 알았다.
  //   ② 설치 예시가 전부 `--target` 이라, 사람이 실제로 쓰는 위치 인자 형태가
  //      무시된다는 사실을 어디서도 읽을 수 없었다.
  //   고친 계약을 문서에 적지 않으면 다음 사람이 같은 자리에서 같은 것을 겪는다.
  //
  // 단언은 **계약어**를 겨냥한다(`vendor` · 위치 인자 · 열린 사이클). 문장 표현을 통째로
  //   잡으면 그 이유를 적은 문장이 스스로를 위반한다 — D-2026-09-07-5fc264.
  const live = livePraise(MANUAL);

  // ① 업그레이드와 열린 사이클
  check('Manual 이 vendor 판정을 다룬다', /vendor/.test(live), `문서 길이 ${MANUAL.length}`);
  check('Manual 이 하네스 설치물을 노드의 이탈로 세지 않는다고 밝힌다',
    /이탈로 세지 않/.test(live), '');
  check('Manual 이 열린 사이클에 주는 영향을 말한다',
    /열린 사이클|열려 있는 사이클/.test(live), '');
  // 막지 않는 것과 모르는 척하는 것은 다르다 — 문서도 그 구분을 지켜야 한다
  check('Manual 이 그 변경이 조용히 넘어가지 않음을 밝힌다',
    /감사|화면에 남|알린다/.test(live.slice(live.indexOf('vendor'))), '');

  // ② 설치 대상
  check('Manual 의 설치 예시에 위치 인자 형태가 있다',
    /node install\.js\s+(?:[A-Za-z]:[\\/]|\.{0,2}\/)[^\s\n]*/.test(MANUAL),
    (MANUAL.match(/node install\.js[^\n]*/g) || []).slice(0, 6).join(' | '));
  check('Manual 이 해석할 수 없는 인자를 거부한다고 밝힌다',
    /거부한다|거부합니다/.test(live) && /인자/.test(live), '');
  check('README 가 위치 인자 형태를 보인다',
    /node install\.js\s+(?:[A-Za-z]:[\\/]|\.{0,2}\/)[^\s\n]*/.test(README),
    (README.match(/node install\.js[^\n]*/g) || []).slice(0, 6).join(' | '));

  // ③ 범위 판정은 이제 4상태다 — `vendor` 와 `out_of_scope` 는 **다른 것**이다
  const skill = read('.claude/skills/process/SKILL.md');
  check('process/SKILL 이 4상태라고 말한다', /4상태/.test(skill), '');
  check('process/SKILL 이 vendor 와 out_of_scope 를 구분한다',
    /vendor[\s\S]{0,400}out_of_scope|out_of_scope[\s\S]{0,400}vendor/.test(skill)
    && /보호/.test(skill) && /면제/.test(skill), '');
  check('Manual 이 범위 판정값을 4개 다 담는다',
    ['in-node', 'other-node', 'vendor', 'outside'].every(v => MANUAL.includes(v)),
    ['in-node', 'other-node', 'vendor', 'outside'].filter(v => !MANUAL.includes(v)).join(', '));
}

console.log('\n[DS-15] should_document_the_v8_suite_side_effect_contracts');
{
  // Manual §오염 탐지는 "테스트는 저장소를 건드리지 않는다" 고 약속했다. v8 이전에는 **거짓**이었다 —
  //   검증 빌드가 매 실행 릴리스 zip 을 덮어썼고, 그것을 잡을 벽은 dist/ 를 면제받고 있었다.
  //   문서는 그 면제를 한 번도 말하지 않았다. 약속이 참이 된 지금 그 사실을 그 자리에 적는다.
  // 단언은 **계약어**를 겨냥한다 — D-2026-09-07-5fc264.
  const live = livePraise(MANUAL);
  const sec = (() => {
    const i = live.indexOf('오염 탐지 — 테스트는 저장소를 건드리지 않는다');
    return i === -1 ? '' : live.slice(i, i + 4000);
  })();
  check('Manual §오염 탐지가 있다', sec.length > 0, '');
  check('§오염 탐지가 검증 빌드의 출력 위치(--out)를 말한다', /--out/.test(sec), sec.slice(0, 200));
  check('§오염 탐지가 검증 빌드는 저장소 밖에 쓴다고 말한다', /저장소 밖/.test(sec), '');
  check('§오염 탐지가 벽이 잡으면 면제가 아니라 쓰기를 고친다고 밝힌다',
    /쓰기를 고친다/.test(sec) && /면제/.test(sec), '');
  // 실물 원장 — 완화는 "쓰이는 중인 마지막 한 줄" 뿐이다. 그 경계를 문서가 말해야 한다.
  check('Manual 이 실물 원장의 꼬리 보류 조건(개행)을 말한다',
    /원장/.test(sec) && /개행/.test(sec) && /보류/.test(sec), '');

  const CL = read('CHANGELOG.md');
  const top = CL.slice(CL.indexOf('<!-- changelog-entries-start -->'), CL.indexOf('<!-- changelog-entries-start -->') + 6000);
  // [v10/N-02] 최상단 버전을 리터럴로 적으면 complete 의 자동 버전 올림마다 빨개진다 — 3.3.1 → 3.4.0 에서 실제로 깨졌다.
  //   최상단은 CLAUDE.md 의 version 과 같아야 한다(VERSION_SITES 와 같은 규칙). 3.3.1 의 내용은 그 절을 찾아서 본다.
  const claudeVer = (read('CLAUDE.md').match(/^version:\s*"([^"]+)"/m) || [])[1];
  const topVer = (top.match(/##\s*\[([^\]]+)\]/) || [])[1];
  check('CHANGELOG 최상단 버전이 CLAUDE.md 의 version 과 같다', !!claudeVer && topVer === claudeVer, `top=${topVer} CLAUDE.md=${claudeVer}`);
  const v331 = (() => {
    const i = CL.search(/##\s*\[3\.3\.1\]/);
    if (i < 0) return '';
    const next = CL.slice(i + 1).search(/\n##\s*\[/);
    return next < 0 ? CL.slice(i) : CL.slice(i, i + 1 + next);
  })();
  check('CHANGELOG 3.3.1 이 봉투 이름을 적는다', /harness-v8-suite-side-effects/.test(v331), v331.slice(0, 120) || '(3.3.1 절 없음)');
}

console.log('\n[DS-16] should_document_the_v10_upgrade_integrity_contracts');
{
  // v10 의 네 결함은 **넷 다 다운스트림이 겪고서야 알았다.** 옛 브랜치 상태가 템플릿 복사본을 들고 왔고,
  //   세션 배너가 빈칸 문구를 작업처럼 찍었고, 사이클 커밋이 남의 스테이징을 담았고, dry-run 이 대상에 썼다.
  //   고친 계약을 약속이 적힌 자리에 적지 않으면 다음 사람이 같은 자리에서 같은 것을 겪는다.
  // 단언은 **계약어**를 겨냥한다(`pipeline-state.legacy-` · `quarantinedLegacy` · `경로를 명시` · `로그 파일`) — D-2026-09-07-5fc264.
  const live = livePraise(MANUAL);
  // 다운스트림 주의 두 가지의 판정은 함수로 둔다 — ⑥ 이 같은 판정을 **틀린 사본**에 대 본다.
  //   두 단어의 근처가 아니라 **계약어가 한 흐름에** 있어야 한다(Loop A): 경고 → 자동으로 해소되지 않는다 → new-cycle ·
  //   비ASCII → 관측 → touch reconcile. 문서 전체에서 따로 찾으면 무관한 절(CLI 표)이 대신 초록을 준다.
  const saysNotAutoResolved = t => /옛 템플릿[\s\S]{0,300}자동으로 해소되지 않는다[\s\S]{0,300}new-cycle/.test(t);
  const saysNonAsciiVisible = t => /비ASCII[\s\S]{0,400}관측[\s\S]{0,400}touch reconcile/.test(t);

  // ① N-01 옛 브랜치 상태
  check('Manual 이 격리 보존 파일 이름(pipeline-state.legacy-)을 말한다', /pipeline-state\.legacy-/.test(live), '');
  check('Manual 상태 스키마가 seededFrom · quarantinedLegacy 를 담는다', /seededFrom/.test(live) && /quarantinedLegacy/.test(live), '');
  check('Manual 이 자동으로 해소되지 않는 옛 상태(경고 → 사용자 판단)를 밝힌다', saysNotAutoResolved(live), '');

  // ② N-02 세션 배너
  check('Manual 이 스테일 세션 요약(앞 3건 + 외 N건)을 말한다', /외 N건|외 \d+건|앞 3건/.test(live), '');
  check('Manual 이 배너가 보인 세션을 아카이브에서도 이어서 할 수 있다고 밝힌다', /아카이브/.test(live) && /이전 세션 이어서/.test(live), '');

  // ③ N-03 사이클 커밋
  const sec = (() => { const i = live.indexOf('사이클 종료 자동 처리'); return i === -1 ? '' : live.slice(i, i + 3000); })();
  check('§사이클 종료 자동 처리가 있다', sec.length > 0, '');
  check('사이클 커밋이 경로를 명시한다고 말한다', /경로를 명시/.test(sec), sec.slice(0, 200));
  check('미리 스테이징된 사이클 밖 파일은 커밋되지 않고 스테이징이 남는다고 말한다', /스테이징/.test(sec) && /남는다|그대로/.test(sec), '');
  check('버전 올림이 고친 버전 사이트를 어떻게 다루는지 말한다', /버전 사이트|README\.md/.test(sec), '');
  check('비ASCII 파일이 관측 게이트에 보인다는 동작 변화를 밝힌다', saysNonAsciiVisible(live), '');

  // ④ N-04 dry-run
  check('Manual 이 dry-run 은 로그 파일도 만들지 않는다고 말한다', /dry-run[\s\S]{0,300}로그 파일|로그 파일[\s\S]{0,300}dry-run/.test(live), '');
  check('Manual 이 --rollback --dry-run 도 복원하지 않는다고 말한다', /--rollback --dry-run|rollback[\s\S]{0,120}dry-run/.test(live), '');

  // ⑤ CHANGELOG — 3.4.0 절이 봉투 이름을 적는다
  const CL = read('CHANGELOG.md');
  const v340 = (() => {
    const i = CL.search(/##\s*\[3\.4\.0\]/);
    if (i < 0) return '';
    const next = CL.slice(i + 1).search(/\n##\s*\[/);
    return next < 0 ? CL.slice(i) : CL.slice(i, i + 1 + next);
  })();
  check('CHANGELOG 3.4.0 이 봉투 이름을 적는다', /harness-v10-upgrade-integrity/.test(v340), v340.slice(0, 160) || '(3.4.0 절 없음)');

  // ⑥ 반증 — 다운스트림 주의 두 판정은 **틀린 Manual 에서 빨개져야** 한다.
  //   Loop A(v10/N-05): 두 단어가 근처에 있는지만 보면 정반대 문장("자동으로 정리된다")도 초록이고,
  //   문서 전체에서 따로 찾으면 관측 변화 문단을 지워도 무관한 CLI 표의 `touch reconcile` 로 초록이다.
  //   변형이 헛돌면(원문과 같으면) 반증도 공허다 — 그것도 본다.
  const flipped = live.replace('자동으로 해소되지 않는다', '즉시 자동으로 정리된다');
  check('반증: 자동 해소를 뒤집은 Manual 에서는 자동 해소 판정이 거짓이다',
    flipped !== live && !saysNotAutoResolved(flipped), flipped === live ? '변형이 일어나지 않았다' : '뒤집힌 문장에도 참');
  const dropped = live.replace(/이제는 보이므로[\s\S]*?관측을 보정한다\./, '');
  check('반증: 비ASCII 관측 변화 문단을 지운 Manual 에서는 비ASCII 판정이 거짓이다',
    dropped !== live && !saysNonAsciiVisible(dropped), dropped === live ? '변형이 일어나지 않았다' : '문단을 지워도 참');
}

console.log('\n[DS-17] should_document_the_v11_cycle_truth_contracts');
{
  // v11 은 사람이 **그대로 믿고 행동하는** 출력 둘을 고쳤다 — 새 사이클의 롤백 명령과 INDEX 의 PRD 상태 열.
  //   약속이 적힌 자리에 적지 않으면 설치본 사용자는 경고로 바뀐 롤백 줄과 `—` 를 결함으로 읽는다.
  // 판정은 계약어를 한 흐름으로 겨냥하고, 사람이 뒤집기 쉬운 둘은 **틀린 사본에서 빨개지는지** 함께 본다(v10/N-05 리뷰 교훈).
  const live = livePraise(MANUAL);
  const saysCycleOwnsRollback = t => /롤백 포인트는 그 사이클의 시작이다[\s\S]{0,200}물려받지 않는다[\s\S]{0,300}new-cycle/.test(t);
  const saysStaleTagWarns = t => /사이클 시작\(`headAtStart`\)보다 \*\*앞\*\*이면 reset 을 안내하지 않는다[\s\S]{0,400}advance-phase\.js checkpoint/.test(t);
  const saysIndexStatusLine = t => /PRD 상태 열은 PRD 의 상태줄[\s\S]{0,400}`—`\(판정 불가\)[\s\S]{0,400}INDEX 표시에만/.test(t);

  check('Manual 이 롤백 포인트는 그 사이클의 시작이고 물려받지 않는다고 말한다 (new-cycle)', saysCycleOwnsRollback(live), '');
  check('Manual 이 사이클 시작보다 앞선 태그로는 reset 을 안내하지 않는다고 말한다', saysStaleTagWarns(live), '');
  check('Manual 이 INDEX PRD 상태 열의 출처(상태줄) · — · INDEX 표시에만 을 말한다', saysIndexStatusLine(live), '');

  const CL17 = read('CHANGELOG.md');
  const v341 = (() => {
    const i = CL17.search(/##\s*\[3\.4\.1\]/);
    if (i < 0) return '';
    const next = CL17.slice(i + 1).search(/\n##\s*\[/);
    return next < 0 ? CL17.slice(i) : CL17.slice(i, i + 1 + next);
  })();
  check('CHANGELOG 3.4.1 이 봉투 이름을 적는다', /harness-v11-cycle-truth/.test(v341), v341.slice(0, 160) || '(3.4.1 절 없음)');

  // 반증 — 판정이 참인 문서에서만 의미가 있다. 치환이 헛돌면(원문과 같으면) 반증도 공허하다.
  const flippedWarn = live.replace('reset 을 안내하지 않는다', 'reset 을 그대로 안내한다');
  check('반증: 앞선 태그 경고를 뒤집은 Manual 에서는 경고 판정이 거짓이다',
    flippedWarn !== live && !saysStaleTagWarns(flippedWarn), flippedWarn === live ? '변형이 일어나지 않았다' : '뒤집힌 문장에도 참');
  const draftAgain = live.replace('`—`(판정 불가)로 보인다', '`Draft` 로 보인다');
  check('반증: 상태줄 없는 문서를 Draft 로 적은 Manual 에서는 INDEX 판정이 거짓이다',
    draftAgain !== live && !saysIndexStatusLine(draftAgain), draftAgain === live ? '변형이 일어나지 않았다' : 'Draft 로 적어도 참');
}

console.log('\n[DS-18] should_generate_the_codex_entry_from_the_canonical_file');
{
  // [v16/N-04] 왜: `AGENTS.md` 는 **이미 갈라져 있었다.** `CLAUDE.md` 의 §현재 실행 방식과
  //   같은 주제를 다른 문장(영어)으로 손으로 적었고, 어느 쪽도 다른 쪽을 가리키지 않았다.
  //   가장 비싼 누락은 **원장 규율 전체**였다 — `decide` CLI · `rejected[]`·`invalidation` 필수 ·
  //   `decisions.jsonl` 이 진실. Codex 세션이 이것을 모르면 Codex 가 내린 결정이 원장에 남지 않고,
  //   다음 세션은 어느 런타임이든 같은 논의를 처음부터 다시 한다 — 하네스의 존재 이유가 새는 것이다.
  //
  //   설정으로는 막을 수 없다: `project_doc_fallback_filenames` 는 사용자 홈에 있고
  //   프로젝트 `.codex/` 는 `trust_level` 이 필요하다(원장 D-2026-09-17-57ca49 가 기각).
  //   그래서 **내용을 물리적으로 넣고**, 그 내용을 손이 아니라 생성기가 넣는다.
  //
  //   판정은 **한 벌**이다 — 원장 규율 유무를 보는 술어를 아래 반증이 그대로 쓴다.
  const gen = require(path.join(REPO, 'scripts', 'gen-manual-tables.js'));
  const AGENTS = read('AGENTS.md');

  // ① 설치 계약을 깨지 않는다 — install.js 의 setupCodexEntry 는 harness-v14 마커로 병합한다.
  //    생성 마커는 그 **안쪽**에 있어야 한다. 밖으로 나가면 설치본에 생성물이 실리지 않는다.
  check('AGENTS.md 에 harness-v14 병합 마커가 남아 있다',
    /<!-- harness-v14:start -->[\s\S]*<!-- harness-v14:end -->/.test(AGENTS), AGENTS.slice(0, 120));
  const v14 = (AGENTS.match(/<!-- harness-v14:start -->([\s\S]*?)<!-- harness-v14:end -->/) || [, ''])[1];
  check('생성 마커가 병합 마커 안쪽에 있다',
    v14.includes('<!-- gen:harness-entry -->') && v14.includes('<!-- /gen:harness-entry -->'),
    v14.slice(0, 200));

  // ② 블록이 생성기 출력과 **바이트 단위로** 같다. applyBlocks 는 다르면 이름을 돌려준다.
  const renderer = gen.BLOCKS && gen.BLOCKS['gen:harness-entry'];
  check('생성기가 gen:harness-entry 블록을 안다', typeof renderer === 'function',
    Object.keys(gen.BLOCKS || {}).join(', '));
  if (typeof renderer === 'function') {
    const { changed } = gen.applyBlocks(AGENTS);
    check('AGENTS.md 의 블록이 생성기 출력과 바이트 단위로 같다',
      !changed.includes('gen:harness-entry'),
      '드리프트: ' + changed.join(', '));
  } else {
    check('AGENTS.md 의 블록이 생성기 출력과 바이트 단위로 같다', false, '생성기에 블록이 없다');
  }

  // ③ 원장 규율이 Codex 에도 전달된다 — 이 판정을 반증이 그대로 쓴다.
  const carriesLedgerRules = t =>
    /advance-phase\.js decide/.test(t) &&
    /rejected\[\]/.test(t) && /invalidation/.test(t) &&
    /decisions\.jsonl/.test(t) && /거부한다/.test(t);
  check('블록이 원장 규율을 담는다 (decide · rejected[] · invalidation · 원장이 진실)',
    carriesLedgerRules(v14), v14.slice(0, 300));

  // ③-b 세 절이 **전부** 들어왔다. [Loop A 정합 렌즈] 처음 판은 원장 규율만 봤다 —
  //    헤딩 하나가 개편으로 바뀌어 §현재 실행 방식이 통째로 빠져도 빨개지지 않았다.
  const canon = (gen.CANONICAL_SECTIONS || []);
  check('생성기가 런타임 중립 절 3개를 선언한다', canon.length === 3, JSON.stringify(canon));
  // 정규식 문자열 대신 줄 단위로 본다 — `new RegExp('^##\\s+…')` 는 이스케이프가 한 겹
  // 벗겨지면 `##s+` 가 되어 **조용히 항상 거짓**이 된다(이 파일을 쓰다 실제로 겪었다).
  const hasHeading = (t, w) => t.split('\n').some(l => l.replace(/\r$/, '').startsWith('## ' + w));
  const carriesEverySection = t => canon.length === 3 && canon.every(w => hasHeading(t, w));
  check('블록이 세 절을 전부 담는다 (현재 실행 방식 · 매 응답 후 · 참조 경로)',
    carriesEverySection(v14), (v14.match(/^##\s+.+$/gm) || []).join(' | '));

  // ④ 32 KiB 상한. **숫자는 한 벌이다** — 생성기가 쓰기 경로에서 쓰는 그 상수를 가져온다.
  //    [Loop A 보안 렌즈] 처음 판은 이 테스트에만 상한이 있었다. 461초 스위트를 생략한
  //    문서 수정 커밋은 상한을 넘겨도 아무 데서도 막히지 않았다.
  const CAP = gen.AGENTS_MD_CAP;
  check('생성기가 상한 상수를 내보낸다 (판정이 한 벌이다)', CAP === 32 * 1024, String(CAP));
  const underCap = t => Buffer.byteLength(t, 'utf8') <= CAP;
  check(`AGENTS.md 가 ${CAP} 바이트 이하다`, underCap(AGENTS),
    `${Buffer.byteLength(AGENTS, 'utf8')} 바이트`);
  const genSrc = read('scripts/gen-manual-tables.js');
  check('상한이 쓰기 경로에서도 판정된다', /AGENTS_MD_CAP[\s\S]{0,200}쓰지 않았습니다/.test(genSrc),
    (genSrc.match(/[^\n]*AGENTS_MD_CAP[^\n]*/g) || []).slice(0, 4).join(' | '));

  // ⑤ 대조군 — 정본을 통째로 베끼지 않는다. 프로젝트 고유 값(yaml 블록)이 섞이면
  //    build-package 의 템플릿 치환이 닿지 않는 **세 번째 동기화 지점**이 생긴다.
  check('대조군: 프로젝트 고유 설정(project_name)이 블록에 섞이지 않는다',
    !/project_name/.test(v14), (v14.match(/[^\n]*project_name[^\n]*/g) || []).join(' | '));

  // ⑤-b 블록이 스스로 지시하는 재생성 명령이 **설치본에도 있다.**
  //    [Loop A 중복 렌즈] 생성기는 패키지 INCLUDE 에는 있었지만 install.js 가 복사하지
  //    않아 zip 에만 있고 프로젝트에는 없었다 — 블록이 실행 불가능한 명령을 지시했다.
  {
    const cmd = /node scripts\/gen-manual-tables\.js/.test(v14);
    check('블록이 재생성 명령을 지시한다', cmd, v14.slice(0, 200));
    const inst = read('install.js');
    check('install.js 가 그 스크립트를 설치한다', /'scripts\/gen-manual-tables\.js'/.test(inst),
      (inst.match(/[^\n]*유틸리티 파일[\s\S]{0,200}/) || [''])[0].slice(0, 200));
    const pkg = read('scripts/build-package.js');
    check('패키지도 그 스크립트를 담는다', /'scripts\/gen-manual-tables\.js'/.test(pkg), '');
  }

  // ⑥ 반증 — 절 하나를 잃은 정본에서는 생성이 **거부**된다(fail-closed).
  //    치환이 헛돌면(원문과 같으면) 반증도 공허하다 — 그것도 본다.
  if (typeof renderer === 'function') {
    const CLAUDE_SRC = read('CLAUDE.md');
    const refuses = text => { try { renderer(text); return false; } catch { return true; } };
    check('대조군: 정본 그대로는 생성이 거부되지 않는다', !refuses(CLAUDE_SRC), '');

    const stripped = CLAUDE_SRC.replace(/\n## 매 응답 후 —[\s\S]*?(?=\n## )/, '\n');
    check('반증: 원장 규율 절을 지운 CLAUDE.md 에서는 생성이 거부된다',
      stripped !== CLAUDE_SRC && refuses(stripped),
      stripped === CLAUDE_SRC ? '변형이 일어나지 않았다' : '지워도 생성된다');

    // 절이 사라지는 흔한 형태는 삭제가 아니라 **이름 변경**이다 — 개편·오타.
    const renamed = CLAUDE_SRC.replace('\n## 현재 실행 방식', '\n## 새 실행 방식');
    check('반증: 헤딩 이름이 바뀐 CLAUDE.md 에서도 생성이 거부된다',
      renamed !== CLAUDE_SRC && refuses(renamed),
      renamed === CLAUDE_SRC ? '변형이 일어나지 않았다' : '이름이 바뀌어도 조용히 생성된다');

    // 모호함도 거부한다 — 같은 이름에 두 절이 걸리면 어느 것이 정본인지 알 수 없다.
    const dup = CLAUDE_SRC.replace('\n## 참조 경로', '\n## 참조 경로 (구)\n\n낡음\n\n## 참조 경로');
    check('반증: 한 이름에 절이 둘이면 생성이 거부된다',
      dup !== CLAUDE_SRC && refuses(dup),
      dup === CLAUDE_SRC ? '변형이 일어나지 않았다' : '모호해도 생성된다');
  } else {
    check('반증: 원장 규율 절을 지운 CLAUDE.md 에서는 생성이 거부된다', false, '생성기에 블록이 없다');
  }

  // ⑦ 반증 — 부풀린 사본에서는 상한 판정이 거짓이 된다.
  const inflated = AGENTS + 'x'.repeat(CAP + 1);
  check('반증: 33 KiB 로 부풀린 사본은 상한 판정이 거짓이다',
    !underCap(inflated), `${Buffer.byteLength(inflated, 'utf8')} 바이트인데도 참`);

  // ⑧ 마커가 사라지면 --check 가 거부한다. [Loop A 정합 렌즈] 예전에는 "건너뜀" 만 찍고
  //    exit 0 이었다 — 생성물이 통째로 사라졌는데 초록불이다.
  check('마커 없는 TARGET 은 계약 위반으로 거부된다',
    /생성 마커가 없습니다[\s\S]{0,120}exitCode = 1/.test(genSrc)
    || /\[MISSING\][\s\S]{0,200}exitCode = 1/.test(genSrc),
    (genSrc.match(/[^\n]*생성 마커[^\n]*/g) || []).join(' | '));
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
