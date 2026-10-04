# PLAN — 장비 창(콘솔) 카메라 레일에 ONVIF 검색 · 조회 · 추가 붙이기

## 목표
실창 EVT-R26-10: ONVIF 검색(t-20261004-094918-9766)이 옛 `CameraDevicePanelView` 에만 붙어 있어, 실제 장비 창
(DeviceDashboardViewModel · DeviceDashboardView, 레일 Console.Devices.Rail.camera)에서는 닿을 수 없었다.
카메라 레일에서 검색 → 조회 → 추가(Draft) → 기존 저장([등록])이 되게 한다.

## 진행
- [x] `DeviceDashboardViewModel` — `CameraDiscovery`(OnvifDiscoveryViewModel) · `IsCameraRail` · `CanOpenCameraDiscovery` ·
      `IsCameraDiscoveryOpen` · `ToggleCameraDiscovery()` · `TryOpenDiscoveredDraft` · `IsCameraListedInConsole` · `UseCameraDiscoveryServices`(시험용)
- [x] [추가] = 툴바 [추가] 와 같은 길: 이동 막기(BeginCreate) → 패널 `CreateDraft`(번호 자동 배정 · 권한) → 조회 값으로 만든
      CameraDeviceModel 로 바꿔 상세 칸 Create 상태로 연다. 목록에는 [등록] 전까지 아무것도 없다(FR-12). [등록] = 기존 Apply → AdoptDraft → 패널 Save.
- [x] `OnvifDiscoveryViewModel.Resync` — Draft 취소 · 등록 · 레일 이동 뒤 줄 상태(추가됨 → 조회됨 / 이미 등록됨) 맞춤. `AddedHint` 로 [저장]/[등록] 문구 구분.
- [x] `DeviceDashboardView.xaml` — 툴바 Extra 에 [ONVIF 검색] 단추(AutomationId `Devices.Camera.Onvif.Toggle`, 카메라 레일에서만),
      목록 칸 Row 1 에 `OnvifDiscoveryView`(Border `Console.Devices.Camera.Onvif`, 열었을 때만). 그리드 · 빈 상태 · 부품으로 찾기는 Row 2.
- [x] `OnvifDiscoveryView.xaml` — BooleanToVisibilityConverter 를 로컬 리소스로(호스트 무관).
- [x] 도움말 문구 [저장] → 장비 창 [등록].
- [x] 시험 6건(DeviceDashboardConsoleTests, STA 디스패처): 레일 한정 · Draft 로 열림/목록 무변 · 취소 뒤 재추가 · [등록] 이 패널 저장에 Draft 넘김 · 이미 등록됨 · 레일 이동 시 닫힘.
- [x] Devices.Ui 전체 4005 통과.

## 결정
- 검색 영역 위치 = 목록 칸 위(배너 아래). 상세 칸은 Draft 폼이 쓰므로 비워 둔다.
- 다른 레일로 가면 검색 영역을 닫는다(검색 · 조회 취소). 창 닫힘(Deactivate)도 닫는다. 찾은 목록은 남긴다(다시 열면 재검색 안 함 — 기존 동작).
- 단추 AutomationId 는 옛 패널과 같은 `Devices.Camera.Onvif.Toggle` (실창 시험이 같은 이름으로 찾음; 옛 패널은 콘솔 트리에 없음).
- 콘솔은 Draft 를 하나만 쥔다 — 손대지 않은 Draft 는 새 [추가] 가 대체, 손댄 Draft 면 이동 막기가 흔든다(기존 규칙).

## 남은 일
- 실창(헤디드) 확인: 가짜 카메라 팜(tools/dummy-cameras run) + 장비 창 카메라 레일 → [ONVIF 검색] → [조회] → [추가] → [등록]. (이 세션은 데스크톱 시험 불가)
- 실카메라 192.168.202.x — 계정은 코드 · 파일 · 커밋에 넣지 않는다.
