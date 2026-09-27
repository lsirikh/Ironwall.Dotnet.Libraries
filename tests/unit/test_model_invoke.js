'use strict';
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),assert=require('node:assert/strict');
const {repository,git}=require('../../.claude/hooks/_harness-store'),{invoke}=require('../../.claude/hooks/_model-invoke');
const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-invoke-'));git(dir,['init']);let repo=repository(dir),checks=0;
(async()=>{
 await assert.rejects(()=>invoke(repo,{role:'documentation',prompt:'write'}),/ANALYSIS_REFERENCE_REQUIRED/);checks++;
 let calls=[];
 const runner=async(p,text)=>{calls.push({model:p.model,text});return {text:p.model==='opus'?'Specific reusable decision':'Readable document',model:{observed:[p.model],status:'verified'},usage:{output_tokens:2}};};
 const analysis=await invoke(repo,{role:'analysis',prompt:'analyze'},runner);
 const document=await invoke(repo,{role:'documentation',analysis_ref:analysis.invocation_id,prompt:'write'},runner);
 assert.deepEqual(calls.map(c=>c.model),['opus','sonnet']);checks++;
 assert.match(calls[1].text,/Specific reusable decision/);assert.equal(document.analysis_ref,analysis.invocation_id);checks++;
 assert.equal(repo.store.list('session/').filter(r=>r.value.status==='active').length,0);checks++;
 await assert.rejects(()=>invoke(repo,{role:'review',prompt:'review'},async()=>{throw Error('CLI unavailable');}),/CLI unavailable/);
 assert.equal(repo.store.list('session/').filter(r=>r.value.status==='active').length,0);assert.ok(repo.store.list('agent/').some(r=>r.value.status==='failed'));checks++;
 console.log('결과: '+checks+' PASS / 0 FAIL');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>repo.store.close());
