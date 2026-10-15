# PLAN — 설치판 GIS 2.10.6 '응답 없음'(Application Hang) 원인 규명

## 목표
10-15 15:49:51 · 17:32:33 두 번 Application Hang → 감시 프로그램이 재시작. UI 스레드가 API 를 기다리며 멈추는 경로를 증거로 찾고, 확정되면 고친다(헤드리스 빨강→초록). 확정 못 하면 근거 · 후보 · 필요한 추가 증거를 적는다.

## 결론(이번 회차)
**원인 확정 못 함 → 동작 수정 없음.** 대신 다음에 같은 일이 나면 원인을 잡을 수 있게 **UI 스레드 정지 감지 기록**(진단 전용)을 더했다.

## 진행
- [x] ApiService.cs 읽음 — 모든 `await _client.XxxAsync(...)` 에 `ConfigureAwait(false)` 없음(BuildUrlAsync 만 있음).
- [x] 감시 프로그램 판정 기준 확인 — 하트비트=UI 스레드 DispatcherTimer(Normal), FreezeThresholdMs 30s × 연속 3회 확인 뒤 kill+재시작(WatchdogEngine.cs:108-124). 기록 위치 `%ProgramData%\Ironwall\Watchdog\watchdog.log`.
- [x] 전수 조사(하위 에이전트, 이 저장소 운영 코드 전체): UI 스레드 동기 대기 · lock+Dispatcher.Invoke 교착 · UI 타이머 API 호출 → **영구 정지를 만드는 경로 없음**(아래 '후보').
- [x] 진단 기록 추가: `Ironwall.Dotnet.Libraries.Base\Services\Startup\UiStallWatch.cs`(새 공개 형식) + `ConnectionWatchdog.Start` 에서 하트비트 옆에 기동 · Dispose/StopWatching 에서 정지.
- [x] 시험: `Ironwall.Dotnet.Libraries.Accounts.Api\Tests\UiStallWatchTests.cs` 2건(멈춘 동안 ERROR · 회복 ERROR / 한가할 땐 기록 없음) 통과.
- [ ] (사람) 설치판에 이 판을 넣고 다음 '응답 없음' 때 아래 '필요한 추가 증거' 수집.

## 이번 세션에서 막힌 것
- 설치판 로그 `C:\PidsMonitoringSystem\Logs\log-2026-10-15.txt` · 감시 로그 `%ProgramData%\Ironwall\Watchdog\watchdog.log` · 이벤트 로그 읽기 = 작업 폴더 밖이라 권한 거부. 목표에 적힌 로그 인용만 근거로 썼다.
- 앱 본체(Dotnet.Monitoring.Solution) 코드는 이 worktree 밖 — 조사 범위는 라이브러리뿐이다. 앱 쪽 ViewModel/부팅 코드에 동기 대기가 있을 가능성은 남는다.

## 발견(근거)
1. **17:28:09 ERROR 가 스레드 [1](UI)에서 찍혔다** = `await _client.GetAsync` 의 이어짐(ConfigureAwait 없음)이 UI 컨텍스트로 돌아와 **실행됐다** → 그 시각 UI 스레드는 펌프 중이었다. 그 GET 을 누가 `.Result`/`.Wait()` 로 막고 있었다면 이어짐이 영원히 못 돌아 이 줄이 찍히지 않는다. **즉 이 GET 이 교착을 만든 것은 아니다.**
2. "disposed SslStream" = HttpClient.Timeout(10s) 취소가 연결을 닫을 때 안쪽 예외로 붙는 꼴. 서버(로컬 8.0.11)가 10초 안에 응답 못 한 **증상**이지 폐기 경합의 원인이 아니다. ApiService 는 HttpClient 하나를 서비스 수명 내내 쓰고 실행 중 폐기하지 않는다(Initialize 에서 한 번 생성).
3. 17:28:09 → 17:32:33 사이 4분 넘게 기록 없음. INFO 는 버퍼링이라 빠졌을 수 있어 '그 사이 멈춰 있었다'는 증명은 안 된다. 감시 프로그램은 30초 × 3회 stale 이면 kill → Windows 가 Application Hang 으로 남긴다(응답 없는 창 상태에서 종료).
4. 15:49 는 PC 전체가 느려졌던 시각 — 하트비트 Normal 우선순위 타이머도 90초 넘게 못 돌 정도의 정체였을 수 있다(진짜 교착이 아니라 과부하 → 감시 프로그램의 강제 종료).

## 후보(라이브러리 안, 확정 아님)
- `ApiService` 7개 동사 메서드의 `ConfigureAwait(false)` 누락 — 누가 UI 스레드에서 동기 대기하면 영구 교착. **라이브러리에선 그런 호출부를 못 찾았다**(발견 1 이 반증). 앱 본체에 있으면 원인 후보.
- `ImprovedRtspStreamingService.Dispose()` 의 `.Wait(5s)` · `.Wait(15s)` — UI 스레드에서 불리면 최대 ~20초 정지(현재는 컨테이너 폐기가 배경 스레드라 해당 없음).
- `MapViewModel.cs` 의 `Dispatcher.Invoke(async () => …)`(선 저장) — 정지는 아니고 끝나기 전에 돌아오는 정확성 결함.
- 과부하로 인한 UI 정체 + 감시 프로그램 오판 kill(발견 4).

## 필요한 추가 증거
- 다음 발생 때 앱 로그의 `[UiStall]` ERROR 줄: 정지 시작 시각 · 길이(5 · 10 · 20 · 40 · 80초 …) · 스레드풀 상태 · 회복 여부. 회복 줄이 있으면 '긴 정체', 없이 kill 이면 '교착'.
- `%ProgramData%\Ironwall\Watchdog\watchdog.log` 의 10-15 15:4x · 17:2x~17:3x 줄('stale 관찰' · '프리즈 확정' 시각) — 감시 프로그램이 죽였는지 확인.
- 교착이면 덤프: 다음 정지 때 `procdump -h <pid>` 또는 작업 관리자 '덤프 파일 만들기' → UI 스레드 [1] 콜스택.

## 결정
- 원인 미확정이라 ApiService/ConfigureAwait 등 동작 변경은 하지 않았다(목표 3항).
- 진단 기록은 ERROR 수준으로 남긴다 — INFO 는 버퍼링 appender 라 강제 종료 때 빠진다(목표에 적힌 관찰).
- 감지 스레드는 스레드풀이 아닌 전용 배경 스레드 — 스레드풀이 굶어도 감지가 멈추지 않게. 문턱 5초, 이후 2배씩(감시 프로그램 kill 전 여러 줄).
- 시험 위치는 Accounts.Api 프로젝트의 Tests(Base 를 참조하는 기존 시험 프로젝트 — Base 전용 시험 프로젝트가 없다).
