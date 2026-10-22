# DECISIONS — 3D 건물 고르기를 2D 마커 고르기처럼 · 판 글자 잘림

## 1. Ctrl+Shift+끌기(building.box add=true)는 2D 처럼 "겹치면 빼기(토글)"인가, "더하기만"인가
- options: (a) 2D 러버밴드(OnMarkersRubberBandSelected)처럼 상자 안 것 중 이미 고른 것은 빼고 나머지는 더함 (b) 목표 원문대로 지금 고름에 더하기만(합집합)
- chosen: (b)
- why: 목표가 "Ctrl+Shift+드래그 = 지금 고름에 더함" · "Shift+드래그 = 상자 안 건물을 모두 고름(새로)" 으로 정했다(목표가 2D 세부보다 우선). pattern: follow-recommended
- effect: `MapViewModel.BuildingErase.cs` `OnMap3DBuildingBox` 의 `add` 분기 한 곳 — 토글로 바꾸려면 이미 있는 id 를 지우게 고친다.

## 2. Shift 만 누른 클릭(엔진 building 메시지 shift=true · ctrl=false)
- options: (a) 일반 클릭(그 건물 하나만) (b) Ctrl+클릭과 같이 넣기 · 빼기
- chosen: (b)
- why: 2D 에서 Shift 를 누르고 누르면 러버밴드가 시작돼 기존 고름이 유지된다(지금 고름을 잃지 않음). 3D 에서도 Shift 를 누른 채 거의 안 끌고 놓은 클릭이 고름을 날리지 않게 한다. pattern: follow-recommended(덜 놀라운 쪽)
- effect: `OnMap3DBuildingPicked` 의 `ctrl || shift` 한 줄 — 일반 클릭으로 바꾸려면 `shift` 를 뺀다.

## 3. Ctrl+클릭으로 고름에 넣거나 뺄 때 판(「건물 높이」)은 어느 건물을 보이나
- options: (a) 방금 넣은 건물로 옮김 · 뺀 것이 판의 건물이면 남은 것 중 마지막으로 (b) 판은 처음 건물에 그대로
- chosen: (a)
- why: building.select 계약의 id = "판에 열린 건물" — 사용자가 마지막으로 누른 건물을 판이 보이는 것이 2D(마지막에 누른 마커가 편집 대상)와 같고, 옛 판 동작(넣으면 그 건물로 판이 옮겨 감)과도 같다. 모두 빼면 판을 닫는다(2D "선택 해제"). pattern: follow-recommended
- effect: `ToggleBuildingSelection` 안 두 줄.

## 4. building.select 의 ids 를 문자열로 보내나 정수로 보내나
- options: (a) 계약대로 문자열 배열 `["9","11"]` (b) id 와 같은 정수
- chosen: (a)
- why: 목표의 계약 원문이 `ids?:[string…]` 이고, 엔진은 다른 세션이 같은 계약으로 고친다 — 계약을 그대로 따라야 양쪽이 맞는다. id(판의 건물)는 기존대로 정수.
- effect: `Map3DBuilding.SelectMessageJson` 한 곳.

## 5. 일반 클릭 때 GIS 가 building.select 를 보내나
- options: (a) 매번 보냄 (b) 여러 개 고른 상태에서 하나로 줄 때만 보냄(하나 → 하나는 엔진이 스스로 강조를 옮긴다)
- chosen: (b)
- why: 기존 시험 · 계약이 "고른 건물 강조는 뷰어가 이미 그렸다 — 고르기만으로는 3D 를 바꾸지 않는다"(TwinContract · PickExclusive 시험)로 고정돼 있고, 여러 개 강조를 하나로 되돌릴 때만 GIS 가 알릴 필요가 있다. Ctrl+클릭 · 상자는 늘 보낸다.
- effect: `OnMap3DBuildingPicked` 의 `wasMany` 조건.

## 6. [✓ 확정](3D 손잡이 edit.commit)과 [✓ 적용](판 저장)을 어떻게 하나로 합치나 (추가 요청 1)
- options: (a) 손잡이 고침은 바로 저장 · 판에는 [적용] 만 (b) 모든 고침을 [적용] 한 번에 모아 저장 (c) 둘 다 남김
- chosen: (a)+보완 — 판의 옮기기 · 돌리기 · 크기 단추는 누를 때마다 바로 저장(뷰어에 키 + Enter → edit.commit → 저장 · 편집 기록 1칸), [✓ 확정] 단추는 없앰. 3D 손잡이를 마우스로 끈 것은 [✓ 적용] 이 Enter 를 먼저 보내 함께 확정한다(3D 키보드 Enter 도 그대로).
- why: 2D 마커는 화살표로 옮기면 바로 반영 · 되돌리기 가능하고, 속성창 값은 [적용] — 같은 흐름. 로그(15:54:45 확정 저장 → 15:54:46 적용 저장)처럼 두 단추를 차례로 누르던 이중 저장을 없앤다. 엔진 계약은 그대로(edit.key Enter). pattern: follow-recommended
- effect: `MapViewModel.Edit3D.cs Edit3DKeyCommand`(키 목록) · `CommitPendingEdit3D` · `UserBuildingPanelStyle.xaml`. 꾸미기 판도 같은 템플릿이라 같은 흐름. 확정 단추를 되살리려면 XAML 단추와 자동 Enter 한 줄.

## 7. [지우기] 두 개 → 하나 — 무엇을 남기나
- options: (a) 꼭짓점 지우기 단추를 빼고 [이 건물 지우기] 만 (b) 꼭짓점 지우기를 「꼭짓점 빼기」 로 이름만 바꿈
- chosen: (a) — 꼭짓점 지우기는 3D 의 Delete 키("?" 말풍선에 있음), 판의 건물 지우기는 「이 건물 지우기」 로 이름을 분명히(한 줄 전체 폭).
- why: 사용자 요청이 "[지우기] 두 개 → 하나". 꼭짓점 지우기는 손잡이 꼭짓점을 고른 뒤에만 뜻이 있어 3D 키로 충분하다.
- effect: XAML 단추 하나 — 되살리려면 `Map.Edit3D.VertexDelete` 단추를 다시 넣는다.

## 8. 「2D 윤곽 고치기 뒤 3D 로 들어올 때」 결함 — 어떻게 고치나 (추가 요청 2)
- 증거: ① 설치본 로그 15:51:51 「2D 에서 고치기로」 → 15:54:24 「편집 · 그리기 중에는 3D 로 들어가지 않습니다」 → 15:54:28 판 닫기(cause=close) → 15:54:32 진입 → 15:54:39 같은 건물을 3D 에서 다시 고름 ② 코드: `IsMap3DBlockedByInputMode` 가 `(IsUserBuildingDrawing && !_ubIn3D)` 로 막고 2D 로 옮긴 판을 3D 위 판으로 되돌리는 길이 없었다(재현 시험: 고치기 전 코드에서 3D 상태가 2D 로 남음).
- options: (a) 3D 들어가기 때 그 영역 「건물 만들기」 판을 3D 위 판으로 함께 데려간다 (b) 판을 저장 · 닫으라고 안내만
- chosen: (a) — 고치던 건물의 고름(userbuilding.select) · 손잡이(edit.start) · 저장 안 한 값의 상자(preview)를 펼칠 때 다시 보낸다. 2D 에서 점을 찍던 중(닫지 않은 윤곽)은 3D 로 이어 그릴 수 없어 예전대로 거절. 3D 를 못 펼치면(준비 취소 · 실패) 판은 2D 판 그대로.
- why: [2D 에서 윤곽 고치기] 의 반대 길이 없어서 생긴 일 — 판을 닫고 다시 고르게 하는 것은 사용자가 말한 "버그". pattern: broad
- effect: `MapViewModel.Edit3D.cs CarryUserBuildingPanelTo3D · ResumeUserBuildingIn3D · CancelUserBuildingCarry3D`, `MapViewModel.Map3D.cs` 세 곳. 옛 시험 `Map3DCompositeScenarioTests.should_refuse_3d_while_drawing_building_started_in_3d…` 는 빈 판 거절을 단정하던 것을 "점을 찍던 중이면 거절" 로 고쳤다(그 시험의 나머지 — 그리기를 끝낸 자리에서 곧바로 다시 들어가지 않음 — 는 그대로).

## 9. 여러 건물 속성 함께 적용 — 칸 판정 · 섞인 종류 (추가 요청 3)
- options: 칸 판정 (a) 판을 연 뒤 손댄 칸만 (b) 판 값과 다른 칸 모두 / 섞인 종류 (c) 지도 건물 + 직접 그린 건물을 한 고름에 (d) 서로 배타(고르면 다른 쪽 판 · 고름이 닫힘)
- chosen: (a) + (d). 값이 서로 다른 칸은 「여러 값」(지도 건물: 높이 입력칸에 "여러 값" · 「고른 건물 n개 · 여러 값: …」 줄 / 직접 그린 건물: 「[✓ 적용] = 고른 n개에 바꾼 칸만 · 여러 값: …」 줄), 손대지 않으면 건물마다 그대로. 지도 건물: 높이([적용]) · 외관([외관 적용])을 따로, 되돌리기 1칸씩(판 안 되돌리기). 직접 그린 건물: 높이 · 층수 · 지붕 · 쓰임 · 외벽 무늬 · 색 · 좁게/넓게를 [✓ 적용] 한 번, GIS 편집 기록 1단위(MacroCommand). 직접 그린 건물 고르기는 2D Ctrl+클릭(기존) + 3D Ctrl+클릭(새로).
- why: 2D 마커 여러 개 속성창 정본(필드 단위 보류 · 그룹 모드 · 바꾼 필드만 · 되돌리기 한 번)과 같게. 두 종류는 저장 창구(heights/looks vs user-buildings) · 판이 달라 한 고름에 섞으면 칸 뜻이 달라진다 — 지금도 판이 서로 배타(2026-10-22 앞 작업 결정). pattern: follow-recommended
- effect: `MapViewModel.BuildingErase.cs ApplyBuildingHeightGroupAsync · ApplyBuildingLookGroupAsync · BuildingMixedFields`, `MapViewModel.UserBuilding.cs UbGroupFields · LoadUserBuildingGroupFields · ApplyUserBuildingGroupAsync`. 3D 미리보기는 판의 건물 하나만(저장하면 모두 3D 에 바로 반영).
