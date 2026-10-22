# DECISIONS — t-20261022-164413-a54e

## 1. 판 스크롤을 어느 판까지 넣나
- options: (a) 「건물 만들기 / 고치기」 · 「건물 높이」 두 판만 (b) 같은 공용 바탕(HeightPanelControlBase)을 쓰는 「꾸미기」 판까지 세 판
- chosen: (b)
- why: 높이를 줄이는 코드가 공용 바탕(MeasureOverride)에 들어가 「꾸미기」 판도 같이 줄어든다 — 스크롤을 안 넣으면 그 판은 잘린다. 같은 결함(판이 화면 밖)이 그 판에도 있다(pattern: broad · reuse).
- effect: 세 판 템플릿의 본문이 ScrollViewer(AutomationId `Map.*.Scroll`). 한 판만 빼려면 그 판 템플릿의 ScrollViewer 를 빼고 공용 바탕 FitHeight 를 판 종류로 거르면 된다.

## 2. 판 최대 높이를 어떻게 정하나
- options: (a) 고정 MaxHeight(예: 700) (b) 지도 Canvas 안 판 자리(Canvas.Top) 아래 남은 높이 − 8 px(최소 200)
- chosen: (b)
- why: 화면 크기(1920×1040 · 1366×768)와 판을 끌어 놓은 자리에 따라 맞아야 [적용] 이 늘 판 안에 있다. 고정값은 작은 화면에서 또 넘친다(pattern: follow-recommended).
- effect: `HeightPanelControlBase.FitBottomMargin` · `FitMinHeight`. 머리 띠 끌기(OverlayHeaderDrag)가 놓을 때마다 다시 잰다.

## 3. 결함 2(높이 바꾸면 다른 속성 바뀜) — 어떤 길을 고치나
- options: (a) 사용자 실측 경로(직접 그린 건물 판 높이 칸 · 손잡이)만 다시 확인 (b) 다른 길을 코드 · 엔진 소스로 끝까지 추적해 증거가 2개 이상인 것만 고침
- chosen: (b) — 두 가지 확정: ① 「건물 높이」 판 높이 [적용] 뒤 외관이 「자동」으로(엔진 응답 줄의 `look` 안을 안 읽음 — heights.py 응답 + GIS Parse/OnBuildingHeightSaved, 고치기 전 시험 실패), ② 직접 그린 건물 판 층고가 3.0 고정이라 층수가 바뀜(ResetUserBuildingDraft 코드 + 고치기 전 시험 실패).
- why: 목표가 "다른 길을 먼저 재현해 원인을 증거로 밝힌 뒤 고친다". 직접 그린 건물 판의 높이 칸 · 손잡이 경로는 코드로도 다른 칸을 건드리지 않는다(사용자 실측과 같음).
- effect: `Map3DBuilding.Parse` · `MapViewModel.UserBuilding.ResetUserBuildingDraft`.

## 4. 직접 그린 건물 판의 층고를 무엇으로 여나
- options: (a) 늘 3.0 (b) 저장된 높이 ÷ 층수(2~10 m 안일 때, 아니면 3.0) (c) 높이를 안 바꿨으면 저장 때 층수만 그대로
- chosen: (b)
- why: 층고가 그 건물 것이어야 높이를 바꿀 때도 층수가 같은 비율로 따라가고(20 m · 5층 → 24 m 면 6층) 3D 미리보기 층 띠도 바뀌지 않는다. (c) 는 높이를 바꾸는 순간 다시 3.0 으로 계산된다(pattern: follow-recommended).
- effect: 새 윤곽(고칠 건물 없음)은 지금 층고를 그대로 둔다. 범위 밖이면 3.0.

## 5. 3D Ctrl+클릭 — 직접 그린 건물 판이 아직 안 열렸을 때
- options: (a) 그 건물을 고치기로 연다(지금대로) — 다음 Ctrl+클릭부터 넣기 · 빼기 (b) 고치기 없이 고름에만 넣는다
- chosen: (a)
- why: 이미 있던 길(「건물 높이」 판 고름 중 Ctrl+클릭한 직접 그린 건물 = 그 건물 판, 기존 시험)을 지키고, 2D 판의 "고치던 건물부터 고름에 넣는다" 와 이어진다. Shift 만 눌린 클릭도 넣기(「건물 높이」 판 DECISIONS 2 와 같게)(pattern: reuse).
- effect: `OnMap3DUserBuildingPicked`.

## 6. 3D 에서 Ctrl+C/V 키를 어디서 받나
- options: (a) 판 안 키만 (b) 3D 화면에서 누른 키도 — 지도 창 PreviewKeyDown(OnMapPreviewKeyDownForGroup)에서 3D 위 판이 열려 있으면 먼저
- chosen: (b)
- why: 3D 에서 건물을 누르면 초점이 3D 화면에 있다 — 판 키만 받으면 Ctrl+C/V 가 안 된다(사용자 "카피 페이스트 안 된다"). 복사할 건물이 없으면 키를 넘겨 심볼 복사 · 붙여넣기가 그대로 받는다(pattern: broad).
- effect: 3D 위 판이 열린 동안 Ctrl+C 는 고친 · 고른 건물 복사가 먼저다.

## 7. 붙여넣은 건물 자리
- options: (a) 마우스 커서 자리(2D 심볼처럼) (b) 복사한 묶음 폭 + 3 m 옆(동 → 서 → 북 → 남 중 3D 영역 안), 다시 붙이면 한 칸 더
- chosen: (b)
- why: 목표 "붙여넣은 건물은 겹치지 않게 조금 옆에". 3D 에는 2D 커서 자리가 없다(심볼 붙여넣기도 3D 에서는 막혀 있다). 붙인 건물은 바로 골라져 3D 손잡이 · 판 단추로 옮긴다(드래그 우선 규칙 — 옮기기는 기존 끌기 손잡이)(pattern: follow-recommended).
- effect: `Map3DUserBuilding.PasteOutlines`(틈 `PasteGapM` = 3 m). 영역 안 자리가 없으면 판 오류 줄만.

## 8. 속성 복제 범위
- options: (a) 직접 그린 건물끼리 — 높이 · 층수 · 지붕 · 외벽 성격 · 외벽 무늬 · 외벽 색 · 지붕 색 · 기울기(윤곽 · 이름 빼고) (b) 지도 건물(「건물 높이」 판)까지
- chosen: (a)
- why: 목표의 예(높이 · 지붕 · 외벽 · 색)가 「건물 만들기」 판 칸이다. 지도 건물은 이미 여러 개 고른 채 [적용] · [외관 적용] 으로 같은 값을 한꺼번에 넣는 길이 있다(새 기능을 지어내지 않음). 키는 Office 서식 복사와 같은 Ctrl+Shift+C/V(pattern: follow-recommended).
- effect: 복사 = 판에 보이는 값(저장 전이어도). 여러 개 고른 판에서는 [속성 복사] 가 꺼진다(어느 건물 값인지 모호).

## 9. 함께 본 것 — 3D 좌표 표시가 28 km 어긋남(보고만)
- options: (a) GIS 에서 고침 (b) 보고만
- chosen: (b) 보고만
- why: GIS 는 엔진 `cursor` 의 lon · lat 을 그대로 보인다(Map3DMath.ParseMessage · SetMap3DCursor, 변환 없음). 엔진은 `lonLatOf(sceneInfo.origin, x, z)`(view.js 422)로 계산하는데, 보인 값(37.39xx, 126.96xx)이 개발 엔진 data 의 다른 영역 job `20261019-183014-f366a1` 의 origin(37.394, 126.967)과 거의 같다 — 뷰어가 쓰는 sceneInfo(origin)가 보고 있는 장면이 아닌 다른 job 것이라는 뜻. 설치본 엔진(5cd00ec) data 를 보지 못해 확정하지 못했고, 추측으로 고치지 않는다.
- effect: 다음 확인 — 설치본에서 3D 를 연 job 과 뷰어가 받은 scene.json(origin) job 이 같은지(엔진 로그 · `/api/jobs/{job}/files/scene.json`).

## 10. 함께 본 것 — 3D 그리기 도구가 켜진 채 저장된 건물을 누르면 새 사각형 첫 점(보고만)
- options: (a) GIS 가 저장 뒤 그리기 도구를 끈다 (b) 엔진이 그리기 첫 점이 직접 그린 상자 위면 userbuilding.pick 으로 보낸다 (c) 보고만
- chosen: (c) — 엔진 계약 (b) 를 보고에 적는다
- why: 그 클릭은 엔진 그리기 모드가 먹어 GIS 에 오지 않는다(GIS 는 draw.point 만 받음). (a) 는 연달아 그리기("다음 건물을 바로 그릴 수 있게")를 깬다.
- effect: 지금은 Esc 로 도구를 내려놓은 뒤 누르면 열린다.
