# 결정 기록 — t-20261022-174808-30e1 (건물 설정 판 높이 줄이기 · 스크롤)

## 1. 앞선 작업(t-20261022-164413-a54e)의 스크롤 변경을 가져오나, 새로 하나
- options: (a) 그 브랜치의 스크롤 변경을 그대로 옮긴다(본문 전체를 스크롤 — 행동 줄도 스크롤 안) · (b) 같은 자리(HeightPanelControlBase 높이 맞춤 + 세 판 ScrollViewer, AutomationId `Map.<판>.Scroll`)를 바탕으로 이번 요구(머리줄 · 행동 줄 고정, 상한 70%)를 새로 넣는다
- chosen: (b)
- why: 그 작업은 v2.6 에 병합되지 않았다(`git merge-base --is-ancestor b5683910 HEAD` = 1). 이번 목표는 "행동 줄 늘 보임"을 요구해 (a)와 모양이 다르다. 같은 이름 · 같은 자리를 써서 나중에 그 브랜치를 받아도 겹치는 곳이 같은 뜻이 되게 했다(pattern: reuse, follow-recommended).
- effect: 그 브랜치를 나중에 병합하면 세 판 XAML · HeightPanelControlBase 에서 충돌이 난다 — 이 작업 쪽(행동 줄 고정)을 남기고, 그 브랜치의 복사 · 붙여넣기 단추 줄은 스크롤 안 내용으로 옮기면 된다.

## 2. 판 최대 높이
- options: (a) 지도 칸 높이 × 70% · (b) 판 자리 아래 남은 높이 − 8 · (c) 고정 상한(px) · (d) (a)와 (b) 중 작은 쪽, 최소 200
- chosen: (d) — `min(지도 높이 × 0.7, 지도 높이 − 판 Top − 8)`, 단 200 아래로는 안 줄인다
- why: 사용자가 "높이 줄이고"라 했으니 남은 높이 전부(b)는 1920×1032 에서 여전히 ~850 px 로 길다. 70% 면 1920×1032(지도 칸 약 910) → 637, 1366×768(약 646) → 452, 600 → 420. 판을 아래로 끌어 놓았을 때도 행동 줄이 지도 밖으로 안 나가게 (b)를 함께 건다. 고정 상한(c)은 화면 크기마다 다시 맞춰야 해 뺐다(pattern: follow-recommended).
- effect: 상수 `HeightPanelControlBase.FitMaxRatio`(0.7) · `FitBottomMargin`(8) · `FitMinHeight`(200) 만 바꾸면 된다(시험 `BuildingPanelScrollScenarioTests` 의 `CapRatio` · `BottomMargin` 도 같이).

## 3. 행동 줄(늘 보이는 아래 칸)에 무엇을 넣나
- options: (a) 단추 묶음만 · (b) 「적용할 내용」 한 줄(Summary) + 단추 묶음
- chosen: 「건물 높이」 · 「건물 고치기/만들기」 = (b), 「꾸미기」 = 단추 묶음(그 판엔 요약 줄이 없다)
- why: 「건물 높이」 판 주석이 "적용할 내용 한 줄(늘 보임 — 적용 단추가 곧 확인)"이라 [적용] 과 떨어지면 안 된다. 그 아래 있던 「지우기 · 다시 만들기」 · 「숨긴 건물」(건물 높이), 「3D 고치기」 칸(건물 고치기), 목록 · 넣은 모델(꾸미기)은 스크롤 내용 맨 아래로 — 행동 줄 위에 온다(pattern: follow-recommended).
- effect: 각 판 XAML 의 `Grid.Row="2"` 칸에서 줄을 옮기면 된다.

## 4. 휠이 지도 · 3D 로 새지 않게 하는 방법
- options: (a) ScrollViewer 기본 처리만 믿는다 · (b) 판(HeightPanelControlBase)이 휠을 모두 처리됨으로 끝낸다
- chosen: (b)
- why: ScrollViewer 밖(머리줄 · 행동 줄) 휠은 처리되지 않고 위로 올라간다. 「이미지 자르기」 판(ImageCropPanelControl.OnMouseWheel)과 같은 관례(pattern: reuse).
- effect: `HeightPanelControlBase.OnMouseWheel` 한 곳.

## 5. Tab 포커스 칸 보이기
- options: (a) WPF 기본(FrameworkElement.OnGotFocus 의 BringIntoView)만 · (b) 판이 GotKeyboardFocus 에서 포커스 칸을 BringIntoView
- chosen: (b)
- why: 기본은 논리 포커스(GotFocus) 경로라 콤보 안쪽 등 일부 칸에서 빠질 수 있다 — 판 한 곳에서 명시적으로 맞추고 시험으로 증명한다.
- effect: `HeightPanelControlBase` 생성자의 처리기 한 줄.
