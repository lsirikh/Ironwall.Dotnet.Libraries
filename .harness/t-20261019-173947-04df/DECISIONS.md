# DECISIONS

## 1. 회전 영역을 무엇으로 주고받나 (모서리 4점 vs 중심 · 가로 · 세로 · 회전각)
- options: (a) 모서리 4점 [lon, lat]×4 (b) 중심 lon/lat · width_m · height_m · rotation_deg
- chosen: (b) `rect: {lon, lat, width_m, height_m, rotation_deg}` — 굽기 요청에 더하고 `bbox`(감싸는 북쪽 정렬 상자)도 계속 보낸다. scene.json · 장면 목록 · /api/scenes/at 은 rect 에 `corners`(4점)를 덧붙여 돌려준다.
- why: 4점은 직사각형이 아닐 수 있어(투영 · 반올림) 검증이 필요하지만 (b)는 늘 직사각형이고, map-3d 회전 좌표계(원점 = 중심, 축 = 회전각)에 그대로 들어간다. bbox 를 함께 보내 옛 map-3d 서버는 모르는 키를 무시하고 감싸는 상자로 굽는다(깨지지 않음).
- effect: 회전각 = 그릴 때 지도 방위(화면 위쪽이 가리키는 방위, 북 0 · 시계). 회전각 0(360 배수)이면 rect 를 보내지 않고(GIS) / 무시하고(map-3d) 예전 bbox 경로 그대로. 4점 방식으로 바꾸려면 GIS `Map3DRotRect.ToJson` 과 map-3d `geo.parse_rect` 두 곳.

## 2. map-3d 좌표계 — ENU 를 회전할까, ENU 그대로 두고 잘라낼까
- options: (a) 장면 로컬 축을 회전 사각형에 맞춘 회전 ENU(LocalFrame.rotation_deg) + 뷰어가 모델을 −회전각 돌림 (b) ENU 그대로 + 지형 격자 · 건물 · 선을 기운 사각형으로 잘라내기
- chosen: (a)
- why: 파이프라인의 지형 격자 · 높이 보간(height_at) · 평탄화 · 건물/철책/도로 잘라내기 · 3D Tiles 상자 · 썸네일 · 사진 텍스처가 모두 "축에 나란한 직사각형"을 전제로 짜여 있어 (a)는 원점 · 축 · 직사각형만 바꾸면 그대로 맞고, 지형 옆면(스커트)도 저절로 회전 사각형 테두리를 따른다. 뷰어는 모델 하나에 rotation.y 만 주면 세계 좌표 = 진짜 ENU 라 camsync(GIS 카메라 동기 · 방위 변환) · 오버레이 · 커서는 손대지 않는다. (b)는 위 모든 곳을 기운 사각형용으로 고쳐야 했다.
- effect: 회전 장면 GLB 의 로컬 x = 사각형 오른쪽, z = −사각형 위쪽(scene.json `frame` · `rect` · `rotation_deg` 에 기록). GLB 를 직접 읽는 다른 도구는 rotation_deg 를 보고 돌려야 한다(뷰어 · 3D Tiles 변환행렬은 이미 반영). 뷰어 높이표(makeHeightField)는 yaw 를 받아 되돌려 읽는다.

## 3. 위경도 ↔ 미터 환산 — 구 vs 타원체
- options: (a) 구(반지름 6378137) 등장방형 (b) WGS84 타원체 곡률 반지름(묘유선 · 자오선) 등장방형
- chosen: (b) — GIS `Map3DRotRect` 와 map-3d `RotRect` 가 같은 식.
- why: 장면 잘라내기는 타원체 ENU(LocalFrame)라 구로 셈하면 0.1~0.3 % 어긋나 2 km 영역 모서리가 수 m 틀어졌다(시험에서 실제로 걸림). (b)면 GIS 가 그린 모서리와 구운 지형 테두리가 cm 안으로 맞는다.
- effect: GIS 의 크기 라벨 · 최대 크기 검사도 회전 영역은 이 식(가로 · 세로 = 화면 사각형 모서리 사이 거리). 북쪽 정렬 영역의 라벨은 예전 식(Map3DGuide.AreaSizeMeters) 그대로.

## 4. 「이 영역 3D 로 보기」 말고 다른 진입(줌 게이트 · 왼쪽 위 「3D 로 보기」 단추)의 방위
- options: (a) 회전 영역이면 모든 진입을 영역 회전각으로 (b) 바로 들어가기(「이 영역 3D 로 보기」 · 「3D 로 보기」 단추 — 둘 다 EnterMap3DDirect)만 영역 회전각, 줌 게이트는 지금 2D 방위 그대로
- chosen: (b)
- why: 목표가 「이 영역 3D 로 보기」를 "사각형 중심 + 그 회전각"으로 정했고, 단추도 같은 바로 들어가기 경로(같은 표시 `_map3DAreaViewJob`)라 "이 영역을 본다"는 뜻이 같다. 줌 게이트는 사용자가 보던 2D 방향 그대로 들어가야 확대하다 넘어가는 전환이 자연스럽다.
- effect: 「이 영역 3D 로 보기」는 2D 지도도 그 회전각으로 돌린 뒤 들어가고(회전이 막혀 있으면 2D 는 그대로), 3D 처음 시점(home)은 바로 들어가기면 회전각. 나가면 들어오기 전 2D 자리(방위 포함)로 — 예전 규칙 그대로. 바꾸려면 MapViewModel.Map3D.cs Reveal3D 의 `areaViewRect` 조건.

## 5. 2D 영역 테두리 — 모든 영역을 다각형으로
- options: (a) 회전 영역만 다각형, bbox 영역은 예전 Rectangle (b) 모두 네 모서리 다각형
- chosen: (b)
- why: 예전 Rectangle 은 북서 · 남동 두 점의 화면 차로 크기를 잡아 지도가 돌아가 있으면 bbox 영역도 틀어졌다. 네 모서리를 각각 화면에 투영하면 영역 종류 · 지도 회전과 상관없이 맞고, 회전 0 · bbox 영역이면 예전과 같은 네모다.
- effect: Map3DAreaMarker 의 테두리 요소가 Rectangle → Polygon(AutomationId 없음 — 예전과 같이 입력 안 받음). 배지는 영역의 "왼쪽 위" 모서리(회전 영역은 사각형 로컬 왼쪽 위).
