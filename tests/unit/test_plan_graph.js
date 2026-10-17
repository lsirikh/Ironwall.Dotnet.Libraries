// tests/unit/test_plan_graph.js
// _plan-graph.js 순수 함수 테스트 (v3.1 / TEST-03)
'use strict';

const assert = require('assert');
const path = require('path');
const G = require(path.resolve(__dirname, '../../.claude/hooks/_plan-graph.js'));

let pass = 0, fail = 0;
function test(name, fn) {
  try { fn(); pass++; console.log(`  ✅ ${name}`); }
  catch (e) { fail++; console.log(`  ❌ ${name}: ${e.message}`); }
}

// 20-태스크 픽스처: 4 leaf(IMPL-01..04) → core(IMPL-05,06) → 12 dependent → 2 test
function fixture20() {
  const lines = ['# plan', ''];
  for (let i = 1; i <= 4; i++) lines.push(`- [ ] **[IMPL-0${i}]** leaf ${i}`, `  - 파일: \`src/leaf${i}.js\``, `  - 예상 공수: 1h`);
  lines.push('- [ ] **[IMPL-05]** core a', '  - 파일: `src/core-a.js`', '  - 의존: IMPL-01, IMPL-02', '  - 예상 공수: 3h');
  lines.push('- [ ] **[IMPL-06]** core b', '  - 파일: `src/core-b.js`', '  - 의존: IMPL-03, IMPL-04', '  - 예상 공수: 3h');
  for (let i = 7; i <= 18; i++) lines.push(`- [ ] **[IMPL-${i}]** dependent ${i}`, `  - 파일: \`src/dep${i}.js\``, '  - 의존: IMPL-05, IMPL-06', '  - 예상 공수: 1h');
  lines.push('- [ ] **[TEST-01]** integration', '  - 의존: 모든 IMPL', '  - 예상 공수: 2h');
  lines.push('- [ ] **[TEST-02]** e2e', '  - 의존: TEST-01', '  - 예상 공수: 2h');
  return lines.join('\n');
}

console.log('\n═══ PG: 태스크 DAG 순수 함수 ═══\n');

test('should_parse_numeric_prefix_ids (FR01-01 형식)', () => {
  const g = G.parse('- [ ] **[FR01-01]** x\n- [x] **[IMPL-2]** y');
  assert.ok(g.tasks['FR01-01'], 'FR01-01 파싱 실패');
  assert.strictEqual(g.tasks['IMPL-2'].status, 'x');
});

test('should_expand_range_and_all_deps', () => {
  const g = G.parse([
    '- [ ] **[IMPL-01]** a', '- [ ] **[IMPL-02]** b', '- [ ] **[IMPL-03]** c',
    '- [ ] **[TEST-01]** t', '  - 의존: IMPL-01~03',
    '- [ ] **[TEST-02]** u', '  - 의존: 모든 IMPL',
  ].join('\n'));
  assert.deepStrictEqual(g.tasks['TEST-01'].deps.sort(), ['IMPL-01', 'IMPL-02', 'IMPL-03']);
  assert.deepStrictEqual(g.tasks['TEST-02'].deps.sort(), ['IMPL-01', 'IMPL-02', 'IMPL-03']);
});

test('should_flag_unresolved_dep_text', () => {
  const g = G.parse('- [ ] **[IMPL-01]** a\n- [ ] **[IMPL-02]** b\n  - 의존: 로그인 기능이 끝난 뒤');
  assert.ok(g.parseErrors.some(e => /미해석/.test(e)), `parseErrors=${JSON.stringify(g.parseErrors)}`);
});

test('should_flag_missing_dep_reference', () => {
  const g = G.parse('- [ ] **[IMPL-01]** a\n  - 의존: IMPL-99');
  assert.ok(g.parseErrors.some(e => /존재하지 않는/.test(e)));
});

test('should_compute_kahn_layers_6_12_2 (+2 core)', () => {
  const s = G.schedule(fixture20());
  // 층: [leaf 4개] [core 2개] [dependent 12개] [TEST-01] [TEST-02]
  const sizes = s.layers.map(l => l.length);
  assert.deepStrictEqual(sizes, [4, 2, 12, 1, 1], `layers=${JSON.stringify(sizes)}`);
  assert.strictEqual(s.cyclic.length, 0);
});

test('should_compute_unblocked_and_blockedBy', () => {
  const s = G.schedule(fixture20());
  assert.deepStrictEqual(s.unblocked.sort(), ['IMPL-01', 'IMPL-02', 'IMPL-03', 'IMPL-04'], `unblocked=${s.unblocked}`);
  assert.deepStrictEqual((s.blockedBy['IMPL-05'] || []).sort(), ['IMPL-01', 'IMPL-02']);
  assert.deepStrictEqual((s.blockedBy['TEST-02'] || []), ['TEST-01']);
});

test('should_advance_unblocked_as_deps_complete', () => {
  let txt = fixture20().replace('- [ ] **[IMPL-01]**', '- [x] **[IMPL-01]**').replace('- [ ] **[IMPL-02]**', '- [x] **[IMPL-02]**');
  const s = G.schedule(txt);
  assert.ok(s.unblocked.includes('IMPL-05'), 'IMPL-01,02 완료 후 IMPL-05 unblocked 여야');
  assert.ok(!s.unblocked.includes('IMPL-01'), '완료된 태스크는 unblocked 아님');
});

test('should_compute_critical_path', () => {
  const s = G.schedule(fixture20());
  // leaf(1) → core(3) → dependent(1) → TEST-01(2) → TEST-02(2) = 9h
  assert.strictEqual(s.criticalPath.len, 9, `CP len=${s.criticalPath.len} path=${s.criticalPath.path}`);
  assert.strictEqual(s.criticalPath.path[s.criticalPath.path.length - 1], 'TEST-02');
});

test('should_detect_order_violation_x_before_deps', () => {
  // IMPL-05 가 [x] 인데 의존 IMPL-01 이 [ ]
  const txt = fixture20().replace('- [ ] **[IMPL-05]**', '- [x] **[IMPL-05]**');
  const s = G.schedule(txt);
  assert.ok(s.violations.some(v => v.id === 'IMPL-05' && v.openDeps.includes('IMPL-01')), `violations=${JSON.stringify(s.violations)}`);
});

test('should_build_fileOwner_map', () => {
  const s = G.schedule(fixture20());
  assert.deepStrictEqual(s.fileOwner['src/core-a.js'], ['IMPL-05']);
  assert.strictEqual(Object.keys(s.fileOwner).length, 18); // 18 파일 (TEST 2개는 파일 없음)
});

test('should_detect_cycle', () => {
  const s = G.schedule([
    '- [ ] **[IMPL-01]** a', '  - 의존: IMPL-03',
    '- [ ] **[IMPL-02]** b', '  - 의존: IMPL-01',
    '- [ ] **[IMPL-03]** c', '  - 의존: IMPL-02',
  ].join('\n'));
  assert.ok(s.cycles.length >= 1, `cycles=${JSON.stringify(s.cycles)}`);
  assert.strictEqual(s.cyclic.length, 3, '순환 3개 노드가 층에 안 들어가야');
});

test('should_treat_skip_dash_as_satisfied', () => {
  const txt = fixture20().replace('- [ ] **[IMPL-01]**', '- [-] **[IMPL-01]**').replace('- [ ] **[IMPL-02]**', '- [x] **[IMPL-02]**');
  const s = G.schedule(txt);
  assert.ok(s.unblocked.includes('IMPL-05'), '[-] 도 충족으로 간주 → IMPL-05 unblocked');
});

test('should_schedule_60_tasks_under_5ms', () => {
  const lines = ['# plan'];
  for (let i = 1; i <= 60; i++) {
    lines.push(`- [ ] **[IMPL-${String(i).padStart(2, '0')}]** t${i}`, `  - 파일: \`src/f${i}.js\``);
    if (i > 1) lines.push(`  - 의존: IMPL-${String(i - 1).padStart(2, '0')}`);
  }
  const txt = lines.join('\n');
  const t0 = process.hrtime.bigint();
  for (let k = 0; k < 10; k++) G.schedule(txt);
  const ms = Number(process.hrtime.bigint() - t0) / 1e6 / 10;
  assert.ok(ms < 5, `schedule 60태스크 ${ms.toFixed(3)}ms (< 5ms 여야)`);
  console.log(`     (60태스크 schedule 평균 ${ms.toFixed(3)}ms)`);
});

// ── [v6/N-01] joinContinuation — 마크다운 리스트 항목의 연속 줄 ──────
//   사람은 긴 파일 목록을 여러 줄에 나눠 적는다. 마크다운에서 **들여쓴 다음 줄은 같은
//   리스트 항목의 연속**이고, 사람은 그 규칙대로 썼다. 파서가 규칙을 몰라 첫 줄만 읽었다.
//
//   조용했다는 것이 핵심이다 — `파싱 오류: 없음` 이면서 7개 중 3개만 읽혔다.
//   그리고 그 잘림이 두 방향으로 샜다: charter 에서는 **의존 간선이 사라져** C8 이 미완료
//   노드를 완료로 봤고, plan 에서는 잘린 첫 줄이 문서 경로라 **증거 게이트가 꺼졌다.**
//
//   여기서 지키는 것은 이어 붙이는 것만이 아니라 **어디서 멈추는가** 다.
//   과하게 삼키면 다음 태스크가 통째로 사라진다.
test('should_join_indented_continuation_lines', () => {
  const L = ['  - 파일: `a.js`,', '    `b.js`, `c.js`'];
  const r = G.joinContinuation(L, 0);
  assert.ok(/a\.js/.test(r.text) && /b\.js/.test(r.text) && /c\.js/.test(r.text), r.text);
  assert.strictEqual(r.next, 2, `다음 인덱스: ${r.next}`);
});

test('should_join_many_continuation_lines', () => {
  const L = ['  - 파일: `a.js`,', '    `b.js`,', '    `c.js`,', '    `d.js`'];
  const r = G.joinContinuation(L, 0);
  for (const f of ['a.js', 'b.js', 'c.js', 'd.js']) assert.ok(r.text.includes(f), `${f} 누락: ${r.text}`);
  assert.strictEqual(r.next, 4);
});

test('should_stop_at_next_list_item', () => {
  // **가장 중요한 경계.** 삼키면 다음 태스크/노드가 통째로 사라진다.
  const L = ['  - 파일: `a.js`,', '    `b.js`', '  - 의존: IMPL-01', '- [ ] **[IMPL-02]** 다음'];
  const r = G.joinContinuation(L, 0);
  assert.ok(!/의존/.test(r.text), `다음 항목을 삼켰다: ${r.text}`);
  assert.ok(!/IMPL-02/.test(r.text), `다음 태스크를 삼켰다: ${r.text}`);
  assert.strictEqual(r.next, 2);
});

test('should_stop_at_blank_line', () => {
  const L = ['  - 파일: `a.js`,', '', '    `b.js`'];
  const r = G.joinContinuation(L, 0);
  assert.ok(!/b\.js/.test(r.text), `빈 줄을 넘어 이어 붙였다: ${r.text}`);
  assert.strictEqual(r.next, 1);
});

test('should_stop_at_unindented_line', () => {
  const L = ['  - 파일: `a.js`,', '# 다음 절'];
  const r = G.joinContinuation(L, 0);
  assert.ok(!/다음 절/.test(r.text), r.text);
  assert.strictEqual(r.next, 1);
});

test('should_be_a_noop_for_single_line_lists', () => {
  // 기존 charter 3개·plan 다수가 전부 한 줄 형태다. **하나도 달라지면 안 된다.**
  const L = ['  - 파일: `a.js`, `b.js`', '  - 예상 공수: 1h'];
  const r = G.joinContinuation(L, 0);
  assert.strictEqual(r.text, L[0]);
  assert.strictEqual(r.next, 1);
});

test('should_handle_last_line_without_crashing', () => {
  const L = ['  - 파일: `a.js`'];
  const r = G.joinContinuation(L, 0);
  assert.strictEqual(r.text, L[0]);
  assert.strictEqual(r.next, 1);
});

// ── [v12/N-01] depGate — 게이트는 원본 deps 로 판정한다 ──────────────
//   `frontier()` 는 "지금 시작할 수 있는 것" 을 보여 주는 표시 함수라, 대상이 [~]·[-] 면
//   blockedBy 계산을 건너뛴다. done 게이트가 그 표시값을 써서 선행이 [ ] 여도
//   start→done · skip→done 이 --force 없이 통과했다(F-01). start 에는 검사가 없었다.
//   판정 의미는 사용자 결정 a4e0d9 — start 는 선행이 착수 이상(~ x -), done 은 완료(x -).
function depPlan(pre, tgt) {
  return [
    `- [${pre}] **[T-01]** 선행`, '  - 파일: `src/a.js`',
    `- [${tgt}] **[T-02]** 후속`, '  - 파일: `src/b.js`', '  - 의존: T-01',
  ].join('\n');
}

test('should_gate_start_on_prerequisite_being_touched', () => {
  for (const s of [' ', '!']) {
    const r = G.depGate(G.parse(depPlan(s, ' ')), 'T-02', 'start');
    assert.strictEqual(r.ok, false, `선행 [${s}] 인데 start 를 허용했다`);
    assert.deepStrictEqual(r.missing, [{ id: 'T-01', status: s }]);
  }
  for (const s of ['~', 'x', '-']) {
    const r = G.depGate(G.parse(depPlan(s, ' ')), 'T-02', 'start');
    assert.strictEqual(r.ok, true, `선행 [${s}] 인데 start 를 거부했다: ${JSON.stringify(r)}`);
    assert.deepStrictEqual(r.missing, []);
  }
});

test('should_gate_done_on_prerequisite_being_finished', () => {
  for (const s of [' ', '~', '!']) {
    const r = G.depGate(G.parse(depPlan(s, '~')), 'T-02', 'done');
    assert.strictEqual(r.ok, false, `선행 [${s}] 인데 done 을 허용했다`);
    assert.deepStrictEqual(r.missing, [{ id: 'T-01', status: s }]);
  }
  for (const s of ['x', '-']) {
    const r = G.depGate(G.parse(depPlan(s, '~')), 'T-02', 'done');
    assert.strictEqual(r.ok, true, `선행 [${s}] 인데 done 을 거부했다: ${JSON.stringify(r)}`);
  }
});

test('should_judge_by_original_deps_regardless_of_target_status', () => {
  // 우회의 뿌리 — 대상이 [~]·[-] 가 되는 순간 표시 계산에서 빠졌다
  for (const tgt of [' ', '~', '-', 'x', '!']) {
    for (const op of ['start', 'done']) {
      const r = G.depGate(G.parse(depPlan(' ', tgt)), 'T-02', op);
      assert.strictEqual(r.ok, false, `대상 [${tgt}] · ${op} 에서 선행 [ ] 를 놓쳤다`);
    }
  }
  // 표시용 blockedBy 는 바꾸지 않는다 — 게이트만 떼어 낸다
  assert.ok(!G.schedule(depPlan(' ', '~')).blockedBy['T-02'], '표시용 blockedBy 가 바뀌었다');
});

test('should_list_every_missing_prerequisite_in_dep_order', () => {
  const txt = [
    '- [ ] **[T-01]** a', '- [~] **[T-03]** c', '- [x] **[T-04]** d',
    '- [ ] **[T-02]** b', '  - 의존: T-01, T-03, T-04',
  ].join('\n');
  const r = G.depGate(G.parse(txt), 'T-02', 'done');
  assert.deepStrictEqual(r.missing, [{ id: 'T-01', status: ' ' }, { id: 'T-03', status: '~' }]);
  assert.deepStrictEqual(G.depGate(G.parse(txt), 'T-02', 'start').missing, [{ id: 'T-01', status: ' ' }]);
});

test('should_ignore_prerequisites_missing_from_plan', () => {
  // plan 에 없는 선행은 parseErrors 가 이미 보고한다 — 게이트가 영구히 잠그지 않는다
  const g = { tasks: { 'T-02': { status: ' ', deps: ['T-99'] } }, order: ['T-02'] };
  assert.deepStrictEqual(G.depGate(g, 'T-02', 'start'), { ok: true, missing: [] });
  assert.deepStrictEqual(G.depGate(g, 'T-02', 'done'), { ok: true, missing: [] });
});


console.log('\n═══════════════════════════════════════════════════════');
// ══ 레거시 사이클이 그래프에 뜬다 (charter harness-v16 / N-06) ═════════
//
// 실측으로 확인한 문제: `advance-phase.js` 안의 work/·node/ 레코드 참조가 **0건**이었다.
//   스토어에는 v14 관리형 작업만 있었고(work 16 · node 30), 레거시 phase 로 한 일은
//   화면에 한 줄도 남지 않았다 — 봉투의 한 줄 진단이 그것이다.
//
// 규칙의 절반은 이미 코드에 있었다. `_coordinator.js` 의 `graph()` 가 Track B 를 노드
//   정확히 1개로 강제하고, Track A 는 애초에 받지 않는다. **빠진 것은 규칙이 아니라 배선**이다.
//
// 이 절에서 가장 중요한 단언은 **Track A 대조군**이다. 가장 쉬운 실수가 "전부 등록하기" 이고,
//   그러면 읽기 전용 응답으로 화면이 가득 찬다.
{
  const fs = require('fs');
  const AP = fs.readFileSync(path.resolve(__dirname, '../../.claude/hooks/advance-phase.js'), 'utf8');
  const code = AP.split('\n').filter(l => !/^\s*(\/\/|\*|\/\*)/.test(l)).join('\n');

  // 판정은 한 벌이다 — 아래 반증이 이 술어를 그대로 쓴다.
  const registersGraph = t => /createWork\s*\(/.test(t) && /legacy-cycle/.test(t);
  test('레거시 사이클이 그래프 레코드를 만든다', () => {
    assert.ok(registersGraph(code), 'createWork 호출이 없다 — 레거시가 그래프에 안 뜬다');
  });

  // **같은 레코드**여야 한다. 손으로 tx.put('work/…') 을 쓰면 모양이 미묘하게 갈리고
  //   화면·코디네이터가 그것을 못 읽는다. 정본은 Coordinator.createWork 다.
  test('레코드를 손으로 만들지 않는다 — 모양의 정본은 createWork 다', () => {
    assert.ok(!/tx\.put\(\s*['"]work\//.test(code), "advance-phase 가 work/ 레코드를 직접 쓴다");
    assert.ok(!/tx\.put\(\s*['"]node\//.test(code), "advance-phase 가 node/ 레코드를 직접 쓴다");
  });

  // [Loop A H-2·H-3] 첫 판은 **파일 전체**(3,400줄)에서 정규식을 찾았다. 그래서
  //   `registerCycleGraph` 의 판정을 통째로 지워도 무관한 다른 줄(590·2927행)이 매치를
  //   대신 줬고, catch 도 근처 400자 안에 토큰이 있는지만 봐서 정반대 동작으로 바꿔도
  //   초록이었다. **함수 본문만** 잘라서 본다.
  const fnBody = (() => {
    const k = code.indexOf('function registerCycleGraph');
    if (k < 0) return '';
    const rest = code.slice(k);
    const stop = rest.indexOf('\nconst harness');
    return stop > 0 ? rest.slice(0, stop) : rest.slice(0, 4000);
  })();

  test('함수 본문을 잘라낼 수 있다 — 아래 대조군의 전제', () => {
    assert.ok(fnBody.length > 200, 'registerCycleGraph 본문을 못 찾았다: ' + fnBody.length);
  });

  test('대조군: Track A 는 그래프를 만들지 않는다', () => {
    assert.ok(/\['B',\s*'C'\]\.includes\(/.test(fnBody) || /track\s*===\s*'A'/.test(fnBody),
      'registerCycleGraph 안에 Track A 배제 판정이 없다');
  });

  test('대조군: 등록 실패가 사이클을 막지 않는다', () => {
    assert.ok(/catch\s*\{/.test(fnBody), 'registerCycleGraph 에 실패를 삼키는 catch 가 없다');
    assert.ok(!/process\.exit/.test(fnBody), 'registerCycleGraph 가 프로세스를 끝낸다 — 사이클을 막는다');
    assert.ok(!/\bthrow\b/.test(fnBody), 'registerCycleGraph 가 예외를 다시 던진다 — 사이클을 막는다');
  });

  test('멱등은 상태 쓰기가 아니라 스토어 조회로 판정한다', () => {
    // [Loop A Critical] 두 저장소에 걸친 2단계 커밋은 보상 로직이 없으면 고아를 만든다.
    assert.ok(/cycle_key/.test(fnBody), '사이클 키로 판정하지 않는다');
    assert.ok(/list\(\s*'work\/'\s*\)/.test(fnBody), '스토어를 조회해 기존 등록을 찾지 않는다');
  });

  test('중복 태스크 id 가 등록을 죽이지 않는다', () => {
    // plan 파서는 중복 id 를 order 에 그대로 넣는다(경고만). graph() 는 INVALID_NODE 로 죽고
    //   catch 가 삼켜 그 사이클은 영영 그래프에 뜨지 않는다.
    assert.ok(/new Set\(tasks\)/.test(fnBody), '중복 태스크 id 를 제거하지 않는다');
    assert.ok(/slice\(0,\s*200\)/.test(fnBody), '200 노드 상한을 지키지 않는다');
  });

  // 반증: 호출을 지운 사본에서는 판정이 거짓이 된다.
  test('반증: createWork 를 지운 사본은 판정이 거짓이다', () => {
    const back = code.replace(/createWork\s*\(/g, 'noop(');
    assert.ok(back !== code, '변형이 일어나지 않았다');
    assert.ok(!registersGraph(back), '지워도 참');
  });

  // 반증: Track B 가 노드 2개를 주면 기존 벽이 막는다 — 이미 있는 규칙을 확인한다.
  // [Loop A 를 기다리지 않고] 소스 텍스트만 보는 단언은 약하다 — 첫 판이 그래서
  //   origin 이 레코드에 안 남는데도 초록이었다. **실제 레코드**를 만들어 확인한다.
  test('출처가 실제 레코드에 남는다 — 소스 텍스트가 아니라 값으로', () => {
    const { Coordinator } = require(path.resolve(__dirname, '../../.claude/hooks/_coordinator.js'));
    const { repository } = require(path.resolve(__dirname, '../../.claude/hooks/_harness-store.js'));
    const repo = repository(path.resolve(__dirname, '../../'));
    try {
      const spec = { track: 'C', title: 'probe', nodes: [{ id: 'n1', tests: [{ mode: 'automated' }] }], origin: 'legacy-cycle', charter: 'c-x', charter_node: 'N-06' };
      const w = new Coordinator(repo.store, { repo_id: repo.repo_id }).createWork(spec);
      try {
        const rec = repo.store.get('work/' + w.work_id).value;
        assert.strictEqual(rec.origin, 'legacy-cycle', 'origin 이 레코드에 없다: ' + JSON.stringify(Object.keys(rec)));
        assert.strictEqual(rec.charter_node, 'N-06', 'charter_node 가 없다');
        const n = repo.store.get('node/' + w.work_id + '/n1').value;
        for (const k of ['status', 'epoch', 'attempts']) assert.ok(k in n, '관리형과 모양이 다르다: ' + k + ' 없음');
      } finally {
        repo.store.transaction(tx => { tx.remove('node/' + w.work_id + '/n1'); tx.remove('work/' + w.work_id); });
      }
    } finally { repo.store.close(); }
  });
  test('반증: Track B 에 노드 2개를 주면 INVALID_GRAPH', () => {
    const C = require(path.resolve(__dirname, '../../.claude/hooks/_coordinator.js'));
    const spec = { track: 'B', title: 't', nodes: [{ id: 'a', tests: [{ mode: 'automated' }] }, { id: 'b', tests: [{ mode: 'automated' }] }] };
    let threw = null;
    try {
      const { repository } = require(path.resolve(__dirname, '../../.claude/hooks/_harness-store.js'));
      const repo = repository(path.resolve(__dirname, '../../'));
      try { new C.Coordinator(repo.store, { repo_id: repo.repo_id }).createWork(spec); } finally { repo.store.close(); }
    } catch (e) { threw = e; }
    assert.ok(threw, 'Track B 에 노드 2개가 통과했다');
    assert.ok(/INVALID_GRAPH/.test(String(threw.code || threw.message)), String(threw.code || threw.message));
  });
}

console.log(`  결과: ${pass} PASS / ${fail} FAIL / ${pass + fail} 총계`);
console.log('═══════════════════════════════════════════════════════\n');
process.exit(fail > 0 ? 1 : 0);
