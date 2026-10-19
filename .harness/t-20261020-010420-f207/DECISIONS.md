# DECISIONS

## 1. 병렬 에이전트 작업 공간을 어디에 둘지
- options: (a) 이 작업 트리 하나에서 에이전트가 번갈아 빌드 (b) Agent worktree 격리 (c) 작업 트리 안 git 무시 폴더에 저장소 복사본
- chosen: (c) — `artifacts/obj/g1~g3`(GIS) · `artifacts/map3d`(map-3d clone)
- why: 빌드가 겹치지 않고, 작업 트리 밖에 찌꺼기를 남기지 않으며(밖은 지울 수 없음) 끝나면 지울 수 있다. 처음엔 `artifacts/gN` 에 두었는데 원본 소스를 훑는 계약 시험(Utils.Tests HelpContractTests 등)이 복사본까지 읽어 7건 실패 → 그 시험들이 공통으로 건너뛰는 `\obj\` 경로 아래로 옮김.
- effect: 결과물은 이 작업 트리로 옮겨 합친 뒤 복사본은 지운다.

## 2. PerfSamplerTests 테스트 호스트 중단(기존 시험의 경쟁)
- options: (a) 그대로 두고 재실행에 맡김 (b) 시험의 gate 폐기만 없앰 (c) 시작 신호 · 끝 신호를 더해 순서를 강제
- chosen: (b) — `should_measure_scheduled_to_run_delay_when_ui_thread_is_blocked` · `should_include_pending_age_in_max_when_reporting_during_stall` 의 `using var gate` → `var gate`
- why: 판정(Assert)은 하나도 바꾸지 않고, 부하 때 디스패처가 늦게 집어 든 `gate.Wait` 콜백이 이미 폐기된 gate 를 만나 ObjectDisposedException 으로 테스트 호스트 전체가 죽는 것만 막는다. 기준선(r0)과 이번 실행에서 2회 재현, 고친 뒤 3회 연속 886 통과.
- effect: 시험 코드 2줄. 제품 코드 무관.
