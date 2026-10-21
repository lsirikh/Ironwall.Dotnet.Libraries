# PLAN — L3 이벤트 · NATS 수신 · 시스템 이벤트 (GOP API 8.0.15 · 브로커 v2.0.12 대조)

목표: 지시문 12항목(G53 · G40 · G41 · G44 · G45 · G46 · G48 · G19 · G52 · G54 · G38 · G43/G47/G49)을 고치고,
각 결함마다 빨강 → 초록 헤드리스 시험을 붙인다. 판정 = `dotnet test Ironwall.Dotnet.Libraries.sln`.

## 진행
- [x] 1 G53 EnumGopCommand 6값 · NatsService 재연결 사건
- [x] 2 G40 억제 202 → EventSuppressedException · 패널 Draft 정리
- [x] 3 G41 삭제 409 사유 · 확인 문구 정정
- [x] 4 G44 억제 범위 '운영' ≥V7_0
- [x] 5 G45 SYNC_MALFUNCTION · SYNC_ACTION_REPORT 자가 필터
- [x] 6 G46 SYNC_OPERATION_EVENT
- [x] 7 G48 탐지 재분류
- [x] 8 G19 함체 밖 문 사유
- [x] 9 G52 시스템 이벤트(DTO · REST · 레일 · NATS)
- [x] 10 G54 ServersChangedMessage
- [x] 11 G38 MIC_PAGING
- [x] 12 G43 · G47 · G49
- [x] 라이브 확인(로컬 8.0.11) · 시험 자료 정리
- [x] 전수 시험 · Dotnet.Monitoring.Solution 빌드(호스트 시험 프로젝트까지 오류 0, 정션 제거 완료)

## 발견
- MSBuild 노드 재사용이 파이프를 붙잡아 `dotnet test … | grep` 이 끝나지 않는다 → `MSBUILDDISABLENODEREUSE=1` + 로그 파일로 받는다.
- Bash 도구에서 python 문자열의 `\n` 이 실제 줄바꿈으로 바뀐 적이 있다 → C# 문자열 이스케이프는 Edit 도구로.
- SuppressionRecurrenceRadioTests 가 한 번 순서 탓으로 흔들림(단독 · 재실행 통과).
- 새 서비스: Events.Ui/Services/EventSyncNatsService.cs(G45·G46), 메시지 Models/EventSyncMessages.cs.

- 빨강 증명: 수정 18곳을 잠시 되돌려 새 시험 46건 실패 확인(목록 red-evidence.txt) → 복구 뒤 전부 통과.
- 라이브(로컬 8.0.11): 시스템 이벤트 목록 · 요약 200(키가 SystemEventDto 와 일치), 억제 창(391) 생성 → 탐지 생성 202 suppressed · schedule_id 일치 → 391 취소 · 하드삭제(남은 시험 스케줄 0).
  acknowledge 는 이미 확인된 것이 없어 1건(id 116 SERVER_STATUS_CHANGED) 확인 → 재확인 409. 되돌릴 수 없음(확인 상태의 유일한 입구).
- 호스트 빌드: $TEMP/hb 에 호스트 사본 + 라이브러리 정션(→ 이 worktree). ★끝나면 정션을 `rmdir` 로 먼저 걷어낸다(재귀 삭제가 worktree 로 넘어가지 않게).

- 하네스 검사 1회차: CameraPopup MapCameraPopupScenarioTests.should_show_broker_toast…(MAP-187) 1건 실패 — 응답 뒤 토스트 갱신을 5초 기다리는 시간 의존 시험.
  같은 클래스 3회 단독 실행: 1회 실패(1분 51초 · 다른 작업 빌드와 겹침) · 2회 통과. 이 작업이 바꾼 파일은 그 경로(브로커 · 카메라 팝업 · MapViewModel)에 없다.
  앞선 전수 실행(본 세션)에서는 334/334 통과. 시험은 고치지 않는다(범위 밖 · 약화 금지).

## 남은 일
- 없음(하네스 재검사 대기)
