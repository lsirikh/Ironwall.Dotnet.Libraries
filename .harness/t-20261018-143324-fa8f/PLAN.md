# PLAN — FenceGroup 탐지 시 3D 이동(t-20261018-143324-fa8f)

## 목표
이벤트(탐지 · 장애) 지도 이동이 장비 심볼이 없는 펜스 구역(FenceGroup)에서도 동작하고, 3D 표시 중이면 2D 비행 없이 바로 3D 카메라(animate)로, 3D 조작 중이면 양보한다. 3D 구역 상태에 탐지 구간을 보낸다.

## 진행
- [x] 1. 구역 목표 — `MapFlyToMath.ZoneGroupCandidates` · `ResolveZoneTarget` · `ZoneSection` · `PointAlong` · `FitZoom` · `ResolveZoneZoom`(순수), `MapViewModel.EventFlyTo.FlyToEventEntry` 에서 심볼이 없으면 구역 선으로, 못 찾으면 로그
- [x] 2. 3D 표시 중 경로 — `MapFlyToMath.ChoosePath` + `Map3DStaysAt`(Map3D.cs, `Sync3DAfter2DMove` 도 같이 씀). ThreeD = 0 ms(2D 즉시 + PostCamera animate), Leave3DThenTwoD = Leave3D 뒤 2D 비행. 2D 비행 중 3D 게이트 보류(`_flyAnimation == null`)
- [x] 3. 3D 조작 양보 — `MapFlyToMath.IsUserCameraReason`, `OnMap3DMessage` camera → `NoteMap3DCameraForFlyTo`
- [x] 4. 3D 구역 JSON `state.activeSection: [from, to] | null`(pidsGroup 만) — `Map3DOverlayLine.ActiveSection`, `Map3DOverlayBridge` 생성자 선택 인자 `zoneSection`(MapViewModel.ZoneDetectionSection)
- [x] 시험 `tests/GMaps.Ui.Tests/MapEventFlyToZoneTests.cs`(24건) 통과
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` 확인

## 발견
- `EventEntry.GroupIds` 에 소속 그룹이 이미 있다. 장비 `DeviceGroups` 도 후보.
- `ActiveSensorDeviceIds` 는 아무도 쓰지 않는다(Phase 2 미구현). 3D 철망 컨트롤은 그 값을 노드 순번으로 읽으므로 이 작업에서 쓰지 않았다.
- 탐지 구간 = 그 구역 소속 장비 중 큐(EQM)에 이벤트가 있는 장비의 자리(심볼 위치, 없으면 장비 좌표)를 선에 투영한 범위. 좌표가 없거나 선에서 50 m 밖이면 구간 없음 → 구역 경계 가운데 + 구역이 보이는 줌.

## 남은 일
- 실기(탐지 보내 3D 이동 확인)는 코디네이터가 녹화하며 한다 — 실기 미확인.
- 뷰어(map-3d) 가 `state.activeSection` 을 표시하는 일은 범위 밖.
