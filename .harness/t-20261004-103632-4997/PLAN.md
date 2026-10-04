# t-20261004-103632-4997 — 프리셋 · ROI 창에 실시간 영상(RTSP)

## 목표
사용자 질문 "프리셋이나 ROI 쪽에 RTSP 영상 나오는거 맞지?" → 확인 결과 **없다**. 두 창에 카메라 실시간 영상을 보이게 한다.
★최우선: 영상에 무슨 일이 생겨도 GIS 는 안 죽는다 → GIS 는 LibVLC 를 부르지 않는다.

## 결정
- D1. 영상 경로 = 지도 카메라 팝업과 **같은 오버레이 경로**: `ICameraPopupHost.OpenOverlay` (호스트 프로세스가 RTSP/ONVIF 디코딩 →
  GIS 소유 공유 메모리 프레임) + `HostedVideoView`(WriteableBitmap 복사만). 호스트 프로토콜 · 호스트 코드 변경 없음.
  (이전 ROI 계획이 뺀 "스냅숏 요청" 은 쓰지 않는다 — 실시간 프레임이 이미 있다.)
- D2. 새 창구 `IMappingCameraVideo`(Events.Ui/Consoles/Mapping) — 기존 `IMappingCameraPresetReader` 는 넓히지 않는다(인터페이스 확장 함정).
  구현 `HostMappingCameraVideo`: 제공자는 이벤트 창과 같은 빌더(`EventWindowPlanning.BuildProvider`), 절대 던지지 않음.
  GotoPreset(카메라를 프리셋 자리로)도 이 창구에 둔다 — 호스트 `CameraRequestKind.GotoPreset`(기존 종류).
- D3. 화면 상태 VM `MappingCameraVideoViewModel`(헤드리스 시험 가능): 연결 중 · 재생 · 멈춤 · 실패 · 쓸 수 없음 문구, [다시 시도], 창 닫힘 때 스트림 닫기.
- D4. ROI 창: 캔버스(`MappingRoiCanvas`)가 그림 칸(기준 해상도 비율)에 영상 프레임을 바탕으로 그리고 그 위에 격자 · 다각형.
  요청 크기는 ROI 비율로(가로 960 기준) → 호스트가 카메라 비율을 프레임 안에서 맞춘다(비율이 다르면 검은 띠 — 기준 해상도를 카메라와 같게 두면 정확).
  기준 해상도를 고치면 400ms 뒤 새 비율로 다시 연다.
- D5. 카메라를 프리셋 자리로 자동 이동하지 않는다(운용 중 카메라를 몰래 돌리지 않게) — 두 창 모두 **[프리셋 자리로 이동]** 버튼(PTZ · 편집 권한일 때만).
- D6. 프리셋 관리 창: 오른쪽 열 맨 위에 16:9 영상 상자. 창 높이 760 → 900.
- D7. 스트림 id: `mapping-presets-{cameraId}` · `mapping-roi-{presetId}` (지도 팝업 `map-cam-{id}` 와 겹치지 않게).

## 진행
- [x] 현재 상태 확인(영상 없음) · 경로 조사
- [x] 계약(`MappingContracts.cs` IMappingCameraVideo · MappingVideoHostState) + 호스트 구현(`HostMappingCameraVideo.cs`) + 영상 VM(`MappingCameraVideoViewModel.cs`)
- [x] 프리셋 관리 창 · ROI 창 배선(VM · XAML · 캔버스 `VideoImage` DP · 코드비하인드 [다시 시도] · [프리셋 자리로 이동])
- [x] 워크벤치(`CameraVideo`) · 런처(선택 생성자 인자 · 프리셋 창 높이 900) · 모듈 DI(`HostMappingCameraVideo.FromHost`) 배선
- [x] 시험 `Events.Ui/Tests/MappingCameraVideoTests.cs` — Mapping 관련 112건 통과(필터 실행)
- [ ] 전체 `dotnet test Ironwall.Dotnet.Libraries.sln` — 실행 중(지금까지 끝난 프로젝트는 전부 통과), 최종 판정은 하네스 검사

## 발견
- `CameraPopupHostSupervisor.OpenOverlay` 는 호스트가 죽어 있어도 등록해 두고 살아나면 복원한다(공유 메모리는 GIS 소유).
- `HostedVideoView` 는 Image 파생 — Source(WriteableBitmap)를 캔버스가 DrawImage 로 그려도 프레임 갱신이 그대로 반영된다.

## 남은 일
- 실기(가짜 팜 RTSP · 192.168.202.66) 헤디드 확인은 이 실행에서 못 한다(사용자 데스크톱 · 승인 없음).
