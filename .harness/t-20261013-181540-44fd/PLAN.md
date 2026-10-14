# PLAN — 카메라 팝업 호스트가 들어오는 포트를 열지 않게(방화벽 창 제거)

## 목표
(사용자) "관리자 권한으로 실행할 수 있게 해줘 자꾸 권한 창이 뜨는데" → 뜨는 창은 Windows 방화벽 창.
호스트(Ironwall.CameraPopupHost.exe)가 0.0.0.0/:: 에 포트를 열지 않게 하고, 회귀 시험(FW-0nn)으로 지킨다.

## 진행
- [x] 1. 실측 시험 작성(호스트 PID 의 비루프백 소켓을 netstat -ano 로 기록) + 측정기 양성 대조(FW-000)
- [x] 2. 원인 확정(아래 '발견')
- [x] 3. 수정 — `Producers/UdpBindGuard.cs`(live555 플러그인 IAT 의 ws2_32!bind 가로채기) + 설정 `AllowRtspUdpFallback`(기본 끔)
- [x] 4. 회귀 시험 FW-000~003 통과(Survival/FirewallPortHostTests.cs) · Host.Tests 단위 시험(OpensInboundPort · ToLoopback · 인자 왕복)
- [x] 5. CameraPopup.Host.Tests 172/172 · CameraPopup.Tests 290/290 통과(2026-10-14). 전체 sln 실행은 세션 중단으로 못 끝냄 → 하네스 검사가 돌린다

## 결정
- 소켓 열거 = 시험에서 `netstat -ano` 파싱(TCP 대기는 상대 주소 0.0.0.0:0/[::]:0 로 — 상태 글자는 언어마다 다름). 200 ms 간격 표본 합집합.
- 환경(mediamtx.exe · ffmpeg.exe) 없으면 FW-002/003 은 출력만 남기고 return(xunit 2.9 에 동적 Skip 없음, 패키지 추가 안 함).
- mediamtx.exe 찾기: `IRONWALL_MEDIAMTX` → 상위 폴더들의 `tools\dummy-cameras\bin\mediamtx\*\mediamtx.exe`(worktree 상위의 메인 저장소에 있음). ffmpeg = PATH.
  게시는 RTMP(TCP)로 — UDP 전용 RTSP 서버(FW-003)에서도 게시자가 UDP 를 열지 않게. `moq: false`(기본 켜짐이라 0.0.0.0 을 열 수 있음).
- 고침 방식: LibVLC 옵션으로는 못 막는다(live555 플러그인 내부 동작) → `LibVlcWaitPatch` 와 같은 결의 네이티브 패치.
  live555 플러그인을 LibVLC 보다 먼저 LoadLibrary 해 IAT 의 bind 칸만 바꾼다(다른 모듈 · ws2_32 자체는 안 건드림).
  - 비루프백 UDP bind → 같은 포트의 루프백으로 바꿔 묶는다.
  - 15947(live555 ourIPAddress 멀티캐스트 시험 포트)만 WSAEACCES 거절 → 호스트 이름 조회로 넘어감(지연 없음: 첫 프레임 781 ms).
  - 처음엔 "전부 거절"로 했으나 TCP 재생까지 깨짐(그룹소켓 생성 실패), "임시 포트만 이동 + 나머지 거절"은 RTCP(짝+1 고정 포트)
    거절 때문에 live555 가 포트 쌍을 찾느라 소켓 수천 개를 만듦 → 지금 규칙으로 확정.
  - TCP bind(연결 전 0.0.0.0:0, 대기 아님)는 그대로.
- 폴백 설정: `CameraPopupHostOptions.AllowRtspUdpFallback`(기본 false) → `HostLaunchArguments --allow-rtsp-udp` → 호스트가 가드를 걸지 않음.
  켜면 방화벽 창이 다시 뜰 수 있음(문서화). GIS appsettings 연결은 호스트 저장소 몫(이번 범위 밖).

## 발견(실측, 2026-10-13)
- FW-001(PNG 파일 · ONVIF PreparePtz/GetPresets/GetVideoResolution · 닿지 않는 RTSP 재시도): 호스트 소켓 0개(고치기 전에도).
- FW-002(MediaMTX RTSP 직접 큰/작은 상자 · ONVIF 가 알려 준 RTSP): netstat 표본 0개(고치기 전에도) — 그러나 bind 를 가로채 보니
  **스트림마다 live555 가 UDP `0.0.0.0:0`(RTP/RTCP 그룹소켓) 과 `0.0.0.0:15947`(ourIPAddress 멀티캐스트 시험)에 잠깐 bind**.
  ms 단위로 닫혀 표본엔 안 잡히지만 이것이 방화벽 창의 원인 → exe 경로가 새로울 때마다(하네스 작업 폴더 · 시험 bin) 창.
- FW-003(TCP 를 461 로 거절하는 UDP 전용 서버): 고치기 전 `UDP 0.0.0.0:54010` · `:54011` 을 재생 내내 열고 재생됨(live555 의 TCP→UDP 전환).
  고친 뒤: `UDP 127.0.0.1:65156/65157` 만(루프백 서버라 재생됨 — 밖의 카메라라면 패킷이 안 와서 실패).
- 호스트 코드 · OnvifSolution 에는 리스너 없음(과제 사실과 일치). `:rtsp-tcp` 는 T-00 부터 있었다.
- GIS 본체(Dotnet.Monitoring.Solution, 정적 확인만): csproj 가 LibVLC · Streaming 을 참조하지 않음, 운영 코드에 TcpListener/HttpListener/UdpClient 없음
  (시험 코드에만). 창을 띄우는 GIS 실행 실측은 하지 않음(창 띄우는 시험 금지). 방화벽 규칙의 GIS exe 항목은 LibVLC 를 품던 옛 판(T-07 전) 흔적으로 추정 — 확증 없음.
- DummyCameras: MiniHttpServer 는 루프백. 기본 CLI 의 WS-Discovery 응답기는 `0.0.0.0:3702` + 멀티캐스트 가입(설계상 — 같은 PC 의 ONVIF 검색이
  찾게). 루프백 바인드로는 멀티캐스트 Probe 를 못 받는다 → 시험(unicast 모드)은 이미 127.0.0.1:0. 시험 도구라 그대로 둠.

## 설치기 보고(호스트 저장소 Installer 몫 — 보고만)
- 이번 수정 뒤 호스트는 들어오는 포트를 열지 않으므로 **설치기가 방화벽 규칙을 넣을 필요 없음**(넣으면 오히려 불필요한 개방).
- 단, `AllowRtspUdpFallback` 를 켜는 현장(UDP 전용 카메라)은 호스트가 UDP 를 0.0.0.0 에 연다 → 그 현장만 설치 때
  `netsh advfirewall firewall add rule name="Ironwall CameraPopupHost" dir=in action=allow program="<설치경로>\...\Ironwall.CameraPopupHost.exe" protocol=UDP`
  같은 프로그램 규칙(UDP)을 관리자 권한으로 미리 넣어야 창이 안 뜬다. 이미 '취소' 로 차단 규칙이 생긴 PC 는 그 규칙을 지워야 한다.
- GIS exe 자체는 이번 확인 범위에서 들어오는 포트 근거 없음.

## 남은 일
- 전체 솔루션 시험 결과 확인(하네스 검사).
