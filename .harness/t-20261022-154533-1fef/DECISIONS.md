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
