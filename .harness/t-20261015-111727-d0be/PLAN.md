# PLAN — 이슈 #7 G7-3 억제 3단 중 빠진 두 단(해제 타이머 · 캐시 TTL)

## 목표
- 해제 타이머: 받은 활성 억제 일정의 `occurrence_end`(단발은 `window_end`) 시각에 그 일정을 활성 목록에서 내리고 `/active` 를 다시 읽는다.
- 캐시 TTL: 마지막 성공 폴링이 폴링 간격 × 3(90초)보다 오래되면 활성 목록을 '모름' 으로 표시한다(오래된 억제를 계속 보이지 않는다).
- `/active` 30초 폴링은 권위로 그대로 둔다.

## 진행
- [x] `SuppressionPollThrottle` 에 순수 함수 `ReleaseAt` · `WithoutReleased` · `NextRelease` 추가
- [x] `SuppressionActiveMonitor` 에 해제 타이머(`_release` DispatcherTimer) — 성공 반영마다 가장 이른 해제 시각으로 다시 건다. 도달하면 지난 일정 제거 → `ActiveChanged` → `RequestImmediatePoll("release-timer")`
- [x] TTL: `IsStale` 이면 `Active` 는 빈 목록('모름'), 30초 틱마다 stale 전이를 확인해 통지(폴링이 못 도는 경우 포함)
- [x] 억제 패널 문구: stale 이면 "⚠ 억제 상태 모름 — 마지막 확인 N분 전"(배너는 `HasActiveBanner` 로 계속 뜬다), 개수 문구에서 stale 병기 제거
- [x] 단위 시험 7건 추가(`SuppressionPollThrottleTests`)

## 결정
- SYNC 수신 시 `GET /{id}` 를 따로 부르지 않는다: SYNC 는 이미 즉시 폴링을 앞당기고, 진행 중인 일정은 `/active` 응답에 `occurrence_end` 가 실려 오므로 같은 경로로 해제 타이머가 걸린다(코드 최소).
- 반복(weekly) 일정의 `window_end` 는 유효기간 끝이라 해제 시각으로 쓰지 않는다. `occurrence_end` 도 `window_end` 도 없으면(무제한) 타이머 없음.
- 응답 반영 시에도 이미 끝난 일정은 즉시 뺀다(서버 응답 지연·시계 차 보정, 다음 폴링이 바로잡음).
- DispatcherTimer 간격은 최대 1일로 자른다(int ms 상한), 도달 시 다시 계산.
- 기존 주석의 "stale 이어도 목록을 버리지 않는다(병기)" 방침은 이번 명세('모름' 표시)로 바꿨다. 배너는 stale 이면 계속 떠서 0건으로 조용히 보이지 않는다.

## 검증
- `dotnet test Ironwall.Dotnet.Libraries.Events.Ui\Ironwall.Dotnet.Libraries.Events.Ui.csproj --filter "...SuppressionPollThrottleTests|SuppressionPanel|EventSuppressionSync"` → 73/73 통과
- 전체 `dotnet test Ironwall.Dotnet.Libraries.sln` 은 하네스가 실행.

## 남은 일
- 없음(실서버 실기 확인은 하지 않음).
