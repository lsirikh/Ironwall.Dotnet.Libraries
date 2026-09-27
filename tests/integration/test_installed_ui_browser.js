'use strict';
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),assert=require('node:assert/strict'),http=require('node:http'),{spawn,execFileSync}=require('node:child_process');
const {CDP}=require('./test_ui_browser'),sleep=ms=>new Promise(r=>setTimeout(r,ms));
const source=path.resolve(__dirname,'../..'),root=fs.mkdtempSync(path.join(os.tmpdir(),'harness-installer-browser-')),target=path.join(root,'project'),out=process.env.HARNESS_BROWSER_EVIDENCE||path.join(root,'evidence');
async function freePort(){const s=http.createServer();await new Promise(r=>s.listen(0,'127.0.0.1',r));const port=s.address().port;await new Promise(r=>s.close(r));return port;}
(async()=>{
 fs.mkdirSync(target);fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(target,'AGENTS.md'),'User instruction: preserve this line.\n');
 const git=args=>execFileSync('git',args,{cwd:target,windowsHide:true,stdio:'pipe'});git(['init']);git(['add','.']);git(['-c','user.name=Test','-c','user.email=test@example.invalid','commit','-m','fixture']);
 const port=await freePort(),url='http://127.0.0.1:'+port,installer=spawn(process.execPath,[path.join(source,'install-gui.js'),'--target',target,'--port',String(port),'--no-open'],{cwd:source,windowsHide:true,stdio:['ignore','pipe','pipe']});let logs='';installer.stdout.on('data',b=>logs+=b);installer.stderr.on('data',b=>logs+=b);
 const browser=process.env.HARNESS_BROWSER||['C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe','C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe'].find(fs.existsSync);if(!browser)throw Error('UNVERIFIED: browser missing');
 let dashboardPID,results=[];
 try{
  let ready=false;for(let i=0;i<100;i++){try{ready=(await fetch(url+'/api/detect')).ok;if(ready)break;}catch{}await sleep(100);}assert.ok(ready,logs);
  assert.equal((await fetch(url+'/api/dashboard',{method:'POST',headers:{Origin:'http://foreign.invalid'}})).status,403);
  for(const mode of ['headless','headed']){
   const profile=path.join(root,mode);fs.mkdirSync(profile);const child=spawn(browser,['--remote-debugging-port=0','--user-data-dir='+profile,'--no-first-run','--no-default-browser-check','--disable-background-networking','--window-size=1440,1000',...(mode==='headless'?['--headless=new']:[]),'about:blank'],{windowsHide:mode==='headless',stdio:'ignore'});let client;
   try{
    const portfile=path.join(profile,'DevToolsActivePort');for(let i=0;i<120&&!fs.existsSync(portfile);i++)await sleep(100);assert.ok(fs.existsSync(portfile),'browser started');
    const browserPort=Number(fs.readFileSync(portfile,'utf8').split('\n')[0]),targets=await(await fetch('http://127.0.0.1:'+browserPort+'/json')).json();client=await CDP.connect(targets.find(t=>t.type==='page').webSocketDebuggerUrl);
    await client.call('Page.navigate',{url});await client.wait("typeof openDashboard==='function'");let checks=1;
    if(mode==='headless'){
     await client.eval("subscribeSSE()");
     await client.eval("fetch('/api/install',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({mode:'fresh'})})");
    }else await client.eval("subscribeSSE()");
    await client.wait("!document.getElementById('screenDone').classList.contains('hidden')");checks++;
    await client.eval("document.getElementById('btnDashboard').click()");await client.wait("!!document.querySelector('#dashboardResult a')");checks++;
    const dashboard=await client.eval("document.querySelector('#dashboardResult a').href");assert.match(dashboard,/^http:\/\/127\.0\.0\.1:[0-9]+\/models$/);checks++;
    const boot=await(await fetch(new URL('/api/bootstrap',dashboard))).json();assert.equal(path.resolve(boot.root),path.resolve(target));checks++;
    await client.call('Page.navigate',{url:dashboard});await client.wait("document.querySelectorAll('#role-rows tr').length===8");checks++;
    await client.call('Page.navigate',{url:dashboard.replace(/\/models(?:\?.*)?$/,'/workflows')});await client.wait('typeof designer!=="undefined" && !!designer');await client.eval('document.getElementById("edit-preset").click()');assert.equal(await client.eval('document.getElementById("preset-editor").open'),true);assert.equal(await client.eval('document.querySelectorAll(".designer-node").length'),5);checks++;
    for(const f of ['preset-graph.js','preset-designer.js','preset-designer.css'])assert.equal(fs.readFileSync(path.join(target,'.claude/hooks/ui',f),'utf8').replace(/\r\n/g,'\n'),fs.readFileSync(path.join(source,'.claude/hooks/ui',f),'utf8').replace(/\r\n/g,'\n'));checks++;
    assert.equal(client.errors.length,0);checks++;
    const shot=await client.call('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(out,mode+'-installed-models.png'),Buffer.from(shot.data,'base64'));
    assert.match(fs.readFileSync(path.join(target,'AGENTS.md'),'utf8'),/User instruction: preserve this line/);assert.match(fs.readFileSync(path.join(target,'AGENTS.md'),'utf8'),/harness-v14:start/);checks++;
    results.push({mode,status:'passed',checks});console.log('PASS installed '+mode+' '+checks+' checks');
   }finally{if(client){await client.call('Browser.close').catch(()=>{});client.close();}if(child.exitCode===null)child.kill();}
  }
  const {repository}=require(path.join(target,'.claude/hooks/_harness-store'));const repo=repository(target);dashboardPID=repo.store.get('ui/server').value?.pid;repo.store.close();
  fs.writeFileSync(path.join(out,'installed-browser-evidence.json'),JSON.stringify({target,results},null,2));console.log('결과: '+results.reduce((n,r)=>n+r.checks,0)+' PASS / 0 FAIL');
 }finally{installer.kill();if(dashboardPID)try{process.kill(dashboardPID);}catch{}fs.writeFileSync(path.join(out,'installer-log.txt'),logs);}
})().catch(e=>{console.error(e);process.exitCode=1;});
