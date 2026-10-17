#!/usr/bin/env bash
# TEST-04: dev-journal 자동 엔트리에서 TDD 마커 자동 추출 (FR-09)

source "$(dirname "$0")/_helpers.sh"

echo "=== TEST-04: TDD 마커 자동 추출 ==="

backup_state

# 현재 phase를 dev로 설정 (force)
node .claude/hooks/advance-phase.js dev --force >/dev/null 2>&1
node .claude/hooks/advance-phase.js test --force >/dev/null 2>&1

# Green 마커 백워드 시도
node .claude/hooks/advance-phase.js dev --reason "[Green] TEST-04 통과 위한 구현" >/dev/null 2>&1

# dev-journal에 TDD 단계 필드가 추가됐는지 검증
JOURNAL="$ROOT/docs/memory/dev-journal.md"
assert_contains "[Green] reason 인식" "$JOURNAL" "TEST-04 통과 위한 구현"
assert_contains "TDD 단계 필드 추가" "$JOURNAL" "TDD 단계.*Green"

# Red 마커 검증
node .claude/hooks/advance-phase.js test --force >/dev/null 2>&1
node .claude/hooks/advance-phase.js dev --reason "[Red] 테스트 작성" >/dev/null 2>&1
assert_contains "[Red] 마커 추출" "$JOURNAL" "TDD 단계.*Red"

# Refactor 마커 검증
node .claude/hooks/advance-phase.js test --force >/dev/null 2>&1
node .claude/hooks/advance-phase.js dev --reason "[Refactor] 중복 제거" >/dev/null 2>&1
assert_contains "[Refactor] 마커 추출" "$JOURNAL" "TDD 단계.*Refactor"

restore_state
print_summary
