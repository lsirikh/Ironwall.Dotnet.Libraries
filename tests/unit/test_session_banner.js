// tests/unit/test_session_banner.js
// 세션 배너 — 빈칸은 표시이지 데이터가 아니다 · 스테일 백로그는 요약한다 (charter harness-v10-upgrade-integrity / N-02)
//
// 왜 이 파일이 존재하는가:
//   세션 파일의 안내 문구 `(작업 내용을 여기에 기록하세요)` 는 serializeSessionFile 이 빈 값 대신 **저장**하고
//   parseSessionFile 이 **값으로 되읽는다**. 그래서 배너의 `(내용 없음)` 폴백은 도달할 수 없고, 빈 세션이
//   "무언가 하던 세션" 처럼 찍힌다. 다운스트림 배너가 이 줄로 33 → 29 → 25줄이 됐다.
//   이 저장소의 세션 배너에도 같은 줄이 실제로 찍혔다.
//
// 왜 샌드박스인가:
//   _session-context.js 는 PROJECT_ROOT 를 __dirname 에서 계산하고, 폴은 docs/memory/sessions 를 실제로 읽고 rename 한다.
//   _sandbox.js 가 훅을 복사하므로 샌드박스의 세션 폴더만 건드린다. 이 모듈은 fs·path 만 require 한다 — 로드 부작용 없음.

'use strict';

const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');
const SB = require('./_sandbox.js');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 220) : ''}`); }
}

const BRANCH = 'feat/sbx';
const BRANCH_DIR = 'feat-sbx';   // _session-context.sanitizeBranch — '/' → '-'
// 하네스가 세션 파일에 써 온 안내 문구 — 테스트는 **파일에 실제로 있던 글자**를 기준으로 삼는다(상수를 import 하지 않는다)
const WORK_TEXT = '(작업 내용을 여기에 기록하세요)';
const NOTES_TEXT = '(다른 세션에 알릴 내용을 여기에 기록하세요)';

// 하네스가 실제로 써 온 세션 파일 모양 — 다운스트림 표본 그대로. updated 가 24시간 넘게 과거라 PID 와 무관하게 스테일이다.
function legacySession(ppid, work, notes) {
  return `---\nppid: ${ppid}\nbranch: ${BRANCH}\nstarted: 2026-01-01T00:00:00\nupdated: 2026-01-01T00:00:00\n` +
    `phase: unknown\ntrack: B\nstatus: active\nsessionNumber: 1\n---\n\n` +
    `## 현재 작업\n${work || WORK_TEXT}\n\n## 최근 수정 파일\n(없음)\n\n## 공유 노트\n${notes || NOTES_TEXT}\n`;
}
const sessionsDir = d => path.join(d, 'docs', 'memory', 'sessions', BRANCH_DIR);
function putSessions(d, list) {
  fs.mkdirSync(sessionsDir(d), { recursive: true });
  for (const s of list) fs.writeFileSync(path.join(sessionsDir(d), `ppid-${s.ppid}.md`), legacySession(s.ppid, s.work, s.notes));
}
const liveSessionFiles = d => { try { return fs.readdirSync(sessionsDir(d)).filter(f => /^ppid-\d+\.md$/.test(f)); } catch { return []; } };

// 샌드박스의 _session-context.js 를 require 한 뒤 body 를 돌리고, 마지막 stdout 줄의 JSON 을 돌려준다.
function inBox(d, body) {
  const code = `const fs = require('fs'); const C = require(${JSON.stringify(path.join(d, '.claude', 'hooks', '_session-context.js'))}); ${body}`;
  const r = spawnSync(process.execPath, ['-e', code], {
    cwd: d, encoding: 'utf8', timeout: 30000, env: Object.assign({}, process.env, { CLAUDE_PROJECT_DIR: d }),
  });
  const last = (r.stdout || '').trim().split('\n').pop();
  try { return JSON.parse(last); } catch { return { error: ((r.stdout || '') + (r.stderr || '')).slice(0, 300) }; }
}
const box = () => SB.makeSandbox({ prefix: 'sbn', branch: BRANCH });

console.log('\n═══ SB: 세션 배너 빈칸·백로그 (v10/N-02) ═══');

// ══ 빈칸 왕복 ═══════════════════════════════════════════════════════════════════
console.log('\n[SB-01] should_read_an_empty_session_as_empty_while_keeping_the_hint_in_the_file');
{
  const d = box();
  try {
    const o = inBox(d, [
      "C.createSessionFile(900100, 'feat/sbx', 'dev', 'C', 1);",
      "const r = C.readSessionFile(900100, 'feat/sbx');",
      "const raw = fs.readFileSync(C.getSessionFilePath(900100, 'feat/sbx'), 'utf8');",
      "console.log(JSON.stringify({ work: r && r.workSummary, notes: r && r.sharedNotes, raw }));",
    ].join(' '));
    check('빈 작업 요약은 빈 값으로 읽힌다', o.work === '', JSON.stringify({ work: o.work, error: o.error }));
    check('빈 공유 노트는 빈 값으로 읽힌다', o.notes === '', JSON.stringify({ notes: o.notes }));
    check('파일에는 안내 문구가 그대로 — 사람이 여는 파일의 모양은 바뀌지 않는다', typeof o.raw === 'string' && o.raw.includes(WORK_TEXT) && o.raw.includes(NOTES_TEXT), (o.raw || '').slice(0, 200));
  } finally { SB.cleanup(d); }
}

console.log('\n[SB-02] should_show_no_content_for_an_already_accumulated_stale_session');
{
  // 이미 쌓인 파일 — 파일을 다시 쓰지 않고 파서만 고쳐 해소돼야 한다
  const d = box();
  try {
    putSessions(d, [{ ppid: 900200 }]);
    const o = inBox(d, [
      "const p = C.pollOtherSessions(process.pid, 'feat/sbx');",
      "const b = C.buildOtherSessionsBanner(p, p.stale);",
      "console.log(JSON.stringify({ stale: p.stale.length, banner: b }));",
    ].join(' '));
    const banner = o.banner || '';
    const line = banner.split('\n').find(l => /이전 미완성 세션 감지 \(ppid-900200\)/.test(l));
    check('스테일로 잡혀 배너에 줄이 있다 (공허 통과 아님)', o.stale === 1 && !!line, JSON.stringify({ stale: o.stale, error: o.error }));
    check('그 줄이 (내용 없음) 을 보인다', !!line && line.includes('(내용 없음)'), line);
    check('안내 문구가 배너 어디에도 없다', !banner.includes('여기에 기록하세요'), banner.split('\n').filter(l => /여기에/.test(l)).join(' | '));
  } finally { SB.cleanup(d); }
}

console.log('\n[SB-03] should_keep_real_content_that_merely_contains_the_hint');
{
  // 정확 일치만 빈 값이다 — 안내 문구를 인용한 실제 메모는 그대로
  const d = box();
  try {
    const work = `메모 — ${WORK_TEXT} 줄은 지우지 말 것`;
    const notes = `배포 전 확인 ${NOTES_TEXT}`;
    fs.mkdirSync(sessionsDir(d), { recursive: true });
    fs.writeFileSync(path.join(sessionsDir(d), 'ppid-900300.md'), legacySession(900300, work, notes));
    const o = inBox(d, "const r = C.readSessionFile(900300, 'feat/sbx'); console.log(JSON.stringify({ work: r && r.workSummary, notes: r && r.sharedNotes }));");
    check('작업 요약 — 실제 내용 그대로', o.work === work, JSON.stringify(o));
    check('공유 노트 — 실제 내용 그대로', o.notes === notes, JSON.stringify(o));
  } finally { SB.cleanup(d); }
}

// ══ 백로그 요약 — I/O 경계(폴당 아카이브 5)와 주의 경계(배너 3줄)는 따로 산다 ══════════════
// session-gate 의 boxLine 과 같은 표시 폭 — 한국어·CJK·이모지는 두 칸. 박스 본문은 62칸에서 잘린다.
const displayWidth = s => [...String(s)].reduce((n, c) => {
  const cp = c.codePointAt(0);
  const wide = (cp >= 0x1100 && cp <= 0x115F) || (cp >= 0x2E80 && cp <= 0xA4CF)
    || (cp >= 0xAC00 && cp <= 0xD7A3) || (cp >= 0xF900 && cp <= 0xFAFF)
    || (cp >= 0xFE30 && cp <= 0xFE6F) || (cp >= 0xFF00 && cp <= 0xFF60)
    || (cp >= 0xFFE0 && cp <= 0xFFE6) || (cp >= 0x1F300 && cp <= 0x1FAFF)
    || (cp >= 0x2600 && cp <= 0x27BF);
  return n + (wide ? 2 : 1);
}, 0);
const POLL_AND_BANNER = [
  "const p = C.pollOtherSessions(process.pid, 'feat/sbx');",
  "const b = C.buildOtherSessionsBanner(p, p.stale);",
  "console.log(JSON.stringify({ stale: p.stale.length, archived: p.archived, banner: b }));",
].join(' ');

console.log('\n[SB-04] should_summarize_a_stale_backlog_into_three_lines_and_one_summary');
{
  // 다운스트림 release-v7.0 의 모양 — 이미 쌓인 스테일 세션이 한 폴에 다 정리되지 않는다
  const d = box();
  try {
    putSessions(d, Array.from({ length: 30 }, (_, i) => ({ ppid: 901000 + i })));
    const o = inBox(d, POLL_AND_BANNER);
    const lines = (o.banner || '').split('\n');
    const staleLines = lines.filter(l => /이전 미완성 세션 감지/.test(l));
    const summary = lines.find(l => /외 \d+건/.test(l));
    check('폴이 30건 전부를 스테일로 본다 (공허 통과 아님)', o.stale === 30, JSON.stringify({ stale: o.stale, error: o.error }));
    check('스테일 줄은 3줄이다', staleLines.length === 3, `${staleLines.length}줄`);
    check('요약 한 줄이 나머지 수·이번 폴 정리 수·남은 수를 말한다',
      !!summary && /외 27건/.test(summary) && /이번 폴 5건/.test(summary) && /남은 25건/.test(summary), summary || lines.slice(-4).join(' | '));
    check('아카이브 상한 5 는 그대로 — 남은 세션 파일 25', liveSessionFiles(d).length === 25, `${liveSessionFiles(d).length}개`);
    const body = summary ? summary.replace(/^║/, '') : '';
    check('요약 줄이 배너 박스 본문(62칸)에 들어간다', !!summary && displayWidth(body) <= 62, summary ? `${displayWidth(body)}칸` : '(요약 줄 없음)');
  } finally { SB.cleanup(d); }
}

console.log('\n[SB-05] should_list_every_stale_session_when_there_are_three_or_fewer');
{
  const d = box();
  try {
    putSessions(d, [900500, 900501, 900502].map(ppid => ({ ppid })));
    const o = inBox(d, POLL_AND_BANNER);
    const lines = (o.banner || '').split('\n');
    const staleLines = lines.filter(l => /이전 미완성 세션 감지/.test(l));
    check('3건 전부 줄로 보인다', o.stale === 3 && staleLines.length === 3, JSON.stringify({ stale: o.stale, lines: staleLines.length, error: o.error }));
    check('요약 줄이 없다', !lines.some(l => /외 \d+건/.test(l)), lines.filter(l => /외/.test(l)).join(' | '));
  } finally { SB.cleanup(d); }
}

// ══ Loop A 지적 반영 — 배너의 안내가 참이어야 한다 ═══════════════════════════════
// 폴은 스테일을 찾는 **즉시** .archived/ 로 옮기고(폴당 5), 배너는 그 뒤에 만들어진다.
// 그래서 배너가 보여 주는 세션은 대개 이미 아카이브돼 있다 — 그런데 배너는 "이전 세션 이어서" 로 복구하라고 말한다.
console.log('\n[SB-06] should_resume_a_session_the_banner_just_showed_even_though_the_poll_archived_it');
{
  const d = box();
  try {
    putSessions(d, [{ ppid: 900600, work: 'API 리팩터링 중 — 인증 미들웨어' }]);
    const o = inBox(d, [
      "const p = C.pollOtherSessions(process.pid, 'feat/sbx');",
      "const shown = p.stale.map(s => s.ppid);",
      "const activeGone = !fs.existsSync(C.getSessionFilePath(900600, 'feat/sbx'));",
      "const r = C.resumeSession(shown[0], 900699, 'feat/sbx');",
      "const back = C.readSessionFile(900699, 'feat/sbx');",
      "const archDir = require('path').join(require('path').dirname(C.getSessionFilePath(900600, 'feat/sbx')), '.archived');",
      "const leftInArchive = fs.existsSync(archDir) ? fs.readdirSync(archDir).filter(f => f.startsWith('ppid-900600-')).length : 0;",
      "console.log(JSON.stringify({ shown, archived: p.archived, activeGone, r, work: back && back.workSummary, ppid: back && back.ppid, status: back && back.status, leftInArchive }));",
    ].join(' '));
    check('배너가 보인 세션은 이미 아카이브됐다 (이 단언의 전제 — 공허 아님)', Array.isArray(o.shown) && o.shown[0] === 900600 && o.archived === 1 && o.activeGone === true, JSON.stringify({ shown: o.shown, archived: o.archived, activeGone: o.activeGone, error: o.error }));
    check('그 ppid 로 이어서 하면 복구된다', !!o.r && o.r.ok === true, JSON.stringify(o.r));
    check('복구된 세션에 작업 요약·새 ppid·active', o.work === 'API 리팩터링 중 — 인증 미들웨어' && o.ppid === 900699 && o.status === 'active', JSON.stringify({ work: o.work, ppid: o.ppid, status: o.status }));
    check('아카이브 사본은 소비된다 — 같은 세션을 두 번 복구하지 않는다', o.leftInArchive === 0, `남은 사본 ${o.leftInArchive}`);
  } finally { SB.cleanup(d); }
}
{
  // 지금의 경로는 그대로다 — 활성 파일이 있으면 거기서, 어디에도 없으면 거부
  const d = box();
  try {
    putSessions(d, [{ ppid: 900610, work: '활성 파일에서 복구' }]);
    const o = inBox(d, [
      "const a = C.resumeSession(900610, 900611, 'feat/sbx');",
      "const back = C.readSessionFile(900611, 'feat/sbx');",
      "const none = C.resumeSession(900620, 900621, 'feat/sbx');",
      "console.log(JSON.stringify({ a, work: back && back.workSummary, none }));",
    ].join(' '));
    check('활성 파일이 있으면 지금처럼 거기서 복구한다', !!o.a && o.a.ok === true && o.work === '활성 파일에서 복구', JSON.stringify(o));
    check('활성에도 아카이브에도 없으면 거부한다', !!o.none && o.none.ok === false, JSON.stringify(o.none));
  } finally { SB.cleanup(d); }
}

console.log('\n[SB-07] should_keep_the_hint_in_the_file_when_updating_with_empty_values');
{
  // 쓰는 쪽은 serialize 만이 아니다 — updateSessionFile 도 같은 상수를 봐야 왕복이 닫힌다
  const d = box();
  try {
    const o = inBox(d, [
      "C.createSessionFile(900700, 'feat/sbx', 'dev', 'C', 1);",
      "C.updateSessionFile(900700, 'feat/sbx', { workSummary: '임시 메모', sharedNotes: '임시 공유' });",
      "C.updateSessionFile(900700, 'feat/sbx', { workSummary: '', sharedNotes: '' });",
      "const raw = fs.readFileSync(C.getSessionFilePath(900700, 'feat/sbx'), 'utf8');",
      "const r = C.readSessionFile(900700, 'feat/sbx');",
      "console.log(JSON.stringify({ raw, work: r && r.workSummary, notes: r && r.sharedNotes }));",
    ].join(' '));
    check('빈 값으로 갱신해도 파일에 안내 문구가 남는다', typeof o.raw === 'string' && o.raw.includes(WORK_TEXT) && o.raw.includes(NOTES_TEXT), (o.raw || o.error || '').slice(0, 200));
    check('읽으면 빈 값이다', o.work === '' && o.notes === '', JSON.stringify({ work: o.work, notes: o.notes }));
  } finally { SB.cleanup(d); }
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
process.exit(fail > 0 ? 1 : 0);
