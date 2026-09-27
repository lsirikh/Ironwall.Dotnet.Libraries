'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path'),{execFileSync}=require('node:child_process');
const {repository,git}=require('../../.claude/hooks/_harness-store'),managed=require('../../.claude/hooks/_managed-work'),{Integration}=require('../../.claude/hooks/_integration'),wt=require('../../.claude/hooks/_worktree');
const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-v14-managed-'));let repo,count=0,calls=0;
function command(script){return [process.execPath,'-e',script];}
async function check(name,fn){await fn();count++;console.log('PASS '+name);}
(async()=>{
 git(dir,['init']);fs.mkdirSync(path.join(dir,'docs','memory'),{recursive:true});fs.writeFileSync(path.join(dir,'docs','memory','decisions.jsonl'),'{"id":"kept-decision"}\n');
 fs.writeFileSync(path.join(dir,'value.txt'),'zero');fs.writeFileSync(path.join(dir,'prd.md'),'# User feature\n');fs.writeFileSync(path.join(dir,'plan.md'),'# Plan\n');
 git(dir,['add','.']);git(dir,['-c','user.name=Harness Test','-c','user.email=harness@example.invalid','commit','-m','baseline']);git(dir,['branch','integration-target']);
 repo=repository(dir);
 const test={mode:'automated',format:'json',command:command("const fs=require('fs'),assert=require('assert');assert.ok(['zero','one'].includes(fs.readFileSync('value.txt','utf8')));console.log(JSON.stringify({status:'passed',checks:1}));")};
 const spec={track:'B',title:'Change one value',nodes:[{id:'A',files:['value.txt'],tests:[test],max_attempts:2}]};
 await check('work creation requires explicit request reference',async()=>assert.throws(()=>managed.create(repo,spec,''),/USER_REQUEST_REFERENCE_REQUIRED/));
 await check('C requires a readable PRD and plan',async()=>assert.throws(()=>managed.create(repo,{...spec,track:'C'},'test fixture'),/DOCUMENT_REQUIRED/));
 const work=managed.create(repo,spec,'explicit automated test fixture');
 const runner=async(profile,prompt)=>{calls++;assert.equal(profile.model,'current');return {text:JSON.stringify({summary:'value changed',files:[{path:'value.txt',content:'one'}]}),model:{requested:'current',status:'unknown',observed:[]}};};
 await check('a B node executes once, verifies and commits in its own worktree',async()=>{
  const notices=[],url='http://127.0.0.1:1234';
  const result=await managed.run(repo,work.work_id,{runtime:'codex',modelRunner:runner,url,onNotice:message=>notices.push(message)});assert.equal(result.status,'verified');assert.equal(calls,1);
  for(const detail of ['작업 시작·재개','A 시작','A 테스트·검증','A 검증 완료','이번 세션 종료'])assert.ok(notices.some(line=>line.includes(url+'/workflows?work='+work.work_id)&&line.endsWith(detail)),detail);
  const n=repo.store.get('node/'+work.work_id+'/A').value;assert.equal(n.status,'verified');assert.equal(fs.readFileSync(path.join(n.candidate.workspace,'value.txt'),'utf8'),'one');assert.equal(fs.readFileSync(path.join(dir,'value.txt'),'utf8'),'zero');assert.match(n.candidate.commit,/^[0-9a-f]{40}$/);assert.match(git(dir,['notes','--ref=harness-evidence','show',n.candidate.commit]),/node-tested/);
 });
 const integration=new Integration(repo);let queued,validated;
 await check('combined candidate is tested before becoming promotable',async()=>{queued=integration.enqueue(work.work_id,'refs/heads/integration-target');assert.throws(()=>integration.promote(queued.integration_id),/INTEGRATION_NOT_VALIDATED/);validated=await integration.validate(queued.integration_id);assert.equal(validated.status,'validated');assert.equal(validated.evidence[0].checks,1);});
 await check('integration CAS advances only the explicit target',async()=>{const before=git(dir,['rev-parse','HEAD']);const r=integration.promote(queued.integration_id);assert.equal(r.status,'integrated');assert.equal(git(dir,['rev-parse','refs/heads/integration-target']),r.candidate_ref);assert.equal(git(dir,['rev-parse','HEAD']),before);});
 await check('rollback creates a new commit and preserves decision history',async()=>{const r=await integration.rollback(queued.integration_id);assert.equal(r.status,'integrated');assert.equal(git(dir,['show','refs/heads/integration-target:value.txt']),'zero');assert.equal(git(dir,['show','refs/heads/integration-target:docs/memory/decisions.jsonl']),'{"id":"kept-decision"}');assert.notEqual(r.candidate_ref,queued.old_ref);});
 await check('out-of-scope model output is rejected without writing any file',async()=>{
  const n=repo.store.get('node/'+work.work_id+'/A').value;
  assert.throws(()=>wt.applyFiles(n.candidate.workspace,[{path:'value.txt',mode:'write'}],[{path:'value.txt',content:'changed'},{path:'outside.txt',content:'escape'}]),/OUT_OF_SCOPE_CHANGE/);
  assert.equal(fs.readFileSync(path.join(n.candidate.workspace,'value.txt'),'utf8'),'one');
 });
 await check('model payload cannot override a validated destination',async()=>{
  const n=repo.store.get('node/'+work.work_id+'/A').value,victim=path.join(dir,'victim.txt');
  wt.applyFiles(n.candidate.workspace,[{path:'value.txt',mode:'write'}],[{path:'value.txt',file:victim,content:'one'}]);
  assert.equal(fs.existsSync(victim),false);
 });
 await check('pre-staged unverified content cannot enter a candidate',async()=>{
  const n=repo.store.get('node/'+work.work_id+'/A').value,workspace=n.candidate.workspace;
  fs.writeFileSync(path.join(workspace,'outside.txt'),'hidden bytes');git(workspace,['add','outside.txt']);fs.unlinkSync(path.join(workspace,'outside.txt'));
  assert.throws(()=>wt.commitScoped(workspace,n.files,'must not commit'),/DIRTY_INDEX/);
 });
 await check('authorized command nodes may edit before separate verification',async()=>{
  const w=managed.create(repo,{...spec,title:'command node',nodes:[{...spec.nodes[0],executor:'command',command:command("require('fs').writeFileSync('value.txt','one')")}]},'explicit command fixture');
  const result=await managed.run(repo,w.work_id,{runtime:'claude',onNotice:()=>{}});assert.equal(result.status,'verified');
 });
 await check('zero-test and source-mutating verifiers cannot produce success',async()=>{
  await assert.rejects(()=>wt.verifyCommand(dir,{mode:'automated',format:'json',command:command("console.log(JSON.stringify({status:'passed',checks:0}))")}),/EMPTY_VERIFICATION/);
  await assert.rejects(()=>wt.verifyCommand(dir,{mode:'automated',format:'json',command:command("require('fs').writeFileSync('value.txt','changed');console.log(JSON.stringify({status:'passed',checks:1}))")}),/TEST_MODIFIED_SOURCE/);
  fs.writeFileSync(path.join(dir,'value.txt'),'zero');
 });
 await check('failed verification retries only up to the declared loop bound',async()=>{
  const failure=managed.create(repo,{...spec,title:'bounded failure',nodes:[{...spec.nodes[0],files:['broken.txt'],tests:[{mode:'automated',command:command("process.exit(2)") }]}]},'explicit failure fixture');let attempts=0;
  await assert.rejects(()=>managed.run(repo,failure.work_id,{runtime:'claude',onNotice:()=>{},modelRunner:async()=>{attempts++;return {text:JSON.stringify({summary:'attempt',files:[{path:'broken.txt',content:'x'}]})};}}),/EMPTY_VERIFICATION|TEST_FAILED/);
  assert.equal(attempts,2);assert.equal(repo.store.get('node/'+failure.work_id+'/A').value.status,'failed');
 });

 await check('one C session runs independent nodes concurrently',async()=>{
  const w=managed.create(repo,{track:'C',title:'parallel nodes',prd:'prd.md',plan:'plan.md',nodes:['left','right'].map(id=>({id,files:[id+'.txt'],tests:[{mode:'automated',format:'json',command:command("require('assert').equal(require('fs').readFileSync('"+id+".txt','utf8'),'ready');console.log(JSON.stringify({status:'passed',checks:1}))") }]}))},'parallel fixture');
  let inFlight=0,maximum=0;
  const r=await managed.run(repo,w.work_id,{runtime:'codex',onNotice:()=>{},modelRunner:async(p,prompt)=>{inFlight++;maximum=Math.max(maximum,inFlight);await new Promise(r=>setTimeout(r,300));const packet=JSON.parse(prompt.split('Context:\n')[1]);inFlight--;return {text:JSON.stringify({summary:'ready',files:[{path:packet.node_id+'.txt',content:'ready'}]})};}});
  assert.equal(r.status,'verified');assert.equal(maximum,2);
 });
 await check('a commit before state failure remains in the recovered candidate',async()=>{
  const w=managed.create(repo,{...spec,title:'commit crash'},'crash fixture'),original=repo.store.transaction;let injected=false,attempt=0;
  repo.store.transaction=function(fn,operation){return original.call(this,tx=>fn({...tx,put:(key,value,version)=>{if(!injected&&key==='node/'+w.work_id+'/A'&&value.candidate){injected=true;throw Error('INJECTED_COMMIT_BEFORE_STATE');}return tx.put(key,value,version);}}),operation);};
  try{
   const r=await managed.run(repo,w.work_id,{runtime:'claude',onNotice:()=>{},modelRunner:async()=>{attempt++;return {text:JSON.stringify({summary:'recovered',files:attempt===1?[{path:'value.txt',content:'one'}]:[]})};}});
   assert.equal(r.status,'verified');assert.equal(attempt,2);const n=repo.store.get('node/'+w.work_id+'/A').value;assert.equal(n.candidate.changed,true);assert.equal(git(dir,['show',n.candidate.commit+':value.txt']),'one');
  }finally{repo.store.transaction=original;}
 });
 await check('invalid preclaim profile fails once instead of looping',async()=>{
  const w=managed.create(repo,{...spec,title:'invalid role',nodes:[{...spec.nodes[0],role:'unknown_role'}]},'invalid fixture');let notices=0;
  await assert.rejects(()=>managed.run(repo,w.work_id,{runtime:'claude',onNotice:()=>notices++}),/UNKNOWN_ROLE/);assert.ok(notices<=1);
 });


 await check('optional B documents cannot read outside the project',async()=>{
  assert.throws(()=>managed.create(repo,{...spec,prd:'../private.txt'},'invalid path fixture'),/INVALID_PATH|PATH_|OUTSIDE|ESCAPE|INVALID_RESOURCE/);
 });
 await check('documentation receives the original analysis commit even after a dependent edit',async()=>{
  const w=managed.create(repo,{track:'C',title:'immutable analysis',prd:'prd.md',plan:'plan.md',nodes:[
   {id:'analysis',role:'analysis',files:['analysis.txt'],tests:[test]},
   {id:'change',deps:['analysis'],files:['analysis.txt'],tests:[test]},
   {id:'document',role:'documentation',deps:['analysis','change'],files:['document.txt'],tests:[test]}
  ]},'analysis lineage fixture');let received;
  const r=await managed.run(repo,w.work_id,{runtime:'claude',onNotice:()=>{},modelRunner:async(p,prompt)=>{
   const packet=JSON.parse(prompt.split('Context:\n')[1]);let files;
   if(packet.node_id==='analysis')files=[{path:'analysis.txt',content:'ORIGINAL_ANALYSIS\n'}];
   else if(packet.node_id==='change')files=[{path:'analysis.txt',content:'LATER_REPLACEMENT\n'}];
   else {received=packet;files=[{path:'document.txt',content:'Document based on original analysis'}];}
   return {text:JSON.stringify({summary:packet.node_id,files})};
  }});
  assert.equal(r.status,'verified');const original=received.upstream.find(x=>x.node_id==='analysis');assert.equal(original.artifacts[0].content,'ORIGINAL_ANALYSIS\n');assert.equal(original.artifacts[0].commit,original.commit);assert.equal(received.documents.prd.content,'# User feature\n');
 });

 // [v15/N-02] A verdict the parser cannot read is as bad as one it reads wrong. This repository's
 //   own test_command prints none of the tokens the parser knew, and JUnit-style runners put the
 //   failure count after its label — so a failing build could still be read as a clean pass.
 await check('this repository\'s own runner output is a verdict',()=>{
  assert.equal(wt.parseChecks('\n  69/69 파일 · 단언 2320  (301.9s)\n'),2320);
  assert.equal(wt.parseChecks('\n  68/69 파일 · 단언 2313  (310.3s)\n'),0);
  assert.equal(wt.parseChecks('  69/69 파일 · 단언 2320 · SKIP 2  (301.9s)'),2320);
 });
 await check('a label-first failure count defeats another family\'s pass',()=>{
  assert.equal(wt.parseChecks('Tests run: 10, Failures: 3, Errors: 0\n# pass 5\n# fail 0'),0);
  assert.equal(wt.parseChecks('Tests run: 10, Failures: 0, Errors: 2'),0);
  assert.equal(wt.parseChecks('Tests run: 10, Failures: 0, Errors: 0, Skipped: 0'),10);
 });
 await check('dotnet and mocha summaries are verdicts',()=>{
  assert.equal(wt.parseChecks('Failed:     0, Passed:    10, Skipped:     0, Total:    10'),10);
  assert.equal(wt.parseChecks('Failed:     2, Passed:     8, Skipped:     0, Total:    10'),0);
  assert.equal(wt.parseChecks('  10 passing (15ms)'),10);
  assert.equal(wt.parseChecks('  10 passing (15ms)\n  2 failing'),0);
 });
 await check('a thousands separator is not truncated',()=>{
  assert.equal(wt.parseChecks('===== 1,234 passed in 9.9s ====='),1234);
 });
 // [v15/N-02 · Loop A] The two directions the widened vocabulary broke.
 //   A fail count on its own line was dropped entirely; a label-first pattern with no anchor
 //   read test names and stderr prose as run summaries.
 await check('a failure count on its own line is still a failure',()=>{
  assert.equal(wt.parseChecks('10 passed\n2 failed'),0);
  assert.equal(wt.parseChecks('10 passed\n0 failed'),10);
  assert.equal(wt.parseChecks('  2 failing\n  10 passing (15ms)'),0);
 });
 await check('label-first patterns outside a summary are not verdicts',()=>{
  assert.equal(wt.parseChecks('✓ retries login after Failed: 401 response (12 ms)\n148 passing (2s)'),148);
  assert.equal(wt.parseChecks('Health check Failed: 1 time, retrying...\n  10 passing (15ms)'),10);
  assert.equal(wt.parseChecks('Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10'),10);
  assert.equal(wt.parseChecks('Passed!  - Failed:     2, Passed:     8, Skipped:     0, Total:    10'),0);
 });
 await check('the runner still prints the summary this parser reads',()=>{
  const runner=fs.readFileSync(path.resolve(__dirname,'../run.js'),'utf8');
  assert.match(runner,/파일 · 단언/,'tests/run.js must keep the summary literal SUITE_SUMMARY parses');
 });
 await check('an omitted format is accepted as auto',()=>{
  for(const format of [null,'']){
   const r=managed.create(repo,{track:'B',title:'blank format '+String(format),nodes:[{id:'N1',files:['value.txt'],tests:[{mode:'automated',format,command:command("console.log('결과: 1 PASS / 0 FAIL')")}]}]},'req');
   assert.ok(r.work_id,'format '+JSON.stringify(format)+' should register');
  }
 });
 await check('an unsupported test format is refused at registration',()=>{
  assert.throws(()=>managed.create(repo,{track:'B',title:'bad format',nodes:[{id:'N1',files:['value.txt'],tests:[{mode:'automated',format:'JSON',command:command('console.log(1)')}]}]},'req'),/INVALID_TEST_FORMAT/);
 });

 // [v15/N-01] Node verification, integration validation and rollback validation are the three
 //   last gates before a change is called done. They must keep sharing one verdict parser —
 //   a second copy is where the three quietly start disagreeing about what "passed" means.
 await check('the three verification gates share one verdict parser',()=>{
  const hooks=path.resolve(__dirname,'../../.claude/hooks');
  const source=name=>fs.readFileSync(path.join(hooks,name),'utf8');
  const [work,integration]=['_managed-work.js','_integration.js'].map(source);
  for(const [name,text] of [['_managed-work.js',work],['_integration.js',integration]]){
   assert.match(text,/verifyCommand/,name+' must verify through _worktree.verifyCommand');
   assert.ok(!text.includes('parseChecks'),name+' must not reach past verifyCommand to the parser');
  }
  assert.equal((source('_worktree.js').match(/parseChecks\(/g)||[]).length,2,'parseChecks is defined once and called once, inside verifyCommand');
  assert.equal(wt.parseChecks('결과: 3 PASS / 2 FAIL\n결과: 10 PASS / 0 FAIL'),0);
 });

})().then(()=>{repo?.store.close();console.log('Fixture worktrees retained for inspection: '+dir);console.log('결과: '+count+' PASS / 0 FAIL');}).catch(e=>{console.error(e);repo?.store.close();process.exitCode=1;});
