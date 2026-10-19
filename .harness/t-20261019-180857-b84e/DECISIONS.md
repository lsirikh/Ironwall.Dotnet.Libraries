## 1. 「3D」 체크 칸의 '여러 값' 표시 방법
- options: Render3D DP 형식을 bool → bool? 로 바꾸기 / 표시 전용 bool? DP(Render3DState) 추가
- chosen: Render3DState(bool?) 추가, 체크 칸은 이 값에 바인딩(null=indeterminate). Render3D(bool)는 그대로
- why: 공개 형식은 더하기만 허용 — Render3D 형식 변경은 기존 시험 · 호출부(FenceLod.Show3D 등)를 깨뜨린다
- effect: 혼합에서 체크 칸을 누르면 그 값이 전원에 적용. 나중에 Render3D 를 bool? 로 통합하려면 Render3DState 를 지우고 바인딩만 되돌리면 된다

## 2. PIDS 그룹 판의 '장비 그룹' 콤보를 그룹 편집에서 비활성
- options: 지금 상태(켜져 있어 전원에 같은 장비 그룹이 쓰일 수 있음) 유지 / PidsPropertyStyle 디바이스 연결과 같은 IsGroupEditing → IsEnabled=False
- chosen: 비활성 + 툴팁 "그룹 편집에서는 장비 그룹을 변경할 수 없습니다 (심볼별 고유)"
- why: 목표 3 "심볼마다 고유한 값은 대표값 표시 + 비활성" — 실제로는 PIDS 그룹 판만 켜져 있었다
- effect: 다중 선택에서 장비 그룹 일괄 변경 불가. 되돌리려면 PidsGroupPropertyStyle.xaml 의 ComboBox.Style 트리거를 지운다

## 3. 기둥 간격 · 높이의 '같은 값' 판정에서 NULL(전역 기본 상속)
- options: 실효값(NULL→기본값)으로 비교 / 저장값(NULL 포함) 그대로 비교
- chosen: 저장값 비교 — 모두 NULL 이면 기본값 + 「기본」 표시, NULL 과 숫자가 섞이면 '—'
- why: 「기본」 표시는 '전역 설정을 따름'이라는 저장 상태를 말하므로 실효값이 같아도 상태가 다르면 섞인 것으로 본다
- effect: 상속 혼합이면 '—'. 실효값 비교로 바꾸려면 ApplyGroupPendingSpecific 의 AllEq 선택자를 `m.PostSpacingM ?? 기본` 으로

## 4. 옛 시험 should_hide_fence_block_when_symbol3d_disabled_or_group_editing 수정
- options: 그대로 두기(새 요구와 모순 → 실패) / 그룹 편집 부분만 새 규칙(보임)으로 바꾸기
- chosen: 3D 꺼짐=숨김 단언은 유지, 그룹 편집 단언은 '3D 켜짐이면 보임'으로 바꾸고 이름도 맞춤
- why: 이 시험이 바로 사용자가 고쳐 달라고 한 동작(그룹이면 숨김)을 고정하고 있었다
- effect: tests/GMaps.PropertyPanel.Tests/PropertyPanelRenderTests.cs 한 시험
