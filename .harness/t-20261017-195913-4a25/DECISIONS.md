# DECISIONS — 2D → 3D 전환 빠르게 · 확실하게

## 1. 최소 안내 시간 0.6 s → 0.4 s 를 어디에 적용할까
- options: (a) Map3DGuide.MinEnterShow 자체를 0.4 s 로 바꾼다 (b) MinEnterShow(0.6 s)는 두고 "이미 rendered 를 받은 장면" 펼치기에만 새 상수 MinReuseShow = 0.4 s
- chosen: (b)
- why: 명세가 "재사용 경로도 이미 rendered 를 받은 장면이면 … 0.6 s → 0.4 s" 라고 재사용 경로만 말하고, 기존 시험(Map3DGuideTests.should_keep_enter_guide_at_least_point6_seconds_when_scene_reused)이 MinEnterShow = 0.6 을 고정한다(기존 시험은 고치지 않는다).
- effect: 처음 여는 장면은 ready · rendered 가 0.6 s 안에 와도 0.6 s 뒤 펼친다(실측상 처음 여는 경우는 거의 늘 0.6 s 를 넘는다). 바꾸려면 Begin3D/RevealAfterGuide 의 상수만 바꾸면 된다.

## 2. 3D 를 접은 뒤(2D) 오버레이 다리를 어떻게 둘까
- options: (a) 접혀 있어도 계속 구독해 1×1 페이지에 patch 를 보낸다 (b) 접을 때 Pause(구독만 풀고 보낸 기록 · rev 유지), 펼칠 때 Resume 이 그 사이 바뀐 것만 add · patch · remove 로 보낸다
- chosen: (b)
- why: (a) 는 2D 를 쓰는 내내(추적 타겟 50 ms 갱신 · 아이콘 다시 그리기) GIS 에 일을 더한다. (b) 는 2D 비용 0 이고 명세의 "rev 이어서 변경분만"과 같다. 아이콘 · 이미지 픽셀은 3D 가 이미 가진 것이라 다시 보내지 않는다.
- effect: 페이지가 상태를 잃으면(overlay.resync · 새 장면 · 다시 불러오기) Stop → 다음 펼침에 전체 스냅샷. 되돌리려면 Leave3D · CancelMap3DPending · ready(Off) 의 Pause 를 빼면 된다.

## 3. 미리 불러오기 시작 · 멈춤 줌(15 와 14 사이)
- options: (a) 줌 ≥ 15 일 때만 새로 연다(14~15 는 새로 열지 않고 연 것은 둔다) (b) 14~15 히스테리시스(한 번 15 를 넘었으면 14 까지는 계속 연다)
- chosen: (a) — Map3DMath.PreloadZoom = 15, ShouldPreload 는 줌 ≥ 15 에서만 true
- why: 명세는 "≥ 15 이면 미리 연다 · 영역 밖이나 < 14 면 연 것은 두되 새로 열지 않는다". 연 것은 어느 줌에서도 닫지 않으므로 히스테리시스가 바꾸는 것이 없다.
- effect: 14~15 에서는 새 장면을 열지 않는다. PreloadZoom 하나로 조정.

## 4. 미리 불러오기가 실패한 장면을 다시 시도할까
- options: (a) 게이트 판정마다 다시 시도 (b) 같은 장면은 다시 미리 열지 않고 줌 17 진입 때 다시 연다
- chosen: (b) — 호스트가 실패한 장면 id(Job)를 그대로 들고 있어 ShouldPreload 가 false
- why: 게이트 판정은 지도가 움직일 때마다 돈다 — 서버가 꺼져 있으면 탐색 실패가 반복된다. 미리 불러오기 실패는 배너를 띄우지 않는다(진입 때 실패하면 기존처럼 배너).
- effect: 서버가 다시 켜진 뒤에는 첫 진입이 미리 불러오기 없이 연다(다른 영역을 미리 열면 다시 시도 가능).

## 5. 앞 탐색의 "취소" 완료를 실패로 볼까
- options: (a) 기존처럼 NavigationCompleted 실패를 모두 LoadFailed (b) 지금 탐색(NavigationStarting 의 NavigationId)의 실패만 LoadFailed
- chosen: (b)
- why: 미리 불러오던 장면을 진입 때 다른 장면으로 바꿔 열면 앞 탐색이 취소로 끝나고, (a) 면 그 취소가 새 진입을 실패로 접어 버린다.
- effect: Map3DViewHost.EnsureCoreAsync 의 NavigationCompleted 처리기 한 곳.

## 6. 이미지 인코딩 2048 · JPEG/PNG 를 어디까지 적용할까
- options: (a) 원본 파일이 있는 이미지(디스크 캐시 대상)만 (b) 다리가 보내는 모든 오버레이 이미지(파일을 모르는 메모리 그림도 같은 인코딩, 캐시만 없음)
- chosen: (b) — Map3DImageCache.GetOrEncode 는 파일을 모르면 Encode 만 한다. 기존 EncodeImagePng(4096 PNG)는 기존 시험이 고정해 함수로 남긴다(다리는 더 쓰지 않음).
- why: 보내는 크기 · 3D 쪽 디코드 시간을 줄이는 이유는 같다. 한 경로로 두는 것이 단순하다.
- effect: 3D 의 오버레이 이미지 해상도 상한이 4096 → 2048 px. Map3DOverlay.CachedImageSide 하나로 조정(키에 들어가 있어 바꾸면 캐시도 새로 만든다).

## 7. 아이콘 메모리 캐시 키
- options: (a) 시각 서명 그대로(마커 id 포함 — 마커별) (b) id 를 뺀 서명(같은 모양 마커끼리 공유)
- chosen: (a)
- why: 2D 마커 Shape 가 서명에 없는 값(번호 등)을 그릴 수 있어 공유하면 틀린 아이콘이 갈 위험이 있다. 다리 인스턴스(세션 동안 하나)에 두어 마커를 다시 만들어도 · 다시 들어가도 다시 그리지 않는다.
- effect: Map3DOverlayBridge._icons. 공유하려면 IconSignature 에서 id 를 빼면 된다.
