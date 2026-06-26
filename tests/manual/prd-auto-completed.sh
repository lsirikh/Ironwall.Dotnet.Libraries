#!/usr/bin/env bash
# TEST-01: PRD 자동 Completed 전환 (FR-01)

source "$(dirname "$0")/_helpers.sh"

echo "=== TEST-01: complete 시 PRD 자동 Completed ==="

# 가짜 Approved PRD 생성
TMP_PRD="$ROOT/docs/prds/_test_completed-prd.md"
cat > "$TMP_PRD" <<'EOF'
# Test Completed PRD

- **상태**: Approved
- **버전**: 1.5

## 변경 이력

| 날짜 | 버전 | 변경 항목 | 변경 이유 | 영향 범위 |
|------|------|---------|---------|---------|
| 2026-04-10 | 1.0 | 초안 | 테스트 | — |
| 2026-04-10 | 1.5 | 사용자 승인 | 가짜 | — |
EOF

# state.activePrd 임시 설정 + complete 강제
backup_state
node -e "
const fs = require('fs');
const f = 'docs/memory/pipeline-state.json';
const s = JSON.parse(fs.readFileSync(f, 'utf8'));
s.activePrd = 'docs/prds/_test_completed-prd.md';
s.phase = 'report';
s.track = 'C';
fs.writeFileSync(f, JSON.stringify(s, null, 2) + '\n');
" 2>/dev/null

# complete 트리거 (게이트 우회)
node .claude/hooks/advance-phase.js complete --force 2>&1 | head -5

# 검증
assert_contains "PRD 상태 Completed" "$TMP_PRD" "^- \*\*상태\*\*: Completed"
assert_contains "변경 이력에 자동 Completed 행" "$TMP_PRD" "사이클 종료"

# 정리
rm -f "$TMP_PRD"
restore_state

print_summary
