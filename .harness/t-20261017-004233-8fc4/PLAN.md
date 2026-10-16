# PLAN — 지도 3D 바꿔 끼우기 2차(충돌 제거 · 오버레이 다리 · 3D 클릭)

## 목표
직전 작업(t-20261016-180252-5221)의 3D 배선에 이어 ① 입력 모드 충돌 제거 ② 마커를 3D 로 보내기 ③ 3D 클릭 받기. 메시지 규약은 작업 지시 원문 그대로(map-3d 쪽은 이 저장소 밖).

## 진행
- [x] 1단계 충돌 제거
  - `Map3DMath.IsBlockedByInputMode` + `MapViewModel.Map3D.IsMap3DBlockedByInputMode`(편집 모드 || ExclusiveInputModeGate) → EvaluateMap3DGate · Begin3D 에서 막음
  - `SyncMap3DWithInputModes()` — 모드 켜짐: Shown 이면 Leave3D(마지막 3D 카메라), Pending 이면 취소 / 모드 꺼짐: 게이트 재판정
    - 호출: 조준 진입·종료(MapViewModel.cs), 심볼 배치 진입·종료, 편집 모드 세터, 선 그리기 StateChanged, 홈 배치(Home.cs), 앵커/3D 영역 그리기(Anchor.cs), 측정 시작 · MeasureStopped(Measure.cs)
  - 이벤트 지도 이동: 출발 전 `Sync2DFrom3DCamera`, 끝나면(애니 끝 · 순간 이동) `Sync3DAfter2DMove` / 지도에서 보기: SetZoomToFitRect 뒤 `Sync3DAfter2DMove`
  - `Map3DMath.ShouldStay3DAfterMove`(지금 장면 안 && 줌 ≥ 16.5) 아니면 Leave3D(null)
- [x] 2단계 오버레이 다리
  - 순수: `Utils/Map3DOverlay.cs`(DTO 레코드 · ToJson · Diff · 메시지 4종)
  - WPF: `Services/Map3D/Map3DOverlayBridge.cs`(Markers.CollectionChanged + 마커 PropertyChanged 구독, 50 ms 코얼레싱, 직전 전송과 Diff → patch)
  - Reveal3D 서두에서 Start(스냅샷), Leave3D 에서 Stop, overlay.resync 면 스냅샷 재전송
  - `Map3DViewHost.PostJson` 추가
- [x] 3단계 3D 클릭
  - `Map3DMath.ParseMessage` 가 pick · overlay.resync 해석(`Map3DMessage.Pick` init 속성 + `Map3DPick` 레코드 — 기존 생성자 유지)
  - `OnMap3DPick`: kind+id → 마커, click/dblclick(구역=TriggerGroupActionReport)/context, 빈 곳 click = 지도에서 보기 강조 해제 · 조립 카드 닫기
  - 우클릭 메뉴: `_map3DMenuAt`(지도 컨트롤 좌표)가 있으면 PlacementTarget=MainMap · Relative 오프셋
  - 조립 카드: OnMarkerPressed(marker, 좌표) + OnReleased(좌표)
- [x] 단위 시험 `tests/GMaps.Ui.Tests/Map3DOverlayTests.cs`(csproj 에 Map3DOverlay.cs · EnumCompositeEventStatus · EnumOperationState 링크)
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` 종료 코드 0(3회차). 1 · 2회차는 이번 변경과 무관한 시간 의존 시험(CameraPopup.Tests EventWindowManagerTests 시간 예산 · Utils.Tests PerfSamplerTests 호스트 중단)이 부하로 흔들렸고, 단독 실행은 통과.
- [x] GMaps.Ui.Tests(솔루션 밖) 903/903 통과

## 발견
- 독점 모드 플래그(IsTargetAimMode 등)는 변경 통지가 없는 일반 속성 → VM 진입·종료 지점마다 호출해야 한다.
- 상세 창(SymbolDetailPanel) · 카메라 팝업(지도 위 상자) · 조립 카드(어도너)는 지도 칸 안의 WPF 라 펼친 WebView2(네이티브 창)에 가려진다(공역). 우클릭 메뉴(ContextMenu = Popup, 자기 HWND)는 위에 뜬다. → DECISIONS 1.
- Newtonsoft 의 string → JToken 암묵 변환은 null 을 String 형 null 로 만든다 → `Str()` 로 JSON null 명시.

## 남은 일(다음 단계 후보)
- 공역: WebView2CompositionControl 검토 또는 상세 · 팝업을 열 때 2D 복귀(DECISIONS 1)
- 이미지 · TIF · 추적 · 도형 · 군사부호 · 시설물 오버레이
- 실기 확인(미검증): 3D 위 우클릭 메뉴 위치 · 팝업 표시 · pick 왕복
