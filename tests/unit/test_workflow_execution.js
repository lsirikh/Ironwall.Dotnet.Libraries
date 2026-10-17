'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {repository,git}=require('../../.claude/hooks/_harness-store'),managed=require('../../.claude/hooks/_managed-work'),{Coordinator}=require('../../.claude/hooks/_coordinator');
const {WorkflowPresets}=require('../../.claude/hooks/_workflow-presets'),{SkillRegistry}=require('../../.claude/hooks/_skill-registry'),{invoke}=require('../../.claude/hooks/_model-invoke'),policy=require('../../.claude/hooks/_trusted-policy');
const {startServer}=require('../../.claude/hooks/_ui-server'),{start}=require('../../.claude/hooks/_run-service');
const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-workflow-execution-'));let repo,server,count=0;
const actor={source:'user',runtime:'ui',reason:'automated test action'},cmd=text=>[process.execPath,'-e',text];
const check=async(name,fn)=>{await fn();count++;console.log('PASS '+name);};
(async()=>{
 git(dir,['init']);fs.cpSync(path.resolve(__dirname,'../../.claude/hooks'),path.join(dir,'.claude/hooks'),{recursive:true});
 for(const [name,body] of Object.entries({'prd.md':'# PRD','plan.md':'# Plan','value.txt':'zero'}))fs.writeFileSync(path.join(dir,name),body);
 git(dir,['add','.']);git(dir,['-c','user.name=Harness Test','-c','user.email=harness@example.invalid','commit','-m','fixture']);
 repo=repository(dir);const registry=new SkillRegistry(repo),presets=new WorkflowPresets(repo);
 const skill=registry.save({content:'---\nname: bounded-edit\ndescription: Use the agreed decision\n---\nWRITE_FROM_FROZEN_SKILL'},0,actor);
 const preset=presets.get('quick-fix');preset.id='skill-edit';preset.name='Registered skill';preset.variants.codex.models.implement={runtime:'claude',model:'sonnet',effort:'medium',require_model:false};preset.steps[0].skill='bounded-edit';
 presets.save(preset,0,actor);
 const tests=[{mode:'automated',format:'json',command:cmd("require('assert').equal(require('fs').readFileSync('value.txt','utf8'),'one');console.log(JSON.stringify({status:'passed',checks:1}));")}];
 const spec={title:'UI workflow',launch_runtime:'codex',workflow:{preset_id:'skill-edit'},tasks:[{step_id:'implement',files:['value.txt'],tests}]};
 let preparation,work;
 await check('preparation is visible before a graph or invocation exists',async()=>{
  preparation=managed.draft(repo,{title:'Preparing the requested change',launch_runtime:'codex',request_ref:'test-user-request'},actor);
  assert.equal(new Coordinator(repo.store,{repo_id:repo.repo_id}).snapshot().works.find(w=>w.work_id===preparation.work_id).status,'preparing');
  await assert.rejects(()=>managed.run(repo,preparation.work_id,{onNotice:()=>{}}),/WORK_STILL_PREPARING/);
 });
 await check('analysis is linked to the preparing work and launch runtime',async()=>{
  const result=await invoke(repo,{role:'analysis',prompt:'analyze',runtime:'claude',work_id:preparation.work_id},async()=>({text:'decision',model:{status:'verified',observed:['claude-opus-fixture']}}));
  const agent=repo.store.list('agent/').map(r=>r.value).find(a=>a.invocation_id===result.invocation_id);
  assert.equal(agent.work_id,preparation.work_id);assert.equal(agent.launch_runtime,'codex');assert.equal(agent.execution_runtime,'claude');
 });
 await check('graph finalization retains its preparation identity and freezes skills',async()=>{
  work=managed.create(repo,{...spec,draft_id:preparation.work_id},'test-user-request',actor);
  assert.equal(work.work_id,preparation.work_id);assert.equal(work.launch_runtime,'codex');assert.equal(work.workflow.preset_id,'skill-edit');
  assert.equal(work.authorization.source,'user');assert.equal(work.authorization.actor.runtime,'ui');
  assert.throws(()=>managed.create(repo,{...spec,draft_id:preparation.work_id},'duplicate',actor),/INVALID_DRAFT/);
  registry.save({content:skill.value.content.replace('WRITE_FROM_FROZEN_SKILL','LATER_EDIT')},1,actor);
 });
 await check('Codex launch delegates the chosen node to Claude using frozen skill text',async()=>{
  let calls=0;const result=await managed.run(repo,work.work_id,{runtime:'codex',onNotice:()=>{},modelRunner:async(profile,prompt)=>{
   calls++;assert.equal(profile.runtime,'claude');assert.equal(profile.model,'sonnet');assert.match(prompt,/WRITE_FROM_FROZEN_SKILL/);assert.ok(!prompt.includes('LATER_EDIT'));
   return {text:JSON.stringify({summary:'applied skill',files:[{path:'value.txt',content:'one'}]}),model:{status:'verified',observed:['sonnet']}};
  }});assert.equal(result.status,'verified');assert.equal(calls,1);
  const agent=repo.store.list('agent/').map(r=>r.value).find(a=>a.work_id===work.work_id&&a.node_id);assert.equal(agent.execution_runtime,'claude');assert.equal(agent.launch_runtime,'codex');
 });
 await check('application scopes cannot change or broadly include harness control files',async()=>{
  for(const scope of ['.claude/hooks/gate.js','.claude/**','./.claude/hooks/gate.js','AGENTS.md'])assert.throws(()=>policy.validateScope({nodes:[{files:[scope]}]}),/HARNESS_MAINTENANCE_REQUIRED|PROTECTED_RESOURCE/);
  policy.validateScope({nodes:[{files:[{path:'.claude/hooks/gate.js',mode:'read'}]}]});
  policy.validateScope({purpose:'harness-maintenance',nodes:[{files:['.claude/hooks/gate.js']}]});
 });
 await check('changed verification contract and runtime engine invalidate execution',async()=>{
  const node=repo.store.get('node/'+work.work_id+'/implement').value;
  assert.throws(()=>policy.assertNode(work,{...node,tests:[]}),/EXECUTION_CONTRACT_CHANGED/);
  assert.throws(()=>policy.assertEngine({...work,engine_hash:'changed'}),/HARNESS_ENGINE_CHANGED/);
 });
 server=await startServer(dir,{context:repo});const boot=await (await fetch(server.url+'/api/bootstrap')).json();
 const post=(url,data)=>fetch(server.url+url,{method:'POST',headers:{Origin:server.url,'X-Harness-CSRF':boot.csrf,'Content-Type':'application/json'},body:JSON.stringify(data)});
 await check('UI API exposes both runtime defaults and versioned registered skills',async()=>{
  assert.equal(boot.runtime_presets.codex.balanced.roles.implementation.runtime,'codex');
  const skills=await (await fetch(server.url+'/api/skills')).json();assert.equal(skills.registered[0].version,2);
  const r=await post('/api/workflow-presets',{value:{...preset,name:'UI changed preset'},expectedVersion:1});assert.equal(r.status,200);
  assert.equal((await r.json()).actor.source,'user');
  assert.equal((await post('/api/workflow-presets',{value:preset,expectedVersion:1})).status,409);
 });
 await check('UI can register a real command graph and run it in an isolated process',async()=>{
  const r=await post('/api/work/create',{request_ref:'UI explicit test action',spec:{track:'B',launch_runtime:'codex',title:'Real process fixture',nodes:[{id:'one',executor:'command',command:cmd("require('fs').writeFileSync('value.txt','one')"),files:['value.txt'],tests}]}});
  assert.equal(r.status,200);const w=await r.json(),launched=await post('/api/work/run',{work_id:w.work_id,runtime:'codex'});
  assert.equal(launched.status,200);const job=await launched.json();assert.equal(job.actor.source,'user');
  assert.equal(start(repo,w.work_id,{runtime:'codex',actor}).run_id,job.run_id);
  const deadline=Date.now()+35000;let row;
  do{row=repo.store.get('run/'+job.run_id).value;if(!['starting','running'].includes(row.status))break;await new Promise(r=>setTimeout(r,100));}while(Date.now()<deadline);
  assert.equal(row.status,'verified',JSON.stringify(row));assert.equal(fs.readFileSync(path.join(dir,'value.txt'),'utf8'),'zero');
  const snap=await (await fetch(server.url+'/api/snapshot')).json();assert.ok(snap.runs.some(r=>r.run_id===job.run_id));assert.equal(snap.works.find(x=>x.work_id===w.work_id).launch_runtime,'codex');
 });
})().then(async()=>{await server?.close();repo?.store.close();console.log('결과: '+count+' PASS / 0 FAIL');}).catch(async e=>{console.error(e);await server?.close();repo?.store.close();process.exitCode=1;});
