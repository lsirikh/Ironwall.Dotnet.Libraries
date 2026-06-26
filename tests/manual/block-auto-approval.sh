#!/usr/bin/env bash
# TEST-02: Claude 자동 PRD 승인 차단 (FR-04)

source "$(dirname "$0")/_helpers.sh"

echo "=== TEST-02: PRD 자동 승인 차단 ==="

# 가짜 기존 PRD 생성 (Draft 상태)
EXISTING_PRD="$ROOT/docs/prds/_test_existing-prd.md"
cat > "$EXISTING_PRD" <<'EOF'
# Existing PRD

- **상태**: Draft
- **버전**: 1.0
EOF

# 1. 기존 파일을 Approved로 변경 시도 → 차단 기대
# JSON 내부에서 \n으로 줄바꿈 표현 (multiline 정규식 매칭을 위해)
echo "  test 1: 기존 PRD를 Approved로 Edit 시도"
assert_block "기존 PRD Approved 변경 차단" \
  env CLAUDE_TOOL_NAME='Edit' \
      CLAUDE_TOOL_INPUT='{"file_path":"docs/prds/_test_existing-prd.md","new_string":"# Existing PRD\n\n- **상태**: Approved\n- **버전**: 1.0"}' \
  node .claude/hooks/pre-tool-gate.js

# 2. 신규 파일 + Draft 작성 → 허용 기대 (예외)
echo "  test 2: 신규 PRD + Draft 작성 시도"
assert_allow "신규 PRD Draft 작성 허용" \
  env CLAUDE_TOOL_NAME='Write' CLAUDE_TOOL_INPUT='{"file_path":"docs/prds/_test_new-prd.md","content":"# New PRD\n- **상태**: Draft"}' \
  node .claude/hooks/pre-tool-gate.js

# 3. 신규 파일 + Approved 작성 시도 → 차단 기대
echo "  test 3: 신규 PRD에 Approved 직접 쓰기 시도"
assert_block "신규 PRD에 Approved 직접 쓰기 차단" \
  env CLAUDE_TOOL_NAME='Write' CLAUDE_TOOL_INPUT='{"file_path":"docs/prds/_test_new2-prd.md","content":"# New2\n- **상태**: Approved"}' \
  node .claude/hooks/pre-tool-gate.js

# 정리
rm -f "$EXISTING_PRD"

print_summary
