#!/usr/bin/env node
// tests/run.js — 샌드박스 테스트 러너
//
// [v3/FR-6.4] 왜 이 파일이 존재하는가
//   CLAUDE.md 의 test_command 가 비어 있어서 검증-수리 루프(Loop V)는 한 번도
//   실행된 적이 없다. 그런데 그냥 배선하면 안 된다 — 실측 결과 테스트 스위트를
//   한 번 돌리면 docs/memory/audit-log.jsonl 에 150행 이상이 추가되고,
//   .claude/install-logs/ 에 파일이 생기며, cost-log.jsonl 과
//   session-context.md 가 변경된다. 즉 **루프가 지키려는 메모리를 루프가 오염**시킨다.
//
//   훅들은 PROJECT_ROOT 를 path.resolve(__dirname,'..','..') 로 계산하므로
//   환경변수 주입만으로는 격리되지 않는다. 그래서 실행 전 텔레메트리 파일을
//   스냅샷하고 실행 후 복원한다. 목표는 단순하다:
//     `node tests/run.js` 실행 후 `git status docs/memory` 가 깨끗할 것.
//
// 사용법:
//   node tests/run.js              전체 실행
//   node tests/run.js state        이름에 'state' 가 포함된 파일만
//   node tests/run.js --no-sandbox 스냅샷/복원 없이 실행(디버깅용)

'use strict';

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const ROOT = path.resolve(__dirname, '..');
const UNIT_DIR = path.join(__dirname, 'unit');

// ── [v3.6/N-07/IMPL-03] 이 실행의 신원 ────────────────────────────────
// 예전 복원은 "실행 전 바이트로 되돌리기" 였다. 자기 것과 남의 것을 가르는 조건이
// 하나도 없어서, 러너가 도는 90초 창의 **모든 쓰기**를 되감았다.
// 실측: 한 세션이 90초에 감사 33~116행을 만들고 원장이 최대 20행 날아간다 —
// 그러면 그 사이클은 원장 없이 complete 할 수 없고 판단을 재구성할 방법이 없다.
//
// env 마커는 하위 프로세스에 100% 전파된다(`_HARNESS_ORIGIN` 이 이미 같은 방식이다).
// **경로 기반 락과 섞지 않는다** — env 를 stand-down 신호로 쓰면 훅 파일을 복사해
// 격리하는 샌드박스 테스트까지 전부 침묵한다.
const RUN_ID = process.env._HARNESS_TEST_RUN
  || `run-${Date.now().toString(36)}-${process.pid.toString(36)}`;
process.env._HARNESS_TEST_RUN = RUN_ID;

// ── append-only 파일의 복원 판정 (순수 함수) ─────────────────────────
// 되감기가 아니라 **내 tail 만 선별 제거**다.
//   - 스냅샷이 현재 내용의 접두가 아니면 손대지 않는다(GC·아카이브 롤오버가 앞을 잘랐다는 뜻)
//   - tail 중 이 실행의 run id 가 붙은 행만 걷어낸다
//   - run id 가 없는 새 행은 **남긴다** — 지우는 쪽이 되돌릴 수 없다
function restoreAppendOnly(current, snapshot, runId) {
  const cur = String(current == null ? '' : current);
  const snap = String(snapshot == null ? '' : snapshot);
  if (cur === snap) return { next: cur, action: 'unchanged', removed: 0 };
  if (!cur.startsWith(snap)) return { next: cur, action: 'skipped-not-prefix', removed: 0 };
  const tail = cur.slice(snap.length);
  const lines = tail.split('\n');
  const trailingNewline = lines[lines.length - 1] === '';
  const body = trailingNewline ? lines.slice(0, -1) : lines;
  const kept = body.filter(l => !(l && l.includes(`"run":"${runId}"`)));
  const removed = body.length - kept.length;
  if (!removed) return { next: cur, action: 'kept-foreign', removed: 0 };
  const rebuilt = kept.length ? snap + kept.join('\n') + (trailingNewline ? '\n' : '') : snap;
  return { next: rebuilt, action: 'filtered', removed };
}

// 디렉터리 안의 새 파일이 **내 것인가**. 모르면 남의 것으로 본다 — 삭제는 되돌릴 수 없다.
function isOwnArtifact(_relPath, ownerRun, myRun) {
  return !!ownerRun && !!myRun && ownerRun === myRun;
}

// ── [v3.6/N-08/IMPL-01] 초록의 단위 (FR-01·FR-02) ────────────────────
// `39/39 통과` 는 **파일 개수**였다. 자식 프로세스의 exit code 만 보고 `passed++` 했으므로
// 단언 0건으로 exit 0 하는 파일도 초록 1건이었고, 파일 안에서 몇 건을 건너뛰든 알 수 없었다.
// 실측: `test_loop_v.js` 의 훅 배선 5건 중 **3건이 조용히 건너뛰어지면서** `17 PASS / 0 FAIL`
// 로 보고되고 있었다 — plan 체크박스 편집 차단의 살아 있는 커버리지가 0인데 초록이었다.
//
// 새 형식을 36개 파일에 강요하지 않는다. 대부분이 이미 `결과: N PASS / M FAIL` 을 찍으므로
// 그것을 읽고, 건너뛴 것은 `[[SKIP]] <사유>` 한 줄로 선언하게 한다.
// **형식을 못 읽는 파일은 '미보고' 로 센다** — 침묵을 초록으로 바꾸지 않는 것이 요점이다.
const SKIP_MARK = /^\s*\[\[SKIP\]\]\s*(.*)$/gm;
function parseCounts(output) {
  const text = String(output || '');
  const skips = [];
  let m;
  SKIP_MARK.lastIndex = 0;
  while ((m = SKIP_MARK.exec(text))) skips.push((m[1] || '').trim());

  // `결과: 12 PASS / 0 FAIL` · `테스트 결과: 87/87 PASS` 두 형식을 읽는다.
  const all = [...text.matchAll(/결과:\s*(\d+)\s*PASS\s*\/\s*(\d+)\s*FAIL/g)];
  if (all.length) {
    const last = all[all.length - 1];
    return { pass: +last[1], fail: +last[2], skip: skips.length, skips, reported: true };
  }
  const ratio = [...text.matchAll(/결과:\s*(\d+)\s*\/\s*(\d+)\s*PASS/g)];
  if (ratio.length) {
    const last = ratio[ratio.length - 1];
    return { pass: +last[1], fail: (+last[2]) - (+last[1]), skip: skips.length, skips, reported: true };
  }
  return { pass: 0, fail: 0, skip: skips.length, skips, reported: false };
}

// ── 공용 리포터 (v4/N-01/FR-01) ──────────────────────────────────────
//
// 왜 별도 헬퍼 파일이 아니라 **여기** 인가:
//   형식을 **만드는** 쪽(makeReporter)과 **읽는** 쪽(parseCounts)이 같은 파일에 있으면
//   형식 변경이 한 번에 움직인다. 파일을 나누면 그 둘이 갈라질 수 있고, 그것이 지금
//   이 노드가 고치는 결함의 정확한 형태다 — 10개 파일이 각자 자기 형식을 찍었고
//   러너는 그것을 못 읽어 exit code 하나로만 판정했다(단언 195건이 집계 밖이었다).
//
//   `require.main !== module` 가드가 아래에 있으므로 테스트가 이 파일을 require 해도
//   러너 본체는 돌지 않는다.
function makeReporter(title) {
  let pass = 0, fail = 0;
  if (title) console.log('\n── ' + title + ' ──');
  const record = (ok, label, detail) => {
    if (ok) { pass++; console.log('  ✅ ' + label); }
    else {
      fail++;
      const d = detail === undefined || detail === null || detail === '' ? '' :
        ' — ' + String(detail).replace(/\s+/g, ' ').slice(0, 300);
      console.log('  ❌ ' + label + d);
    }
  };
  return {
    // 던지면 실패 — assert 기반 파일용
    test(name, fn) {
      try { fn(); record(true, name); }
      catch (e) { record(false, name, e && e.message); }
    },
    // 불리언 — check 기반 파일용
    check(label, cond, detail) { record(!!cond, label, detail); },
    get pass() { return pass; },
    get fail() { return fail; },
    // 정규 형식. parseCounts 가 읽는 바로 그 형식이다 — 문자열을 바꾸려면 위도 같이 바꾼다.
    done() {
      console.log(`\n  결과: ${pass} PASS / ${fail} FAIL`);
      process.exit(fail > 0 ? 1 : 0);
    },
  };
}

const args = process.argv.slice(2);
const NO_SANDBOX = args.includes('--no-sandbox');
// [v3.3] 필터를 여러 개 받는다 — 영향 그래프가 고른 테스트 목록을 그대로 넘길 수 있어야 한다.
//   경로가 와도 파일명만 떼어 부분문자열로 쓴다 (`tests/unit/test_x.js` → `test_x.js`).
// [v4/N-01/FR-04] 값을 받는 플래그의 **값**은 필터가 아니다.
//   실측: `--accept-drop --reason "중복 단언 정리"` 가 그 이유 문자열을 파일명 필터로 읽어
//   "실행할 테스트 없음" 으로 끝났다. 값-플래그를 여기 한 곳에 적는다.
const VALUE_FLAGS = new Set(['--reason']);
const FILTERS = args
  .filter((a, i) => !a.startsWith('--') && !VALUE_FLAGS.has(args[i - 1]))
  .map(a => a.split(/[\\/]/).pop());
const FILTER = FILTERS.join(' ');

// ── 샌드박스: 실행 중 훅이 건드리는 런타임 상태 ──────────────────────
// 내용을 통째로 스냅샷했다가 되돌린다. 삭제 대상은 "실행 후 새로 생긴 것"만.
const SNAPSHOT_FILES = [
  // [v3.2/IMPL-18] 원장과 Loop C 마커. 테스트가 훅을 spawn 하면 실제 원장에 행이 남거나
  //   마커가 생기는데, 여기 없으면 러너가 되돌리지 못해 "실행 후 트리가 깨끗하다"가 거짓이 된다.
  'docs/memory/decisions.jsonl',
  'docs/memory/audit-log.jsonl',
  'docs/memory/cost-log.jsonl',
  'docs/memory/session-context.md',
  'docs/memory/session-context.md.bak',
  'docs/memory/instincts.jsonl',
  'docs/memory/feedback-rules.json',
  'docs/memory/pipeline-state.json',
  'docs/INDEX.md',
  // [v3.6/N-07/IMPL-04] 훅이 실제로 쓰는데 목록에 없던 것들 (FR-05).
  //   "실행 후 git status docs/memory 가 깨끗하다" 는 목표가 이 넷에 대해 이미 거짓이었다.
  'docs/memory/dev-journal.md',
  'docs/memory/compact-log.jsonl',
  'CHANGELOG.md',
];

// append-only 파일 — 되감지 않고 **내 tail 만** 걷어낸다 (FR-01).
const APPEND_ONLY = new Set([
  'docs/memory/decisions.jsonl',
  'docs/memory/audit-log.jsonl',
  'docs/memory/cost-log.jsonl',
  'docs/memory/instincts.jsonl',
  'docs/memory/compact-log.jsonl',
]);
const SNAPSHOT_DIRS = [
  '.claude/install-logs',
  'docs/memory/sessions',
  // [v3.6/N-07/IMPL-04] 하네스가 실제로 쓰는 백업 디렉터리는 `.claude/.branch-*/state-backups/`
  //   다(`_state.js:90-92`). 여기 있던 `docs/memory/state-backups` 는 2026-09-03 에 멈춘
  //   레거시라 **백업 보호가 작동한 적이 없었다.** 그 사이 test_state_lock 이 실제
  //   백업 디렉터리에 createBackup 을 호출하고 purgeOldBackups(10) 이 남의 최신
  //   롤백 포인트를 영구 삭제했다 — 스냅샷 대상이 아니라 복원되지도 않았다.
  'docs/memory/dev-journal-archive',
  'docs/memory/observations-archive',
  'docs/memory/audit-log-archive',
  // [v3.6/N-06/IMPL-03] Loop C 마커는 이제 소유자별 파일이다 (FR-08).
  //   예전에는 전역 단일 파일이 SNAPSHOT_FILES 에 있었고, 러너가 그것을 내용까지 복원했다 —
  //   그래서 `verify` 가 도는 수십 초 동안 (a) 다른 세션이 새로 만든 마커가 삭제되고
  //   (b) 다른 세션이 방금 적은 notified_sessions 가 실행 전 값으로 되감겨 같은 위반을
  //   다시 통지받았다. 디렉터리 스냅샷은 **실행 중 새로 생긴 것만** 지우고 남의 것은 두지 않는다.
  '.claude/.pending-signal-violations',
];

function listDirRecursive(abs) {
  const out = new Set();
  const walk = (d, rel) => {
    let entries;
    try { entries = fs.readdirSync(d, { withFileTypes: true }); } catch { return; }
    for (const e of entries) {
      const r = rel ? `${rel}/${e.name}` : e.name;
      if (e.isDirectory()) walk(path.join(d, e.name), r);
      else out.add(r);
    }
  };
  walk(abs, '');
  return out;
}

// [v3] git HEAD 가드 — 2중 방어.
//   실측 사례: test_git_module.js 가 cwd 를 넘기지 않아 실제 저장소에
//   `; echo INJECTED ...` 제목의 빈 커밋을 6개 남겼다. 텔레메트리 스냅샷만으로는
//   이런 오염을 못 잡는다. 테스트는 저장소 히스토리를 절대 바꾸면 안 된다.
function currentHead() {
  try {
    return execFileSync('git', ['rev-parse', 'HEAD'], {
      cwd: ROOT, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'],
    }).trim();
  } catch { return null; }
}

// [v3.1/IMPL-17] 브랜치별 pipeline-state 도 스냅샷 — 전체 실행이 updated_at/cycle 을 덮어쓰던 문제(S2-02).
//   경로가 동적(.claude/.branch-<sanitized>/)이라 glob 으로 모은다. _state 를 require 하지 않는다
//   (시딩 부작용 회피).
// [v3.6/N-07/IMPL-04] **현재 브랜치만** 스냅샷한다 (FR-05).
//   예전에는 `.claude/.branch-*` 전부를 순회했다 — 다른 브랜치(워크트리 세션)의
//   phase 전이·task done·decide 가 90초 창에서 통째로 무효화됐다.
function currentBranchDir() {
  try {
    const head = fs.readFileSync(path.join(ROOT, '.git', 'HEAD'), 'utf8').trim();
    const m = head.match(/^ref:\s*refs\/heads\/(.+)$/);
    if (!m) return null;
    const sanitized = m[1].replace(/[^A-Za-z0-9]+/g, '-').toLowerCase();
    const cd = path.join(ROOT, '.claude');
    // `.branch-<sanitized>-<hash>` 형태다. 접두로 찾는다.
    for (const d of fs.readdirSync(cd)) {
      if (d.startsWith('.branch-' + sanitized)) return d;
    }
  } catch { /* git 없거나 detached — 아래에서 전부 건너뛴다 */ }
  return null;
}
const CURRENT_BRANCH_DIR = currentBranchDir();

function branchStateFiles() {
  const out = [];
  try {
    const cd = path.join(ROOT, '.claude');
    for (const d of fs.readdirSync(cd)) {
      if (!d.startsWith('.branch-')) continue;
      // 남의 브랜치는 스냅샷하지 않는다 — 스냅샷하면 90초 뒤 그 세션의 작업을 되감는다.
      if (CURRENT_BRANCH_DIR && d !== CURRENT_BRANCH_DIR) continue;
      // [v3.6/N-03/IMPL-08] 브랜치 상태 디렉터리의 런타임 산물을 전부 스냅샷한다.
      //   상태 파일만 모으던 동안 훅을 spawn 하는 테스트가 **실제 영향 그래프를 오염시켰다** —
      //   지금 그래프에 남아 있는 저장소 밖 31노드가 그 유입 경로다.
      for (const name of ['pipeline-state.json', 'state-baseline.json', 'impact-graph.json', 'task-graph.json']) {
        const f = path.join('.claude', d, name);
        if (fs.existsSync(path.join(ROOT, f))) out.push(f.split(path.sep).join('/'));
      }
      // [v16/N-05] 세션 소유 상태(`session-<id>/`)도 담는다. 이것을 빠뜨리면 전수 실행이
      //   **개발자의 실제 상태를 오염시키고 복원하지 못한다** — 테스트 실패가 아니라
      //   데이터 손실로 나타난다. 기본값이 꺼져 있어 지금은 디렉터리가 없지만,
      //   켜는 순간 생긴다. 안전망은 위험보다 **먼저** 있어야 한다.
      try {
        for (const sd of fs.readdirSync(path.join(ROOT, '.claude', d))) {
          if (!sd.startsWith('session-')) continue;
          for (const name of ['pipeline-state.json', 'state-baseline.json', 'impact-graph.json', 'task-graph.json']) {
            const f = path.join('.claude', d, sd, name);
            if (fs.existsSync(path.join(ROOT, f))) out.push(f.split(path.sep).join('/'));
          }
        }
      } catch { /* 세션 디렉터리 없음 — 기본값(OFF)에서 정상 */ }
    }
  } catch {}
  return out;
}

function takeSnapshot() {
  const snap = { files: new Map(), dirs: new Map() };
  for (const rel of [...SNAPSHOT_FILES, ...branchStateFiles()]) {
    const abs = path.join(ROOT, rel);
    try {
      snap.files.set(rel, fs.existsSync(abs) ? fs.readFileSync(abs) : null);
    } catch { snap.files.set(rel, null); }
  }
  for (const rel of SNAPSHOT_DIRS) {
    const abs = path.join(ROOT, rel);
    snap.dirs.set(rel, fs.existsSync(abs) ? listDirRecursive(abs) : null);
  }
  return snap;
}

function restoreSnapshot(snap) {
  let restored = 0, removed = 0, kept = 0;
  const warnings = [];
  for (const [rel, buf] of snap.files) {
    const abs = path.join(ROOT, rel);
    try {
      if (buf === null) {
        // 실행 전에 없던 파일 → 생겼으면 제거
        if (fs.existsSync(abs)) { fs.unlinkSync(abs); removed++; }
        continue;
      }
      const cur = fs.existsSync(abs) ? fs.readFileSync(abs) : null;
      if (cur && cur.equals(buf)) continue;

      // [v3.6/N-07/IMPL-03] append-only 는 되감지 않고 **내 tail 만** 걷어낸다 (FR-01·FR-02).
      //   전체 되감기는 90초 창의 남의 append 를 예외 없이 지웠다 — 실측으로
      //   감사 최대 116행, 원장 최대 20행. 원장이 날아가면 그 사이클은 원장 없이
      //   complete 할 수 없고 모델은 판단을 재구성할 방법이 없다.
      if (APPEND_ONLY.has(rel)) {
        const r = restoreAppendOnly(cur ? cur.toString('utf8') : '', buf.toString('utf8'), RUN_ID);
        if (r.action === 'filtered') { fs.writeFileSync(abs, r.next); restored++; }
        else if (r.action === 'skipped-not-prefix') {
          kept++;
          warnings.push(`${rel}: 스냅샷이 접두가 아님 — 되감지 않았습니다(GC·롤오버 가능성)`);
        } else if (r.action === 'kept-foreign') {
          kept++;   // 남이 쓴 행만 늘었다. 남긴다.
        }
        continue;
      }

      if (!cur || !cur.equals(buf)) { fs.writeFileSync(abs, buf); restored++; }
    } catch { /* 복원 실패는 아래 검증에서 드러난다 */ }
  }
  for (const [rel, before] of snap.dirs) {
    const abs = path.join(ROOT, rel);
    if (!fs.existsSync(abs)) continue;
    const after = listDirRecursive(abs);
    for (const f of after) {
      if (before && before.has(f)) continue;   // 원래 있던 것은 둔다
      // [v3.6/N-07/IMPL-04] 새로 생긴 파일이라고 무조건 지우지 않는다 (FR-04).
      //   90초 창에 시작한 남의 세션 파일과 남의 Loop C 마커가 그렇게 사라졌다.
      //   소유자를 확인할 수 있을 때만 지운다 — 모르면 남긴다(삭제는 되돌릴 수 없다).
      if (!isOwnArtifact(f, ownerRunOf(path.join(abs, f)), RUN_ID)) { kept++; continue; }
      try { fs.unlinkSync(path.join(abs, f)); removed++; } catch { /* 무시 */ }
    }
  }
  return { restored, removed, kept, warnings };
}

// 파일이 어느 실행의 산물인가. 러너가 도는 동안 만들어진 것은 내용에 run id 가 실린다
//   (`_common.appendAudit` 과 마커 기록이 `_HARNESS_TEST_RUN` 을 넣는다).
//   실측할 수 없으면 null 을 돌려주고, 그러면 남의 것으로 취급된다.
function ownerRunOf(abs) {
  try {
    const st = fs.statSync(abs);
    if (st.size > 512 * 1024) return null;
    const txt = fs.readFileSync(abs, 'utf8');
    const m = txt.match(/"run"\s*:\s*"([^"]+)"/);
    return m ? m[1] : null;
  } catch { return null; }
}

// [v3.6/N-07/IMPL-03] 순수 판정을 밖으로 낸다 — 테스트가 스위트를 재귀 실행하지 않고
//   복원 규칙을 단독 검증할 수 있어야 한다. 실행은 아래 `require.main === module` 아래에만 있다.
// ── [v12/N-08] verify.lock 은 **배타적으로** 잡는다 ───────────────────
// 예전에는 writeFileSync 로 덮어썼다. 두 번째 러너가 첫 러너의 락을 덮어쓰고 먼저 끝나 자기 락을 지우면,
// 첫 러너가 아직 도는데 락이 없는 창이 생겼다(통합 분석 F-09) — 그 창의 phase 전이는 러너 복원에 되감긴다.
//   · 같은 run → 재진입(러너 안의 테스트가 같은 실행 id 로 러너를 다시 띄운다, RI-06)
//   · stale(죽은 PID·15분 초과·깨짐) → 회수 후 한 번 더
//   · 살아 있는 남의 락 → 거부
//   · EEXIST 가 아닌 오류 → 락 없이 진행(degraded) — "락을 못 잡아도 테스트는 돈다" 는 기존 정책
// 판정은 `_common.verifyLockState` 하나를 쓴다. 판정기를 못 불러오면 **거부하지 않고 degraded 로 진행**한다(소유하지 않으므로 해제도 않는다).
// [Loop A] 처음에는 보수적으로 거부했다 — 그러나 판정할 수단이 없는 곳의 거부는 죽은 락을 **영구 차단**으로 굳힌다(audit.lock 의 교훈).
//   advance-phase 의 refuseWhileForeignVerify 도 판정 불가면 통과한다. 두 지점의 방향을 맞춘다.
//   되감기 위험이 남는 곳은 판정기가 없는 복사 저장소뿐이고, 하네스 설치본에는 판정기가 있다.
function acquireVerifyLock(lockPath, runId, now) {
  const t = Number.isFinite(now) ? now : Date.now();
  const rec = JSON.stringify({ pid: process.pid, acquired_at: new Date(t).toISOString(), run: runId }) + '\n';
  try { fs.mkdirSync(path.dirname(lockPath), { recursive: true }); } catch { /* 아래 쓰기에서 드러난다 */ }
  for (let attempt = 0; attempt < 2; attempt++) {
    try {
      fs.writeFileSync(lockPath, rec, { flag: 'wx' });
      return { ok: true, created: true };
    } catch (e) {
      if (!e || e.code !== 'EEXIST') return { ok: true, degraded: true, reason: (e && e.code) || String(e) };
      let held = null;
      try { held = JSON.parse(fs.readFileSync(lockPath, 'utf8')); } catch { held = null; }
      if (held && held.run && held.run === runId) return { ok: true, reentrant: true };
      let judge = null;
      try { judge = require(path.join(ROOT, '.claude', 'hooks', '_common.js')).verifyLockState; } catch { judge = null; }
      if (typeof judge !== 'function') return { ok: true, degraded: true, reason: 'judge-unavailable' };
      const st = judge(held, t);
      if (st && st.stale) {
        try { fs.unlinkSync(lockPath); } catch { /* 다른 러너가 먼저 회수했을 수 있다 — 다시 시도한다 */ }
        continue;
      }
      return { ok: false, held };
    }
  }
  return { ok: false, held: null };
}

module.exports = { restoreAppendOnly, isOwnArtifact, ownerRunOf, parseCounts, makeReporter, RUN_ID, acquireVerifyLock };

if (require.main !== module) return;

// ── 테스트 수집 ──────────────────────────────────────────────────────
let files;
try {
  // `_` 로 시작하는 파일은 헬퍼다 — 테스트로 실행하지 않는다 (`_sandbox.js`).
  files = fs.readdirSync(UNIT_DIR).filter(f => f.endsWith('.js') && !f.startsWith('_')).sort();
} catch {
  console.error(`[ERROR] 테스트 디렉터리 없음: ${UNIT_DIR}`);
  process.exit(1);
}
if (FILTERS.length) files = files.filter(f => FILTERS.some(x => f.includes(x)));

if (files.length === 0) {
  console.error(`[ERROR] 실행할 테스트 없음${FILTER ? ` (필터: ${FILTER})` : ''}`);
  process.exit(1);
}

// ── 실행 ─────────────────────────────────────────────────────────────
console.log(`\n  테스트 ${files.length}개${FILTER ? ` (필터: ${FILTER})` : ''}${NO_SANDBOX ? ' [샌드박스 꺼짐]' : ''}\n`);

// [v3.6/N-07/IMPL-05] verify.lock — 이 실행이 도는 동안 다른 세션의 phase 전이를 막는다 (FR-06).
//   append-only 는 접두+run id 로 지켰지만 `pipeline-state.json`·`session-context.md`·
//   `INDEX.md` 는 성질상 전체 복원이라 그 보호를 못 받는다. 거부는 되돌릴 수 있다
//   (90초 뒤 재시도). 허용하면 그 전이가 조용히 되감긴다.
const VERIFY_LOCK = path.join(ROOT, 'docs', 'memory', 'verify.lock');
// [v12/N-08] 이 러너가 **직접 만든** 락만 해제한다. 재진입한 안쪽 러너가 끝나며 락을 지우면
//   바깥 러너가 아직 도는데 락이 사라진다 — 덮어쓰기를 막은 의미가 없어진다.
let _ownsVerifyLock = false;
if (!NO_SANDBOX) {
  const got = acquireVerifyLock(VERIFY_LOCK, RUN_ID, Date.now());
  if (!got.ok) {
    const h = got.held || {};
    // 이 줄의 모양은 하네스가 INFRA(verify-lock)로 분류하는 기준이다 — advance-phase-harness.js VERIFY_LOCK_REFUSAL.
    console.error(`\n  ❌ [검증 잠금] 다른 실행(run ${h.run || '?'}, pid ${h.pid || '?'})이 검증 중입니다 — 끝난 뒤 다시 실행하세요`);
    console.error(`     락: ${path.relative(ROOT, VERIFY_LOCK)} · 죽은 PID·15분 초과 락은 자동 회수됩니다\n`);
    process.exit(1);
  }
  _ownsVerifyLock = !!got.created;
}
function releaseVerifyLock() {
  if (!_ownsVerifyLock) return;
  try {
    const rec = JSON.parse(fs.readFileSync(VERIFY_LOCK, 'utf8'));
    if (rec && rec.run === RUN_ID) fs.unlinkSync(VERIFY_LOCK);   // 남의 락은 건드리지 않는다
  } catch { /* 없거나 남의 것이면 그만 */ }
}

const snapshot = NO_SANDBOX ? null : takeSnapshot();

// [v3.6/N-07/IMPL-04] **정리 보장** (FR-03).
//   예전에는 복원이 직선 코드였다 — `finally` 도 시그널 핸들러도 없었다.
//   그런데 기본 `test_timeout_ms`(60초)가 실제 실행시간(92초)보다 짧아서, 환경 변수 없이
//   돌리면 상위 execSync 가 SIGTERM 으로 죽인다. 그때마다 복원이 **아예 실행되지 않고**
//   자기 오염과 남의 오염이 전부 트리에 남았다.
let _restored = false;
function cleanupOnce(reason) {
  if (_restored) return;
  _restored = true;
  releaseVerifyLock();
  if (!snapshot) return;
  try {
    const { restored, removed, kept, warnings } = restoreSnapshot(snapshot);
    console.log(`\n  [샌드박스] 텔레메트리 복원 ${restored}건 / 잔여물 제거 ${removed}건`
      + (kept ? ` / 남의 기록 보존 ${kept}건` : '') + (reason ? ` (${reason})` : ''));
    for (const w of (warnings || []).slice(0, 3)) console.log(`     ⚠ ${w}`);
  } catch (e) { console.error(`  ⚠ 복원 실패: ${e.message}`); }
}
process.on('exit', () => cleanupOnce(null));
for (const sig of ['SIGTERM', 'SIGINT', 'SIGHUP']) {
  process.on(sig, () => { cleanupOnce(sig); process.exit(1); });
}

// ── [v4/N-10/FR-01] 저장소 오염 탐지 ────────────────────────────────
//
// 러너의 존재 이유는 한 문장이다: "실행 후 트리가 깨끗할 것."
// 그런데 그 보증을 **손으로 쓴 SNAPSHOT_FILES 목록**에 맡기고 있었다 —
// CHANGELOG.md 는 담으면서 CLAUDE.md·install.js·README.md·docs/Manual.md 는 담지 않았다.
//
// 실측(2026-09-08): N-05 를 만들며 쓴 테스트가 버전 동기화를 실제 훅 경로로 호출해
// 그 네 파일을 전부 9.9.9 로 바꿨고, 러너는 되돌리지 못했으며 **스위트는 초록이었다** —
// 네 곳이 일관되게 바뀌어 버전 일치 검사를 통과했기 때문이다.
//
// **목록에 항목을 더하지 않는다.** 판정 기준을 바꾼다: 실행 전후의 `git status` 를 비교해
// 새로 dirty 해진 **추적 파일** 중 러너·훅이 정상적으로 쓰는 경로가 아닌 것을 실패로 드러낸다.
// 되돌리지는 않는다 — 남의 변경을 삼킬 수 있다. 목적은 **조용한 오염을 초록으로 두지 않는 것**이다.
const OWNED_BY_HARNESS = [
  'docs/memory/',        // 원장·감사·저널·세션컨텍스트 — 기존 복원 로직이 담당한다
  'docs/INDEX.md',
  'tests/baseline/',     // 단언·커버리지 기준선
  '.claude/.branch-',    // 브랜치별 런타임 상태
  '.claude/install-logs/',
  'CHANGELOG.md',
  // [v8/N-01] 여기에 'dist/' 가 있었다 — **지웠다. 다시 넣지 말 것.**
  //   v3.0.0 zip 을 커밋하자 이 벽이 test_install_parity 의 빌드가 dist/ 를 덮어쓰는 것을 잡았다.
  //   그때 "이건 오염이 아니라 테스트가 하는 일" 이라며 **테스트 대신 벽을 열었다.**
  //   결과: 스위트를 돌릴 때마다 추적 중인 릴리스 zip 이 덮어써졌고(빌드가 재현 가능하지 않아
  //   소스가 같아도 바이트가 달랐다), 배포된 zip 과 저장소의 zip 이 다른 물건이 됐다.
  //   이제 검증 빌드는 `--out <임시>` 로 저장소 밖에 쓴다.
  //   이 벽이 dist/ 를 다시 잡는다면 **누군가 또 dist/ 에 쓰기 시작한 것이다** — 그 쓰기를 고친다.
  //   벽이 무언가를 잡았을 때, 벽을 여는 것은 답이 아니라 질문의 시작이다.
];
function dirtyTracked() {
  try {
    const out = execFileSync('git', ['status', '--porcelain', '-uno'], {
      cwd: ROOT, encoding: 'utf8', timeout: 15000, maxBuffer: 8 * 1024 * 1024,
    });
    const set = new Set();
    for (const line of out.split('\n')) {
      const m = line.match(/^..\s+(.+)$/);
      if (!m) continue;
      const p = m[1].trim().replace(/^"|"$/g, '').split(' -> ').pop();
      if (p) set.add(p);
    }
    return set;
  } catch { return null; }
}
// [v16] 오염 판정에는 **주체 신호**가 필요하다.
//
// 판정은 스위트 실행 전후의 `git status` 차집합이다. 그 차집합에는 "누가 썼는가" 가 없다.
// 스위트는 5 분 넘게 돈다. 그 사이에 **다른 창이 추적 파일을 한 줄 고치면 테스트 오염으로 신고**된다.
//
// 실측(2026-09-17): 단위 테스트 92/92 통과 뒤 `.claude/hooks/_coordinator.js` 가 오염으로 신고됐다.
// 그 변경은 다른 창이 실행 중(10:00:33)에 쓴 `supersedeWindow()` 였다. 안내대로
// `git checkout --` 했다면 남의 작업이 사라졌다. 이 파일 474 행이 이미 *"되돌리지는 않는다 —
// 남의 변경을 삼킬 수 있다"* 고 적어 두었는데, 출력만 되돌리라고 말하고 있었다.
//
// 그리고 `exit 1` 이라 **다른 창이 일하는 동안에는 전체 스위트가 초록이 될 수 없다** —
// 다중 세션을 목표로 하는 저장소에서 검증 자체가 막힌다.
//
// 그래서 벽을 열지 않는다(`OWNED_BY_HARNESS` 에 파일을 더하는 것은 484 행이 dist/ 로 겪은 실수다).
// **판정 기준에 주체를 더한다**: 나 말고 살아 있는 대화형 창이 있으면 주체를 가릴 수 없으므로
// 실패가 아니라 경고다. 혼자일 때의 오염은 여전히 실패다 — 그때는 테스트가 유일한 용의자다.
function liveInteractiveWindows() {
  try {
    const { repository } = require(path.join(ROOT, '.claude', 'hooks', '_harness-store.js'));
    const repo = repository(ROOT);
    try {
      let live = 0;
      for (const row of repo.store.list('session/')) {
        const s = row.value;
        if (!s || s.status !== 'active' || s.kind !== 'interactive') continue;
        try { process.kill(s.pid, 0); live++; } catch (e) { if (e.code !== 'ESRCH') live++; }
      }
      return live;
    } finally { try { repo.store.close(); } catch { /* 닫기 실패가 판정을 막지 않는다 */ } }
  } catch { return null; }   // 스토어를 못 읽으면 주체를 모른다 — 아래에서 보수적으로 실패 처리한다
}
function mtimeOf(p) {
  try { return fs.statSync(path.join(ROOT, p)).mtime.toTimeString().slice(0, 8); } catch { return '?'; }
}
const dirtyBefore = NO_SANDBOX ? null : dirtyTracked();

const headBefore = currentHead();
const started = Date.now();
const failures = [];
let passed = 0;
let totalAsserts = 0, totalSkips = 0;
const skipDetail = [], unreported = [];
const fileAsserts = Object.create(null);   // [v4/N-01/FR-04] 기준선 판정의 재료

for (const f of files) {
  const t0 = Date.now();
  let ok = true, output = '';
  try {
    output = execFileSync(process.execPath, [path.join(UNIT_DIR, f)], {
      cwd: ROOT, encoding: 'utf8', timeout: 120000, stdio: ['ignore', 'pipe', 'pipe'],
    });
  } catch (e) {
    ok = false;
    output = String((e.stdout || '') + (e.stderr || '') || e.message);
  }
  const ms = Date.now() - t0;
  const counts = parseCounts(output);
  totalAsserts += counts.pass + counts.fail;
  fileAsserts[f] = counts.pass + counts.fail;
  totalSkips += counts.skip;
  if (counts.skip) skipDetail.push(`${f}: ${counts.skips.slice(0, 2).join(' · ')}`);
  // [v4/N-01/FR-03] **침묵은 통과가 아니다.**
  //   예전에는 형식을 못 읽으면 안내 한 줄로 끝났고 판정은 exit code 하나였다.
  //   실측(2026-09-08): 44개 중 10개가 그 상태였고 단언 195건이 집계 밖이었다 —
  //   그중 하나가 0단언으로 퇴화해도 ✅ 였고 집계 숫자는 움직이지 않았다(원래 0이었으니까).
  //   형식 목록을 늘리지 않는다. 계약은 "정규 형식으로 보고하라" 하나다.
  if (!counts.reported) {
    unreported.push(f);
    ok = false;
    // [v16/N-05] **원인을 숨기지 않는다.** 예전에는 무조건 "makeReporter 를 쓰세요" 라고만
    //   했다. 그런데 정규 형식을 이미 쓰는 파일도 **중간에 throw 하면** 마지막 결과 줄에
    //   도달하지 못해 여기로 온다 — 그때 이 안내는 읽는 사람을 **엉뚱한 곳으로 데려간다**
    //   (실측: test_v14_stability.js 가 두 사이클 연속 이 메시지로 막혔고, 실제 원인은
    //   형식이 아니었다). 잡아 둔 출력이 있으면 그 꼬리를 보여 준다.
    //   안내는 **지우지 않는다** — 보고하지 않는 파일에게 makeReporter 는 언제나 옳은
    //   조언이고, 그 안내가 사라지면 진짜 형식 미비를 잡던 벽(AC-02)이 무너진다.
    //   더하는 것은 **꼬리**다: 무엇이 죽었는지 볼 수 있어야 한다.
    const tail = String(output || '').trim().split('\n').filter(Boolean).slice(-6);
    const looksFormatted = /결과\s*:/.test(String(output || ''));
    console.log(`  ❌ ${f.padEnd(38)} ${String(ms).padStart(6)}ms  단언 미보고`);
    if (tail.length) {
      console.log(`       ↳ 마지막 출력 ${tail.length}줄:`);
      for (const l of tail) console.log(`         ${l.slice(0, 200)}`);
    }
    if (looksFormatted) {
      console.log(`       ↳ 정규 형식의 흔적이 있습니다 — 형식이 아니라 **중간에 죽은 것**일 수 있습니다.`);
    }
    console.log(`       → makeReporter 로 정규 형식을 내세요:`);
    console.log(`         const { makeReporter } = require('../run.js');`);
    console.log(`         const R = makeReporter();  …  R.check(라벨, 조건);  …  R.done();`);
    failures.push({
      file: f,
      line: looksFormatted
        ? '결과 줄에 도달하지 못함 — 중간에 죽었을 수 있습니다 (출력 꼬리 참조)'
        : '단언 미보고 — makeReporter 를 쓰세요',
      output,
    });
    continue;
  }
  if (ok) {
    passed++;
    const tag = `${counts.pass}건` + (counts.skip ? ` · SKIP ${counts.skip}` : '');
    console.log(`  ✅ ${f.padEnd(38)} ${String(ms).padStart(6)}ms  ${tag}`);
  }
  else {
    // 실패 원인을 한 줄로 압축 — Loop V 의 실패 시그니처 입력이 된다
    const line = output.split('\n').filter(l => /❌|FAIL|Error|Assertion/i.test(l))[0] || output.split('\n')[0] || '';
    failures.push({ file: f, line: line.trim().slice(0, 200), output });
    console.log(`  ❌ ${f.padEnd(38)} ${String(ms).padStart(6)}ms`);
  }
}

const elapsed = Date.now() - started;

// ── 복원 ─────────────────────────────────────────────────────────────
cleanupOnce(null);

// ── git HEAD 오염 검사 ───────────────────────────────────────────────
// 테스트가 실제 저장소 히스토리를 바꿨다면 실패로 취급한다. 조용히 넘기면
// 안 된다 — 커밋 로그는 사용자의 멀티세션 복구 기반이다.
const headAfter = currentHead();
if (headBefore && headAfter && headBefore !== headAfter) {
  console.error(`\n  ❌ [격리 위반] 테스트가 git HEAD 를 변경했습니다`);
  console.error(`     before: ${headBefore.slice(0, 8)}  after: ${headAfter.slice(0, 8)}`);
  console.error(`     원인 테스트를 찾아 임시 저장소(gitRun 의 두 번째 인자 cwd)를 쓰도록 고치세요.`);
  console.error(`     되돌리기: git reset --hard ${headBefore.slice(0, 8)}\n`);
  process.exit(1);
}

// ── [v4/N-10/FR-01] 오염 검사 ───────────────────────────────────────
// 복원(cleanupOnce)이 끝난 **뒤에** 본다 — 러너가 되돌린 것은 오염이 아니다.
if (dirtyBefore) {
  const after = dirtyTracked();
  if (after) {
    const contaminated = [...after].filter(p =>
      !dirtyBefore.has(p) && !OWNED_BY_HARNESS.some(pre => p === pre || p.startsWith(pre)));
    if (contaminated.length) {
      // 스위트 실행 구간을 함께 낸다 — 파일 mtime 과 대조하면 사람이 몇 초에 주체를 판별한다.
      const window = `${new Date(started).toTimeString().slice(0, 8)} ~ ${new Date().toTimeString().slice(0, 8)}`;
      const others = liveInteractiveWindows();
      // 나 자신도 대화형 창으로 등록돼 있다 — 2 이상이어야 '남이 있다'.
      // 스토어를 못 읽었으면(null) 주체를 모르므로 보수적으로 실패다.
      const concurrent = others !== null && others >= 2;
      const label = concurrent
        ? `⚠ [격리 판정 보류] 추적 파일 ${contaminated.length}개가 스위트 실행 중에 변경됐습니다`
        : `❌ [격리 위반] 테스트가 저장소 파일 ${contaminated.length}개를 오염시켰습니다`;
      console.error(`\n  ${label}`);
      console.error(`     스위트 실행 구간: ${window}`);
      for (const p of contaminated.slice(0, 8)) console.error(`     ${p}   (mtime ${mtimeOf(p)})`);
      if (concurrent) {
        console.error(`     살아 있는 대화형 창 ${others}개 — 이 변경의 주체를 가릴 수 없어 실패로 판정하지 않습니다.`);
        console.error('     내용을 먼저 보세요: git diff ' + contaminated.slice(0, 3).join(' '));
        console.error('     다른 창의 작업이면 그대로 두세요. 혼자일 때 같은 파일이 다시 나오면 그때가 오염입니다.\n');
      } else {
        console.error('     테스트는 임시 디렉터리에서 돌아야 합니다 — tests/unit/_sandbox.js 의 makeSandbox 를 쓰거나,');
        console.error('     훅을 임시 디렉터리로 **복사**해 PROJECT_ROOT 가 거기로 잡히게 하세요.');
        // 되돌리기 명령을 권하지 않는다 — 474 행의 약속(남의 변경을 삼키지 않는다)과 어긋난다.
        console.error('     내용을 먼저 보세요: git diff ' + contaminated.slice(0, 3).join(' ') + '\n');
        process.exit(1);
      }
    }
  }
}

// ── 결과 ─────────────────────────────────────────────────────────────
// [v3.6/N-08/IMPL-01] 파일 개수가 아니라 **단언과 건너뜀**을 함께 보고한다.
console.log(`\n  ${passed}/${files.length} 파일 · 단언 ${totalAsserts}`
  + (totalSkips ? ` · SKIP ${totalSkips}` : '')
  + (unreported.length ? ` · 단언 미보고 ${unreported.length}파일` : '')
  + `  (${(elapsed / 1000).toFixed(1)}s)\n`);
for (const s of skipDetail.slice(0, 5)) console.log(`     ⏭ ${s}`);
if (unreported.length) console.log(`     ℹ 단언 수를 보고하지 않는 파일: ${unreported.slice(0, 4).join(', ')}${unreported.length > 4 ? ` +${unreported.length - 4}` : ''}`);

// SKIP 허용치 — 넘으면 실패한다. 조용한 건너뜀이 초록으로 남는 경로를 없앤다.
//   허용치는 baseline 파일에 **명시**한다. 0 이 기본이고, 늘리려면 사유를 적어야 한다.
const SKIP_ALLOWANCE = (() => {
  try { return JSON.parse(fs.readFileSync(path.join(ROOT, 'tests', 'baseline', 'skip-allowance.json'), 'utf8')).allowed | 0; }
  catch { return 0; }
})();
if (totalSkips > SKIP_ALLOWANCE) {
  console.error(`\n  ❌ [건너뜀 초과] SKIP ${totalSkips} > 허용 ${SKIP_ALLOWANCE}`);
  console.error(`     건너뛴 테스트는 초록이 아닙니다. 픽스처를 심어 실행시키거나,`);
  console.error(`     tests/baseline/skip-allowance.json 에 사유와 함께 허용치를 올리세요.\n`);
  process.exit(1);
}

if (failures.length > 0) {
  console.log('  실패 상세:');
  for (const f of failures) console.log(`    ❌ ${f.file}\n       ${f.line}`);
  console.log('');
  process.exit(1);
}

// ── [v4/N-01/FR-04] 파일별 단언 기준선 ───────────────────────────────
//
// 왜 필요한가: 초록은 "지금 도는 것이 통과했다" 만 말한다. 어제 30건을 재던 파일이
//   오늘 3건만 재도 초록이다. 그 침묵을 숫자로 깨는 것이 이 기준선이다.
//
// **이것은 사람이 유지하는 목록이 아니다** — 관측 기록이다. 신규는 자동 등록되고
//   증가는 자동 반영된다. 사람에게 묻는 것은 **감소** 하나뿐이다.
//   (이 저장소가 다섯 번 걸린 "잊을 수 있는 목록" 이 되지 않도록 한 설계다.)
//
// 필터 실행에서는 판정하지 않는다 — 일부만 돌린 결과로 기준선을 깎으면 안 된다.
const BASE_FILE = path.join(ROOT, 'tests', 'baseline', 'assert-counts.json');
const ACCEPT_DROP = args.includes('--accept-drop');
const DROP_REASON = (() => {
  const i = args.indexOf('--reason');
  return i >= 0 && args[i + 1] && !args[i + 1].startsWith('--') ? args[i + 1] : '';
})();

if (!FILTERS.length) {
  let base = {};
  try { base = JSON.parse(fs.readFileSync(BASE_FILE, 'utf8')).files || {}; } catch { /* 첫 실행 */ }

  const now = {};
  for (const f of files) now[f] = fileAsserts[f] || 0;

  const drops = [];
  for (const [f, prev] of Object.entries(base)) {
    if (!(f in now)) continue;                 // 삭제된 파일은 아래에서 알린다
    if (now[f] < prev) drops.push(`${f}: ${prev} → ${now[f]}`);
  }
  const gone = Object.keys(base).filter(f => !(f in now));
  const added = Object.keys(now).filter(f => !(f in base));

  if (drops.length && !ACCEPT_DROP) {
    console.error(`\n  ❌ [단언 감소] ${drops.length}개 파일의 단언 수가 줄었습니다`);
    for (const d of drops) console.error(`     ${d}`);
    console.error(`\n     초록이지만 검증량이 줄었습니다. 테스트를 되살리거나,`);
    console.error(`     의도한 것이라면 사유와 함께 인정하세요:`);
    console.error(`       node tests/run.js --accept-drop --reason "왜 줄었는가"\n`);
    process.exit(1);
  }
  if (drops.length && ACCEPT_DROP && !DROP_REASON) {
    console.error(`\n  ❌ [사유 필요] --accept-drop 은 이유를 요구합니다`);
    console.error(`     node tests/run.js --accept-drop --reason "왜 줄었는가"\n`);
    process.exit(1);
  }
  if (drops.length) {
    console.log(`\n  ⚠ 감소 인정: ${drops.join(' · ')}`);
    console.log(`     사유: ${DROP_REASON}`);
  }

  try {
    fs.mkdirSync(path.dirname(BASE_FILE), { recursive: true });
    fs.writeFileSync(BASE_FILE, JSON.stringify({
      _note: '파일별 단언 수의 관측 기록입니다. 손으로 고치지 마세요 — 러너가 갱신합니다. 감소는 --accept-drop --reason 을 요구합니다.',
      updated_at: new Date().toISOString(),
      files: now,
    }, null, 2) + '\n');
  } catch (e) { console.error(`  ⚠ 기준선 기록 실패: ${e.message}`); }

  if (added.length) console.log(`  ℹ 기준선 신규 등록 ${added.length}개: ${added.slice(0, 3).join(', ')}${added.length > 3 ? ' …' : ''}`);
  if (gone.length) console.log(`  ℹ 기준선에서 제거 ${gone.length}개(파일 없음): ${gone.slice(0, 3).join(', ')}${gone.length > 3 ? ' …' : ''}`);
}

process.exit(0);
