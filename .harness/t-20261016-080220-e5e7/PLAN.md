# PLAN — RTSP 스트리밍 열기 최적화 (t-20261016-080220-e5e7)

## 목표
(사용자 2026-10-16) RTSP · RTP · NAL · 디코드 · 디스패처 · 렌더 계측(a284ba7e)으로 찾은 병목부터 최적화한다.
앞 측정의 판정: 재생 중 단계(RTP~렌더)는 60 스트림에서도 여유, 병목은 **열기**(① 여는 줄 대기 ② 첫 프레임 1~1.8초).

## 진행
- [x] 기준 재측정(오늘 같은 PC · 같은 시험) — `trace-base.md`
- [x] 실험 4가지(임시 환경변수 스위치로 호스트 재빌드 없이 비교, 스위치는 실험 뒤 코드에서 뺐다)
  - `es` 연결 줄 자리를 첫 트랙(ES) 에서 돌려주기 → **채택**
  - `es-c100` + network/live-caching 100 → **버림**(표시 fps 하락)
  - `es-clk` + `:clock-jitter=0 :clock-synchro=0` → **버림**(표시 fps 절반)
- [x] 채택분 구현
  - `LibVlcFrameProducer`: ESAdded 처리기를 늘 붙이고, 첫 ES 에서 `ReleaseLease()` (계측 꺼져 있어도)
  - 수신 버퍼 설정화: `CameraPopupHostOptions.StreamNetworkCachingMs`(기본 300) → `HostLaunchArguments.NetworkCachingMs`
    (`--network-caching-ms`, 0 이상) → `HostRuntime` 에서 `DecodeProfile.NetworkCachingMs` 로. 기본값 300 = 측정으로 정한 값.
  - 단위 시험 `StreamLoadPolicyTests.should_use_measured_300ms_caching_by_default_and_round_trip_when_caching_is_configured`
- [x] 후 측정 — `trace-after.md`
- [x] 회귀: 60 타일 동시 열기(`EventWindowLoadTests`, `load60.runsettings`) — 60/60 재생 · 재시작 0 · CPU 평균 4.5% · 모두 재생까지 4076 ms (`load60-after.json`)
- [x] 시험: CameraPopup.Host.Tests 197/197 · CameraPopup.Tests(RealCamera 제외) 331/331 · DummyCameras.Tests 78 통과 / 8 건너뜀(opt-in 부하 · MediaMTX 필요) / 0 실패

## 전/후 표 (StreamTraceLoadTests · Release 호스트 · 가짜 카메라 960x540 · 12fps · GOP 2초 · 헤드리스 · 30초 평균)
기준(전) = 오늘 08:10 재측정(`trace-base.md`). 괄호 안은 과제 문서의 07:57 측정.

| 스트림 | | CPU% | 협상 ms | 첫 프레임 ms | 줄 대기 ms | 깨짐 | 끊김 | 디코드 fps | 표시 fps | 버린 그림/5s | UI 점유 % |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 전 | 0.07 (0.1) | 9 | 1413 (989) | 0 | 0 | 0 | 23.93 | 23.33 | 0 | 0.1 |
| 1 | 후 | 0.12 | 30 | 887 | 0 | 0 | 0 | 23.93 | 22.6 | 0 | 0.1 |
| 4 | 전 | 0.23 | 5 | 768 (567) | 0 | 0 | 0 | 96.07 | 92.57 | 0 | 0.3 |
| 4 | 후 | 0.32 | 9 | 508 | 0 | 0 | 0 | 96 | 91.7 | 0 | 0.33 |
| 8 | 전 | 0.47 | 8 | 706 (710) | 0 | 0 | 0 | 192.07 | 183.33 | 0 | 0.43 |
| 8 | 후 | 0.6 | 11 | 587 | 0 | 0 | 0 | 191.85 | 180.28 | 0 | 0.68 |
| 16 | 전 | 1.22 (2.17) | 6 | 1429 (1421) | 420 (452) | 0 | 0 | 383.6 | 371.1 | 0 | 0.8 |
| 16 | 후 | 1.6 | 10 | 641 | 7 | 0 | 0 | 383.38 | 366.38 | 0 | 1.15 |
| 60 | 전 | 6.03 (7.52) | 6 | 1840 (1848) | **5540** (5586) | 0 | 0 | 1442.12 | 1401.95 | 0 | 3.35 |
| 60 | 후 | 7.77 | 6 | **593** | **47** | 0 | 0 | 1437.63 | 1317 | 0 | 4.87 |

판정
- 60 스트림: 줄 대기 5540 → 47 ms, 첫 프레임 1840 → 593 ms. 16 스트림: 줄 대기 420 → 7, 첫 프레임 1429 → 641.
- 깨짐 · 끊김 · 버린 그림 0 그대로. CPU · UI 점유 · 표시 fps 차이는 측정 흔들림 범위로 본다 — 근거: ① 측정 구간은 모두 재생된 뒤 6초부터라
  바뀐 코드(열기 때 자리 반납)는 그 구간에 작동하지 않는다 ② 같은 코드의 기준 두 번(07:57 · 08:10)도 60 스트림 CPU 7.52 ↔ 6.03, 표시 fps 1370 ↔ 1402 로 흔들렸다
  ③ 같은 변경의 실험 회차(`trace-es.md`)는 CPU 6.75 · 표시 1373.88.
- 1 스트림 첫 프레임은 호스트 기동 직후 첫 스트림이라 LibVLC 코어 초기화 + 키프레임 위치(GOP 2초)에 따라 0.9~2.2초로 흔들린다(줄과 무관).

## 실험 표 (버린 것 — 근거)
| 회차 | 60 스트림 첫 프레임 ms | 60 줄 대기 ms | 60 표시 fps | 4 스트림 표시 fps | 판정 |
|---|---|---|---|---|---|
| es (`trace-es.md`) | 831 | 47 | 1373.88 | 90.7 | 채택 |
| es + caching 100 (`trace-es-c100.md`) | 834 | 50 | **858.1** | 91.9 | 버림 — 첫 프레임 이득 없음, 늦은 그림 버림으로 표시 fps 하락 |
| es + clock-jitter=0 · clock-synchro=0 (`trace-es-clk.md`) | 792 | 62 | 1332.3 | **50.92** | 버림 — 1~16 스트림 표시 fps 절반 |

## 발견
- 첫 프레임 시간(ES 이후)의 대부분은 **첫 키프레임 대기**(가짜 카메라 GOP = fps×2 = 2초, `FfmpegArgs.cs:81`)와 300ms 버퍼다.
  버퍼를 줄여도 첫 프레임은 줄지 않았다(키프레임은 카메라 쪽 설정) → 클라이언트 옵션으로 줄일 몫은 거의 없다. 실카메라는 GOP 설정을 짧게 하는 것이 효과적.
- 60 스트림에서 첫 프레임이 1.8초까지 늘던 것은 줄 대기 · 동시 열기 경합 때문이었다 — 자리를 ES 에서 넘기자 0.6초.
- "보이는 타일 · 이벤트 창 먼저" 순서 바꾸기는 하지 않았다: 줄 대기가 60 스트림에서도 47 ms 라 순서로 얻을 것이 없다(우선 줄 — 오버레이 — 은 그대로).
- 동시 열기 수(8)는 그대로다(LibVLC WaitOnAddress 스핀 대책 유지). 자리 반납만 앞당겼고 CPU 폭주 없음(60 스트림 CPU 7.77% · 60 타일 동시 열기 시험 평균 4.5%).
- 하네스 셸: bash 변수 확장 · 긴(1015바이트 초과) 명령 · Get-Process 는 자동 거부. 측정은 PowerShell 에서 dotnet test 한 줄씩.

## 측정 방법(재현)
1. `dotnet build Ironwall.Dotnet.Libraries.CameraPopup.Host\Ironwall.Dotnet.Libraries.CameraPopup.Host.csproj -c Release`
2. `dotnet test tools\dummy-cameras\tests\DummyCameras.Tests.csproj --settings .harness\t-20261016-080220-e5e7\after.runsettings --filter "FullyQualifiedName~StreamTraceLoadTests"`
   (runsettings 가 IRONWALL_STREAM_TRACE_LOAD · IRONWALL_STREAM_TRACE_MEDIAMTX(원본 체크아웃의 mediamtx.exe) · IRONWALL_CAMHOST_EXE · IRONWALL_STREAM_TRACE_OUT 를 넘긴다) — 표는 `trace-after.md`.
3. 60 타일 회귀: `--settings .harness\t-20261016-080220-e5e7\load60.runsettings --filter "FullyQualifiedName~EventWindowLoadTests"` (DUMMYCAM_ROOT 로 원본 체크아웃의 MediaMTX 사용).
- `es.runsettings` · `es-c100.runsettings` · `es-c100-clk.runsettings` 는 실험용 임시 환경변수(IRONWALL_EXP_*)를 넘기던 파일이다 — 그 스위치는 코드에서 뺐으므로 지금 돌리면 `after` 와 같다(기록용).

## 남은 일
- 없음. (시험 임시 폴더 `%TEMP%\dummycam-tests\*` 는 남아 있다 — 하네스 셸에서 프로세스 · 폴더 정리 명령이 막혀 확인 못 함.)
