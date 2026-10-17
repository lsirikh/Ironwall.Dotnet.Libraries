## 1. 네이티브 창 3D(hwnd 폴백)에서 창을 열 때
- options: (a) 지금처럼 2D 로 돌아온 뒤 연다 (b) 3D 를 유지하고 WPF Popup(별도 창)으로 띄운다
- chosen: (a) — WPF 안 3D(composition, 메인 앱 기본)에서만 3D 를 유지하고 화면 자리를 따라간다(`Map3DGuide.ShouldLeave3DForWindow`)
- why: 네이티브 창 WebView2 는 같은 자리의 WPF(카메라 상자 · 카드)를 덮는다(공역). 메인 앱 대상(net8.0-windows10.0.17763.0)은 composition 이 기본이라 폴백은 드물다. 창을 새로 만들지 말라는 요구와도 맞다
- effect: hwnd 폴백에서는 창을 열면 2D 로 돌아온다(전과 같음). 2D 에서 연 창을 둔 채 hwnd 3D 로 들어가면 창이 3D 밑에 가려진다. 바꾸려면 `ShouldLeave3DForWindow` 와 창 호스트를 Popup 으로 바꾼다

## 2. 3D 에서 카메라 상자 자리 규칙
- options: (a) 위경도 앵커(상자 좌상단)를 viewer 가 3D 로 투영한 자리 (b) 마커의 3D 화면 자리 + 2D 화면 오프셋(앵커 − 마커)을 3D 줌 차(2^Δz)만큼 키우고 방위 차만큼 돌린 자리
- chosen: (b)
- why: 기울인 3D 에서 앵커 지점(마커에서 100 px + 상자 높이 위쪽 땅)은 멀리 눌리거나 지평선 너머로 가 상자가 마커에서 떨어진다. (b)는 2D 와 같은 "마커 옆 오프셋 · 줌 · 회전을 따라감" 규칙을 화면에서 지킨다. 3D 에서 끌어다 놓으면 역변환으로 2D 앵커를 저장해 2D 로 돌아가도 같은 관계다
- effect: `Map3DScreen.PopupTopLeft3D` · `DraggedAnchor2D` 한 곳. 기울기(pitch)에 따른 오프셋 눌림은 반영하지 않는다

## 3. 마커가 3D 카메라 뒤로 갔을 때
- options: (a) 상자를 마지막 자리에 둔다 (b) 상자를 감춘다 (c) 화면 가장자리에 가둔다
- chosen: 카메라 상자 = (a), 조립 카드 = 감춤(좌표를 다시 받으면 나타남)
- why: 2D 규칙은 "감추지도 가두지도 않고 앵커를 따라감"(FR-A1). 카메라 앞이면 화면 밖이어도 좌표를 받아 그대로 따라가고, 좌표가 뜻이 없는 카메라 뒤만 예외다. 상자를 감추면 영상 · PTZ 조작 중 상자가 사라져 혼란스럽다. 카드는 2D 에서도 아이콘 옆에만 뜨는 작은 정보판이라 자리를 모르면 감춘다
- effect: `OnMap3DScreens`(x · y 가 null 이면 기록을 지움) · `ComponentCardService.PlaceOverlay`(anchor null → Hidden)

## 4. 3D 위 조립 카드를 띄우는 방식
- options: (a) 3D 호스트 위 Canvas(`Map3DViewHost.OverlayLayer`)에 같은 VM 으로 카드 보기를 붙인다 (b) 3D 호스트를 지도 AdornerDecorator 안으로 옮겨 지도 어도너 층을 3D 위로 (c) 지금처럼 2D 로 돌아온다
- chosen: (a)
- why: (b)는 라벨 · 선택 · 조준 등 모든 2D 어도너가 3D 위에 떠 버린다. (c)는 요구(3D 에서 같은 창)와 다르다. (a)는 카드 VM · 배치 규칙(ComponentCardPlacement)을 그대로 쓰고 전환 때 VM 을 닫지 않고 옮긴다(카드 보기 컨트롤만 새로 만든다)
- effect: `ComponentCardService.UseOverlay(layer, anchor)` · `Relayout()` 공개 메서드 추가(`IComponentCardHost` 는 그대로)

## 5. 옛 판정 함수 `Map3DGuide.ShouldLeave3DForPanel`
- options: (a) 함수 동작을 바꾸고 기존 시험 기댓값을 고친다 (b) 새 함수 `ShouldLeave3DForWindow` 를 더하고 옛 함수는 그대로 둔다
- chosen: (b)
- why: 기존 시험을 고치지 말라는 작업 규칙 · 공개 형식은 더하기만
- effect: 옛 함수는 시험에서만 쓰인다(문서 주석에 "화면 자리를 받기 전의 옛 규칙"). 필요 없으면 시험과 함께 지우면 된다

## 6. GIS ↔ 3D 화면 좌표 메시지 모양
- options: (a) GIS 가 지켜볼 목록(`overlay.watch`, 통째로 바꾸기)을 주고 3D 가 그린 프레임 뒤 바뀐 항목만 `overlay.screen {items:[{key,x,y,visible}]}` 로 묶어 보냄 (b) 3D 가 모든 마커 화면 좌표를 계속 보냄 (c) GIS 가 필요할 때마다 묻기
- chosen: (a) — 항목은 마커 kind · id(3D 가 그린 머리 자리) + 위경도(마커가 3D 에 없을 때 땅 위 지점)
- why: 열린 창 수만큼만, 카메라 · 렌더가 바뀔 때만(마우스 이동마다 아님 · 0.5 px 넘게 바뀐 것만) 보내 전송량이 작다. 같은 목록을 다시 보내면 "지금 자리 요청"이 된다
- effect: viewer `overlay-screen.js` · GIS `Map3DScreen`. 목록은 3D 를 펼칠 때 · 상자를 열고 닫을 때 · 3D 에서 카드를 열 때 보낸다
