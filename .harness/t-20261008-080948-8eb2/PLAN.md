# PLAN — 실창 레이아웃 점검 2건 수정 (t-20261008-080948-8eb2)

## 목표
1. [FAIL] 이벤트 창 조치 트레이 문구 콤보(Console.Events.Tray.Phrase) 목록이 창 밖 · 작업 표시줄까지 내려감 → 창(작업 영역) 안에.
2. [WARN] 부대 창 편제 트리 "?"(Help.Devices.Units.Tree) 말풍선이 창 오른쪽 밖으로 펼쳐짐 → 공용 HelpTip 배치 규칙을 창 우선으로.

## 원인
- 1: 콤보 팝업은 템플릿 배치(아래) 그대로, WPF 팝업은 모니터 전체(작업 표시줄 포함) 안으로만 민다 → 창 · 작업 영역 기준이 없다.
- 2: HelpTip.Place 가 경계로 **모니터 작업 영역**만 썼다 → 창 오른쪽 끝 "?" 는 화면 안이지만 창 밖으로 밀림. 공용 부품 규칙 문제.

## 결정
- 공용 `Utils/Console/PopupScreenBounds`(internal): 창 클라이언트 영역(GetClientRect+ClientToScreen) · 작업 영역을 콜백 좌표계로. HelpTip 의 기존 단위 환산 로직을 옮김.
- `HelpCalloutPlacement.Resolve(target, callout, window, screen)` 신설: 창∩작업영역 안에 통째로 들어가는 쪽이 있으면 그 안, 없으면(작은 대화창) 기존 작업 영역 판정 그대로 → 다른 창 말풍선은 창 안에 들어오던 것이면 결과 동일(시험으로 고정).
- TryFit 에 가로/세로 폭 조건 추가(말풍선이 경계보다 넓으면 그 쪽은 "들어감" 아님) — 기존 시험 결과 불변.
- 콤보: `c:DropDownFit.IsEnabled` 부착 동작 + 순수 함수 `DropDownPlacement`. PART_Popup 의 배치 콜백을 **감싼다**(MDIX ComboBoxPopup 이 콜백에서 모양 템플릿을 고르므로 원래 콜백을 먼저 부르고 넘칠 때만 옮김). 열릴 때 MaxDropDownHeight 를 위·아래 넓은 쪽 자리에 맞게 자름(처음 값 이상으로는 안 늘림) → 문구가 늘면 스크롤.
- 적용 범위는 트레이 문구 콤보만(명세 범위).
- 시험: Utils.Tests/PopupFitTests(순수 배치 + 배선 계약). 실창은 대화형 세션의 LayoutSweepTests 로 재측정(이 세션에서는 창 띄우지 않음).
- 새 파일 UTF-8 BOM 추가는 셸 권한 거부로 못 함(컴파일 영향 없음).

## 진행
- [x] HelpCalloutPlacement 창 우선 오버로드 + TryFit
- [x] PopupScreenBounds 추출, HelpTip.Place 배선
- [x] DropDownPlacement + DropDownFit, ActionTrayView 부착
- [x] PopupFitTests
- [x] dotnet test Ironwall.Dotnet.Libraries.sln 전부 통과(Utils.Tests 818 등) · Events.Ui(sln 밖) 단독 빌드 오류 0

## 남은 일
- 실창 재측정: IRONWALL_LAYOUT_ONLY=Events,Units 로 LayoutSweepTests (대화형 세션).
