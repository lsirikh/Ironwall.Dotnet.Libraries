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

## 5. '같은 종류'의 단위 — PIDS 장비는 판이 하나(GMapPidsMarker)지만 DeviceType 마다 칸이 다르다
- options: 판 종류(마커 클래스)만 같으면 같은 종류 / DeviceType 까지 같아야 같은 종류
- chosen: 판은 지금처럼 마커 클래스로 고르고, DeviceType 이 섞이면 종류 전용 칸(모델 변형 · 통문 폭 · 문 상태)만 감춘다(IsMixedDeviceType · IsGate/HasDoor 를 '전원' 기준으로)
- why: 카메라+센서처럼 섞인 선택에서도 공통 PIDS 칸(탐지 · FOV · 설치 높이)은 계속 일괄 편집할 수 있고, 통문 폭이 카메라에 쓰이는 일은 막는다
- effect: 같은 DeviceType 끼리면 단일 판 칸 전부. 섞이면 종류 전용 칸 숨김. 바꾸려면 GMapPropertyPidsControl.ApplyGroupPendingSpecific

## 6. 제목을 '고유 값'으로 — 그룹 편집에서 비활성
- options: 지금처럼 다중 선택에서 제목 일괄 변경 허용 / 비활성(대표값 표시)
- chosen: 비활성(툴팁 "그룹 편집에서는 제목을 바꿀 수 없습니다 (심볼별 고유)"). 표시는 기존 규칙(같으면 값 · 다르면 빈칸) 유지
- why: 사용자 지시 "제목처럼 하나뿐이어야 하는 값"은 대표값 + 비활성. 표시 규칙은 기존 시험(MapSymbolReflectionTests: 다름=빈칸)을 고치지 않으려고 그대로 둠
- effect: 다중 선택에서 제목 일괄 변경 불가(공통 혼합 판 포함). 되돌리려면 BasePropertyStyle.xaml 제목 TextBox 의 DataTrigger 삭제

## 7. 저장되지 않는 칸(색상 강도) · 런타임 칸(이미지 표시)
- options: 대표 심볼에만 적용(지금 동작) / 판이 전원 Shape 에 직접 적용 / 대표값 + 비활성
- chosen: 대표값 + 비활성. 이미지 '표시'는 그룹 어도너 메뉴(그룹 표시 · 그룹 숨김) 안내 툴팁
- why: 색상 강도는 모델 · DB · Undo 경로가 없는 화면 값이라 '전원 적용 · 저장 · Undo 1단위' 규칙을 지킬 수 없다. 칸은 보이게 해서 '단일 칸 전부' 규칙은 지킨다
- effect: 바꾸려면 Pids/InfraPropertyStyle 의 TintStrength Slider.Style 트리거 삭제

## 8. 읽기 전용 정보 칸의 그룹 표시
- options: 합계 / 범위 / '여러 값'
- chosen: 포인트 수 · 총 거리 = 고른 선(구역) 전부의 합계, 3D 철망 요약(기둥 N개 · 실간격) = "—"
- why: 개수 · 길이는 합이 뜻이 있고(전체 펜스 길이), 실간격은 구역마다 선 모양에 딸려 하나로 합칠 수 없다
- effect: GMapPropertyLineControl / GMapPropertyPidsGroupControl.ApplyGroupPendingSpecific · UpdateFenceSummary

## 9. bool(체크 칸) · int(층수) 칸의 '여러 값' 표시
- options: DP 형식을 bool?/double 로 바꾸기 / 표시 전용 플래그 DP 추가 / 대표값 표시
- chosen: 층수(int)는 IsFloorCountMixed · IsBasementFloorCountMixed(공개 DP 추가)로 글만 "—", 선 닫힘 · 이미지 비율유지(bool)는 대표값 표시(여러 값 표시 없음)
- why: 공개 형식은 더하기만 — 형식 변경 금지. 층수는 글 칸이 있어 플래그로 충분, 체크 칸 2개는 3상태용 DP 를 더하는 것보다 대표값이 덜 위험하다고 판단
- effect: 선 닫힘 · 비율유지가 서로 다를 때 체크 칸은 대표값을 보인다(누르면 전원 적용은 됨). 3상태가 필요하면 Render3DState 와 같은 방식으로 bool? DP 를 더한다
