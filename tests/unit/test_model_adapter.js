'use strict';
const assert=require('node:assert/strict'),{provenance,matches}=require('../../.claude/hooks/_model-adapter');let count=0;
function check(name,fn){fn();count++;console.log('PASS '+name);}
check('runtime-selected model alone is explicitly unknown',()=>assert.equal(provenance('opus',[],'claude-opus-5').status,'unknown'));
check('all response models must match, not only the final response',()=>assert.equal(provenance('opus',['claude-sonnet-5','claude-opus-5']).status,'mismatch'));
check('aliases are verified against actual response family',()=>assert.equal(provenance('sonnet',['claude-sonnet-5']).status,'verified'));
check('a configured custom Fable ID is compared without substitution',()=>{assert.ok(matches('claude-fable-5-1','claude-fable-5-1'));assert.ok(!matches('claude-fable-5-1','claude-opus-5'));});
check('current runtime still records every actual model',()=>assert.deepEqual(provenance('current',['a','b','a']).observed,['a','b']));
console.log('결과: '+count+' PASS / 0 FAIL');
