#!/usr/bin/env node
// scripts/gen-manual-tables.js — 문서의 **이름 열**을 소스에서 생성한다
//
// [v3.6/N-11/IMPL-01] 왜 이 파일이 존재하는가:
//   문서와 코드가 갈라졌다. 서브커맨드는 27개인데 Manual 은 6개만 다뤘고, audit 이벤트는
//   85종인데 표는 7행이었으며 그중 2개는 emitter 조차 없었다. 손으로 유지하는 표는
//   **반드시** 갈라진다 — 이 charter 가 열 번 고친 것이 전부 그 형태였다.
//
// ⚠ 이 생성기는 **이름 열까지만** 약속한다.
//   조건·설명 열은 코드에서 뽑을 수 없다: 규칙 조건은 최대 47줄 인라인 술어이고, 런타임
//   상태에 의존하며, 교차 모듈 판정기를 부르고, git 규칙 넷은 단일 `if` 에 묶여 순서가
//   암묵적이다. 그리고 **실제로 드리프트한 것은 정확히 조건 열이었다**
//   (규칙 1 이 감사 장부로, 규칙 2 가 charters 로 조용히 넓어졌다).
//   그러므로 조건 열은 사람이 유지하고, 이 생성기는 "무엇이 있는가" 만 고정한다.
//
// 사용법:
//   node scripts/gen-manual-tables.js            마커 블록을 갱신한다
//   node scripts/gen-manual-tables.js --check    갱신 없이 차이만 보고한다(테스트용)
//   node scripts/gen-manual-tables.js --print    생성 결과를 표준출력으로

'use strict';

const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const HOOKS = path.join(ROOT, '.claude', 'hooks');
const read = rel => { try { return fs.readFileSync(path.join(ROOT, rel), 'utf8'); } catch { return ''; } };

// ── 1. advance-phase 서브커맨드 ──────────────────────────────────────
// 중앙 레지스트리가 없다. `targetPhase === 'X'` 분기 **와** 배열 분기 둘 다 봐야 한다 —
// 정규식 하나만 쓰면 `session-clear` 를 놓친다(실측).
function collectSubcommands() {
  const src = read('.claude/hooks/advance-phase.js');
  const set = new Set();
  for (const m of src.matchAll(/targetPhase === '([a-z-]+)'/g)) set.add(m[1]);
  for (const m of src.matchAll(/\[([^\]]*)\]\.includes\(targetPhase\)/g)) {
    for (const q of m[1].matchAll(/'([a-z-]+)'/g)) set.add(q[1]);
  }
  // phase 이름은 서브커맨드이면서 파이프라인 단계다 — 따로 표시한다
  const phases = new Set();
  const gates = read('.claude/hooks/_gates.js');
  const pm = gates.match(/const PHASES = \[([\s\S]*?)\]/);
  if (pm) for (const q of pm[1].matchAll(/'([a-z-]+)'/g)) phases.add(q[1]);

  // 서브-서브커맨드
  //   `sub === 'X'` 는 charter 블록 안에서만 charter 의 것이다 — 파일 전체를 훑으면
  //   task/touch 의 `reconcile` 이 charter 목록으로 새어 들어온다(실측).
  const subs = {};
  const charterBlock = (src.match(/targetPhase === 'charter'[\s\S]{0,6000}/) || [''])[0];
  const charterSub = [...charterBlock.matchAll(/sub === '(\w+)'/g)].map(m => m[1]);
  if (charterSub.length) subs.charter = [...new Set(['status', ...charterSub])];
  if (/positional\[1\] === 'settle'/.test(src)) subs.decide = ['settle'];
  if (/id === 'reconcile'/.test(src)) subs.touch = ['reconcile'];
  const taskOps = src.match(/\['start', 'done', 'block', 'skip', 'reconcile'\]/);
  if (taskOps) subs.task = ['start', 'done', 'block', 'skip', 'reconcile'];

  return { all: [...set].sort(), phases: [...phases].sort(), subs };
}

// ── 2. audit 이벤트 ─────────────────────────────────────────────────
// emitter 가 여섯 형태다: appendAudit · logAudit · appendGuardAudit(위치 인자) ·
// audit() · auditBlock() · raw appendFileSync. 이름 열만 뽑는다.
function collectAuditEvents() {
  const set = new Set();
  const dyn = new Set();
  let files = [];
  try { files = fs.readdirSync(HOOKS).filter(f => f.endsWith('.js')); } catch { /* 없으면 빈 목록 */ }
  for (const f of files) {
    const src = read(path.join('.claude', 'hooks', f).replace(/\\/g, '/'));
    for (const m of src.matchAll(/event:\s*'([\w-]+)'/g)) set.add(m[1]);
    for (const m of src.matchAll(/appendGuardAudit\(\s*'([\w-]+)'/g)) set.add(m[1]);
    // [v3.7/C1] 예전에는 `auditBlock('<규칙>')` 의 **규칙 이름**을 이벤트로 세었다 —
    //   실제 이벤트는 언제나 `gate-block` 하나였는데 감사 이벤트 목록에 17개가 실렸다.
    //   이벤트는 이벤트 리터럴에서만 뽑는다: 얇은 래퍼가 이벤트명을 넘기는 자리를 본다.
    for (const m of src.matchAll(/auditEvent\(\s*'([\w-]+)'/g)) set.add(m[1]);
    // 연결 생성 — 닫힌 집합이면 펼치고, 아니면 접두로 남긴다
    for (const m of src.matchAll(/event:\s*'([\w-]+)-'\s*\+\s*(\w+)/g)) dyn.add(m[1] + '-*');
    for (const m of src.matchAll(/'([\w-]+)-'\s*\+\s*op\b/g)) {
      for (const op of ['start', 'done', 'block', 'skip']) set.add(m[1] + '-' + op);
    }
  }
  // `'loopv-' + action` 같은 연결의 **접두만** 뽑힌 잔재는 이름이 아니다 — 뺀다.
  return { fixed: [...set].filter(n => !n.endsWith('-')).sort(), dynamic: [...dyn].sort() };
}

// ── 3. pre-tool-gate 규칙 이름 ──────────────────────────────────────
// 이름은 뽑을 수 있다. **조건은 못 뽑는다** — 위 경고 참조.
function collectGateRules() {
  const gate = read('.claude/hooks/pre-tool-gate.js');
  const common = read('.claude/hooks/_common.js');
  const set = new Set();
  for (const m of gate.matchAll(/auditBlock\(\s*'([\w-]+)'/g)) set.add(m[1]);
  for (const m of common.matchAll(/rule:\s*'([\w-]+)'/g)) set.add(m[1]);
  // [v3.7/C1] `auditAllow` 는 세지 않는다 — 통과 경로의 이름은 차단 규칙이 아니다.
  //   예전에는 둘 다 `auditBlock` 이라 봉투 자동 승인(prd-approve-via-charter)이 이 목록에
  //   차단 규칙으로 실렸다. 이제 호출 이름이 그 구분을 담으므로 소스만 보고 갈라낼 수 있다.
  return [...set].sort();
}

// ── 4. 훅 등록 ──────────────────────────────────────────────────────
function collectHooks() {
  const out = [];
  try {
    const st = JSON.parse(read('.claude/settings.json'));
    for (const [event, arr] of Object.entries(st.hooks || {})) {
      for (const entry of arr) {
        for (const h of entry.hooks || []) {
          const m = String(h.command || '').match(/hooks[/\\]([\w.-]+\.js)/);
          if (m) out.push({ event, script: m[1], matcher: entry.matcher || null, timeout: h.timeout });
        }
      }
    }
  } catch { /* settings 를 못 읽으면 빈 표 */ }
  return out.sort((a, b) => (a.event + a.script).localeCompare(b.event + b.script));
}

// ── 렌더링 ──────────────────────────────────────────────────────────
// [v5/N-06] 버그 인덱스 표. 업스트림과 다운스트림이 **같은 표에 각각 append** 하다
//   중복 행이 생겼다(실측 2026-09-09). 목록을 손으로 맞추는 대신 리포트 파일에서 만든다 —
//   각 리포트의 `- **상태**:` 줄이 유일한 출처다.
function collectBugReports() {
  const dir = path.join(ROOT, 'docs', 'bugs');
  let files = [];
  try { files = fs.readdirSync(dir).filter(f => /^\d{4}-\d{2}-\d{2}-.+\.md$/.test(f)); } catch { return []; }
  return files.map(f => {
    const t = fs.readFileSync(path.join(dir, f), 'utf8');
    const pick = k => {
      const m = t.match(new RegExp('^- \\*\\*' + k + '\\*\\*:\\s*(.+)$', 'm'));
      return m ? m[1].trim() : '';
    };
    const title = (t.match(/^#\s+(.+)$/m) || [, ''])[1].trim();
    const sev = pick('심각도').split('—')[0].trim();
    return {
      date: f.slice(0, 10),
      slug: f.replace(/\.md$/, ''),
      name: f.replace(/^\d{4}-\d{2}-\d{2}-/, '').replace(/\.md$/, ''),
      sev: sev || '중간',
      status: pick('상태') || '미해결',
      title,
    };
  }).sort((a, b) => (a.date === b.date ? a.name.localeCompare(b.name) : a.date.localeCompare(b.date)));
}
const NOTE = '<!-- 이 블록은 scripts/gen-manual-tables.js 가 생성합니다. 손으로 고치지 마세요.\n'
  + '     **이름 열만 생성됩니다** — 조건·설명은 사람이 유지합니다(코드에서 뽑을 수 없습니다). -->';

function renderSubcommands() {
  const { all, phases, subs } = collectSubcommands();
  const cmds = all.filter(c => !phases.includes(c));
  let out = `${NOTE}\n\n**서브커맨드 ${cmds.length}개** (phase 이름 ${phases.length}개는 별도)\n\n`;
  out += '```\n' + cmds.join(' · ') + '\n```\n\n';
  out += '**phase 이름**\n\n```\n' + phases.join(' · ') + '\n```\n';
  const subKeys = Object.keys(subs).sort();
  if (subKeys.length) {
    out += '\n**서브-서브커맨드**\n\n';
    for (const k of subKeys) out += `- \`${k}\`: ${subs[k].map(s => '`' + s + '`').join(' · ')}\n`;
  }
  return out;
}
function renderAuditEvents() {
  const { fixed, dynamic } = collectAuditEvents();
  let out = `${NOTE}\n\n**audit 이벤트 ${fixed.length}종**\n\n`;
  out += '```\n' + fixed.join(' · ') + '\n```\n';
  if (dynamic.length) out += '\n연결 생성(접두만 고정): ' + dynamic.map(d => '`' + d + '`').join(' · ') + '\n';
  return out;
}
function renderGateRules() {
  const rules = collectGateRules();
  return `${NOTE}\n\n**판정 이름 ${rules.length}종** — 조건은 아래 표에서 사람이 유지합니다\n\n`
    + '```\n' + rules.join(' · ') + '\n```\n';
}
function renderHooks() {
  const hooks = collectHooks();
  let out = `${NOTE}\n\n| 이벤트 | 스크립트 | matcher | timeout |\n|---|---|---|---|\n`;
  for (const h of hooks) {
    out += `| ${h.event} | \`${h.script}\` | ${h.matcher ? '`' + h.matcher.slice(0, 60) + (h.matcher.length > 60 ? '…' : '') + '`' : '(없음)' } | ${h.timeout}s |\n`;
  }
  return out;
}

function renderBugsIndex() {
  const rows = collectBugReports().map(r => `| ${r.date} | [${r.name}](${r.slug}.md) | ${r.sev} | ${r.status} | ${r.title} |`);
  const note = '<!-- 이 블록은 scripts/gen-manual-tables.js 가 docs/bugs/*.md 에서 생성합니다. 각 리포트의 상태줄이 유일한 출처입니다 — 표를 손으로 고치지 마세요. -->';
  return [note, '| 날짜 | 파일 | 심각도 | 상태 | 요약 |', '|---|---|---|---|---|', ...rows].join('\n');
}
// ── 6. Codex 진입점 — CLAUDE.md 에서 생성한다 (v16/N-04) ─────────────
// 왜: `AGENTS.md` 는 손으로 쓰인 **두 번째 사본**이었고 이미 갈라져 있었다. 가장 비싼 누락은
//   원장 규율 전체였다 — Codex 가 내린 결정이 원장에 남지 않으면 다음 세션은 어느 런타임이든
//   같은 논의를 처음부터 다시 한다. 설정으로는 막을 수 없다(사용자 홈 `~/.codex/config.toml`
//   또는 `trust_level` 이 필요하고, 프로젝트 레이어는 무시된다 — 원장 D-2026-09-17-57ca49).
//   그래서 내용을 **물리적으로** 넣고, 넣는 것은 손이 아니라 이 생성기다.
//
// 번역하지 않는다. 옮겨 적은 문장은 그 자체로 새 사본이고, 사본은 검증할 수 없다.
//   정본의 **바이트를 그대로** 옮기면 드리프트가 구조적으로 불가능하다.
//
// 담는 절은 **런타임 중립 규칙**뿐이다. `## 프로젝트 정보`(yaml)는 담지 않는다 —
//   `build-package.js` 의 템플릿 치환이 CLAUDE.md 의 그 블록만 비우므로, 여기로 새면
//   치환이 닿지 않는 세 번째 동기화 지점이 생긴다.
const CANONICAL_SECTIONS = ['현재 실행 방식', '매 응답 후', '참조 경로'];

// Codex 는 진입점 문서가 크면 잘라 읽거나 무시할 수 있다. 숫자는 **한 벌**이다 —
//   쓰기 경로(아래 CLI)와 `test_docs_sync.js`(DS-18) 가 둘 다 이 상수를 쓴다.
const AGENTS_MD_CAP = 32 * 1024;

// `## ` 헤딩으로 자른다. 제목이 CANONICAL_SECTIONS 의 접두어로 시작하는 절만 남긴다.
//
// **fail-closed 다.** 세 절 중 하나라도 못 찾으면, 또는 한 이름에 두 절이 걸리면 throw 한다.
//   [Loop A 정합 렌즈] 처음 판은 `if (!secs.length)` 만 봤다 — 세 절이 **전부** 사라진
//   경우만 잡혔고, 헤딩 하나가 개편으로 이름이 바뀌면(`## 현재 실행 방식` → `## 새 실행 방식`)
//   그 절만 조용히 빠진 블록이 완성됐다. Track 판정 기준이 통째로 Codex 에서 사라져도
//   아무 데서도 빨개지지 않는다 — 이 노드가 고치려던 바로 그 형태의 침묵이다.
//
// 코드펜스(```) 안의 `## ` 는 헤딩이 아니다. 지금 CLAUDE.md 의 yaml·bash 블록에는 그런 줄이
//   없지만 그것은 우연이지 방어가 아니다 — `test_docs_sync.js` 의 `livePraise` 가 같은 이유로
//   같은 추적을 한다.
function extractCanonicalSections(claudeText) {
  const lines = String(claudeText).split('\n').map(l => l.replace(/\r$/, ''));
  const out = [];
  let cur = null, inFence = false;
  for (const line of lines) {
    if (/^\s*```/.test(line)) { inFence = !inFence; if (cur) cur.body.push(line); continue; }
    const m = inFence ? null : /^##\s+(.+)$/.exec(line);
    if (m) {
      const title = m[1].trim();
      const want = CANONICAL_SECTIONS.find(w => title.startsWith(w));
      cur = want ? { title, want, body: [] } : null;
      if (cur) out.push(cur);
      continue;
    }
    if (cur) cur.body.push(line);
  }
  for (const w of CANONICAL_SECTIONS) {
    const hits = out.filter(s => s.want === w);
    if (!hits.length) {
      throw Error(`CLAUDE.md 에 런타임 중립 절 "${w}" 이 없습니다 — 생성 거부.`
        + ` 찾은 절: ${out.map(s => s.title).join(' · ') || '없음'}`);
    }
    if (hits.length > 1) {
      throw Error(`CLAUDE.md 의 "${w}" 에 절이 ${hits.length}개 걸립니다 — 생성 거부.`
        + ` 어느 것이 정본인지 모호합니다: ${hits.map(s => s.title).join(' · ')}`);
    }
  }
  // 절 사이 구분선(`---`)과 꼬리 빈 줄은 정본의 레이아웃이지 규칙이 아니다
  return out.map(s => ({
    title: s.title,
    body: s.body.join('\n').replace(/\n*(?:^|\n)---\s*$/, '').replace(/^\s*\n/, '').replace(/\s+$/, ''),
  }));
}

function renderHarnessEntry(claudeText) {
  const src = claudeText === undefined ? read('CLAUDE.md') : claudeText;
  const secs = extractCanonicalSections(src);
  const head = [
    '<!-- 이 블록은 scripts/gen-manual-tables.js 가 CLAUDE.md 에서 생성합니다.',
    '     정본은 CLAUDE.md 입니다 — 여기를 손으로 고치면 문서 동기화 테스트(DS-18)가 깨집니다. -->',
    '',
    '# 하네스 공통 진입점 (생성물)',
    '',
    '규칙의 **정본은 `CLAUDE.md`** 이고, 아래 절은 그 파일에서 바이트 그대로 옮겨진다.',
    '이 파일을 고치지 말고 `CLAUDE.md` 를 고친 뒤 `node scripts/gen-manual-tables.js` 를 돌린다.',
    '',
    '런타임 고유 배선·관리형 CLI 계약·UI 테스트 요구는 `.claude/skills/process/SKILL.md` 와',
    '`.claude/skills/process/MANAGED.md` 에 있다 — 여기로 복사하지 않는다.',
  ].join('\n');
  return [head, ...secs.map(s => `\n## ${s.title}\n\n${s.body}`)].join('\n');
}

const BLOCKS = {
  'gen:subcommands': renderSubcommands,
  'gen:audit-events': renderAuditEvents,
  'gen:gate-rules': renderGateRules,
  'gen:hooks': renderHooks,
  'gen:bugs-index': renderBugsIndex,
  'gen:harness-entry': renderHarnessEntry,
};

// 마커: `<!-- gen:NAME -->` … `<!-- /gen:NAME -->`
function applyBlocks(text) {
  let out = text, changed = [];
  for (const [name, render] of Object.entries(BLOCKS)) {
    const re = new RegExp(`(<!-- ${name} -->)([\\s\\S]*?)(<!-- /${name} -->)`);
    if (!re.test(out)) continue;
    const body = '\n' + render().trim() + '\n';
    out = out.replace(re, (_, a, old, b) => {
      if (old !== body) changed.push(name);
      return a + body + b;
    });
  }
  return { out, changed };
}

// [v5/N-06] 대상이 둘이다 — Manual 과 버그 인덱스. applyBlocks 는 마커가 없는 블록을 건너뛴다.
const TARGETS = ['docs/Manual.md', 'docs/bugs/INDEX.md', 'AGENTS.md'];
const CHECK = process.argv.includes('--check');
const PRINT = process.argv.includes('--print');

// [v3.7/C1] 여기부터는 CLI 로 실행됐을 때만 돈다. 가드가 없을 때 `require` 하는 것만으로
//   Manual 을 덮어썼고, 실제로 `test_docs_sync` 가 추출 함수를 쓰려고 require 하면서
//   테스트가 저장소 파일을 수정했다 — 러너의 격리가 지켜야 할 바로 그 종류의 부작용이다.
module.exports = { extractCanonicalSections, renderHarnessEntry, CANONICAL_SECTIONS, AGENTS_MD_CAP, collectSubcommands, collectAuditEvents, collectGateRules, collectHooks, collectBugReports, applyBlocks, BLOCKS };
if (require.main !== module) return;

if (PRINT) {
  for (const [name, render] of Object.entries(BLOCKS)) {
    console.log(`\n──── ${name} ────\n`);
    console.log(render());
  }
  process.exit(0);
}

let anyChanged = 0, anyMissing = 0;
for (const TARGET of TARGETS) {
  const text = read(TARGET);
  if (!text) { console.error(`[ERROR] ${TARGET} 를 읽을 수 없습니다`); process.exit(1); }
  const { out, changed } = applyBlocks(text);
  const markers = Object.keys(BLOCKS).filter(n => text.includes(`<!-- ${n} -->`));
  // [Loop A 정합 렌즈] `anyMissing` 은 세기만 하고 아무도 읽지 않는 죽은 변수였다. 그래서
  //   `AGENTS.md` 에서 마커 쌍이 통째로 사라지면(병합 충돌 해소 실수·수동 편집) `--check` 가
  //   "건너뜀" 만 찍고 **exit 0** 으로 끝났다 — 생성물이 사라졌는데 초록불이다.
  //   TARGETS 에 있는 파일은 마커를 갖는 것이 계약이다. 없으면 거부한다.
  if (!markers.length) {
    anyMissing++;
    console.error(`[MISSING] ${TARGET} 에 생성 마커가 없습니다 — 계약 위반입니다.`);
    process.exitCode = 1;
    continue;
  }
  if (CHECK) {
    if (changed.length) {
      console.error(`[DRIFT] ${TARGET} 의 생성물과 문서가 다릅니다: ${changed.join(', ')}`);
      process.exitCode = 1;
    }
    continue;
  }
  // [v16/N-04] 상한 판정을 **쓰기 경로에** 둔다. 테스트에만 있으면 461초 스위트를 돌린
  //   사람만 알게 되고, 문서 수정류 커밋은 그 스위트를 생략하는 것이 이 저장소의 관행이다
  //   (Loop A 보안 렌즈 지적). 숫자는 한 벌이다 — 테스트가 이 상수를 import 한다.
  if (TARGET === 'AGENTS.md' && Buffer.byteLength(out, 'utf8') > AGENTS_MD_CAP) {
    console.error(`[ERROR] ${TARGET} 가 ${AGENTS_MD_CAP} 바이트 상한을 넘습니다`
      + ` (${Buffer.byteLength(out, 'utf8')} 바이트) — 쓰지 않았습니다.`);
    console.error('  넘으면 Codex 가 잘라 읽거나 무시할 수 있습니다.'
      + ' CLAUDE.md 의 런타임 중립 절을 줄이고 상세는 .claude/skills/process/SKILL.md 로 옮기세요.');
    process.exit(1);
  }
  if (out !== text) { fs.writeFileSync(path.join(ROOT, TARGET), out); anyChanged += changed.length; }
  console.log(`[OK] ${TARGET} 생성 블록 ${markers.length}개 갱신 (변경 ${changed.length}개)`);
}
if (CHECK && !process.exitCode) console.log('[OK] 생성물과 문서가 일치합니다');
