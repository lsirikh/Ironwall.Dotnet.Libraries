# DECISIONS — 지도 3D R1 · R2 GIS 쪽

## 1. 아이콘 키를 무엇으로 만드나
- options: (a) 시각 상태 문자열의 해시 (b) 그린 PNG 내용의 SHA-1
- chosen: (b) 키 = PNG 내용 SHA-1 앞 16자. 다시 그릴지는 별도의 "시각 서명"(상태 · 스타일 · 크기 · 보임 — `Map3DOverlay.IconSignature`)으로 판단
- why: 같은 그림이면 마커가 달라도 같은 키 → "같은 key 는 한 번만"이 저절로 지켜지고, 서명에 빠진 값이 있어도 키가 잘못 겹치지 않는다
- effect: 상태 · 크기 · 색이 바뀌면 다시 그려 새 키가 patch 의 icon 으로 간다. 서명 항목은 `Map3DOverlayBridge.StyleOf` 에서 바꾼다

## 2. 아이콘에 2D 표시 회전을 넣나
- options: (a) 2D 화면 그대로(맵 회전 · 심볼 방위 반영) (b) 회전을 뺀 모양 + bearing 은 따로
- chosen: (b) 루트 변환(표시 회전 · 클릭 펄스)의 역변환을 걸어 회전 없는 모양을 그린다
- why: P 에 이미 bearing 이 있어 3D 가 돌린다 — 아이콘에 회전이 들어가면 두 번 돈다. 2D 지도 회전은 3D 카메라와 무관
- effect: 3D 가 bearing 으로 빌보드를 돌려야 한다(M 쪽). 바꾸려면 `RenderIconPng` 의 역변환 블록을 빼면 된다

## 3. 지도 CustomImages(TIF · 이미지 파일 오버레이)의 id
- options: (a) ImageModel.Id 그대로(대개 0 · 중복) (b) 다리가 음수 id(-1, -2, …)를 붙임
- chosen: (b)
- why: 이 이미지들은 DB 키가 없어 0 이 겹치고, DB 이미지 마커(양수 id)와 같은 kind "image" 를 쓰므로 부호로 가른다
- effect: 3D pick 의 음수 id 는 `FindCustomImage` 로 되찾아 2D 이미지 클릭(OnMapImageClicked)을 부른다. 다리 인스턴스가 살아 있는 동안 id 유지

## 4. 보안 가림(mask) 값
- options: (a) 이미지마다 모델 값 (b) 항상 true
- chosen: (b) 항상 true
- why: 모델(ImageModel)에 가림/깔기 필드가 없고 이번 범위 파일 밖이다. 명세 기본값이 true
- effect: 이미지마다 바꾸려면 ImageModel 에 필드를 더하고 `Map3DOverlayBridge.Build` 의 `mask: true` 두 곳을 그 값으로 바꾼다

## 5. 추적 타겟 label · 꼬리 · 방위의 출처
- options: label = (a) track_id(2D 호버 툴팁) (b) 속도 글(2D 핀 아래 상시) / 꼬리 · 방위 = (a) GMapTrailMarker._points · GMapTrackingMarker._worldBearing 을 리플렉션으로 (b) 다리가 따로 기록
- chosen: label = track_id, 꼬리 · 방위 = 리플렉션(못 읽으면 빈 꼬리 · 0°), 색 = 2D 핀(PackIcon) 색
- why: 3D 에는 호버 툴팁이 없어 식별자가 더 쓸모 있다. 꼬리를 다리가 따로 모으면 3D 진입 전 이동 경로가 빠진다. 두 마커 파일은 이번 범위 밖이라 공개 속성을 더하지 않았다
- effect: GMapTrailMarker/GMapTrackingMarker 에 공개 읽기 속성을 더하면 리플렉션 두 줄을 그 속성으로 바꾸면 된다. 재생(Playback) 추적도 보내며 id 에 ":play" 를 붙인다

## 6. 오버레이 이미지 PNG 인코딩 시점
- options: (a) UI 스레드에서 동기(ImageSource 별 1회 캐시) (b) 백그라운드 인코딩 뒤 늦게 보냄
- chosen: (a)
- why: imageKey 가 PNG 내용 해시라 인코딩 전에는 항목을 만들 수 없다. 한 번 만들면 캐시해 재진입 · resync 때는 다시 하지 않는다
- effect: 아주 큰 TIF 는 3D 첫 진입 때 한 번 멈칫할 수 있다. 문제가 되면 `BlobOf` 를 비동기로 바꾸고 항목 add 를 인코딩 뒤로 미룬다

## 7. 3D 의 track pick 처리
- options: (a) 기록만 (b) 무언가 동작(카메라 팝업 등)
- chosen: (a) 기록만
- why: 2D 추적 마커는 클릭 동작이 없다(IEditableMarker 아님, 호버 툴팁만) — "2D 와 같은 동작"
- effect: 동작이 필요해지면 `OnMap3DPick` 의 track 분기에서 부른다
