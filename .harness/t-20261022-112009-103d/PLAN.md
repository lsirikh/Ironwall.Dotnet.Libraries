# PLAN — 카메라 팝업 ONVIF 기능 철저 확인 (t-20261022-112009-103d)

## 목표
(사용자 원문, 2026-10-22) "우리 카메라 팝업될때 카메라 온비프 기능들이 제대로 동작하는지 철저하게 확인해줘"
카메라 팝업(호스트 프로세스 Ironwall.CameraPopupHost)이 쓰는 ONVIF 호출 전부를 가짜 카메라로 헤드리스 시험하고, 결함은 증거로 원인을 밝혀 고친다.
범위 밖: GMaps.Ui(3D) — 다른 작업(t-20261022-104834-b193). OnvifSolution 은 서브모듈이라 고치지 않는다(AGENTS.md).

## 관련 코드 위치
- ONVIF 호출 단일 소유: `Ironwall.Dotnet.Libraries.CameraPopup.Providers/Onvif/PtzController.cs` (IPtzController) — 카메라 id 별 CamCtx(모델 · 프로파일 토큰 · GetNode 공간 · 게이트)
- 제공자 래퍼(호출 쪽 제한 시간 FR-26): `.../Onvif/OnvifPtzProvider.cs`(Bounded) · `OnvifVideoProvider.cs`
- 정책 상수: `.../Ptz/PtzMotionPolicy.cs`(PT2S · 유지 재전송 · 이번에 StallThreshold 1초 · BackgroundCallTimeout 2초)
- 호스트 쪽 호출부: `Ironwall.Dotnet.Libraries.CameraPopup.Host/Cameras/HostCameraServices.cs`(제한 5초 이동 · 12초 요청, 준비 안 됐으면 준비 후 실행), `ProviderTileCameraControl.cs`(이벤트 창 타일), `EventWindow/TileViewModel.cs`(BeginAutoMove = 매핑 프리셋 이동 · ReturnHomeOnCloseAsync)
- ONVIF 라이브러리(서브모듈, 읽기만): `Ironwall.Dotnet.Libraries.OnvifSolution` — `Factories/OnvifClientFactory.cs`(시계 동기 1회 · WS-UsernameToken + HTTP Digest · Expect 끄기), `Factories/BindingFactory.cs`(Open 5 · Send 8 · Receive 30초), `Services/OnvifModelBuilder.cs`(번들 → 프로파일 ∥ 노드)
- 가짜 카메라 팜: `tools/dummy-cameras`(sln 밖) — `src/Onvif/OnvifResponder.cs` · `OnvifXml.cs` · `Infra/CameraSpec.cs`(장애 주입), 시험 `tools/dummy-cameras/tests`
- 단위 시험용 가짜 ONVIF: `Ironwall.Dotnet.Libraries.CameraPopup.Tests/Support/FakeOnvifCamera.cs`

## 팝업이 쓰는 ONVIF 호출 전수(조사 결과)
| 기능 | ONVIF 작업 | 부르는 곳 |
|---|---|---|
| 연결 | GetSystemDateAndTime(시계, 계정 없음) → GetCapabilities(All) → GetProfiles ∥ GetConfigurations+GetNode | PtzController.EnsureReady → OnvifService.InitializePtzAsync |
| 영상 주소 | GetStreamUri(RTPUnicast/RTSP, 프로파일 = 그릴 크기 · 서브 선호) | OnvifVideoProvider → ResolveStreamUriAsync |
| PTZ 누름 | ContinuousMove(Timeout PT2S, 1초마다 유지 재전송) · Stop(PanTilt+Zoom) | HandlePtz / 타일 패드 · 휠 줌 |
| 드래그 | RelativeMove(화각 · 일반) · AbsoluteMove · 시간 제한 ContinuousMove · GetStatus | DragMoveAsync |
| 프리셋 | GetPresets · GotoPreset · SetPreset(토큰 없이) · RemovePreset · SetHomePosition · GotoHomePosition | 팝업 프리셋 탭 · 이벤트 창 자동 이동/복귀 |
| 이동 상태 | GetStatus(MoveStatus) | GetPtzMoveStatus 요청(이동 중 표시) |
| 영상 옵션 | GetImagingSettings · SetImagingSettings(read-modify-write) · GetMoveOptions · Move(ContinuousFocus) · Stop | 옵션 탭(주야간 · 자동 초점 · 수동 초점) |
| 해상도 | (추가 호출 없음 — 프로파일 캐시) | GetVideoResolution 요청(ROI) |
스냅샷 URI(GetSnapshotUri) · 절대/상대 줌 단추는 팝업이 쓰지 않는다(RelativeZoom · AbsoluteMove 공개 API 는 있으나 호스트 호출 0건).

## 진행
- [x] 팝업이 쓰는 ONVIF 호출 전수 목록(위 표)
- [x] 기존 dummy-cameras ONVIF 시험 기준선: 61/61 통과(수정 전)
- [x] 가짜 카메라 팜 보강: 이미징 서비스 · 시계 차이 장애 · 첫 프로파일 PTZ 없음
- [x] 시나리오 시험 `tools/dummy-cameras/tests/PopupOnvifScenarioTests.cs` 28건 · `EventWindowFarmTests` +3건 · .sln `CameraPopup.Tests/Unit/OnvifRecoveryTests.cs` 4건
- [x] 결함 4건 수정(아래) + 고치기 전 실패 → 고친 뒤 통과 증명
- [x] 알려진 함정 회귀: Expect:100-continue(8방향 시험이 이동 · 정지 각 500ms 미만 단정, 기존 expect100 단정 통과) · 정지 우선(정지 경합 시험) · 호스트 Background 굶주림(Host.Tests DispatcherStarvationTests — .sln) · LibVLC 다수 플레이어(옵트인 부하 60타일 통과) · GIS 생존(CameraPopup.Tests Survival — .sln)
- [x] .sln 시험 · 관련 시험 프로젝트 직접 실행
- [x] 헤디드 확인 목록(아래)

## 발견 · 고친 결함(증거)
1. **틀린 비밀번호 · 잠긴 계정이 "연결됨"으로 굳음** — 증거 ① 가짜 카메라 요청 기록: cam5 틀린 비밀번호에서 GetCapabilities ok(PRE_AUTH) · GetProfiles fault:NotAuthorized 뒤, 맞는 비밀번호 준비에서 새 GetSystemDateAndTime/GetProfiles 없이 GetStatus · GetPresets 가 fault:NoProfile(빈 토큰) ② 시험: 틀린 계정 PrepareAsync → Connected=true, 고친 뒤 GetPresets=null. 수정: `InitCoreAsync` 에서 프로파일 0개면 캐시 안 함.
2. **무응답 카메라의 버려진 호출이 줄을 8초 막음** — 증거 ① 재연결 6552 ms · 복구 뒤 첫 명령 7004 ms(WCF SendTimeout 8초와 일치) ② 클라이언트 로그 "요청 채널이 … 00:00:08 이후에 시간 초과"(준비 직후 배경 위치 읽기가 게이트를 쥠). 수정: 초기화 WaitAsync(포기 즉시 게이트 해제) · `CallAsync`(포기 + 1초 넘게 답 없음 → 연결 끊기) · `BackgroundCallAsync`(배경 호출 2초) · `LiveModelAsync`(다음 명령이 게이트 안에서 다시 붙음). 첫 구현에서 "끊기가 호출 쪽 반환 직후에 일어나 다음 명령이 끊긴 모델을 만나는" 경합을 .sln 시험이 잡아 `LiveModelAsync` 로 보완.
3. **접속 정보가 바뀌어도 옛 연결을 씀** — 증거: 같은 id 를 고정 카메라 → PTZ 카메라 주소로 바꿔도 PtzCapable=false · 영상 주소가 옛 카메라(`/old`). 수정: `CtxFor`(주소 · 포트 · 계정 키).
4. **첫 프로파일에 PTZ 구성이 없으면 PTZ 전부 실패** — 증거 ① ONVIF: PTZ 구성 없는 프로파일엔 ter:NoPTZProfile ② 같은 파일 `ReadDefaultSpeeds` 는 이미 PTZ 구성 프로파일로 폴백하는데 명령 경로는 첫 프로파일 고정 ③ 시험: 이동 안 됨 · 명령 본문이 P0. 수정: PTZ 구성이 붙은 프로파일 우선.

## 검증 기록(명령 · 결과)
- 기준선(수정 전): `dotnet test tools/dummy-cameras/tests/DummyCameras.Tests.csproj --filter "…OnvifClientTests|OnvifAuthTests|PresetTokenStyleTests|PtzSimulatorTests|DragPtzHostTests|OnvifDiscoveryFarmTests"` → 61/61 통과
- 새 시나리오 첫 실행(수정 전 코드): 25건 중 4 실패(결함 1 · 2)
- 고치기 전 증명(PtzController 를 HEAD 판으로 바꿔 실행, 끝나고 복원 · cmp 확인): dummy `PopupOnvifScenarioTests` 6/28 실패, .sln `OnvifRecoveryTests` 4/4 실패
- 고친 뒤: dummy `PopupOnvifScenarioTests|OnvifClientTests|OnvifAuthTests|DragPtzHostTests` 46/46 · dummy 전체 115 통과 · 2 건너뜀(옵트인 부하) · `IRONWALL_CAMHOST_LOAD=1` EventWindowLoadTests 통과(60/60 재생 3075 ms · CPU 평균 6.9% · 최대 18.7% · 재시작 0) · `CameraPopup.Tests --filter Unit` 299/299
- .sln: `dotnet test Ironwall.Dotnet.Libraries.sln` → (아래 "최종 결과"에 기록)

## 헤디드 확인 목록(사용자 PC 가 빌 때 대화 세션에서 — 가짜 카메라 팜 `DummyCameras run` + 실앱)
1. 지도에서 PTZ 카메라 더블클릭 → 팝업 영상 재생 · PTZ 패드 8방향 누름 동안 움직이고 떼면 바로 멈춤(1초 끊김 없음) · 휠 줌 인/아웃.
2. 프리셋 탭: 목록(P1~P5) → 하나 눌러 이동 · "현재 위치 저장"(이름 입력) → 목록에 생김 → 삭제 → 사라짐 · 홈 저장 → 다른 데로 → 홈 이동.
3. 옵션 탭: 주야간(주간/야간/자동) 바꾸면 가짜 카메라 `/status` 의 imaging 상태가 바뀜 · 자동 초점 끄기 → 수동 초점 원/근 누르는 동안 이동 · 떼면 정지.
4. 고정 카메라 팝업: PTZ 단추가 꺼지고 이유 알림, 옵션 탭은 켜짐.
5. 팝업 연 채 `POST /cam/N/fault?noreply=1` → 누름 · 프리셋이 몇 초 안에 실패 알림(GIS 멈춤 없음) → `noreply=0` → 다시 누르면 1초 안에 동작.
6. 장비 설정에서 카메라 비밀번호를 틀리게 → 팝업 다시 열기: "연결 안 됨"(PTZ 꺼짐) → 바르게 고치기 → 다시 열면 PTZ 됨(호스트 재시작 없이).
7. 탐지 이벤트(매핑 프리셋 3) → 이벤트 창 타일이 P3 로 이동 · "P3 로 이동 중 · N초" → 창 닫기(복귀 켬) → 복귀 프리셋/홈으로.
8. 같은 카메라 이벤트 두 개 연속 → 두 창 모두 재생 · 마지막 이벤트 프리셋으로 · 한 창 닫아도 다른 창 계속 재생.
9. `fault?skew=10800`(카메라 시계 +3시간) → 팝업 PTZ · 프리셋 정상.
10. 호스트 강제 종료(작업 관리자) → GIS 살아 있음 · 팝업 다시 열면 호스트 재시작 후 재생.

## 남은 한계
- OnvifSolution(서브모듈)은 프리셋 이동 · 저장 · 삭제 실패를 원인(거부 · 시간 초과 · Home 없음) 구분 없이 false 로 준다 → 팝업 알림도 한 가지. 서브모듈에서 고칠 일.
- `SoapSecurityHeader.PasswordDigest` 가 매개변수 대신 필드 비밀번호를 쓴다(지금은 같은 값이라 무해) — 서브모듈.
- 실제 카메라 · 원격 운영 서버에는 쓰지 않았다(지시대로). 실기 확인은 헤디드 목록으로.
- 응답이 1초 넘게 걸리는 아주 느린 링크에서 부른 쪽(5 · 12초)이 먼저 포기하면 연결을 다시 만든다(초기화 왕복 3번) — 정상 링크 영향 없음.

## 최종 결과
- `dotnet test Ironwall.Dotnet.Libraries.sln` → 종료 코드 0, 실패 0(Messages 504 · SystemResources 22 · Monitoring.Models 250 · Watchdog 15 · Accounts.Api 261 · Reports.Api 3 · Sso 106 · CameraPopup.Host.Tests 197 · Reports.Ui 377 · Utils.Tests 880(+6 건너뜀, 기존) · CameraPopup.Tests 338)
- `dotnet test tools/dummy-cameras/tests/DummyCameras.Tests.csproj` → 115 통과 · 2 건너뜀(옵트인 부하 2 — 그중 EventWindowLoadTests 는 `IRONWALL_CAMHOST_LOAD=1` 로 따로 돌려 통과) · 실패 0
- 정리: 시험 프로세스 남은 것 없음(확인) · 워크트리에 복사했던 `tools/dummy-cameras/bin/mediamtx`(gitignore, 메인 체크아웃에서 복사) 삭제 · `%TEMP%\dummycam-tests` 삭제. 이벤트 창 · 라이브 시험을 다시 돌리려면 `DummyCameras fetch-mediamtx` 또는 메인 체크아웃의 `tools/dummy-cameras/bin/mediamtx/v1.21.1` 을 복사.
