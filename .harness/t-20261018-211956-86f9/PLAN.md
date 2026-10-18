# PLAN — 3D HUD 정리: 줌 컨트롤 숨김 · 좌표 칩/축척 막대 3D 연동 · 3D 축소 한계 (t-20261018-211956-86f9)

## 목표
사용자 요청(2026-10-18): 3D 에서는 줌 컨트롤 · 줌 정보를 없애고, WGS-84 좌표 · 거리(축척)는 3D 와 정확히 연동, 3D 에서 너무 멀리 빠지지 않게.

## 설계
- 뷰어(map-3d 사본 `claude-skill-harness-map3d/`, 기준 사본 `claude-skill-harness-map3d-base/` — .gitignore `claude-skill-harness*/`)
  - camsync.js 순수 함수: `cursorDelay` · `cursorMessage`(던지기 · 바뀐 때만) · `fitAreaView` 결과에 `distance` · `maxOrbitDistance`(영역 전체 거리 × 1.3, 방위 0/45/90/135 중 최대) · `clampZoomSteps`(휠이 한계에 딱 멈추게)
  - view.js: 끼워 넣기에서 마우스 이동(끌기 아님) · 휠 → 지형/건물 레이캐스트 → `{type:"cursor", lon, lat, alt}`(50 ms 간격, 마지막 위치도 보냄), controls.maxDistance = maxOrbitDistance(장면 불러올 때 · 화면 크기 바뀔 때, 접힌 화면은 건너뜀), 휠은 clampZoomSteps
  - 축척(m/px)은 뷰어가 따로 보내지 않는다 — camera 메시지의 zoom 이 이미 화면 가운데 m/px(mapFromCamera: zoom = log2(C·H / (256·보이는 높이)))
- GIS
  - Map3DMath: `IsZoomControlVisible(status)` · `MetersPerPixel(zoom, lat)` · `ScaleBar(m/px)` · `AltitudeText(alt)` · ParseMessage "cursor" → `Map3DMessage.Cursor`
  - VM: `IsMap3DZoomControlVisible`(SetMap3DState 에서 알림), cursor → 좌표 칩(WGS · MGRS · UTM · 고도), CreateScaleBar 가 3D 표시 중이면 3D 카메라로, 2D 복귀 때 다시 2D 값 · 고도 지움
  - MapCoordinateControl `AltitudeText`(더하기) — 값이 있을 때만 "고도:" 줄
  - 로그인 직후 3D 안내 글의 "줌 16.5 밑" → 새 단계 "shown"(나가기 · ESC)
  - MapView.xaml 줌 컨트롤 Visibility

## 진행
- [x] 뷰어 camsync.js · view.js · tests/camsync_check.mjs(축소 한계 · 휠 자르기 · 커서 던지기 시험)
- [x] map3d.patch — 기준 사본과 diff -u(a/ b/), 원본 map-3d 에 `git apply --check` 통과(CRLF 유지)
- [x] GIS Map3DMath(IsZoomControlVisible · MetersPerPixel · ScaleBar · AltitudeText · cursor 해석) · Map3DGuide "shown"
- [x] VM(IsMap3DZoomControlVisible · CurrentAltitudeText · SetMap3DCursor · TryCreateMap3DScaleBar · Leave3D 에서 2D 막대 · 고도 지움), 안 쓰이게 된 Map3DCurrentGate 제거
- [x] MapCoordinateControl.AltitudeText + MapCoordinateStyle 고도 줄(두 파일 UTF-8 BOM 으로) · MapView.xaml 바인딩 · MapsHelp "Map.Map3D.Exit" 두 줄
- [x] tests/GMaps.Ui.Tests/Map3DHudTests.cs
- [x] 검증 — 아래 「검증」

## 발견
- 최소 거리(controls.minDistance)는 15 m — 장비(높이 수 m)를 가까이 볼 수 있는 값이라 그대로 둔다.
- 2D 축척 막대(ScaleHelper)는 정수 줌 표 값이라 원래 근사치 — 3D 는 실제 m/px 로 셈한다.

## 남은 일
- 실기 확인(창 · WebView2 시험 금지 — 실기 미확인)
- map-3d 원본에 map3d.patch 적용(코디네이터/사용자 몫)
