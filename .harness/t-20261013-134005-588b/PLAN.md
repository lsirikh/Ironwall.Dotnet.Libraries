# PLAN — 실창 결선 회귀 FAIL 15건 고치기 (t-20261013-134005-588b)

## 목표
2026-10-13 실창 회귀(호스트 UiTests 펜스 · 결선 8개, 3 통과 · 5 실패, FAIL 15건)를 원인 확정 후 고친다.
회귀(프리셋 작업 t-20261013-111040-db54 · t-20261013-115417-837d)면 기존 계약을 되살린다. 끝에 FAIL 별 표.

## 근본 원인(확정)
- 저장 때 `MigrateMountForSave`(FR-19)가 모든 센서 자리에 `mount_preset_id` 를 적는다 → 다시 열면 모든 센서가
  `IsPresetMounted` = 참 → 펜스 보기의 기존 키 · 끌기 · 그림이 프리셋 길로 샜다.
  - `FenceCanvas.HandleKey`: Alt+↑/↓ → `FenceNudgePresetHeight`(높이 10 cm) — 원래 `FenceStepStop`(자리 단계) · Shift+Alt+↑/↓ → 1 cm(원래 높이 조정 `FenceNudgeHeight`).
  - Ctrl+←/→ → `FenceStepPresetSnap`(붙는 점 다음) — 원래 `FenceStepGrid`(9점 격자 다음).
  - 한 대 끌기 → `BeginPresetDrag`(허용 붙는 점 — 스마트2 는 망 가운데 불가 → 기둥 꼭대기로) — 원래 9점 격자 `BeginSnapDrag`.
  - 그림: `FenceWorld.FromLayout(..., FencePoseOf)` 가 이관된 센서를 프리셋 자세로 그려 9자리 계산(같은 자리 벌리기 · 줄)과 어긋남 →
    [왼쪽 아래] 등 9자리 편집이 칩을 못 움직이고(SC-FEN-024), 여러 대 끌기 빨강 점(9자리)과 칩(자세) 자리가 어긋난다(SC-FEN24-004 · 026).

## 결정
- D1. 레이아웃 JSON 자리에 `mount_preset_chosen`(bool? · 없으면 싣지 않음 — 더하기만)을 더한다. 사용자가 프리셋을 골라 세운 자리
  (Apply · WithOverride · MoveTo)만 참. 저장 때 이관(Migrate)은 적지 않는다 → 이관된 자리 · 이미 저장된 옛 데이터(표지 없음)는
  **9자리 계산이 정본**(그림 · 키 · 끌기 모두 옛 길). 설치 위치 절(모델 · 프리셋 콤보 · 높이 칸)은 그대로 이관값을 보여 준다.
- D2. 펜스 보기 키는 기존 계약으로 되돌린다: Alt+↑/↓ = 자리 단계(줄) · Shift+Alt+↑/↓ = 높이 조정 · Ctrl+←/→ = 9점 격자 다음.
  프리셋 붙는 점 키는 **끄는 중에만** Tab/Shift+Tab(이미 있음). 프리셋 높이는 설치 위치 절 높이 칸.
- D3. 9자리 편집(프리셋 편집 범위 밖의 EditFence)이 프리셋 자리의 위치 칸(panel · spot · column · height_offset · lane)을 바꾸면
  그 센서의 `mount_preset_id` · `mount_override` · 표지를 지운다(모델 · 속성 프리셋은 둔다) → 9자리로 그려지고 저장 때 다시 이관.
- D4. 한 대 끌기의 붙는 점 끌기는 사용자가 고른 프리셋 센서(D1 참)에만 남긴다(FR-06).

## 진행
- [x] D1~D4 구현 — `SensorMountSpec.MountPresetChosen`/`IsPresetChosen` · `WiringPresetPlan`(Apply/WithOverride/MoveTo 표지 · `ReleaseMovedPresets`) ·
      `WiringBoard.ApplyFenceEdit`(+`KeepMountPresets`) · `WiringViewModel.Presets.cs`(`FencePoseOf` 고른 것만 · `PresetPoseOf` · `EditPresetFence`) ·
      `PresetDrag.cs`(`IsPresetMounted` 고른 것만) · `FenceCanvas.HandleKeyDown`(프리셋 키 가로채기 삭제) · 도움말 `Devices.Wiring.MountPreset` 키 안내.
- [x] 헤드리스 시험 `Devices.Ui/Tests/WiringPresetKeyContractTests.cs`(11건 · SC-FEN-020/024/025/026 · SC-FEN24-003/004 · WIR-R27-135) ·
      `Monitoring.Models/Tests/SensorMountChosenFlagTests.cs`(1건). 빨강 확인: 옛 조건으로 되돌리면 이관 그림 · 한 대 끌기 시험이 실패.
- [x] Devices.Ui 창 없는 Wiring/Fence/SensorPreset/Help 시험 1088건 통과 · Models SensorPreset 30건 통과.
- [x] 추가: 설치 위치 콤보 `SelectedMountPreset` · `SelectedAttributePreset` 같은 값 되쓰기 무시(콤보가 목록을 다시 받을 때 이관 자리를 "고름"으로 바꾸지 않게) + 시험.
- [x] 펜스 외 R27 원인 조사(조사 에이전트 + 직접 확인) — 아래 표.
- [x] FenceGateRuntimeDiag — 보고만(호스트 시험 · 지도 3D, 이 저장소의 프리셋 변경과 무관).
- [x] 도움말 고침(`DevicesHelp.SensorPresets.cs` Devices.Wiring.MountPreset). PRD 원본은 이 저장소에 없음(호스트 `docs/prds/sensor-mount-preset-prd.md`) → 아래 "PRD 고칠 문안".
- [ ] sln 시험(하네스 검사)

## FAIL 15건 판정
| FAIL | 판정 | 원인 · 근거 |
|---|---|---|
| SC-FEN-020 Alt+↓ | 고침(회귀) | `FenceCanvas.HandleKeyDown` 프리셋 높이 가로채기(18629655) + 저장 이관으로 전 센서 `IsPresetMounted` |
| SC-FEN24-003 Ctrl+→/← | 고침(회귀) | 같은 원인 — `FenceStepPresetSnap` 가 9점 격자 대신 |
| SC-FEN24-004 여러 대 끌기 | 고침(회귀) | 이관 센서를 프리셋 자세로 그려 칩(자세)과 빨강 점(9자리)이 어긋남 |
| SC-FEN-024 [왼쪽 아래] · Alt+↑/↓ | 고침(회귀) | 9자리 편집이 자세 그림을 못 움직임 + 키 가로채기 |
| SC-FEN-025 끌기 = 놓은 칸 | 고침(회귀) | 한 대 끌기가 붙는 점 끌기로(스마트2 는 망 가운데 불가 → 기둥 꼭대기) |
| SC-FEN-026 함께 옮기기 | 고침(회귀) | 025 의 잘못 놓인 센서 + 그림 어긋남(연쇄) |
| WIR-R27-135 Shift+Alt+↑/↓ | 고침(회귀) | 프리셋 1 cm 가 오프셋 칸 대신 |
| SC-FEN24-001 선택 테두리 | 보고만 | 선택 윤곽(hit 사각형) 코드는 바뀌지 않음 · 이관 자세 그림 영향일 가능성 — 실창 재확인 |
| WIR-R27-153/152 줌 % | 보고만 | `FenceView`/줌 코드 프리셋 전후 변경 없음 · 글이 처음부터 '100%' = ViewChanged 미구독 의심, 근거 부족 |
| WIR-R27-302/303/304 제안 | 시험 쪽 | 공유 시드 — 같은 회차 R21 SC-FEN-017 이 대역 번호로 저장해 "저장 안 된 시드" 전제가 깨짐 |
| WIR-R27-409 대역 250~260 | 시험 쪽(+기존 동작) | `WiringBandRowViewModel.Parse` 가 end>255 를 칸 오류로 막고 대역을 적용하지 않음 → 저장 사유 칸은 그대로(프리셋 전과 같음) |
| WIR-R27-412 대역 고르기 칸 펴기 | 시험 쪽 | `ShowBandPicker` 의 `IsDetailPaneOpen = true`(D1) 그대로 · 시작 상태가 이미 펴짐(collapsed=False) |
| WIR-R27-606t ✕ 가림 | 시험 쪽 | ✕ `SlotRemove.1-11` = 12번째 줄 — 표 스크롤 아래로 잘린 줄(첨부 그림: 보이는 마지막 줄은 11번째) |
| WIR-R27-607 다크 | 시험 쪽 | 셸 테마 단추를 결선 창(소유 창)이 가림 — 앞 실패 연쇄 |
| FenceGateRuntimeDiag | 보고만 | 호스트 시험 · 지도 3D(U+2060 패턴은 시험 쪽) |

## PRD 고칠 문안(호스트 `docs/prds/sensor-mount-preset-prd.md` v1.2)
- §10 FR-07 줄: "펜스 보기: 끄는 중 Tab/Shift+Tab 다음/이전 붙는 점" 만 남기고 "Ctrl+←/→ 다음 붙는 점 · Alt+↑/↓ 높이 10 cm" 삭제 →
  "펜스 보기 키는 기존 계약 그대로(Ctrl+←/→ 9점 격자 · Alt+↑/↓ 높이 단계 · Shift+Alt+↑/↓ 높이 조정). 프리셋 높이는 설치 위치 절 높이 칸".
- §10 v1.1 결정에 더함: "레이아웃 자리에 `mount_preset_chosen`(더하기만) — 사람이 프리셋을 고른 자리만 펜스 보기가 프리셋 자세로 그리고 한 대 끌기를 붙는 점 끌기로.
  저장 때 이관(FR-19)된 자리 · 표지 없는 옛 문서는 9자리 계산이 정본. 9자리 편집이 프리셋 자리를 옮기면 프리셋을 놓는다(저장 때 다시 이관)."

## 남은 일 / 발견
