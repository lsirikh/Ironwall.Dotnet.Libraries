# DECISIONS

## 1. 3D 위에서 가려지는 WPF 창(상세 창 · 카메라 지도 위 상자 · 조립 카드)을 어떻게 할지
- options: (a) 코드로 확인만 하고 보고(동작 변경 없음) (b) 그 창을 열 때 2D 로 돌아오기 (c) WebView2 를 WebView2CompositionControl 로 바꿔 공역 자체를 없애기
- chosen: (a)
- why: 지시가 "코드로 확인" 이고 범위 확장을 금지했다. (b)는 사용자 동작을 바꾸고, (c)는 호스트 교체라 실기 검증 없이 넣기 위험하다.
- effect: 3D 에서 마커를 클릭 · 더블클릭 · 메뉴 "상세 보기" 하면 경로는 실행되지만 상세 창 · 카메라 상자 · 조립 카드는 WebView2 뒤에 그려져 보이지 않는다(우클릭 메뉴는 Popup 이라 보인다). 바꾸려면 OnMap3DPick 또는 ShowSymbolDetail · OpenCameraStreamPopupAsync 앞에서 Leave3D 를 부르거나 Map3DViewHost 의 WebView2 를 CompositionControl 로 바꾼다.

## 2. patch 에 무엇을 담을지
- options: (a) 변경 통지마다 항목 전체 필드 (b) 직전에 보낸 JSON 과 비교해 바뀐 필드만(state 는 바뀐 하위 필드만)
- chosen: (b)
- why: 규약이 "일부 필드" 를 허용하고, 선택 · 크기 등 3D 와 무관한 PropertyChanged 를 자연히 걸러 보낼 양이 준다.
- effect: 바뀐 게 없으면 아무것도 보내지 않는다. 전체를 보내려면 Map3DOverlayBridge.Flush 에서 Diff 대신 item 전체를 set 으로 쓰면 된다.

## 3. "symbol"(기타 점 심볼) 대상과 필드
- options: (a) GMapCustomMarker 만 (b) 이미지 · 시설물 · 군사부호까지 점으로
- chosen: (a) — deviceType · model · composite · door 는 null, operation 만 보낸다.
- why: 지시가 이미지 · 시설물 · 군사부호 · 도형을 이번 범위에서 뺐고, 남는 점 심볼(모델 ISymbolModel)이 GMapCustomMarker 뿐이다.
- effect: Map3DOverlayBridge.KindOf/BuildItem 에 case 를 더하면 확장된다.

## 4. pids 의 door 값
- options: (a) 항상 EnumDoorState 이름(Unknown 포함) (b) 통문(Gate)일 때만, 아니면 null
- chosen: (a)
- why: 지시가 "enum 이름 문자열 그대로" 라 규칙을 더하지 않았다.
- effect: 통문이 아닌 장비도 "Unknown" 이 간다. 바꾸려면 BuildItem 에서 DeviceType == Gate 일 때만 DoorState 를 넘긴다.

## 5. 3D 중 2D 이동 처리 범위와 카메라
- options: 이벤트 비행 경로만 / 공용 FlyTo 완료 지점 전체, 방위 · 기울기는 2D 값 / 3D 마지막 값
- chosen: 공용 FlyTo 완료 지점(지금 호출자는 이벤트 비행뿐)에서 보내고, 방위 · 기울기는 3D 마지막 카메라 값을 유지. 출발 전에 3D 마지막 카메라를 2D 에 맞춰 이동 계획이 보이는 자리에서 시작하게 함. 장면 밖 · 줌 < 16.5 면 카메라를 되돌리지 않고 2D 로(2D 가 이미 목표 자리).
- why: 사용자가 3D 에서 돌려 둔 시점을 이동 때문에 잃지 않게, 그리고 2D 가 3D 진입 시점에 멈춰 있어 "이미 중심" 으로 판정돼 이동이 생략되는 일을 막으려고.
- effect: Sync3DAfter2DMove · Sync2DFrom3DCamera(MapViewModel.Map3D.cs) 에서 조정.

## 6. 3D 빈 곳 클릭이 하는 일
- options: (a) 지도에서 보기 강조 해제만 (b) + 조립 카드 닫기(2D 빈 곳 클릭과 같게)
- chosen: (b)
- why: 2D 빈 곳 클릭도 카드를 닫는다(ComponentCardService.OnEmptyPressed/OnReleased). "빈 곳 클릭 처리" 에 포함된다고 봤다.
- effect: OnMap3DPick 의 빈 곳 분기에서 두 줄을 빼면 (a).

## 7. 모드가 끝난 뒤 3D 재진입
- options: (a) 게이트를 다시 판정해 조건이 맞으면 자동 재진입 (b) 다음 줌 · 이동 때까지 보류
- chosen: (a)
- why: 지시가 "모드가 끝나면 게이트를 다시 판정한다".
- effect: 줌 ≥ 17 · 장면 안이면 모드 종료 즉시 3D 로 돌아간다. 보류하려면 SyncMap3DWithInputModes 에서 Leave3D 때 _map3DEntrySuppressed = true 로.
