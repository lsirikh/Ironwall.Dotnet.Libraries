#!/usr/bin/env bash
# TEST-01: approve prd 명령 시나리오 (FR-02, FR-03)

source "$(dirname "$0")/_helpers.sh"

echo "=== TEST-01: approve prd 명령 ==="

# 가짜 PRD 생성
TMP_PRD="$ROOT/docs/prds/_test_approve-prd.md"
cat > "$TMP_PRD" <<'EOF'
# Test Approve PRD

- **상태**: Draft
- **버전**: 1.0

## 변경 이력

| 날짜 | 버전 | 변경 항목 | 변경 이유 | 영향 범위 |
|------|------|---------|---------|---------|
| 2026-04-10 | 1.0 | 초안 | 테스트 | — |
EOF

# 1. approve 명령 실행
backup_state
node .claude/hooks/advance-phase.js approve prd "테스트 승인" 2>&1 | head -5
echo ""

# 2. 검증
assert_contains "PRD 상태 Approved 변경" "$TMP_PRD" "^- \*\*상태\*\*: Approved"
assert_contains "변경 이력에 사용자 승인 행 추가" "$TMP_PRD" "사용자 승인"
assert_contains "audit-log에 user-approval 이벤트" "$AUDIT_LOG" "user-approval"

# 정리
rm -f "$TMP_PRD"
restore_state

print_summary
