# PLAN — 3D 관성 회전 중 카메라 상자 떨림

## 목표
3D 지도에서 회전 뒤 관성으로 서서히 멈추는 동안 카메라 상자(오버레이 창)가 심하게 떨리는 문제 — 원인을 수치로 규명하고 고친다.

## 진행
- [x] 작업 시작 전 정리: 앞 세션 중단(16:35 "stopped by the user") 때 파일이 NUL 로 채워짐 → 복구
  - WIP 커밋(8de895e4)이 csproj 16개를 공백으로 덮어씀 → HEAD~1 에서 되살림
  - git 이 못 알아챈(크기 · 시각 같음) NUL 파일 148개(csproj · Theme xaml · Utils.Tests 등) → HEAD 블롭 그대로 되살림(목록: restored-zeroed-files.txt). 내용은 HEAD 와 같고 줄 끝만 LF.
  - 서브모듈 Ironwall.Dotnet.Libraries.OnvifSolution 의 csproj 1개도 NUL → 서브모듈 HEAD 블롭으로 되살림.
- [x] 원인 규명(아래 「증거」)
- [x] 3D 뷰어 수정(map-3d 복사본 → `map3d.patch`), node · pytest 통과
- [x] GIS 수정(Map3DScreen · Map3DMath · MapViewModel.Map3DWindows · CameraStreamPopupViewModel/Control)
- [x] 재현 하네스 + 시험(tests/GMaps.Ui.Tests/Map3DPopupJitterTests.cs, 22건)
- [ ] (실기 미확인) 실제 3D 화면에서 관성 회전 중 상자 확인 — map3d.patch 를 map-3d 에 적용해야 효과가 난다

## 증거 — 원인
코드 확인:
- 3D 뷰어 `view.js` controls "change" 의 camera 메시지는 **150 ms 에 한 번**(`if (now - liveT < 150) return`). 반면 `overlay.screen`(마커 화면 자리)은 **그린 프레임마다**.
- GIS `Map3DViewPair()` 는 상자 오프셋(앵커 − 마커)을 키우고 돌릴 3D 줌 · 방위로 `_map3DLastCamera`(=마지막 camera 메시지)를 썼다.
  → 마커 자리는 매 프레임 새 값, 오프셋 방위는 최대 150 ms 묵은 값. 관성 회전(초기 4°/프레임)이면 최대 ~36° 늦게 돌다가 camera 메시지 때 한꺼번에 따라잡는다 = 상자가 마커 둘레에서 7 Hz 계단처럼 늦었다 튀었다(후보 ②④).
- 마커를 중심으로 돌면 마커 화면 자리가 그대로라 옛 뷰어는 overlay.screen 을 아예 안 보냄 → 상자는 camera 메시지 때만 돈다(후보 ③: '바뀐 것만' 문턱).
- camera 메시지마다 `Apply3DCameraTo2D` → 2D 줌 · 위치 · 회전 통지 3번이 `RefreshCameraPopupPositions` → `TryPlaceCameraPopupsIn3D` 를 다시 불러 한 프레임에 최대 4번 자리를 정함(후보 ①⑥). 2D 규칙(앵커 위경도) 자체는 3D 중 건너뛰었으나 2D 통지가 3D 배치를 다시 돌렸다.
- 2D 화면 좌표는 GMap 정수(FromLatLngToLocal) — 2D 가 camera 메시지마다 따라 움직이며 오프셋을 다시 재면 ±1 px(× 3D 줌 배율) 튐(수정 1단계 뒤 남은 잔떨림의 원인 — 「camera 전부 누락」 시나리오에선 0 이라 분리 확인).
- 끄는 중(드래그) 3D 관성이 이어지면 따라가기가 매 프레임 CanvasLeft 를 덮고 끌기는 그 값에 dx 만 더함 → 끌기와 따라가기가 번갈아 자리를 정함.
- 후보 ⑤ 제외: 카메라 상자는 지도 Canvas 의 WPF 항목(CameraStreamPopupControl)이고 호스트 프로세스는 영상 프레임만 준다. SetWindowPos 는 호스트의 별도 이벤트 창에만 있음.
- 조립 카드: 3D 위 층에서 overlay.screen 좌표에 바로 붙음(오프셋 회전 없음) — 같은 문제 없음, 경로는 그대로.

재현 하네스 수치(60 Hz, 감쇠 0.05, 놓을 때 4°/프레임, 240 프레임):

| 시나리오 | 왕복(진행 방향 오차 부호 바뀜) | 역행 | 최대 오차 | 프레임당 최대 적용 | 2D 통지 적용 |
|---|---|---|---|---|---|
| 수정 전 | 21 | 6 | 148.80 px | 4 | 69 |
| 수정 1단계(view 만) | 7 | 0 | 1.73 px | 1 | 0 |
| 수정 후 | **0** | **0** | **1.08 px** | **1** | **0** |
| 마커 중심 회전 전 → 후 | 20 → 0 | 0 → 0 | 148.73 → 0.98 px | 3 → 1 | 69 → 0 |
| 회전 + 줌 관성 전 → 후 | 22 → 0 | 4 → 0 | 147.13 → 1.36 px | 4 → 1 | 69 → 0 |
| 끄는 중 관성 전 → 후 | 12 → 0 | 4 → 0 | 148.80 → 1.04 px | 4 → 1 | 69 → 0 |
| screen 먼저 / camera 먼저 / camera 누락(후) | 0 / 0 / 0 | 0 | 0.29 / 1.08 / 0.29 px | 1 | 0 |

## 수정
- 3D 뷰어(map3d.patch): `overlay.screen` 에 그 프레임의 `view: {zoom, bearing}` 를 싣고, view 가 바뀌면 마커 자리가 그대로여도 전부 다시 보냄(`frameView` · `ScreenReporter.changes(cur, view)`).
- GIS:
  - `Map3DMessage.ScreenView` · `Map3DScreen.ParseScreenView` — view 해석(없으면 null = 옛 뷰어).
  - `Map3DScreen.OffsetView3D` — 오프셋용 3D 값: 같은 프레임 view → 마지막 camera → 2D 순.
  - `Map3DScreen.PlaceOn2DChange` — view 를 받는 동안 2D 통지로는 놓지 않음(overlay.screen 한 출처 · 프레임당 1회).
  - `Map3DScreen.BaseOffset` · `PopupTopLeft3DFromBase` — 오프셋을 줌 0 · 방위 0 기준으로 상자마다 한 번만 잼(앵커 · 마커 위경도가 바뀌면 다시). 기존 `PopupTopLeft3D` 와 수식상 같음(시험).
  - `CameraStreamPopupViewModel.IsWindowDragging` — 끄는 중엔 3D 따라가기가 건너뜀, 놓으면 기존 DragCompleted 가 새 앵커 저장.
- 옛 3D 뷰어(view 없음)와 붙으면 예전 동작 그대로(떨림도 그대로) — 패치 적용이 필요.

## 발견
- view.js 의 camera 메시지 150 ms 스로틀은 끝 값을 보내지 않을 수 있다(감쇠 마지막 150 ms 안의 변화는 안 보냄) → 2D 나침반 · 축척이 멈춘 뒤 살짝 묵을 수 있음. 상자 자리는 이제 view 로 정해 영향 없음. 범위 밖이라 손대지 않음.
- 개발 PC 에서 앞 세션 중단 때 파일 내용이 NUL 로 바뀐 사례(크기 · 시각은 그대로라 git status 에 안 보임) — 다음 작업 전 NUL 스캔 권장.

## 남은 일
- 실기 확인(미확인): map-3d 에 map3d.patch 적용 → 3D 에서 회전 뒤 관성 중 카메라 상자 확인.
- map-3d 복사본: `%TEMP%\map3d-t-20261019-162523-2e52`(git clone, 원본 저장소 무변경). 이 작업은 바깥 폴더를 지울 수 없어 남겨 둠.
