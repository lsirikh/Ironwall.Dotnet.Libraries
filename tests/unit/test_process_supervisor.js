'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {spawnSupervised}=require('../../.claude/hooks/_process-supervisor');
const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-v14-supervisor-'));let count=0;
function execute(args,input='',timeout=5000){return new Promise((resolve,reject)=>{const child=spawnSupervised(process.execPath,args,{cwd:dir,supervisorTimeout:timeout});let out='',err='';child.stdout.on('data',b=>out+=b);child.stderr.on('data',b=>err+=b);child.on('error',reject);child.on('close',code=>resolve({code,out,err}));child.stdin.end(input);});}
(async()=>{
 const echo=path.join(dir,'echo.cjs');fs.writeFileSync(echo,"let s='';process.stdin.setEncoding('utf8');process.stdin.on('data',b=>s+=b);process.stdin.on('end',()=>{console.log(JSON.stringify({args:process.argv.slice(2),input:s}));});");
 const args=['space value','quote"value','trailing\\','한글'],input='한글 입력\nsecond line';
 const echoResult=await execute([echo,...args],input);assert.equal(echoResult.code,0,echoResult.err);assert.deepEqual(JSON.parse(echoResult.out),{args,input});count++;console.log('PASS native argv and stdin preserve spaces, quotes and Unicode');
 const childScript=path.join(dir,'late.cjs'),parent=path.join(dir,'parent.cjs'),victim=path.join(dir,'victim.txt');
 fs.writeFileSync(childScript,"setTimeout(()=>require('fs').writeFileSync(process.argv[2],'late write'),1000);");
 fs.writeFileSync(parent,"require('child_process').spawn(process.execPath,[process.argv[2],process.argv[3]],{detached:true,stdio:'ignore'}).unref();console.log('1 passed');");
 const result=await execute([parent,childScript,victim]);assert.equal(result.code,0,result.err);await new Promise(r=>setTimeout(r,1300));assert.equal(fs.existsSync(victim),false);count++;console.log('PASS detached descendants cannot write after verification returns');
 const hang=path.join(dir,'hang.cjs');fs.writeFileSync(hang,"setInterval(()=>{},1000);");
 const timeout=await execute([hang],'',150);assert.equal(timeout.code,124,timeout.err);count++;console.log('PASS timed out process tree terminates with an explicit status');
 console.log('결과: '+count+' PASS / 0 FAIL');
})().catch(e=>{console.error(e);process.exitCode=1;});
