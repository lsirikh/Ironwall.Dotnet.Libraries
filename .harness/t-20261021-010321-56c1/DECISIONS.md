# DECISIONS — t-20261021-010321-56c1 (3D 지도 꾸미기 GIS 쪽)

## 1. 3D 에서 직접 그린 건물을 고르면(userbuilding.pick · user_uid 있는 building) 어디서 고치나
- options: (a) 3D 를 나가 2D 의 「건물 그리기」 판을 그 건물 고치기로 연다 (b) 3D 위에 판만 띄우고 3D 에 머문다
- chosen: (a)
- why: 계약 B 가 "2D 의 직접 그린 건물 고치기 판"이라 하고, 윤곽 고치기 표면(UserBuildingAdorner)은 2D 지도에만 있다. 기존 StartUserBuildingDraw 가 이미 3D → 2D 전환을 한다.
- effect: 3D 에서 고르면 2D 로 바뀐다. 바꾸려면 MapViewModel.UserBuilding.OnMap3DUserBuildingPicked 에서 StartUserBuildingDraw 대신 판만 여는 경로를 만들면 된다.

## 2. 「건물 높이」 판의 [이 건물 지우기](계약 D)와 B 의 "user_uid 있으면 2D 판으로" 가 겹침
- options: (a) user_uid 있는 건물은 B 대로 2D 판으로 보내고, 높이 판의 [이 건물 지우기]는 origin=user · uid 를 아는 경우에만 켠다 (b) 높이 판에서 지우기를 우선
- chosen: (a)
- why: 사용자 요청 2 가 "3D 에서 선택도 되지 않는다"이므로 고치기 판으로 가는 것이 우선. D 의 단추 글 · 동작(user-buildings DELETE)은 그대로 구현해 두었다.
- effect: 지금 엔진 메시지에서는 user_uid 가 있으면 높이 판이 열리지 않으므로 [이 건물 지우기]는 거의 보이지 않는다. 옛 엔진(user_uid 없음)에서는 꺼진 채 보인다.

## 3. 색 입력 방식
- options: (a) 이름 붙은 색 목록 콤보("자동" + 12색) (b) "#rrggbb" 직접 입력칸 (c) 색 고르기 창
- chosen: (a)
- why: 쉬운 말 규칙 · 기존 콤보 관용구와 같고 잘못된 값이 들어갈 수 없다. 값은 계약대로 "#rrggbb" 소문자 또는 null.
- effect: 목록에 없는 색(엔진 · 다른 곳에서 넣은 색)은 콤보가 비어 보이지만 값은 그대로 유지된다. 목록은 Utils/Map3DLook.Colors 한 곳에서 바꾼다.

## 4. 「건물 높이」 판 외관 저장 단추
- options: (a) 높이 [적용] 과 따로 [외관 적용] (b) [적용] 하나로 높이 · 외관을 함께
- chosen: (a)
- why: 높이 · 외관은 엔진 경로가 다르고(…/height · …/look), 기존 높이 흐름 · 시험을 그대로 둘 수 있다. 판 안 되돌리기 칸은 하나로 함께 쌓는다.
- effect: 단추가 하나 늘었다. 합치려면 ApplyBuildingHeightAsync 에서 IsBuildingLookChanged 면 SaveBuildingLookAsync 도 부르면 된다.

## 5. 숨긴 뒤 「건물 높이」 판을 닫을지
- options: (a) 판을 연 채 두고 숨김 상태 · 숨긴 목록을 보인다 (b) 판을 닫는다
- chosen: (a)
- why: 숨긴 건물은 3D 에서 다시 누를 수 없으므로, 판이 열려 있어야 바로 [되돌리기] · [되살리기] 를 할 수 있다.
- effect: 숨긴 뒤 [이 건물 숨기기] 는 흐려지고, 강조 윤곽은 지운다(building.select null).

## 6. 모임지붕(hip) · 외쪽 지붕(shed) 판정
- options: (a) 박공과 같은 "네모에 가까운 모양" 판정(CanGable) (b) 늘 된다고 본다
- chosen: (a)
- why: 둘 다 직사각형 바탕 지붕이라 엔진도 박공과 같은 조건일 가능성이 크다. 계약에 판정 규칙이 없어 "이 모양은 평지붕만 됩니다" 안내를 박공과 같이 띄운다.
- effect: 엔진 규칙이 다르면 Map3DUserBuilding.CanBuildRoof 한 줄만 바꾸면 된다.

## 7. 꾸미기 놓기 · 옮기기 흐름
- options: (a) 점 도구는 클릭 즉시 저장, 선 · 영역은 Enter/두 번 누르기/[마치기] 로 저장, 옮기기는 [다시 놓기] (b) 놓은 뒤 [저장] 을 따로 누름
- chosen: (a)
- why: 나무 · 소품을 여러 개 빨리 놓는 흐름. 계약 C 의 "지도 위 새 드래그 금지 · 옮기기는 다시 놓기" 를 따른다. 저장마다 편집 기록 1단위라 되돌리기로 취소할 수 있다.
- effect: 저장 전 미리보기 메시지(decoration.preview · decoration.clear)는 보내지 않는다(꾸미기는 2D 에서 하고 저장 즉시 decorations.reload). 필요하면 Map3DDecoration 에 메시지를 더해 PlaceDecorationAsync 앞에서 보내면 된다.

## 8. 3D 에서 꾸미기 항목을 고르면(decoration.pick)
- options: (a) 3D 를 나가 2D 「꾸미기」 판을 열고 그 항목을 고른다 (b) 3D 위에서 판만 연다
- chosen: (a)
- why: 꾸미기 입력 표면이 2D 에만 있고 결정 1 과 같은 관용구.
- effect: 결정 1 과 같다(MapViewModel.Decoration.OnMap3DDecorationPicked).

## 9. 옛 GIS 호환 — 직접 그린 건물 PUT 본문
- options: (a) 외관을 넘긴 호출에서만 wall_color · roof_color · facade 키를 넣는다 (b) 늘 넣는다
- chosen: (a)
- why: 계약 B "빠지면 null/auto(옛 GIS 호환)" 이고 기존 시험(본문 키 목록)이 옛 호출의 본문 모양을 고정한다. 판 저장 경로는 늘 외관을 넘긴다.
- effect: 없음(엔진은 키가 없으면 null/auto).

## 10. 엔진 판(184925c · cd943f6)의 needs_rebake · 외벽 무늬 안내를 어디에 보이나
- options: (a) 「건물 높이」 판 외관 칸 밑 경고색 한 줄 + 상태 띠 (b) 상태 띠만 (c) 오류 줄
- chosen: (a)
- why: 사용자 지시가 "판에 안내" 이고, 오류가 아니라 다음 할 일(다시 만들기) 안내라 오류 줄(빨강)은 맞지 않는다. needs_rebake=true 면 building.look 을 보내지 않는다(장면 GLB 에 없는 건물).
- effect: 판을 닫거나 다른 건물을 고르면 안내는 지워진다. 글은 Map3DLook.NeedsRebakeText · FacadeRebakeText.

## 11. GET looks 의 새 `uid` 키 · 닫지 않은 outline
- options: (a) 지금은 쓰지 않는다(해석은 모르는 키를 무시, outline 은 받은 꼭짓점 그대로) (b) 직접 그린 건물 표시에 쓴다
- chosen: (a)
- why: 숨긴 목록은 hidden 항목만 보이고, 직접 그린 건물은 숨기지 않으므로(409) 목록에 나오지 않는다. outline 은 이미 닫는 점을 덧붙이지 않고 넓이만 셈한다.
- effect: 필요해지면 Map3DLook.ParseList 에 uid 를 읽어 Map3DLookItem 에 더하면 된다.

## 12. 품질 검사가 짚은 중복 중 남긴 것 — 파일 머리의 using 지시문
- options: (a) 파일마다 using 을 둔다(C# 은 파일 단위) (b) global using 으로 옮긴다 (c) partial 파일을 합친다
- chosen: (a)
- why: DecorationAdorner · MapViewModel.Decoration · MapViewModel.BuildingLook 의 "중복" 1-19 줄은 using 지시문(과 namespace 줄)뿐이다. global using 은 프로젝트 전체 이름 해석을 바꾸고, partial 을 합치면 이미 큰 파일이 더 커진다. 실제 코드 중복(어도너 구독 · 그리기 · 정리, 편집 기록 본문)은 공용 바탕 GeoSurfaceAdorner · AreaStateCommand 로 옮겨 두 쪽이 함께 쓴다.
- effect: 없음. 바꾸려면 GMaps.Ui 에 GlobalUsings.cs 를 두면 된다.
