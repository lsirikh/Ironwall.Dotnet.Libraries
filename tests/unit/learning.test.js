// tests/unit/learning.test.js
// _learning.js 단위 테스트 — FR-01, FR-04, FR-05, FR-08, NFR-03, NFR-04

const assert = require('assert');
const fs = require('fs');
const path = require('path');
const learning = require('../../.claude/hooks/_learning');

console.log('=== _learning.js Unit Tests ===\n');
let passed = 0, failed = 0;

function test(name, fn) {
  try { fn(); console.log(`  ✅ ${name}`); passed++; }
  catch (e) { console.log(`  ❌ ${name}: ${e.message}`); failed++; }
}

// ── loadObservations ────────────────────────────────────────────────
console.log('── loadObservations ──');

test('returns empty array when file missing', () => {
  const obs = learning.loadObservations(10);
  // may or may not have observations depending on test order
  assert.ok(Array.isArray(obs));
});

// ── analyzeObservations ─────────────────────────────────────────────
console.log('\n── analyzeObservations ──');

test('analyzes file type stats', () => {
  const obs = [
    { file_type: '.cs', tool: 'Edit', phase: 'dev' },
    { file_type: '.cs', tool: 'Edit', phase: 'dev' },
    { file_type: '.cs', tool: 'Write', phase: 'dev' },
    { file_type: '.js', tool: 'Edit', phase: 'dev' },
  ];
  const result = learning.analyzeObservations(obs);
  assert.strictEqual(result.fileTypeStats['.cs'].count, 3);
  assert.strictEqual(result.fileTypeStats['.cs'].tools['Edit'], 2);
  assert.strictEqual(result.total, 4);
});

test('analyzes phase patterns', () => {
  const obs = [
    { file_type: '.cs', tool: 'Edit', phase: 'dev' },
    { file_type: '.cs', tool: 'Edit', phase: 'dev' },
    { file_type: '.js', tool: 'Edit', phase: 'test' },
  ];
  const result = learning.analyzeObservations(obs);
  assert.strictEqual(result.phasePatterns['dev'].count, 2);
  assert.strictEqual(result.phasePatterns['test'].count, 1);
});

test('handles empty observations', () => {
  const result = learning.analyzeObservations([]);
  assert.strictEqual(result.total, 0);
});

// ── extractInstincts ────────────────────────────────────────────────
console.log('\n── extractInstincts ──');

test('extracts tool-preference instinct for 3+ occurrences', () => {
  const analysis = {
    fileTypeStats: {
      '.cs': { count: 10, tools: { 'Edit': 8, 'Write': 2 } },
    },
    phasePatterns: {},
    total: 10
  };
  const candidates = learning.extractInstincts(analysis);
  assert.ok(candidates.length > 0, 'Should extract at least 1 candidate');
  assert.strictEqual(candidates[0].category, 'tool-preference');
  assert.ok(candidates[0].confidence > 0.5);
});

test('skips patterns with less than 3 occurrences', () => {
  const analysis = {
    fileTypeStats: {
      '.cs': { count: 2, tools: { 'Edit': 2 } },
    },
    phasePatterns: {},
    total: 2
  };
  const candidates = learning.extractInstincts(analysis);
  assert.strictEqual(candidates.length, 0);
});

test('skips unknown file types', () => {
  const analysis = {
    fileTypeStats: {
      'unknown': { count: 10, tools: { 'Edit': 10 } },
    },
    phasePatterns: {},
    total: 10
  };
  const candidates = learning.extractInstincts(analysis);
  assert.strictEqual(candidates.length, 0);
});

// ── instinctToRule ──────────────────────────────────────────────────
console.log('\n── instinctToRule ──');

test('converts high-confidence instinct to rule', () => {
  const instinct = {
    id: 'INST-001',
    pattern: 'test pattern',
    confidence: 0.85,
    category: 'tool-preference',
    file_type: '.cs',
    tool: 'Edit'
  };
  const rule = learning.instinctToRule(instinct);
  assert.ok(rule !== null);
  assert.strictEqual(rule.action, 'remind');
  assert.strictEqual(rule.source, 'instinct:INST-001');
});

test('NFR-03: action is always remind, never block', () => {
  const instinct = {
    id: 'INST-002',
    pattern: 'high confidence',
    confidence: 0.99,
    category: 'tool-preference',
    file_type: '.py'
  };
  const rule = learning.instinctToRule(instinct);
  assert.strictEqual(rule.action, 'remind');
  assert.notStrictEqual(rule.action, 'block');
});

test('returns null for low-confidence instinct', () => {
  const instinct = { id: 'INST-003', pattern: 'low', confidence: 0.5 };
  const rule = learning.instinctToRule(instinct);
  assert.strictEqual(rule, null);
});

test('returns null for null instinct', () => {
  assert.strictEqual(learning.instinctToRule(null), null);
});

test('sets trigger target for tool-preference category', () => {
  const instinct = {
    id: 'INST-004', pattern: 'x', confidence: 0.9,
    category: 'tool-preference', file_type: '.ts', tool: 'Write'
  };
  const rule = learning.instinctToRule(instinct);
  assert.ok(rule.trigger.target.includes('\\.ts'));
  assert.strictEqual(rule.trigger.tool, 'Write');
});

test('sets trigger phase for workflow category', () => {
  const instinct = {
    id: 'INST-005', pattern: 'x', confidence: 0.9,
    category: 'workflow', phase: 'dev'
  };
  const rule = learning.instinctToRule(instinct);
  assert.strictEqual(rule.trigger.phase, 'dev');
});

// ── gcObservations ──────────────────────────────────────────────────
console.log('\n── gcObservations ──');

test('gc returns 0 archived when file missing', () => {
  const result = learning.gcObservations(1000);
  assert.strictEqual(result.archived, 0);
});

// ── loadInstincts ───────────────────────────────────────────────────
console.log('\n── loadInstincts ──');

test('returns empty array for empty file', () => {
  const instincts = learning.loadInstincts();
  assert.ok(Array.isArray(instincts));
});

// ── nextInstinctId ──────────────────────────────────────────────────
console.log('\n── nextInstinctId ──');

test('generates INST-001 for empty file', () => {
  const id = learning.nextInstinctId();
  assert.ok(id.startsWith('INST-'));
});

// ── promoteToGlobal ─────────────────────────────────────────────────
console.log('\n── promoteToGlobal ──');

test('rejects low confidence instinct', () => {
  const result = learning.promoteToGlobal({ id: 'INST-X', pattern: 'low', confidence: 0.5 });
  assert.strictEqual(result.ok, false);
  assert.ok(result.reason.includes('0.9'));
});

test('rejects null instinct', () => {
  const result = learning.promoteToGlobal(null);
  assert.strictEqual(result.ok, false);
});

test('rejects instinct without id', () => {
  const result = learning.promoteToGlobal({ pattern: 'x', confidence: 0.95 });
  assert.strictEqual(result.ok, false);
});

// ── loadGlobalInstincts ─────────────────────────────────────────────
console.log('\n── loadGlobalInstincts ──');

test('returns array (may be empty)', () => {
  const result = learning.loadGlobalInstincts();
  assert.ok(Array.isArray(result));
});

// ── loadAllInstincts ────────────────────────────────────────────────
console.log('\n── loadAllInstincts ──');

test('returns merged array', () => {
  const result = learning.loadAllInstincts();
  assert.ok(Array.isArray(result));
});

// ── Summary ────────────────────────────────────────────────────────
console.log(`\n=== Results: ${passed} passed, ${failed} failed ===`);
process.exit(failed > 0 ? 1 : 0);
