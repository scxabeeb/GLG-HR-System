# Executed only inside check-native-admin.py's isolated fixture.
assert 'native_admin_fixture' in env['ConnectionStrings__DefaultConnection']
assert env['Automation__DisableWorker']=='true'
assert request('admin/automation/load',{'values':{}})['fields']
secret='SyntheticWebhook'+secrets.token_hex(16)
action('automation','OnPostAsync',{'Input.Enabled':True,'Input.WebhookUrl':'http://127.0.0.1/webhook','WebhookPassword':secret},400)
action('automation','OnPostAsync',{'Input.Enabled':True,'Input.WebhookUrl':'https://automation.example.invalid/webhook/garowe','WebhookPassword':secret,'Input.Tasks':True,'Input.Meetings':True,'Input.Attendance':True,'Input.Payroll':True})
assert sql('SELECT Enabled FROM AutomationSettings')=='1'
assert sql('SELECT ProtectedWebhookSecret FROM AutomationSettings')!=secret
keyfile=action('automation','OnPostRotateAsync',{})
callback=json.loads(base64.b64decode(keyfile['data'])) if 'data' in keyfile else None
assert callback and len(callback['key'])==64
adminid=sql("SELECT Id FROM AspNetUsers WHERE Email='admin@example.test'")
request('manage/tasks',{'id':0,'title':'Automation fixture task','description':'Synthetic','assigneeIds':[adminid],'priority':1,'dueDate':'2026-11-28'},200)
count=int(sql("SELECT COUNT(*) FROM AutomationEvents WHERE Module='tasks'"));assert count==1
payload=json.loads(sql("SELECT Payload FROM AutomationEvents WHERE Module='tasks' LIMIT 1"));assert payload['data']['id']>0 and payload['eventId'] and payload['timeZone']=='Africa/Mogadishu'
assert not any(term in json.dumps(payload).lower() for term in ['password','bankaccount','fingerprint','securitystamp'])
# Callback key cannot use the Admin APIs and requires explicit contact sharing.
def callback_request(path,key=None,expected=200):
 headers={'X-Garowe-Automation-Key':key or callback['key']}
 try:
  with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:5198/api/integrations/n8n/'+path,headers=headers),timeout=20) as r:code=r.status;raw=r.read()
 except urllib.error.HTTPError as error:code=error.code;raw=error.read()
 assert code==expected,(path,code,raw[:500])
 return json.loads(raw) if raw else None
callback_request('summary?module=tasks',key='wrong',expected=401)
assert callback_request('summary?module=tasks')['items']
callback_request('recipients?eventId='+payload['eventId'],expected=403)
for kind in ['events','reminders','daily']:
 export=action('automation','OnGetWorkflow',{'kind':kind})
 workflow=json.loads(base64.b64decode(export['data']))
 assert not workflow['active'] and workflow['settings']['timezone']=='Africa/Mogadishu' and workflow['nodes']
 # Keep these synthetic workflows available for structural checks only.
# A separate key can queue only preset reminders; repeat calls are idempotent.
jobfile=action('automation','OnPostReminderKeyAsync',{})
jobkey=json.loads(base64.b64decode(jobfile['data']))['key']
def job_request(key,expected):
 body=json.dumps({'kind':'tasks'}).encode()
 try:
  with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:5198/api/integrations/n8n/reminders',data=body,headers={'Content-Type':'application/json','X-Garowe-Reminder-Key':key}),timeout=20) as r:code=r.status;raw=r.read()
 except urllib.error.HTTPError as e:code=e.code;raw=e.read()
 assert code==expected,(code,raw[:300])
 return json.loads(raw) if raw else None
job_request(jobkey,401)
sql('UPDATE AutomationSettings SET AllowReminders=1')
job_request(callback['key'],401)
sql("UPDATE WorkTasks SET DueDate=CURDATE() WHERE Title='Automation fixture task'")
assert job_request(jobkey,200)['queued']==1
assert job_request(jobkey,200)['queued']==0
assert sql("SELECT COUNT(*) FROM AutomationReminders")=="1"
action('automation','OnPostAsync',{'Input.Enabled':True,'Input.WebhookUrl':'https://automation.example.invalid/webhook/garowe','Input.Tasks':False,'Input.Meetings':True,'Input.Attendance':True,'Input.Payroll':True})
callback_request('summary?module=tasks',expected=403)
request('manage/tasks',{'id':0,'title':'Disabled module fixture','assigneeIds':[adminid],'priority':1},200)
assert int(sql("SELECT COUNT(*) FROM AutomationEvents WHERE Module='tasks'"))==count
print('PASS automation config validation, encrypted secret, atomic generated-ID event, module controls, read-only key, contact privacy and importable workflow exports (no external traffic).')

# The fake-delivery harness shares this disposable DB; never points to production.
from xml.sax.saxutils import escape
worker=p/'worker';worker.mkdir()
project=escape(str(root/'HrSystemDhg.Web/HrSystemDhg.Web.csproj'))
(worker/'worker.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><ProjectReference Include="{project}" /></ItemGroup></Project>')
shutil.copyfile(root/'scripts/fixtures/automation-delivery.cs',worker/'Program.cs')
worker_env=os.environ.copy();worker_env['AUTOMATION_FIXTURE_CONNECTION']='Server=127.0.0.1;Port=3312;Database=native_admin_fixture;User=root;Password=;'
subprocess.run(['dotnet','run','--project',str(worker/'worker.csproj'),'-p:NuGetAudit=false'],env=worker_env,check=True)
