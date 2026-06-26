#!/usr/bin/env bash
# TEST-01 + TEST-03 + TEST-05 + TEST-06: PRD 검토 마커 통합 시나리오
# (마커 생성 + Bash approve 차단 + 키워드 해제)

source "$(dirname "$0")/_helpers.sh"

echo "=== TEST: PRD 검토 마커 통합 시나리오 ==="

isolate_test

# 1. Draft PRD 생성 (post-write-sync 트리거)
TMP_PRD="$ROOT/docs/prds/_test_marker-prd.md"
cat > "$TMP_PRD" <<'EOF'
# Test Marker PRD

- **상태**: Draft
- **버전**: 1.0
EOF

# post-write-sync 직접 호출 (Claude Code Hook 시뮬레이션)
CLAUDE_TOOL_NAME='Write' CLAUDE_TOOL_INPUT='{"file_path":"docs/prds/_test_marker-prd.md","content":"# Test"}' \
  node .claude/hooks/post-write-sync.js >/dev/null 2>&1

# 2. 마커 파일 존재 검증
MARKER="$ROOT/.claude/.pending-prd-review"
if [ -f "$MARKER" ]; then
  echo -e "  ${GREEN}PASS${NC}: 마커 파일 자동 생성"
  PASS_COUNT=$((PASS_COUNT + 1))
else
  echo -e "  ${RED}FAIL${NC}: 마커 미생성"
  FAIL_COUNT=$((FAIL_COUNT + 1))
fi

# 3. Bash로 approve prd 호출 시도 → 차단 기대
assert_block "Bash approve 차단 (마커 존재)" \
  env CLAUDE_TOOL_NAME='Bash' \
      CLAUDE_TOOL_INPUT='{"command":"node .claude/hooks/advance-phase.js approve prd \"우회 시도\""}' \
  node .claude/hooks/pre-tool-gate.js

# 4. 사용자 메시지 시뮬레이션 — "approve" stdin 주입
echo "approve" | node .claude/hooks/session-gate.js >/dev/null 2>&1

# 5. 마커 자동 삭제 검증
if [ ! -f "$MARKER" ]; then
  echo -e "  ${GREEN}PASS${NC}: 사용자 'approve' 키워드 → 마커 자동 삭제"
  PASS_COUNT=$((PASS_COUNT + 1))
else
  echo -e "  ${RED}FAIL${NC}: 마커 미삭제"
  FAIL_COUNT=$((FAIL_COUNT + 1))
fi

# 정리
rm -f "$TMP_PRD"

print_summary
