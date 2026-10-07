# PLAN — ROI 기준 해상도 자동 채우기 (t-20261007-220502-abf1)

## 목표
ROI 설정 창(Events.Ui/Consoles/Mapping/MappingRoiEditorViewModel)의 **새 ROI 기준 해상도**를
카메라 실제 해상도(ONVIF 메인 스트림 프로파일)로 채운다. GIS 는 카메라를 직접 부르지 않는다 — 카메라 팝업 호스트 경유.
못 읽으면 지금처럼(기존 ROI 해상도 → 없으면 1920×1080). 기존 ROI 의 읽은 값은 바꾸지 않는다.

## 결정
- D1 소스 = **ONVIF 메인 스트림 해상도**(프로파일 중 VideoEncoder 해상도 넓이 최대). 실제 프레임 크기 경로는 하지 않음.
  - 이유: 창을 열 때 바로(그리기 전) 알 수 있고, 분석 기준(메인) 해상도와 같다. 프레임 크기는 오버레이 상자 크기에 따라
    호스트가 서브/메인 프로파일을 바꿔 고르므로(OnvifProfileSelector 상자 덮기 규칙) 비율이 흔들리고 되먹임(상자↔프로파일) 위험.
- D2 경로 = 새 `CameraRequestKind.GetVideoResolution`(=10) → 호스트 `HostCameraServices` → `ICameraPtzProvider.GetVideoResolutionAsync`
  → `IPtzController.GetMainVideoResolutionAsync`(워밍된 ctx.Model.Profiles 캐시만 읽음, 추가 ONVIF 호출 없음).
  응답 `CameraResponse.VideoWidth/VideoHeight`. 새 인터페이스 멤버는 모두 **기본 구현**(가짜 · 목 안 깨지게).
- D3 프로토콜 판 올리지 않음 — 구판 호스트는 새 열거 문자열을 Malformed 로 버리고 GIS 는 시간 초과 → 기본값 폴백(무해).
- D4 우선순위(새 ROI): 카메라 해상도 > 기존 ROI 해상도 > 1920×1080.
- D5 늦게 도착: [새 ROI] 를 이미 눌렀고 점이 0개 · 해상도 칸을 손대지 않았으면 도착한 카메라 해상도로 바꾼다. 그 밖엔 안 바꾼다.
  고른 것 없는 빈 상태의 캔버스 비율(영상 상자)도 카메라 해상도를 따른다.
- D6 GIS 쪽 `IMappingCameraVideo.ReadVideoResolutionAsync` 기본 구현은 "읽을 수 없음" 실패.

## 진행
- [x] 코드 조사(편집기 VM · 호스트 영상 · 요청/응답 경로 · 가짜 카메라 팜 --size)
- [x] Contracts: `CameraRequestKind.GetVideoResolution` · `CameraResponse.VideoWidth/VideoHeight`
- [x] Providers: `OnvifProfileSelector.MainResolution` + `PtzController.GetMainVideoResolutionAsync` · `OnvifPtzProvider` · `ExternalVmsProvider`(NotSupported)
- [x] Host: `HostCameraServices.ExecuteCoreAsync` case
- [x] Events.Ui: `IMappingCameraVideo.ReadVideoResolutionAsync`(기본 구현) · `HostMappingCameraVideo` · `MappingRoiEditorViewModel`(OnInitialize 에서 읽기, BeginNew 우선순위, 늦은 도착 반영)
- [x] 시험: Events.Ui `MappingRoiCameraResolutionTests`(13) · Host.Tests `HostCameraServicesResolutionTests`(4) ·
      CameraPopup.Tests `VideoResolutionProviderTests`(5, 가짜 ONVIF) · `Survival/VideoResolutionHostTests`(실제 호스트 exe) ·
      tools/dummy-cameras `VideoResolutionFarmTests`(ONVIF 팜 1280×1024) — 전부 통과
- [x] CHANGELOG 항목
- [ ] 전체 `dotnet test Ironwall.Dotnet.Libraries.sln` 결과 확인
- [ ] (사람) 실카메라 192.168.202.66 · 실앱 헤디드 확인 — 이 회차에선 못 함(계정 없음 · 데스크톱 시험 금지). 확인법: 프리셋 관리 → [ROI 설정…] → [새 ROI] 해상도 칸이 카메라 메인 해상도인지, 영상 띠가 없는지. 호스트 로그 `request GetVideoResolution ... ok=True`.

## 구현 메모
- 가짜 ONVIF 카메라(CameraPopup.Tests/Support/FakeOnvifCamera)에 `Profiles` 속성을 더했다(기본값 = 예전 1920×1080 · 640×360).
- ROI VM: `_autoSize` = BeginNew 가 채운 칸 문자열 — 늦은 도착 때 칸이 그대로일 때만 바꾼다. `ResolutionTask`(internal)로 시험이 기다린다.

## 발견
- 가짜 카메라 팜 `--size` 는 메인만 바꾼다(서브 기본 640×360). ONVIF GetProfiles 에 메인/서브 해상도 광고.
- 오버레이 960 폭 상자는 "덮는 가장 낮은 해상도" 규칙 때문에 실카메라(cam66 720×480 서브)에선 서브를 고를 수 있다 →
  메인과 서브 비율이 다르면 띠가 남을 수 있음(프로파일 선택 문제 — 이 과제 범위 밖, 남은 위험).

## 남은 일
(위 체크리스트)
