#!/usr/bin/env bash
# TEST-02: approve 명령이 PRD의 실제 버전 추출 (FR-02 / BUG-2)

source "$(dirname "$0")/_helpers.sh"

echo "=== TEST-02: approve 변경 이력 실제 버전 ==="

# 가짜 Draft PRD with 명시 버전 1.5
TMP_PRD="$ROOT/docs/prds/_test_version-prd.md"
cat > "$TMP_PRD" <<'EOF'
# Test Version PRD

- **상태**: Draft
- **버전**: 1.5

## 변경 이력

| 날짜 | 버전 | 변경 항목 | 변경 이유 | 영향 범위 |
|------|------|---------|---------|---------|
| 2026-04-10 | 1.0 | 초안 | 테스트 | — |
| 2026-04-10 | 1.5 | 가짜 수정 | 테스트 | — |
EOF

# touch로 mtime 최신화
touch "$TMP_PRD"

# approve 실행
node .claude/hooks/advance-phase.js approve prd "버전 추출 테스트" 2>&1 | head -3

# 검증: 변경 이력에 1.5 (실제 버전) 포함, x.x 미포함
if grep "사용자 승인" "$TMP_PRD" | grep -q "1\.5"; then
  echo -e "  ${GREEN}PASS${NC}: 실제 버전 1.5 추출"
  PASS_COUNT=$((PASS_COUNT + 1))
else
  echo -e "  ${RED}FAIL${NC}: 실제 버전 추출 안 됨"
  grep "사용자 승인" "$TMP_PRD" | head -1
  FAIL_COUNT=$((FAIL_COUNT + 1))
fi

if grep "사용자 승인" "$TMP_PRD" | grep -q "x\.x"; then
  echo -e "  ${RED}FAIL${NC}: 'x.x' placeholder가 여전히 존재"
  FAIL_COUNT=$((FAIL_COUNT + 1))
else
  echo -e "  ${GREEN}PASS${NC}: 'x.x' placeholder 없음"
  PASS_COUNT=$((PASS_COUNT + 1))
fi

# 정리
rm -f "$TMP_PRD"
print_summary
