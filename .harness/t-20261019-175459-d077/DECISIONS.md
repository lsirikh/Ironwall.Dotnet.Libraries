# DECISIONS

## 1. 17:41 회차의 막힘 주체를 확정하지 못한 채 무엇을 고칠지
- options: (a) 추정 원인 하나만 (b) 재현된 실제 결함 수정 + 나가기를 순서 · 격리로 튼튼하게 + 진단 줄 (c) 진단 줄만
- chosen: (b)
- why: 증거로는 "지도까지 입력이 라우팅되지 않았다"까지만 확정. 추정 수정은 금지, 진단만으론 같은 증상을 내는 실제 결함이 남는다.
- effect: 재발 시 진단 줄 "가운데=" · "막는조상=" · "지도상태=" 값으로 추가 수정.

## 2. 나갈 때 IsHitTestVisible 을 무엇으로 되돌릴지
- options: (a) 늘 true (b) 펼치기 직전 로컬 값(없으면 ClearValue), 두 번 펼쳐도 처음 값
- chosen: (b)
- why: 과제 문구 "들어갈 때 기록하고 나올 때 되돌림". 제품에서 이 값을 바꾸는 곳은 3D 뿐이라 결과는 true 와 같다.
- effect: RestoreMap2DInput 첫 두 줄을 바꾸면 (a).

## 3. 「건물 높이」 판 닫기 중 보내기 예외
- options: (a) Leave3D 에서 try/catch (b) CloseBuildingHeightPanel 안에서 보내기만 격리
- chosen: (b)
- why: (a)는 판 상태가 남아 2D 에 판이 뜬다.
- effect: Error 로그 「「건물 높이」 판 닫기 — 3D 로 보내기 실패(격리)」.

## 4. 줌 단추 증언 반영 방식
- options: (a) 나갈 때 줌을 바꿨다 되돌리기 (b) 줌 변경이 비우는 것 중 입력을 막는 일시 상태를 직접 비우고 이름을 로그로
- chosen: (b)
- why: (a)는 깜빡이고 원인이 남지 않는다. 모드(그리기 · 측정)는 3D 중 열리지 않으므로 건드리지 않는다.
- effect: GMapCustomControl.ResetTransientInput 한 곳에 상태 추가로 확장.

## 5. 이벤트 패널 가설
- options: 패널 경로 수정 / 수정 안 함
- chosen: 수정 안 함(메인 앱 변경 · host.patch 없음)
- why: OpenEventPanel 은 셸 속성만 바꾸고, 사용자가 새 빌드에서 3D 중 패널 열고 나가기 정상 확인.
- effect: 없음.

## 6. v2.6 병합 충돌(Reveal3D)
- options: 한쪽만 / 둘 다
- chosen: 둘 다 — v2.6 의 회전 영역 진입 방위(EnterBearing) + 이 작업의 HoldMap2DInput(옛 IsHitTestVisible=false 줄 대체)
- why: 두 변경은 다른 일을 한다.
- effect: 없음.
