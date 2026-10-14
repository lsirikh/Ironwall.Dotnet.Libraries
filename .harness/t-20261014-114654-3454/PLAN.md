# PLAN — MNT-R28-4xx 실창 실패를 실제 컨트롤 경로로 재현 · 수정

## 목표
결선 창 "설치 위치" 절 실창 시험(MNT-R28-401 · 403 · 425 · 440 · 444 · 451 · 453)이 2회 연속 실패.
헤드리스(뷰모델 · 부모 없는 캔버스) 시험은 통과 → 실제 WiringView XAML 을 화면 밖 창(Left=-20000 · ShowActivated=false)에 띄워
콤보 · 키 · 창을 거치는 재현 시험을 만들고, 제품 결함은 고치고 실창 시험 쪽 결함은 근거와 함께 보고한다.

## 결정
- 결선 뷰: `WiringNarrowLayoutTests.LooseWiringView()`(WiringView.xaml 그대로 · 이벤트 처리기만 뺌 — 컴파일된 WiringView 는
  Application 자원 없이는 StaticResource 를 못 찾고, 시험 프로세스에 Application 을 만들면 다른 시험이 흔들린다).
  콤보 · 바인딩 · 펜스 캔버스(컴파일된 FenceView · FenceCanvas 키 처리)는 그대로다.
- 저장 확인 창: 컴파일된 `PresetSaveConfirmView`(DynamicResource 만 씀) 를 실창과 같은 420×260 창에.
- 입력: 콤보는 UIA peer(IExpandCollapseProvider · ISelectionItemProvider — 실창 FlaUI 와 같은 길),
  키는 창의 입력 원천으로 PreviewKeyDown → KeyDown → (처리 안 됐으면) WM_CHAR 의 TextInput — Win32 순서 그대로.
- 401 수정: 설치 위치 절 콤보 셋(모델 · 위치 프리셋 · 속성 프리셋)에 `IsTextSearchEnabled="False"`.
  글자 찾기를 잃지만 항목이 한글 이름 몇 개라 실익이 작고, 위치 프리셋 콤보에서는 Esc 한 번에 ★기본이 적용되는 더 큰 해를 막는다.
- ShowDialog 는 쓰지 않는다(화면 밖이어도 창을 활성화해 사용자 작업을 건드린다) — 453 은 Enter 경로(취소 길)까지만 실창 창으로 확인.

## 진행
- [x] 401 재현(빨강) — 콜스택: `ItemsControl.OnTextInput → TextSearch.DoSearch("\x1b") → ComboBox.FocusItem → SelectJustThisItem → SelectedPresetModel setter(펜스센서 MEMS)`
- [x] 401 수정(WiringView.xaml 콤보 셋) — 수정 빼면 위치 프리셋 시험도 빨강(Esc → ★기본 '기둥 꼭대기 2.6 m · 밖향' 적용) 확인 후 되돌림
- [x] 403 · 425 · 440 · 444 · 451 · 453 실제 컨트롤 경로 재현 · 판정(아래) — `Tests/RealWindowR28Tests.cs` 8건 통과
- [x] Devices.Ui csproj 전체: 4436 통과 · 1 실패(UnitConsoleNonModalTests STA 시간 초과 — 단독 재실행 19/19 통과, 부하성)
- [ ] sln 시험(하네스 검사) 결과 확인

## 판정(실창 시험 원본은 호스트 저장소 — 고치지 않고 고칠 내용만 보고)
- 401 제품 결함 → 고침.
- 403 시험 쪽: 402 에서 모델 '새 모델 12' 를 고르면 그 모델 ★기본(= R28-MOUNT-…-A, 결과 7행 IsDefault=True)으로 이미 선다 →
  같은 A 를 [고른 센서]에 다시 적용하면 "바뀐 것이 없습니다." · 칩 그대로(406 이 기대하는 것과 같은 동작). 403 은 ★기본이 아닌 프리셋을 적용해야 한다.
- 425 시험 쪽: 424 의 Ctrl+→ · Ctrl+← 는 계약상 9점 격자 걸음이라 프리셋을 놓는다(SC-FEN24-003 · SC-FEN-024) → 높이 칸이 비어
  FlaUI 가 이름 '설치 높이' 를 읽음 · Alt+↑ 는 9자리 단계. 고른 프리셋에 선 센서에선 실제 칸이 1.00 → 1.10 → 1.00.
  425 앞에서 그 센서에 프리셋을 다시 고르거나(위치 프리셋 콤보) 424 에서 안 쓴 센서를 쓴다.
- 440 · 444 시험 쪽: '표준' 은 [+ 모델] 이 만든 ★기본이라 센서들이 실제로 쓰는 프리셋이 맞다. 그러나 [+ 항목] 줄의 쓰임 기본이 '참고' 이고
  R28SetAttr 은 쓰임 칸(표 4번째 열)을 건드리지 않는다 → FR-14 "그림 · 알림 값만 읽는다" 에 따라 결선맵이 읽지 않음([탐지 범위] 꺼짐 · 알림 '').
  쓰임을 '그림'(탐지 거리) · '알림'(권장 간격)으로 고르면 실제 단추가 켜지고 절에 "권장 간격 넘음 n곳".
  참고: 쓰임 콤보 열은 enum 이름(Render/Alert/Reference)으로 보인다 — 실창 시험이 고를 글자.
- 451 시험 쪽: R28Status(C) 는 틀 안내 줄 `Dialog.Devices.SensorPresets.SaveConfirm.Message` 를 읽는데 안내는 칩 아래
  `Devices.SensorPresets.SaveConfirm.Note`(UIA 이름 '따로 고친 값은 그대로 남습니다.', 420×260 창에서 화면 안)에 있다.
- 453: 실제 창 입력 원천의 Enter([취소] 포커스 · 창 뿌리 둘 다) → 틀 SecondaryInvoked(취소) 2회 · PrimaryInvoked 0. 제품의 Enter 경로는 정상.
  실창에서 아무 일도 없었던 것(편집기 상태 '' = 확인 창이 닫히지도 취소되지도 않음)은 키가 확인 창에 닿지 않았다는 뜻 —
  유력 원인: `FindTopWith(R28ConfirmMsg)` 가 주인 창(편집기, UIA 에서 소유 창을 자식으로 품음)을 돌려주고 ForegroundHelper.Force 가 모달로 꺼진
  편집기를 앞으로 가져와 Enter 가 버려짐(같은 C 로 읽은 451 안내도 편집기 Message 를 먼저 읽는다). 확정은 못 함(FindTopWith 원본 없음).
  시험 쪽 권고: Enter 전에 `ByIdIn(C, "Devices.SensorPresets.SaveConfirm.Cancel").Focus()` 로 포커스를 확인 창 단추에 두고, C 는
  `Dialog.Devices.SensorPresets.SaveConfirm.Root` 를 가진 창 자체로 찾는다.
- 426 · 460 은 이 작업 범위 밖(떠 있는 칩 — 다른 작업이 FenceWorld/FenceCanvas 수정 중).
