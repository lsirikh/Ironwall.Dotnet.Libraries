'use strict';
const fs=require('fs'),path=require('path'),os=require('os'),assert=require('assert/strict');
const {repository,git}=require('../../.claude/hooks/_harness-store'),managed=require('../../.claude/hooks/_managed-work'),{start}=require('../../.claude/hooks/_run-service'),{Coordinator}=require('../../.claude/hooks/_coordinator');
const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-session-service-'));let repo,count=0;
const sleep=ms=>new Promise(r=>setTimeout(r,ms));async function until(fn){const t=Date.now();while(!fn()){if(Date.now()-t>30000)throw Error('TIMEOUT');await sleep(100);}}
async function check(name,fn){await fn();count++;console.log('PASS '+name);}
(async()=>{
 git(dir,['init','-q']);for(const f of ['prd.md','plan.md'])fs.writeFileSync(path.join(dir,f),'# fixture');git(dir,['add','.']);git(dir,['-c','user.name=Harness','-c','user.email=harness@test','commit','-qm','fixture']);repo=repository(dir);
 const w=managed.create(repo,{title:'Add two controllers',track:'C',max_parallel:2,prd:'prd.md',plan:'plan.md',nodes:['left','right'].map(id=>({id,files:[id+'.txt'],executor:'command',command:[process.execPath,'-e',"setTimeout(()=>require('fs').writeFileSync('"+id+".txt','done'),1500)"],tests:[{mode:'automated',format:'json',command:[process.execPath,'-e',"require('assert').equal(require('fs').readFileSync('"+id+".txt','utf8'),'done');console.log(JSON.stringify({status:'passed',checks:1}))"]}]}))},'test request');
 let a,b;
 await check('explicit add starts two named sessions on one work',()=>{a=start(repo,w.work_id,{runtime:'claude',session_name:'Claude UI'});b=start(repo,w.work_id,{runtime:'codex',new_session:true,session_name:'Codex API',operation_id:'same-button-click'});assert.notEqual(a.run_id,b.run_id);});
 await check('retry of the same add operation cannot create another process',()=>{const again=start(repo,w.work_id,{runtime:'codex',new_session:true,session_name:'Codex API',operation_id:'same-button-click'});assert.equal(again.run_id,b.run_id);assert.ok(again.reused);assert.equal(repo.store.list('run/').length,2);});
 await check('same key on another runtime is rejected',()=>assert.throws(()=>start(repo,w.work_id,{runtime:'claude',new_session:true,operation_id:'same-button-click'}),/OPERATION_ID_REUSED/));
 await check('both runs retain session identities and finish verified',async()=>{await until(()=>repo.store.list('run/').every(r=>!['starting','running'].includes(r.value.status)));const runs=repo.store.list('run/').map(r=>r.value);assert.ok(runs.every(r=>r.status==='verified'),JSON.stringify(runs));assert.ok(runs.every(r=>r.session_id));assert.equal(new Set(runs.map(r=>r.session_id)).size,2);const s=new Coordinator(repo.store,{repo_id:repo.repo_id}).snapshot().sessions;assert.deepEqual(new Set(s.map(x=>x.name)),new Set(['Claude UI','Codex API']));assert.ok(s.every(x=>x.status==='closed'));});
 console.log('결과: '+count+' PASS / 0 FAIL');
})().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>repo?.store.close());
