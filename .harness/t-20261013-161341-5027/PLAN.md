# PLAN — 통문 · 함체 문 열림 깜빡임 (t-20261013-161341-5027)

## 목표
문이 열려 있는 동안 지도 PIDS 심볼(2D 폴백 · 3D 하우징)이 깜빡인다. 원천 = 마커 컨트롤 `DoorStatus`(0f84f06905).

## 결정
- 새 요소 `GMapSymbols/DoorOpenMark.cs` — 심볼 **좌상단** 작은 문 표지(둥근 사각 + 빈 문틀 + 비스듬히 열린 문짝).
  이벤트 점(우상단 원) · 부품 배지(우하단) · 문 배지(좌하단)와 자리 · 모양이 다르다. 색 = `StatusInfoBrush`(파랑, DynamicResource) —
  빨강(탐지) · 주황(장애) · 앰버(경고/미저장)와 겹치지 않는다. 라이트 #15589F/표면 #F2F5F9, 다크 #4FB8FF/표면 #161D26.
- 통문 · 함체의 2D 는 `GMapMarkerPidsFallbackControl`(NeedsFallback) → 두 템플릿 모두 `Housing3DMarkerStyle.xaml` 에 있다. 3D 문짝(DoorOpen) 애니메이션은 건드리지 않는다.
- 공용 시계: 스레드(=디스패처)당 `AnimationClock` 하나를 깜빡이는 표지 모두의 Opacity 에 건다. 마지막 표지가 빠지면 시계를 멈추고 뗀다.
- 우선순위: EventStatus Detecting · Fault, MarkerState ERROR 이면 템플릿 트리거가 `IsSuppressed=True` → 표지는 고정(깜빡임 없음)으로 계속 보인다.
- 멈춤 조건: 숨김(Visibility · IsVisible) · ShowShape=False · 미리보기 · 화면 밖(지도가 뷰포트 바뀔 때 `DoorOpenMark.IsInView` 상속 값을 심볼에 내려 준다) · 애니메이션 줄이기(`SystemParameters.ClientAreaAnimation` false).
- 시험 판독: `DoorOpenMark.IsBlinking`(public get) · `IsDrawn` · 공용 시계 상태.

## 발견
- 창 없는 시험 스레드에서 Forever 시계를 걸거나 다시 그리기(InvalidateVisual · AffectsRender)를 요청하면 렌더 패스가 돌아
  같은 스레드의 문짝 스토리보드가 앞서 나간다 → 기존 EVM-108("시계가 돌기 전 DoorOpen=0") 실패.
  해결: 시계 · 다시 그리기는 창(PresentationSource)에 붙은 표지만. 깜빡임 상태(IsBlinking)는 창과 무관하게 판정. Loaded 때 다시 그린다.
  시험은 `ClockWithoutWindow`(ThreadStatic)로 시계를 걸어 보고, 픽셀 판독 전에 직접 InvalidateVisual + Layout.
- 창 없는 지도의 ViewArea 는 실제 화면과 다르다(경도 -180~45) → 화면 밖 시험은 심볼 위치를 그 영역 밖/안으로 옮겨 본다.

## 진행
- [x] 조사
- [x] DoorOpenMark.cs
- [x] 템플릿 2개 배선 + 트리거(Housing3DMarkerStyle.xaml: 3D 하우징 · 폴백)
- [x] 지도 뷰포트 → IsInView(GMapCustomControl.RefreshComponentStripDensity)
- [x] 시험 DOOR-101~105 19건 (tests/GMaps.Housing.Tests/DoorOpenBlinkScenarioTests.cs)
- [x] dotnet test: GMaps.Housing.Tests 331/331, GMaps.PropertyPanel.Tests(하우징 템플릿 사용 3클래스) 29/29, sln 통과

## 남은 일
- 실창(헤디드) 눈 확인은 하지 않았다(실창 회차 진행 중 · 창 띄우는 시험 금지).
