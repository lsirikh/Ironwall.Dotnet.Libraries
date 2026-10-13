# PLAN — 통문 · 함체 문 열림이 지도 마커(UIA 이름 · 툴팁)에 안 보이는 문제 + 운영 모드 우클릭

## 목표
1. OPERATION_EVENT 문 열림/닫힘 → 지도 마커(2D · 3D)의 UIA 이름 · 툴팁 · 3D 문짝까지 이어지게(늦은 장비 바인딩 · 부팅 복원 포함).
2. 시드 통문 · 함체 3D 마커에 운영 모드 우클릭 메뉴('상세 보기')가 뜨게.
3. 헤드리스 시험(창 없음): DOOR-001~ + 기존 id(EVM-047/108 등) Trait.

## 발견(증거)
- 문 상태 경로는 끊기지 않았다: `SymbolLifecycle.RegisterDevice(device, symbol.Model)` 로 마커 자신의 모델이 조회표에 등록되고,
  `ApplyDoorState → MarshalUpdate → SetUpdate → GMapPidsMarker.ApplyModelUpdate → PropertyChanged(DoorState) → 컨트롤 DoorState DP`.
  늦은 바인딩(MarkerFactory 경고) 뒤에도 `InitializeDeviceSymbolIntegration` 이 `symbol.LinkedDevice = device` 로 다시 잇는다.
- **진짜 원인**: `GMapMarker3DHousingControl.DoorLeavesShowPosition = true` → `DoorIndicatorRules.Resolve(..., leaves:true)` 가 열림/닫힘을 `None` 으로 만든다
  (문짝이 위치를 그리니 좌하단 배지는 빼자는 설계). 그런데 UIA 이름(PidsMarkerAutomationPeer)과 툴팁(SymbolToolTip)이 **같은 배지 값(DoorIndicator)** 을 써서
  3D 에서는 문 절이 통째로 사라졌다. 실창 두 마커 모두 3D 하우징 → 'LRT-E2EDOOR-ENC · 이벤트 정상 · 장비 활성 · 부품 정상 1'.
- 우클릭: `HitTest=False` 는 **의도**다(`GMapCustomControl.SetEditMode` — 운영 모드에선 마커가 팬/줌을 통과시키고, 우클릭은
  `OnMouseRightButtonDown → GetMarkerAtScreen` 수동 AABB 스캔으로 받는다). 그런데 이 스캔은 구역선(GMapPidsGroupMarker)의 화면 bbox 전체를
  후보로 잡고 ZIndex 우선으로 고른다 → 구역 안의 점 심볼을 구역이 가로챈다. 증거: 2026-10-13 14:46 실창 진단 로그 —
  같은 지점(카메라)이 편집 모드에선 카메라 메뉴, 운영 모드에선 '[구역 — 구역-02, 등록 센서 정보, 그룹 탐지 이력]'.
  (15:23 회차 앱 로그는 50줄 버퍼로 잘려 운영 모드 우클릭 결과가 남지 않았다 — 같은 원인으로 판단.)

## 결정
- 공개 형식은 더하기만: `GMapMarkerPidsControl.DoorStatus`(읽기 전용 DP, `DoorIndicatorKind`) 추가 — 위치 표시 여부와 무관한 '알림용' 문 상태.
  UIA 이름 · 툴팁은 이것을 쓴다. 배지(`DoorIndicator`)는 그대로(3D 는 구동 중만).
- `GetMarkerAtScreen` 정렬 맨 앞에 "구역선(테두리만 그리는 bbox) 후순위" 키를 더한다. 점 심볼 후보가 없으면 구역은 종전대로 잡힌다.
- 시험은 `tests/GMaps.Housing.Tests/DoorMarkerStatusScenarioTests.cs`(새 파일, UTF-8 BOM). 부모 없는 비주얼 + Measure/Arrange, Window · HwndSource 없음.

## 진행
- [x] 원인 분석
- [x] DoorStatus DP + peer · 툴팁 (`GMapMarkerPidsControl.cs` · `PidsMarkerAutomationPeer.cs`)
- [x] GetMarkerAtScreen 구역 후순위 (`GMapCustomControl.cs`)
- [x] 헤드리스 시험 `DoorMarkerStatusScenarioTests` 11건 — DOOR-001(통문 · 함체 × 3D 켬/끔, EVM-108) · DOOR-002(두 문 동시) ·
      DOOR-003(부팅 복원 × 4) · EVM-047(함체 접점) · DOOR-004(구역 안 3D 함체 우클릭 스캔)
- [x] 빨강 확인: 수정 두 곳을 옛 코드로 되돌리면 7건 실패(3D 전부 + DOOR-004), 2D 4건은 원래도 통과 → 복원 뒤 11건 통과
- [x] GMaps 시험 7개 프로젝트 전부 통과(Housing 312 · MapAux 76 · MapEdit 55 · MapLayer 63 · MapNav 47 · Ui 757 · PropertyPanel 285)
- [ ] `dotnet test Ironwall.Dotnet.Libraries.sln`

## 남은 일 / 메모
- Devices.Ui 폴더는 건드리지 않는다(다른 작업 중).
- 실창 재확인(헤디드 SymbolDetailRuntimeDiagTests ②)은 이 하네스에서 돌리지 않았다 — 앱 bin 반영(v2.6 머지) 뒤 사용자 PC 에서.
- 앱 INFO 로그가 50줄 버퍼라 15:23 회차 운영 모드 우클릭의 앱 쪽 기록이 남지 않았다(진단 한계). 우클릭 원인 판단 근거는 14:46 회차 로그.
