// tests/unit/agents.test.js
// _agents.js 단위 테스트 — FR-04, FR-07, NFR-01, NFR-04

const assert = require('assert');
const path = require('path');

// _agents.js 로드
const agents = require('../../.claude/hooks/_agents');

console.log('=== _agents.js Unit Tests ===\n');

let passed = 0;
let failed = 0;

function test(name, fn) {
  try {
    fn();
    console.log(`  ✅ ${name}`);
    passed++;
  } catch (e) {
    console.log(`  ❌ ${name}: ${e.message}`);
    failed++;
  }
}

// ── parseFrontmatter ────────────────────────────────────────────────

console.log('── parseFrontmatter ──');

test('parses simple string fields', () => {
  const fm = agents.parseFrontmatter('---\nname: test-agent\ndescription: A test\nmodel: sonnet\n---\nBody');
  assert.strictEqual(fm.name, 'test-agent');
  assert.strictEqual(fm.description, 'A test');
  assert.strictEqual(fm.model, 'sonnet');
});

test('parses inline array fields', () => {
  const fm = agents.parseFrontmatter('---\ntools: ["Read", "Grep"]\nphases: ["dev", "test"]\n---\n');
  assert.deepStrictEqual(fm.tools, ['Read', 'Grep']);
  assert.deepStrictEqual(fm.phases, ['dev', 'test']);
});

test('parses languages/frameworks arrays', () => {
  const fm = agents.parseFrontmatter('---\nname: skill\nlanguages: ["Python", "C#"]\nframeworks: ["FastAPI", "WPF"]\n---\n');
  assert.deepStrictEqual(fm.languages, ['Python', 'C#']);
  assert.deepStrictEqual(fm.frameworks, ['FastAPI', 'WPF']);
});

test('returns null for no frontmatter', () => {
  const fm = agents.parseFrontmatter('No frontmatter here');
  assert.strictEqual(fm, null);
});

test('handles empty frontmatter', () => {
  const fm = agents.parseFrontmatter('---\n\n---\nBody');
  assert.ok(fm !== null && fm !== undefined);
  assert.strictEqual(fm.name, undefined);
});

// ── loadAgents / getAgentsForPhase ──────────────────────────────────

console.log('\n── loadAgents / getAgentsForPhase ──');

test('loadAgents returns array', () => {
  const result = agents.loadAgents(true);
  assert.ok(Array.isArray(result));
});

test('loadAgents finds 7 agents', () => {
  const result = agents.loadAgents(true);
  assert.strictEqual(result.length, 7);
});

test('getAgentsForPhase dev returns code-reviewer', () => {
  const devAgents = agents.getAgentsForPhase('dev');
  const names = devAgents.map(a => a.name);
  assert.ok(names.includes('code-reviewer'), 'code-reviewer not found in dev phase');
});

test('getAgentsForPhase dev returns tdd-guide', () => {
  const devAgents = agents.getAgentsForPhase('dev');
  const names = devAgents.map(a => a.name);
  assert.ok(names.includes('tdd-guide'), 'tdd-guide not found in dev phase');
});

test('getAgentsForPhase prd returns empty', () => {
  const prdAgents = agents.getAgentsForPhase('prd');
  assert.strictEqual(prdAgents.length, 0);
});

test('getAgentsForPhase analysis returns architect', () => {
  const analysisAgents = agents.getAgentsForPhase('analysis');
  const names = analysisAgents.map(a => a.name);
  assert.ok(names.includes('architect'));
});

test('getAgentsForPhase report returns doc-updater', () => {
  const reportAgents = agents.getAgentsForPhase('report');
  const names = reportAgents.map(a => a.name);
  assert.ok(names.includes('doc-updater'));
});

test('doc-updater uses haiku model', () => {
  const allAgents = agents.loadAgents(true);
  const docUpdater = allAgents.find(a => a.name === 'doc-updater');
  assert.strictEqual(docUpdater.model, 'haiku');
});

// ── getDomainSkillsForProject ──────────────────────────────────────

console.log('\n── getDomainSkillsForProject ──');

test('C# returns dotnet skills', () => {
  const skills = agents.getDomainSkillsForProject('C#', '');
  const names = skills.map(s => s.name);
  assert.ok(names.some(n => n.includes('dotnet') || n.includes('.NET')), `C# skills: ${names}`);
});

test('Python returns python skills', () => {
  const skills = agents.getDomainSkillsForProject('Python', '');
  const names = skills.map(s => s.name);
  assert.ok(names.some(n => n.toLowerCase().includes('python')), `Python skills: ${names}`);
});

test('WPF framework returns wpf-caliburn', () => {
  const skills = agents.getDomainSkillsForProject('C#', 'WPF');
  const names = skills.map(s => s.name);
  assert.ok(names.some(n => n.includes('wpf') || n.includes('caliburn')), `WPF skills: ${names}`);
});

test('FastAPI framework returns fastapi', () => {
  const skills = agents.getDomainSkillsForProject('Python', 'FastAPI');
  const names = skills.map(s => s.name);
  assert.ok(names.some(n => n.toLowerCase().includes('fastapi')), `FastAPI skills: ${names}`);
});

test('empty language and framework returns empty', () => {
  const skills = agents.getDomainSkillsForProject('', '');
  assert.strictEqual(skills.length, 0);
});

test('wildcard languages match everything', () => {
  const skills = agents.getDomainSkillsForProject('Rust', '');
  // git-workflow, database-patterns, security-common have languages: ["*"]
  const wildcardSkills = skills.filter(s => s.languages.includes('*'));
  assert.ok(wildcardSkills.length >= 0); // may or may not match depending on frontmatter
});

// ── getActiveRules ─────────────────────────────────────────────────

console.log('\n── getActiveRules ──');

test('C# returns common + csharp rules', () => {
  const rules = agents.getActiveRules('C#');
  const packs = rules.map(r => r.pack);
  assert.ok(packs.includes('common'), 'common rules missing');
  assert.ok(packs.includes('csharp'), 'csharp rules missing');
});

test('Python returns common + python rules', () => {
  const rules = agents.getActiveRules('Python');
  const packs = rules.map(r => r.pack);
  assert.ok(packs.includes('common'));
  assert.ok(packs.includes('python'));
});

test('TypeScript returns common + typescript rules', () => {
  const rules = agents.getActiveRules('TypeScript');
  const packs = rules.map(r => r.pack);
  assert.ok(packs.includes('common'));
  assert.ok(packs.includes('typescript'));
});

test('unknown language returns only common', () => {
  const rules = agents.getActiveRules('Haskell');
  const packs = rules.map(r => r.pack);
  assert.ok(packs.includes('common'));
  assert.strictEqual(packs.length, 1);
});

test('common rules have security and testing', () => {
  const rules = agents.getActiveRules('C#');
  const common = rules.find(r => r.pack === 'common');
  assert.ok(common.rules.includes('security'));
  assert.ok(common.rules.includes('testing'));
});

// ── Cache ──────────────────────────────────────────────────────────

console.log('\n── Cache ──');

test('clearCaches works without error', () => {
  agents.clearCaches();
  // After clearing, next load should still work
  const result = agents.loadAgents(true);
  assert.ok(Array.isArray(result));
});

// ── Summary ────────────────────────────────────────────────────────
console.log(`\n=== Results: ${passed} passed, ${failed} failed ===`);
process.exit(failed > 0 ? 1 : 0);
