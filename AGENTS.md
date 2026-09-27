
<!-- harness-v14:start -->
<!-- gen:harness-entry -->
<!-- 이 블록은 scripts/gen-manual-tables.js 가 CLAUDE.md 에서 생성합니다.
     정본은 CLAUDE.md 입니다 — 여기를 손으로 고치면 문서 동기화 테스트(DS-18)가 깨집니다. -->

# 하네스 공통 진입점 (생성물)

규칙의 **정본은 `CLAUDE.md`** 이고, 아래 절은 그 파일에서 바이트 그대로 옮겨진다.
이 파일을 고치지 말고 `CLAUDE.md` 를 고친 뒤 `node scripts/gen-manual-tables.js` 를 돌린다.

런타임 고유 배선·관리형 CLI 계약·UI 테스트 요구는 `.claude/skills/process/SKILL.md` 와
`.claude/skills/process/MANAGED.md` 에 있다 — 여기로 복사하지 않는다.

## 현재 실행 방식 (v14)

새 작업은 [process 스킬의 v14 진입점](.claude/skills/process/SKILL.md)을 한 번 읽고 따른다. 같은 내용을 매 단계 다시 로드하지 않는다.

- A: 읽기 전용 응답. B: 1노드·현재 모델·필요한 테스트. C: 대표 PRD + Plan + 의존 그래프.
- 큰 작업은 분석을 시작하기 전에 `work draft`로 등록하고, `models invoke --work-id`와 `work create --draft`로 같은 ID를 유지한다.
- 모델 설정·현황: `node .claude/hooks/advance-phase.js models`
- 다중 창은 작업 ID를 공유하고 `work run`으로 격리 실행한다. `work context WORK_ID`로 짧은 컨텍스트를 받는다.
- 이미 받은 실행 요청을 되묻지 않는다. 필요하지 않은 분석·테스트 결과·보고서 MD는 생략한다.
- 아래 봉투·phase 규칙은 진행 중인 레거시 작업을 이어갈 때 적용한다.

## 매 응답 후 — 컨텍스트 보존

**결정을 내렸으면 기록한다.** 이것이 하네스의 존재 이유다 —
다음 세션의 내가 같은 논의를 반복하지 않도록.

기록은 **CLI 로만** 한다. `docs/memory/decisions.jsonl` 이 진실이고,
`session-context.md` 의 5열 표는 하네스가 렌더링하는 파생 투영이다 — 손으로 고치면 덮어쓴다.

```bash
node .claude/hooks/advance-phase.js decide --json-file <경로>
# 또는  decide --json '{…}'   ·   decide --from-commit <sha> --json-file <경로>
```

- **기각안(`rejected[]`)**: 택하지 않은 대안. 이 칸이 재논쟁을 막는다 — 결정만 적으면 못 막는다.
- **재발방지(`invalidation`)**: 이 결정이 틀렸다면 무엇을 보고 알 수 있는가.
  `specs` 에 기계 검증 문법으로 쓰면 Loop C 가 매 세션 종료 때 실제로 평가한다.
- 둘 중 하나라도 비면 `decide` 가 **거부한다**.
- 결정을 뒤집을 때는 행을 고치지 않고 새 행에 `supersedes` 로 이전 id 를 가리킨다.

기록할 때: 명명된 대안 중 하나를 택했을 때 · 사용자가 교정했을 때.
백워드 전이 · `--force` · Loop V 재시도 통과 · escalate 는 하네스가 `cycle.needsDecision[]` 에
자동 적재하고, 그 사이클은 **원장 없이 `complete` 할 수 없다.**

문법·스키마 상세는 [`.claude/skills/process/LOOPS.md`](.claude/skills/process/LOOPS.md) 의 Loop D.

> `docs/INDEX.md` 와 결정 표는 파생 산출물이라 직접 갱신하지 않는다.
> post-write-sync 훅과 `advance-phase-heal.js`가 만든다.

## 참조 경로

| 필요할 때 | 읽을 파일 |
|----------|---------|
| Track/Phase 상세 규칙 | `.claude/skills/process/SKILL.md` |
| 루프 계약 (검증·원장·완료검증·탐색) | `.claude/skills/process/LOOPS.md` |
| 해당 Phase의 작업 지침 | `.claude/skills/{phase별 스킬}/SKILL.md` |
| Hook·명령어·설치·전체 설명 | [`docs/Manual.md`](docs/Manual.md) |
| 현재 세션 상태 | `docs/memory/session-context.md` |
| 기계적 파이프라인 상태 | `node .claude/hooks/advance-phase.js status` (브랜치별 경로 출력) |
<!-- /gen:harness-entry -->
<!-- harness-v14:end -->
