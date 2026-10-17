#!/usr/bin/env bash
# TEST-03: session-gate 검토 대기 표시 (FR-05)

source "$(dirname "$0")/_helpers.sh"

echo "=== TEST-03: session-gate 검토 대기 표시 ==="

# Draft PRD 생성
TMP_PRD="$ROOT/docs/prds/_test_awaiting-prd.md"
cat > "$TMP_PRD" <<'EOF'
# Awaiting PRD

- **상태**: Draft
- **버전**: 1.0
EOF

# session-gate 실행 → 출력에 Awaiting Review 포함되어야 함
OUTPUT=$(node .claude/hooks/session-gate.js 2>&1)
echo "$OUTPUT" | head -20

if echo "$OUTPUT" | grep -q "Awaiting Review"; then
  echo -e "  ${GREEN}PASS${NC}: 검토 대기 표시 포함"
  PASS_COUNT=$((PASS_COUNT + 1))
else
  echo -e "  ${RED}FAIL${NC}: 'Awaiting Review' 표시 없음"
  FAIL_COUNT=$((FAIL_COUNT + 1))
fi

if echo "$OUTPUT" | grep -q "approve prd"; then
  echo -e "  ${GREEN}PASS${NC}: approve 명령 안내 포함"
  PASS_COUNT=$((PASS_COUNT + 1))
else
  echo -e "  ${RED}FAIL${NC}: 'approve prd' 안내 없음"
  FAIL_COUNT=$((FAIL_COUNT + 1))
fi

# 정리
rm -f "$TMP_PRD"

print_summary
