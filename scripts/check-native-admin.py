"""Exercise native Admin APIs only against an isolated disposable local MySQL/app."""
import base64,os,shutil,subprocess,tempfile,time,json,urllib.request,urllib.error,secrets
from pathlib import Path
root=Path(__file__).resolve().parents[1]
mysqld=shutil.which('mysqld') or '/opt/homebrew/bin/mysqld'
mysqladmin=shutil.which('mysqladmin') or '/opt/homebrew/bin/mysqladmin'
mysql=shutil.which('mysql') or '/opt/homebrew/bin/mysql'
with tempfile.TemporaryDirectory(prefix='garowe-admin-',dir='/private/tmp') as folder:
 p=Path(folder);data=p/'data';data.mkdir()
 subprocess.run([mysqld,'--no-defaults','--initialize-insecure','--datadir='+str(data)],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 server=subprocess.Popen([mysqld,'--no-defaults','--datadir='+str(data),'--socket='+str(p/'mysql.sock'),'--port=3312','--bind-address=127.0.0.1','--mysqlx=OFF'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
 app=None;token=None
 try:
  for _ in range(80):
   if subprocess.run([mysqladmin,'--socket='+str(p/'mysql.sock'),'-uroot','ping'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode==0:break
   time.sleep(.25)
  else:raise Exception('Disposable MySQL failed')
  password='Fixture!'+secrets.token_hex(12)
  env=os.environ.copy();env.update({'ConnectionStrings__DefaultConnection':'Server=127.0.0.1;Port=3312;Database=native_admin_fixture;User=root;Password=;','Bootstrap__Email':'admin@example.test','Bootstrap__Password':password,'ASPNETCORE_ENVIRONMENT':'Development','ASPNETCORE_URLS':'http://127.0.0.1:5198','Logging__LogLevel__Default':'Warning','Attendance__AutomaticClosingEnabled':'false','Automation__DisableWorker':'true'})
  dll=root/'HrSystemDhg.Web/bin/Debug/net8.0/HrSystemDhg.Web.dll'
  subprocess.run(['dotnet',str(dll),'--migrate'],env=env,cwd=root/'HrSystemDhg.Web',check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
  log=open(p/'app.log','w');app=subprocess.Popen(['dotnet',str(dll)],env=env,cwd=root/'HrSystemDhg.Web',stdout=log,stderr=log)
  def request(path,values=None,expected=200,auth=True):
   headers={'Content-Type':'application/json'}
   if token and auth:headers['Authorization']='Bearer '+token
   body=json.dumps(values).encode() if values is not None else None
   try:
    with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:5198/api/mobile/v2/'+path,data=body,headers=headers),timeout=45) as response:code=response.status;raw=response.read()
   except urllib.error.HTTPError as e:code=e.code;raw=e.read()
   assert code==expected,(path,code,raw[:1000])
   return json.loads(raw) if raw else None
  def sql(q):return subprocess.check_output([mysql,'--socket='+str(p/'mysql.sock'),'-uroot','-N','-B','native_admin_fixture','-e',q],text=True).strip()
  for _ in range(80):
   try:request('admin/catalog',expected=401,auth=False);break
   except OSError:time.sleep(.5)
  token=request('auth/login',{'username':'admin@example.test','password':password},auth=False)['accessToken']
  request('admin/catalog',expected=403)
  token=request('auth/password',{'current':password,'password':password+'changed'})['accessToken']
  assert request('auth/me')['isAdmin']
  catalog=request('admin/catalog');assert len(catalog)>=25
  failures=[]
  for module in catalog:
   try:
    page=request('admin/'+module['key']+'/load',{'values':{}})
    assert 'fields' in page and 'actions' in page and 'lookups' in page,module['key']
    content=json.dumps(page);assert 'PasswordHash' not in content and 'SecurityStamp' not in content
   except Exception as e:failures.append(str(e))
  assert not failures,'Module load failures: '+str(failures)
  def action(key,method,values,expected=200):return request('admin/'+key+'/action/'+method,{'values':values},expected)
  for code in ['A','B']:
   action('branches','OnPostAsync',{'Name':'Office '+code,'Code':code,'Timezone':'Africa/Mogadishu','Active':True})
  sample=action('register','OnGetSample',{});assert sample['file'].endswith('.csv')
  a=int(sql("SELECT Id FROM Branchs WHERE Code='A'"));b=int(sql("SELECT Id FROM Branchs WHERE Code='B'"))
  action('departments','OnPostAsync',{'Name':'Admin and Finance','Code':'AF','ParentId':a,'AdditionalBranchIds':[b],'Active':True})
  asset=request('admin/asset-edit/load',{'values':{}})
  assert any(field['name']=='Input.Name' for field in asset['fields']), 'Asset registration fields missing'
  action('asset-edit','OnPostAsync',{'Input.Tag':'NATIVE-001','Input.Name':'Office laptop','Input.Category':'Computer','Input.BranchId':b,'Input.Currency':'USD','Input.Status':'Available'})
  assert sql("SELECT COUNT(*) FROM Assets WHERE Tag='NATIVE-001'")=='1'
  settings=request('admin/settings/load',{'values':{}})
  assert any(field['name'].startswith('Input.') for field in settings['fields']), 'Settings fields missing'
  fleet=request('admin/fleet/load' ,{'values':{}})
  save=next(item for item in fleet['actions'] if item['key']=='OnPostSaveAsync')
  assert any(field['name']=='Input.Plate' for field in save['fields'])
  assert next(field for field in save['fields'] if field['name']=='Input.Status')['value']=='Available'
  action('fleet','OnPostSaveAsync',{'Input.Plate':'FIX-001','Input.Name':'Native fixture vehicle','Input.BranchId':b,'Input.Status':'Available','Input.OdometerKm':0})
  assert sql("SELECT COUNT(*) FROM FleetVehicles WHERE Plate='FIX-001'")=='1'
  csv='Kind,Name,Code,City,Address,Timezone,ParentId,Active,RequiresSub,AdditionalBranchIds\nBranch,CSV office,CSV,,,Africa/Mogadishu,,True,False,\n'
  action('branches','OnPostImportAsync',{'upload.data':base64.b64encode(csv.encode()).decode(),'upload.name':'office.csv','upload.type':'text/csv'})
  assert sql("SELECT COUNT(*) FROM Branchs WHERE Code='CSV'")=='1'
  dept=int(sql("SELECT Id FROM Departments WHERE Code='AF'"))
  action('register','OnPostAsync',{'Name':'Native Employee','Email':'employee@example.test','Title':'Officer','Hire':'2026-01-01','BranchId':b,'DepartmentId':dept})
  assert sql('SELECT BranchId FROM EmployeeAssignments')==str(b)
  users=request('admin/users/load',{'values':{}});assert len(users['records']['Items'])==2
  action('register','OnPostAsync',{'Name':'','Email':'invalid','Title':'','Hire':'2026-01-01','BranchId':b,'DepartmentId':dept},400)
  action('roles','OnPostDeleteAsync',{'id':sql("SELECT Id FROM AspNetRoles WHERE Name='Admin'")},400)
  action('branches','OnPostAsync',{'Name':'Duplicate','Code':'A','Timezone':'Africa/Mogadishu','Active':True},400)
  if os.environ.get('DHG_AUTOMATION_CHECK')=='1':exec((root/'scripts/check-automation-api.py').read_text())
  request('admin/data-reset/load',{'values':{}},404)
  request('admin/users/action/DeleteDatabase',{'values':{}},404)
  request('admin/catalog',expected=401,auth=False)
  token=request('auth/login',{'username':'EMP0001','password':'EMP0001123'},auth=False)['accessToken']
  token=request('auth/password',{'current':'EMP0001123','password':'employee1234'})['accessToken']
  request('admin/catalog',expected=403)
  print('PASS all native admin module schemas, required initial password change, real registration, shared-branch assignment, duplicate validation, secret redaction and denied non-admin/unknown access.')
 finally:
  if app:app.terminate();app.wait(timeout=30)
  server.terminate();server.wait(timeout=30)
