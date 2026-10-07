# PLAN — 맵핑 창 카메라 연동 칸 프리셋 드롭다운 빈칸 (EVT-DEMO-R26)

## 목표
맵핑 LRT-DUMMY-MAP → LRT-DUMMY-CAM1 줄 칩은 "타깃 P2 · 홈 P1 · 지연 3초" 인데 아래 연동 설정 칸의
[타깃 프리셋]·[홈 프리셋] 드롭다운이 빈칸. 증거로 원인 확정 → 수정. 빈칸 상태에서 [적용] 시 서버 값이
지워지는지도 확인하고 막는다.

## 진행
- [x] 관련 코드 읽기 (VM 카메라 연동 칸 · BoardRow · DTO · 게이트웨이 · 서버 스키마 · seed)
- [x] 진단 시험: 실제 XAML 콤보 + 실제 MappingWorkbenchView(MaterialDesign, 창 없이 Measure/Arrange)
- [x] 원인 확정(증거 아래)
- [x] 수정 + 회귀 시험(헤드리스 6건 · 실제 뷰 2건)
- [x] [적용] 시 프리셋 값 지워짐 여부 확인 → 지워지지 않음(시험으로 고정)
- [x] dotnet test 솔루션 — Events.Ui 1868건 통과. 전체 실행에서 CameraPopup `SupervisorSurvivalTests.should_restart_without_spending_crash_budget_when_host_exceeds_memory_limit`
      1건 실패(31 s, 호스트 프로세스 재시작 시간 의존) → 단독 재실행 통과(3 s). 이번 변경(Events.Ui 맵핑)과 무관한 부하 의존 흔들림으로 판단, 손대지 않음.

## 원인 (증거)
1. 실제 뷰 진단: 콤보 `SelectedItem` = P2(id 12, 목록 안 같은 참조)인데 `SelectedIndex = -1`, `SelectionBoxItem = ''`(빈칸).
   VM · 행 값은 12/11, 상태 Pristine — 데이터는 멀쩡하고 콤보 선택만 비었다.
2. 서버 응답이 없어 목록이 한 번만 서면(자리 항목만) 정상('P2', idx 1). 목록이 **두 번째로**(서버 목록 도착) 서면 빈칸.
   DataContext 를 끊었다 붙이면(null → P2 전이) 바로 'P2' — WPF 가 "값이 안 바뀜" 으로 선택을 건너뛴 것.
3. 기전: `ShowPresets` 가 `PresetOptions.Clear()` → 콤보가 null 을 밀어 넣고 getter 를 곧바로 다시 읽음 →
   그 순간 목록이 비어 `OptionFor` 가 **목록 밖 임시 P2** 를 새로 만들어 줌 → 콤보 DP 에 남음 →
   다 세운 뒤 서버 P2 를 줘도 `MappingPresetOption` 이 record(값 동등)라 Equals → 선택 콜백 없음.
4. 되돌림 실험: 수정을 빼면 헤드리스 회귀 시험 5흐름 중 4개 실패(target="-:"), getter 수정만 넣어도 전부 통과.

## [적용] 영향
- `MappingRequestBuilder.PatchCamera` 는 행 값 vs 서버 원본만 비교 → 빈칸 상태에서도 행은 12/11 이라 프리셋 키 안 실림.
- 버그 상태(수정 되돌림)에서도 "지연만 바꿔 PATCH" 시험 통과 → 서버 값이 지워지지는 않았다. 시험으로 고정.

## 수정
- `MappingWorkbenchViewModel.SelectedTargetPreset/SelectedHomePreset` getter: `_rebuildingPresets` 동안 null (직접 원인).
- `MappingPresetOption`: record → 참조 동등 sealed class (둘째 방어).

## 결정
- 창을 띄우지 않는다(사용자 데스크톱 불가침) — 실제 뷰도 Measure/Arrange 만.
- 실제 뷰 시험은 `tests/Consoles.ViewTests`(sln 밖, Application 상주 필요) 에 둠. sln 검사는 Events.Ui 헤드리스 시험이 지킨다.
- 시험은 `SelectedItem` 이 아니라 `SelectedIndex` + `SelectionBoxItem` 을 본다(SelectedItem 은 결함 상태에서도 P2).

## 시험
- `Ironwall.Dotnet.Libraries.Events.Ui/Tests/MappingCameraEditorComboBindingTests.cs` (sln 포함)
- `tests/Consoles.ViewTests/MappingCameraEditorViewTests.cs` (`dotnet test tests/Consoles.ViewTests --filter MappingCameraEditorViewTests`)

## 남은 일
- 솔루션 전체 시험 결과 확인.
- 실앱(시연 환경)에서 재확인은 사람이 할 일(이 회차는 창을 띄우지 않음).
