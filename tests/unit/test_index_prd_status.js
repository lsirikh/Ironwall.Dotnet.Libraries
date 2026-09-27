// tests/unit/test_index_prd_status.js
// PRD 상태는 상태줄 하나에서 읽는다 — charter harness-v11-cycle-truth / N-02
//
// 왜 이 파일이 존재하는가:
//   docs/INDEX.md 의 PRD 상태 열은 사람이 "승인됐나" 를 훑는 목록인데, 그 열을 쓰는 판정이 다섯이었다.
//   post-write-sync 의 등록·갱신은 상태줄이 아니라 **본문 낱말**로 정했고(우선순위도 서로 반대),
//   INDEX 재구축은 상태줄을 읽지만 상태줄이 없으면 Draft 를 지어냈다. 그래서 템플릿대로 쓴 Draft PRD 가
//   Review·Approved 로 보였고, 표시가 저장과 phase 전이 사이에서 번갈아 바뀌었다(다운스트림 실사고 1 · 샌드박스 4/4).
//
// 무엇을 어떻게 보는가:
//   샌드박스에서 재구축(rebuildIndexAuto)으로 INDEX 를 만들고 실제 post-write-sync 를 PostToolUse 로 부른 뒤
//   INDEX 의 행을 읽는다. IP-04·IP-08·IP-09 는 대조 — 판정을 모으다가 원래 맞던 자리를 깨지 않는지 본다.
//   파일 이름은 서로의 부분 문자열이 되지 않게 짓는다(등록 여부를 부분 문자열로 판단하는 별개 결함이 있다).

'use strict';

const fs = require('fs');
const path = require('path');
const SB = require('./_sandbox');

let pass = 0, fail = 0;
function check(label, cond, detail) {
  if (cond) { pass++; console.log(`    ✅ ${label}`); }
  else { fail++; console.log(`    ❌ ${label}${detail ? ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 260) : ''}`); }
}

const UNKNOWN = '—';

function sandbox(prefix) {
  const d = SB.makeSandbox({ prefix });
  SB.cli(d, ['status']);
  rebuild(d);                                   // 재구축이 만든 INDEX 에서 시작한다
  return d;
}
function rebuild(d) {
  const heal = require(path.join(d, '.claude', 'hooks', 'advance-phase-heal.js'));
  return heal.rebuildIndexAuto();
}
function save(d, rel, content) {
  const abs = path.join(d, rel);
  fs.mkdirSync(path.dirname(abs), { recursive: true });
  fs.writeFileSync(abs, content);
  return SB.hook(d, 'post-write-sync.js', {
    hook_event_name: 'PostToolUse', tool_name: 'Write', session_id: 'sIP',
    tool_input: { file_path: abs, content }, cwd: d,
  });
}
function indexText(d) {
  try { return fs.readFileSync(path.join(d, 'docs', 'INDEX.md'), 'utf8'); } catch { return ''; }
}
// `| [name](prds/name) | 토픽 | 상태 | 날짜 |` 의 상태 칸
function statusCell(d, name) {
  const line = indexText(d).split('\n').find(l => l.startsWith(`| [${name}](prds/`));
  if (!line) return '(행 없음)';
  return (line.split('|')[3] || '').trim();
}

console.log('\n[IP-01] should_list_draft_when_body_mentions_approved_on_register');
{
  let d;
  try {
    d = sandbox('ip01');
    const r = save(d, 'docs/prds/ip01-demo-prd.md', '# demo PRD\n\n- **상태**: Draft\n\n이 PRD 는 Approved 가 되면 plan 으로 넘어간다.\n');
    check('훅이 정상 종료한다', r.code === 0, r.out);
    check('INDEX 에 Draft 로 등록된다', statusCell(d, 'ip01-demo-prd.md') === 'Draft', statusCell(d, 'ip01-demo-prd.md'));
  } catch (e) { check('IP-01', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log('\n[IP-02] should_list_draft_when_prd_follows_the_skill_template');
{
  let d;
  try {
    d = sandbox('ip02');
    save(d, 'docs/prds/ip02-tpl-prd.md', '# tpl PRD\n\n- **상태**: Draft\n\n## 완료 기준\n- [ ] code-reviewer WARN 항목 해소\n');
    check('템플릿 완료 기준(code-reviewer)이 있어도 Draft', statusCell(d, 'ip02-tpl-prd.md') === 'Draft', statusCell(d, 'ip02-tpl-prd.md'));
  } catch (e) { check('IP-02', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log('\n[IP-03] should_keep_draft_when_resaved_body_mentions_approved_and_completed');
{
  let d;
  try {
    d = sandbox('ip03');
    save(d, 'docs/prds/ip03-demo-prd.md', '# demo PRD\n\n- **상태**: Draft\n');
    save(d, 'docs/prds/ip03-demo-prd.md', '# demo PRD\n\n- **상태**: Draft\n\n이 PRD 는 Approved 가 되면 plan 으로, Completed 가 되면 닫힌다.\n');
    check('이미 등록된 행을 갱신해도 Draft', statusCell(d, 'ip03-demo-prd.md') === 'Draft', statusCell(d, 'ip03-demo-prd.md'));
  } catch (e) { check('IP-03', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log('\n[IP-04] should_list_approved_when_status_line_is_approved');
{
  let d;
  try {
    // 대조 — 상태줄이 Approved 면 Approved. 지금도 초록이어야 한다.
    d = sandbox('ip04');
    save(d, 'docs/prds/ip04-ok-prd.md', '# ok PRD\n\n- **상태**: Approved\n');
    check('등록: Approved', statusCell(d, 'ip04-ok-prd.md') === 'Approved', statusCell(d, 'ip04-ok-prd.md'));
    save(d, 'docs/prds/ip04-ok-prd.md', '# ok PRD\n\n- **상태**: Approved\n\n본문 수정\n');
    check('갱신: Approved', statusCell(d, 'ip04-ok-prd.md') === 'Approved', statusCell(d, 'ip04-ok-prd.md'));
  } catch (e) { check('IP-04', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log('\n[IP-05] should_list_unknown_when_status_line_is_missing_in_both_paths');
{
  let d;
  try {
    d = sandbox('ip05');
    save(d, 'docs/prds/ip05-guide-prd.md', '# 가이드\n\n상태줄이 없는 문서다. Approved 라는 낱말만 있다.\n');
    check(`증분 등록: ${UNKNOWN}`, statusCell(d, 'ip05-guide-prd.md') === UNKNOWN, statusCell(d, 'ip05-guide-prd.md'));
    rebuild(d);
    check(`재구축: ${UNKNOWN}`, statusCell(d, 'ip05-guide-prd.md') === UNKNOWN, statusCell(d, 'ip05-guide-prd.md'));
  } catch (e) { check('IP-05', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log('\n[IP-06] should_update_a_row_whose_status_cell_is_unknown');
{
  let d;
  try {
    d = sandbox('ip06');
    save(d, 'docs/prds/ip06-later-prd.md', '# later\n\n- **상태**: Draft\n');
    // 판정 불가(—)로 적힌 행을 만든다 — 한 번 — 가 된 행이 상태줄을 얻은 뒤 갱신되는지 본다
    const f = path.join(d, 'docs', 'INDEX.md');
    const before = indexText(d);
    const edited = before.split('\n').map(l => l.startsWith('| [ip06-later-prd.md](prds/')
      ? l.split('|').map((c, i) => (i === 3 ? ` ${UNKNOWN} ` : c)).join('|') : l).join('\n');
    fs.writeFileSync(f, edited);
    check(`사전 조건: 행의 상태 칸이 ${UNKNOWN}`, statusCell(d, 'ip06-later-prd.md') === UNKNOWN, statusCell(d, 'ip06-later-prd.md'));
    save(d, 'docs/prds/ip06-later-prd.md', '# later\n\n- **상태**: Approved\n');
    check(`${UNKNOWN} 였던 행이 Approved 로 갱신된다`, statusCell(d, 'ip06-later-prd.md') === 'Approved', statusCell(d, 'ip06-later-prd.md'));
  } catch (e) { check('IP-06', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log('\n[IP-07] should_write_the_same_status_from_save_and_rebuild');
{
  let d;
  try {
    d = sandbox('ip07');
    save(d, 'docs/prds/ip07-mixed-prd.md', '# mixed\n\n- **상태**: Draft\n\n승인(Approved) 뒤 Completed 로 닫는다.\n');
    save(d, 'docs/prds/ip07-plain-prd.md', '# plain\n\n- **상태**: Approved\n');
    const afterSave = [statusCell(d, 'ip07-mixed-prd.md'), statusCell(d, 'ip07-plain-prd.md')];
    rebuild(d);
    const afterRebuild = [statusCell(d, 'ip07-mixed-prd.md'), statusCell(d, 'ip07-plain-prd.md')];
    check('재구축해도 열이 바뀌지 않는다 (mixed)', afterSave[0] === afterRebuild[0], `저장 ${afterSave[0]} → 재구축 ${afterRebuild[0]}`);
    check('재구축해도 열이 바뀌지 않는다 (plain)', afterSave[1] === afterRebuild[1], `저장 ${afterSave[1]} → 재구축 ${afterRebuild[1]}`);
  } catch (e) { check('IP-07', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log('\n[IP-08] should_create_review_marker_only_for_draft_status_line');
{
  let d;
  try {
    // 대조 — 검토 마커는 원래 상태줄을 읽었다. 지금도 초록이어야 한다.
    d = sandbox('ip08');
    const marker = path.join(d, '.claude', '.pending-prd-review');
    save(d, 'docs/prds/ip08-done-prd.md', '# done\n\n- **상태**: Approved\n\nDraft 였던 문서다.\n');
    check('Approved(본문에 Draft 낱말) 는 마커를 만들지 않는다', !fs.existsSync(marker), fs.existsSync(marker) ? fs.readFileSync(marker, 'utf8') : '');
    save(d, 'docs/prds/ip08-new-prd.md', '# new\n\n- **상태**: Draft\n');
    const m = fs.existsSync(marker) ? JSON.parse(fs.readFileSync(marker, 'utf8')) : null;
    check('Draft 는 마커를 만든다', !!m && m.prd === 'docs/prds/ip08-new-prd.md', JSON.stringify(m));
  } catch (e) { check('IP-08', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log('\n[IP-09] should_register_active_prd_only_for_draft_status_line');
{
  let d;
  try {
    // 대조 — activePrd 자격도 원래 상태줄을 읽었다. 지금도 초록이어야 한다.
    d = sandbox('ip09');
    save(d, 'docs/prds/ip09-approved-prd.md', '# a\n\n- **상태**: Approved\n');
    check('Approved PRD 는 activePrd 가 되지 않는다', !(SB.readState(d) || {}).activePrd, JSON.stringify((SB.readState(d) || {}).activePrd));
    save(d, 'docs/prds/ip09-draft-prd.md', '# d\n\n- **상태**: Draft\n\nApproved 가 되면 plan 으로.\n');
    check('Draft PRD 는 activePrd 가 된다', (SB.readState(d) || {}).activePrd === 'docs/prds/ip09-draft-prd.md', JSON.stringify((SB.readState(d) || {}).activePrd));
  } catch (e) { check('IP-09', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log('\n[IP-10] should_route_every_status_read_through_one_function');
{
  try {
    // 구현을 보는 검사다 — charter 가 요구한 대로 한 곳만 고치고 나머지를 남기는 회귀를 막는다.
    const pws = fs.readFileSync(path.join(SB.REPO, '.claude', 'hooks', 'post-write-sync.js'), 'utf8');
    const heal = fs.readFileSync(path.join(SB.REPO, '.claude', 'hooks', 'advance-phase-heal.js'), 'utf8');
    const wordRegex = pws.match(/\/(Approved|Completed|Review)\/i\.test\(/g) || [];
    check('post-write-sync 에 상태 낱말 정규식이 없다', wordRegex.length === 0, wordRegex.join(' '));
    const lineRegex = pws.split('\n').filter(l => l.includes('\\*\\*상태\\*\\*'));
    check('post-write-sync 가 상태줄 정규식을 직접 쓰지 않는다', lineRegex.length === 0, lineRegex.join(' / '));
    const calls = (pws.match(/readPrdStatus\(/g) || []).length;
    check('post-write-sync 의 네 자리가 readPrdStatus 를 부른다', calls >= 4, `readPrdStatus( ${calls}회`);
    check('재구축의 extractPrdStatus 가 readPrdStatus 를 쓴다', /function extractPrdStatus[\s\S]{0,400}readPrdStatus\(/.test(heal), '');
    check('advance-phase-heal 이 readPrdStatus 를 내보낸다', /module\.exports\s*=\s*\{[^}]*readPrdStatus/.test(heal), '');
  } catch (e) { check('IP-10', false, e.message); }
}

console.log('\n[IP-11] should_keep_hook_alive_when_status_judge_cannot_load');
{
  let d;
  try {
    // Loop A(v11/N-02): post-write-sync 는 부가 기능 실패를 조용히 통과시키는 훅이다. 판정 모듈을 불러오지 못해도
    //   감사·touched 기록까지 함께 죽으면 안 된다 — 판정만 판정 불가로 두고 나머지는 계속 돈다.
    d = sandbox('ip11');
    fs.writeFileSync(path.join(d, '.claude', 'hooks', 'advance-phase-heal.js'), "throw new Error('load failure (IP-11)');\n");
    SB.patchState(d, s => {
      s.phase = 'dev'; s.track = 'C';
      s.cycle = { startedAt: new Date().toISOString(), touched: [], touchedDocs: [], decisionsAtStart: 0, needsDecision: [] };
      return s;
    });
    const r = save(d, 'src/ip11.js', '// ip11\n');
    check('판정 모듈을 불러오지 못해도 훅은 정상 종료한다', r.code === 0, r.out);
    const touched = ((SB.readState(d) || {}).cycle || {}).touched || [];
    check('소스 쓰기는 여전히 touched 에 기록된다', touched.some(t => String((t && t.path) || t).includes('src/ip11.js')), JSON.stringify(touched));
    const p = save(d, 'docs/prds/ip11-lone-prd.md', '# lone\n\n- **상태**: Draft\n\nApproved 가 되면 plan 으로.\n');
    check('PRD 저장도 정상 종료한다', p.code === 0, p.out);
    check(`INDEX 행은 판정 불가(${UNKNOWN}) — 낱말로 추측하지 않는다`, statusCell(d, 'ip11-lone-prd.md') === UNKNOWN, statusCell(d, 'ip11-lone-prd.md'));
    check('판정 모듈을 못 불렀다는 사실이 감사 로그에 남는다', SB.auditRows(d).some(x => x.event === 'prd-status-judge-unavailable'),
      JSON.stringify(SB.auditRows(d).map(x => x.event).slice(-8)));
  } catch (e) { check('IP-11', false, e.message); } finally { if (d) SB.cleanup(d); }
}

console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
process.exit(fail ? 1 : 0);
