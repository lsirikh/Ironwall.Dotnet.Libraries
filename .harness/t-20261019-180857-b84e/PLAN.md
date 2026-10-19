# PLAN — PIDS 그룹 다중 선택 속성 판에 3D 철망(펜스) 칸 보이기 · 그룹 편집

## 목표
다중 선택(PIDS 그룹 ≥2) 속성 판에도 3D 철망 칸을 보이고, 기존 그룹 편집 관용구(동일=값 / 다름=— · 바꾼 필드만 전원 적용 ·
마커별 DB 저장 · Undo 1단위)를 따르게 한다. 3D 기능 꺼짐이면 숨김 유지. 장비 그룹은 대표값 + 비활성.

## 펜스 칸 전수(Themes/PidsGroupPropertyStyle.xaml, AutomationId)
| 칸 | 모델 필드 | 그룹 표시(다름) | 그룹 적용 |
|---|---|---|---|
| FenceMode 콤보 | FenceMode | 무선택((EnumFenceMode)(-1)) | 고른 값 전원 |
| Render3D 체크 | Render3D | indeterminate(새 DP Render3DState=null) | 누른 값 전원 |
| PostSpacing 슬라이더 + 글 + 「기본」 표시 | PostSpacingM(null=상속) | NaN → 글 "—", 슬라이더 최소값 파킹 | 지연 커밋 뒤 전원 |
| 프리셋 2/3/5 m | PostSpacingM | — | 전원 |
| PostSpacingReset 「기본」 | PostSpacingM=null | — | 전원 null |
| FenceHeight 슬라이더 + 글 | FenceHeightM(null=상속) | NaN → "—" | 전원 |
| FenceHeightReset 「기본」 | FenceHeightM=null | — | 전원 null |
| FenceSummary | (계산값, 마커 하나 선 모양) | 그룹에선 빈칸 | — |
(ReverseSensorOrder 는 모델 · Undo 에는 있으나 이 칸에 UI 없음 → 대상 아님)

## 진행
- [x] 원인 확인(XAML IsGroupEditing→Collapsed, CanWriteFence !IsGroupEditing, Reset* 그룹 차단)
- [x] GMapPropertyPidsGroupControl: 그룹 Pending(ApplyGroupPendingSpecific override) · 그룹이면 대표 직접쓰기 억제 · 「기본」 그룹 지원 · Render3DState(bool?) DP 추가
- [x] MapViewModel.ApplyGroupPropertyChangeAsync: PostSpacingM/FenceHeightM 의 null(「기본」) 허용
- [x] DoubleNaNToBlankConverter.ParkValue(공개 속성 추가) — NaN 파킹값을 슬라이더 최소값으로
- [x] XAML: 그룹 숨김 트리거 제거 · 값 글 NaN→"—" · 3D 체크=Render3DState · 장비 그룹 콤보 그룹 편집 비활성
- [x] 시험: tests/GMaps.PropertyPanel.Tests/PidsGroupFenceGroupEditScenarioTests.cs(11개) · PropertyPanelRenderTests 의 옛 "그룹이면 숨김" 단언을 새 규칙으로
- [x] 시험 실행: PropertyPanel 328/328 · MapEdit 451/451 · Housing 353/353 통과
- [ ] sln 시험(dotnet test Ironwall.Dotnet.Libraries.sln)
- [x] 수정 전 실패 증명: 제품 소스 4개를 HEAD 원본으로 잠시 되돌려 should_hide_fence_block_when_symbol3d_disabled_but_show_it_in_group_editing 실행 → 실패(Expected Visible, Actual Collapsed) → 복원 후 통과
  (새 시나리오 파일은 새 공개 속성 Render3DState 를 써서 원본에선 컴파일이 안 되므로 증명엔 이 시험을 썼다)

## 발견
- PIDS 그룹 판은 ApplyGroupPendingSpecific 를 override 하지 않아 라인 · 장비 그룹은 대표값 표시(기존 동작, 범위 밖이라 유지).
- PidsGroup 판의 장비 그룹 콤보는 그룹 편집에서도 켜져 있었다(목표 3 "지금처럼 비활성" 과 다름) → 비활성 트리거 추가(DECISIONS 2).
- VM 그룹 적용은 잠긴 마커 제외 · 마커별 DbUpdateProcess · BeginBatch(1 매크로) — 그대로 재사용.
