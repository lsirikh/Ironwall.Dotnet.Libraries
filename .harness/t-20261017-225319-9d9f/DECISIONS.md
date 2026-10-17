# DECISIONS

## 1. tiles 일 때 요청 본문에 overlay_mode 를 넣을지
- options: 항상 `"overlay_mode":"tiles"` 를 넣는다 / tiles 면 본문을 예전 그대로(bbox 만) 둔다
- chosen: tiles 면 bbox 만
- why: 규약상 tiles 가 기본값이라 뜻이 같고, 규약 이전 map-3d 서버(추가 키를 거절할 수 있음)와도 그대로 맞는다
- effect: 서버 로그에서 tiles 요청에 overlay_mode 가 보이지 않는다. 바꾸려면 `Map3DBake.BuildBody` 의 `if (m == ModeTiles) return body;` 한 줄

## 2. 서버가 받지 못하는 원본(TIF · 50 MB 초과 PNG/JPG)을 어떻게 보낼지
- options: 빼고 굽는다 / 원본 경로를 그대로 보내 서버 400 을 받는다 / GIS 가 PNG/JPG 로 바꾼 캐시 파일 경로를 보낸다
- chosen: 캐시 파일(`%LocalAppData%\Ironwall\Gis\map3d-cache`, 긴 변 2048 px · 투명 없으면 JPEG 85 / 있으면 PNG) 경로를 보낸다
- why: 실제 GIS 이미지(GeoTIFF)로도 되게 하라는 요청 — 원본을 보내면 굽기 전체가 400 으로 실패한다. 3D 오버레이 다리와 같은 캐시를 써 새 의존성이 없다
- effect: TIF 는 2048 px 로 줄어 합성된다(지형 텍스처 해상도 한계). 원본 해상도가 필요하면 `Map3DImageCache.GetOrEncodeFile` 에서 줄이지 않고 PNG 로 쓰게 바꾼다. 50 MB 는 서버 쪽 정의를 몰라 1000 단위(50,000,000 B)로 작게 잡음 — `Map3DBake.MaxImageBytes`

## 3. 이미지 "겹침" 판정 방식
- options: 회전한 사각형과 영역의 정확한 다각형 교차 / 회전한 네 모서리를 감싸는 사각형으로 넉넉히 판정
- chosen: 감싸는 사각형(경도는 위도 cos 보정)
- why: 빠뜨리면 가려야 할 이미지가 3D 에 안 들어가 보안상 더 나쁘다. 넉넉히 넣은 이미지는 서버가 실제 범위로만 가린다
- effect: 회전한 이미지가 영역 모서리 바깥 가까이에 있으면 패널이 뜨거나 overlays 에 들어갈 수 있다. 정밀 판정은 `Map3DBake.Intersects` 만 바꾸면 된다

## 4. 지도 CustomImages(TIF) 의 id
- options: 3D 다리(Map3DOverlayBridge)가 붙이는 음수 id / 모델 Id
- chosen: 다리의 음수 id(`IdOf`)
- why: 3D 화면은 스냅샷 id 와 overlays_baked id 를 맞춰 그리지 않는다 — 스냅샷이 쓰는 값과 같아야 한다. 모델 Id 는 이미지 마커 id 와 겹칠 수 있다
- effect: 이 id 는 세션 단위라 앱을 다시 켠 뒤엔 맞지 않을 수 있다(구운 TIF 위에 3D 화면이 이미지를 한 번 더 덮음 — 내용은 같은 그림). 고치려면 다리의 CustomImageId 를 모델 기준 고정 값으로 바꾼다

## 5. 고른 방식을 저장하는 시점
- options: 라디오를 바꿀 때마다 / [만들기] 를 누를 때만
- chosen: [만들기] 때만(값이 바뀌었을 때) appsettings Map3D 절 저장
- why: "마지막 고른 값" = 실제로 만든 방식. 취소하면 기억하지 않는다
- effect: 저장은 Map3D 절 통째 교체(IsEnabled · ServerUrl · UseCompositionHost · OverlayMode). 저장 실패는 경고만 남기고 이번 실행 동안 기억
