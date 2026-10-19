# REPORT — 3D 회전 영역 "다른 영역 3D 가 깨져 보임" 원인 · 수정 · 수치

## 1. 원인(증거)
1. **캡처는 회전 장면(f366a1)이 아니라 옛 장면 20261017-205808-5e0a01** 이다.
   - GIS 로그(Dotnet.Monitoring.Solution bin Logs/log-2026-10-19.txt): 18:42:11 2D 복귀(방위 −30) → 18:42:18 `장면 찾음 id=20261017-205808-5e0a01` → 18:42:20 `바로 들어가기 job=20261017-205808-5e0a01` → 18:42:35 복귀. 이후 3D 진입 없음.
   - 5e0a01 썸네일의 원형 공연장 · 아파트 = 캡처 왼쪽 지형. 사용자 말 "다른 영역의 3D" 와도 맞다.
   - 그 장면 원점(126.960, 37.38655)에서 발전소 사진 가운데까지 **1009 m**(방위 37.8°), 장면 북동 모서리에서 사진 남서 모서리까지 **233 m** — 지형 밖. 뷰어는 장면 밖 오버레이도 지형 가장자리 높이로 그려 검은 허공에 떴다(방위 −30 시점에서 오른쪽 · 조금 뒤 = 캡처 배치와 같음).
2. **회전 정의는 서로 맞는다**(가설 기각): f366a1 GLB 지형 정점을 뷰어처럼 sceneYaw(−65°)로 돌린 네 모서리 vs pyproj 측지선으로 구한 회전 사각형 모서리의 진짜 ENU = **0.003~0.004 m**. 볼록한 OSM 건물 77동 가운데 vs scene.json 위경도: 중앙값 0.004 m · 90 % 0.006 m. GIS Map3DRotRect == map-3d RotRect(1e-9°, 실제 scene.json 모서리 · bbox 그대로). GMap 회전(RotateTransform(−Bearing)) = map-3d 방위 규약.
3. **작은 실제 결함**: 뷰어 `camsync.enuOf`(오버레이 · 카메라 · 높이표 묻는 자리)가 구 반지름이라 타원체로 구운 지형과 0.24~0.31 % 어긋남 — f366a1 모서리 **1.375 m**, 높이표 **0.493 m**. 예전 시험(rotated_check.mjs)은 기준값 도우미도 구라서 같은 가정을 공유해 못 잡았다.

## 2. 수정(map-3d — .harness/t-20261019-184431-1007/map3d.patch, 7 파일 +320 −15, 깨끗한 클론에 `git apply --check` 통과)
- `camsync.js` enuOf · lonLatOf: WGS84 묘유선 · 자오선 곡률 반지름(RotRect._k 와 같은 길이). `view.js` lonlatOf → camsync.lonLatOf(쓰이지 않게 된 R 상수 제거).
- `overlay-geom.js` itemInArea(점=안 · 선/면/이미지=겹침), `overlay3d.js` refreshVisibility 가 장면 영역 밖 항목 숨김(만들 때 한 번 셈), `view.js` ctx.area = scene rect 또는 bbox.
- `tests/rotated_check.mjs` rect() 도우미를 RotRect 와 같은 타원체 식으로(DECISIONS 4).
- 새 시험: `tests/test_rotated_truth.py`, `tests/overlay_area_check.mjs`.
- GIS(이 저장소) 코드 변경 없음 — 새 시험 `tests/GMaps.Ui.Tests/Map3DRotRectParityTests.cs` 만.
- 이미 만든 회전 장면 f366a1 은 **다시 굽지 않아도 된다**: 같은 요청을 새 폴더에 CLI 로 다시 구우니 지형 x/z 차 0.0 m(y 는 정점 0.2 % 에서 건물 자리 다지기 차이). 뷰어 JS 만 바뀌면 맞는다.

## 3. 수치(수정 전 → 후)
| 대상 | 오버레이(enuOf) ↔ GLB 지형/건물 최대 | 높이표 ↔ 지형 최대 | 판정(±0.5 m / ±0.1 m) |
|---|---|---|---|
| 합성 0° (720×590 m) | 1.005 → 0.034 m | 0.052 → 0.016 m | 실패 → 통과 |
| 합성 30° | 1.332 → 0.014 m | 0.068 → 0.015 m | 실패 → 통과 |
| 합성 65° | 1.371 → 0.016 m | 0.056 → 0.014 m | 실패 → 통과 |
| 합성 90° | 1.156 → 0.017 m | 0.053 → 0.017 m | 실패 → 통과 |
| 합성 −45° | 1.400 → 0.014 m | 0.054 → 0.013 m | 실패 → 통과 |
| 실제 f366a1(읽기만) | 1.375 → 0.251 m* | 0.493 → 0.001 m | 실패 → 통과 |
| 다시 구운 안양(artifacts/rebake-anyang) | 1.375 → 0.251 m* | 0.493 → 0.001 m | 실패 → 통과 |

\* 0.251 m 는 건물 볼록 껍질 가운데 재기의 한계(바닥 면적 1 % 이내 볼록 건물만 씀). 지형 모서리만 보면 0.004 m.

장면 밖 오버레이(overlay_area_check.mjs): 옛 장면 5e0a01 에서 발전소 사진 · 표식 = 숨김, 회전 장면에서 = 보임, 회전 장면의 감싸는 bbox 모서리(회전 사각형 밖) = 숨김, Overlay3D.refreshVisibility 배선(three 실물) 확인. 수정 전 코드에는 itemInArea 가 없어 시험이 실패(import 오류)한다.

## 4. 확인 명령
- 원인: GIS 로그 grep `[Map3D]` 18:42 · `.harness/…/measure_real.py`(실제 장면 읽기만) · 썸네일 비교
- `node .harness/t-20261019-184431-1007/truth_eval.mjs <map-3d 7001042 깨끗한 클론> <truth…>` → 7건 모두 실패 / `… artifacts/map3d <truth…>` → 7건 모두 통과(진실값: test_rotated_truth.py `--basetemp` 의 truth.json 5개 + measure_real.py 2개)
- map-3d pytest: 수정 전 깨끗한 클론 210 passed → 수정 후 215 passed(새 시험 5). node *.mjs 전부 통과(extract_view_check 는 pytest 가 인자로 부름)
- GIS: `dotnet test tests/GMaps.Ui.Tests` 1538 통과(새 Map3DRotRectParityTests 14 포함) · `dotnet test tests/GMaps.MapEdit.Tests` 475 통과 · `dotnet test Ironwall.Dotnet.Libraries.sln` exit 0(실패 0)
- 실기(GIS 안 3D 화면)는 **실기 미확인**(창 · WebView2 · 브라우저 시험 금지).

## 5. 범위 밖으로 남긴 것
- 18:42:18 에 옛 장면을 고른 장면 찾기 판단(지도 가운데가 어디였는지 로그에 없음)은 동시 작업 t-20261019-183759-f6fc(옛 장면 참조) 범위라 손대지 않았다.
- 추적 타겟(track)은 장면 밖이어도 예전대로 그린다(움직이는 대상이라 이번 범위 밖).
