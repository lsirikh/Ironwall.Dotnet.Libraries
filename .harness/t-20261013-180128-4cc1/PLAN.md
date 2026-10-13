# PLAN — 실창 4회차 지도 쪽 제품 결함 6건 (t-20261013-180128-4cc1)

## 목표
round4-results.txt 의 지도 결함 6건: 각 건 원인 확정 → 고침 → 헤드리스 시험(빨강→초록). 시험 쪽 문제면 근거와 함께 보고만.

## 진행
- [x] 1. NUM-R29-B6 탐지범위 다시 연 뒤 NaN — 시험 쪽(헤드리스 초록)
- [x] 2. NUM-R29-A1/B1 범위 끝 키(Home/End) — 시험 쪽 / 회전 360=0 설계
- [x] 3. NUM-R29-DEL 자기 심볼 UI 삭제 실패 — 시험 쪽(로그 근거)
- [x] 4. 통문 심볼 우클릭 메뉴 45초 안에 안 뜸 — 시험 시드(최소 줌 18)
- [x] 5. OPC-H06 운영 카드 더블클릭 → 지도 이동 안 함 — 시험 쪽(처리기 로그 0줄)
- [x] 6. FenceGateRuntimeDiag ① 그리기 정점 수 2(기대 3) — 시험 쪽(HUD 가림)

## 결정 · 발견
- 시험 코드 위치: C:\workspace_app\Dotnet.Monitoring.Solution\Dotnet.Monitoring.Solution.UiTests\Functional\MapSliderPrecisionR29Tests.cs (읽기만)
- B6 의 NaN 은 시험의 SliderValue() 가 슬라이더를 못 찾았을 때 내는 값(double.NaN) — 제품 슬라이더 Value 는 NaNBlank 변환기 때문에 NaN 이 될 수 없다(NaN→0.0).

- 1·2번 헤드리스 재현(tests/GMaps.PropertyPanel.Tests/MapRound4NumericScenarioTests.cs — Property3DRig, 앱 PidsPropertyStyle 의 진짜 슬라이더,
  Slider.MinimizeValue/MaximizeValue = 키보드 Home/End 명령, VM HidePropertyPanel → 다시 ShowPropertyPanel):
  탐지범위 Home=10 · End=1000 · 297.4→297, 다시 연 뒤 297/98 그대로 → **처음부터 초록**. 제품 경로 정상.
  실창 B1 은 모든 단계 값이 30(끌기 · 키 모두 무반응) · B6 은 슬라이더 자체를 못 찾음(NaN = 시험 SliderValue(null)) → 입력/판독이 그 슬라이더에 닿지 않은 시험 쪽 문제로 판정.
- 회전 End → 0: GMapBaseMarker.Bearing 세터가 360 을 0 으로 감는다(while value>=360 value-=360) — PRD 표 '마커가 0~360 감음'과 같은 설계. 360°=0° 이라 제품 결함 아님, 보고만.

- 6번: 정점 로직 회귀 아님. 시험(FenceGateRuntimeDiagTests.cs)의 p1·p2·p3 가 같은 줄 130px 간격 → p3(p1+260)가 드로잉 HUD
  (LineDrawingAdorner.cs:309-311 첫 정점 +(20,-50), 폭 240) 본문 안 → HUD 가 클릭을 먹음. 증거: fg-01-drawing.png 의 HUD '2 점',
  p3 를 다른 줄에 둔 회차(09-08 06:38 · 15:31)는 3. 드로잉 파일의 최근 변경(1c8df85c)은 우클릭 후보 정렬만. → 시험 쪽, 보고만.

- 4번: 시험 시드 문제. E2EDoorSeed.cs:390 이 통문 심볼(Id 316, 장비 5219) 최소 줌을 18 로 넣는데 실창 실효 줌 16.5(로그 '줌 변경됨: 16' +
  디지털 1단) → ZoomLadder.IsVisibleAtEffectiveZoom 으로 숨김 → GetMarkerAtScreen(GMapCustomControl.cs:1492)이 건너뜀 → 어떤 메뉴도 안 뜸
  (운영 모드 우클릭 로그에도 '마커 컨텍스트 메뉴 표시' 없음). 3D 하우징 적중 AABB · 오프셋 · 크기는 그림과 일치. → 보고만(시드 Zoom=0 권장).

- 1·3번 공통 근거(앱 로그 Dotnet.Monitoring.Solution\...\Logs\log-2026-10-13.txt 25912~26163줄): 17:55:39 제목 R29-SYM-B 변경 뒤
  탐지범위/각도 '속성창 변경에 의한 마커 속성 변경' 로그가 0줄, 대신 17:56:10~17:56:38 지도 줌이 17→8→18→8→18 로 오르내림(1단씩 · 묶음).
  = 시험 EnsureLabelVisible 의 Mouse.Scroll(_panelHover) 가 속성창이 아니라 지도에 떨어짐 → 슬라이더 입력이 닿지 않음(B1 전부 30),
  다시 열기 · 삭제 단계에서 심볼 화면 좌표(spots)가 줌 이동으로 낡아 선택/삭제 실패. 그 시각 ShowPropertyPanel · 삭제 로그 0줄.
  제품 삭제 경로(MapViewModel.ExecuteDeleteSelected → 확인 팝업 → DbDeleteProcess)는 조사상 이상 없음 → 시험 쪽, 보고만.

- 5번: 더블클릭은 카드 종류 무관 ListBoxEvents 한 곳(EventCardListPanelView.xaml:125 → OnClickEventCard → LocateCardAsync → MapLocateRequest).
  운영 카드 XAML(OperationEventCardView)엔 본문을 덮는 단추 · 별도 DataContext 없음, 헤드리스 OPC-013 이 운영 카드 → MapLocateRequest 확인.
  실창 17:49:31~38 구간에 처리기 로그('[지도에서 보기]' · '생략' · '처리 실패') 0줄 = 더블클릭이 목록에 닿지 않음(H06 은 셸 전면화 · 선클릭 없이
  본문 중앙 더블클릭). 탐지 카드는 같은 처리기로 10-08 실창 동작 기록 있음. → 제품 결함 근거 없음, 시험 쪽으로 판정 · 보고만(Events.Ui 무변경).

## 결론
6건 모두 제품 코드 변경 없음(원인 = 시험 입력/시드/좌표). 1·2번 헤드리스 회귀 시험 2개 추가(초록).

## 남은 일
없음. 시험 쪽 고칠 곳(다른 솔루션 · 이 작업 범위 밖): R29 EnsureLabelVisible 휠 위치 · 삭제 전 좌표 재산출, FenceGate p3 를 HUD 밖으로,
E2EDoorSeed 통문 Zoom=0, OPC-H06 더블클릭 전 ForegroundHelper.Force + 선클릭.
