#!/usr/bin/env bash
# tests/manual/_helpers.sh — 시나리오 테스트 공통 헬퍼

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
STATE_FILE="$ROOT/docs/memory/pipeline-state.json"
AUDIT_LOG="$ROOT/docs/memory/audit-log.jsonl"
JOURNAL_FILE="$ROOT/docs/memory/dev-journal.md"
SESSION_FILE="$ROOT/docs/memory/session-context.md"
MARKER_FILE="$ROOT/.claude/.pending-prd-review"

PASS_COUNT=0
FAIL_COUNT=0

# 색상
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# assert: 명령이 차단되어야 함 (exit != 0 또는 stdout에 block JSON)
assert_block() {
  local desc="$1"
  shift
  local output
  output=$("$@" 2>&1)
  local rc=$?
  if [ $rc -ne 0 ] || echo "$output" | grep -q '"decision":"block"' || echo "$output" | grep -q 'BLOCKED\|차단'; then
    echo -e "  ${GREEN}PASS${NC}: $desc (blocked as expected)"
    PASS_COUNT=$((PASS_COUNT + 1))
  else
    echo -e "  ${RED}FAIL${NC}: $desc (expected block, got success)"
    echo "    output: $(echo "$output" | head -3)"
    FAIL_COUNT=$((FAIL_COUNT + 1))
  fi
}

# assert: 명령이 허용되어야 함 (exit 0 + block JSON 없음)
assert_allow() {
  local desc="$1"
  shift
  local output
  output=$("$@" 2>&1)
  local rc=$?
  if [ $rc -eq 0 ] && ! echo "$output" | grep -q '"decision":"block"'; then
    echo -e "  ${GREEN}PASS${NC}: $desc (allowed as expected)"
    PASS_COUNT=$((PASS_COUNT + 1))
  else
    echo -e "  ${RED}FAIL${NC}: $desc (expected allow, got block)"
    echo "    output: $(echo "$output" | head -3)"
    FAIL_COUNT=$((FAIL_COUNT + 1))
  fi
}

# assert: 파일/내용에 패턴 포함
assert_contains() {
  local desc="$1"
  local file="$2"
  local pattern="$3"
  if [ -f "$file" ] && grep -q "$pattern" "$file"; then
    echo -e "  ${GREEN}PASS${NC}: $desc"
    PASS_COUNT=$((PASS_COUNT + 1))
  else
    echo -e "  ${RED}FAIL${NC}: $desc — pattern '$pattern' not found in $file"
    FAIL_COUNT=$((FAIL_COUNT + 1))
  fi
}

# assert: 파일/내용에 패턴 미포함
assert_not_contains() {
  local desc="$1"
  local file="$2"
  local pattern="$3"
  if [ ! -f "$file" ] || ! grep -q "$pattern" "$file"; then
    echo -e "  ${GREEN}PASS${NC}: $desc"
    PASS_COUNT=$((PASS_COUNT + 1))
  else
    echo -e "  ${RED}FAIL${NC}: $desc — pattern '$pattern' should not be in $file"
    FAIL_COUNT=$((FAIL_COUNT + 1))
  fi
}

# state 백업/복원 — IMPL-01 (FR-01): 5개 파일 모두 격리
# pipeline-state, audit-log, dev-journal, session-context, .pending-prd-review (C-1 fix)
backup_state() {
  for f in "$STATE_FILE" "$AUDIT_LOG" "$JOURNAL_FILE" "$SESSION_FILE" "$MARKER_FILE"; do
    [ -f "$f" ] && cp "$f" "$f.test.bak" 2>/dev/null || true
  done
}
restore_state() {
  for f in "$STATE_FILE" "$AUDIT_LOG" "$JOURNAL_FILE" "$SESSION_FILE" "$MARKER_FILE"; do
    if [ -f "$f.test.bak" ]; then
      mv "$f.test.bak" "$f"
    else
      # 백업이 없으면 (시나리오 중에 새로 생긴 파일) 삭제
      [ -f "$f" ] && [ "$f" = "$MARKER_FILE" ] && rm -f "$f"
    fi
  done
}

# 자동 격리 — 시나리오 시작 시 backup, 종료 시 restore (정상/에러 모두)
isolate_test() {
  backup_state
  trap restore_state EXIT
}

# 결과 출력 (run-all.sh가 grep으로 카운트할 때 어설션 마커와 충돌하지 않도록 라벨 분리)
print_summary() {
  echo ""
  echo "─────────────────────────────"
  echo -e "  ${GREEN}Passed${NC}: $PASS_COUNT   ${RED}Failed${NC}: $FAIL_COUNT"
  echo "─────────────────────────────"
  if [ $FAIL_COUNT -gt 0 ]; then
    return 1
  fi
  return 0
}
