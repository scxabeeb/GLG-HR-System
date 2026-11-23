import os,subprocess,re,json,time,urllib.request,urllib.error,secrets
from pathlib import Path
root=Path(__file__).resolve().parents[1]
cs=os.environ.get('DHG_NATIVE_TEST_CONNECTION')
if not cs:
    raise SystemExit('Set DHG_NATIVE_TEST_CONNECTION to a local MySQL connection with permission to create a temporary database. No production data is used.')
parts={k.lower():v for k,v in (p.split('=',1) for p in cs.split(';') if '=' in p)}
database='dhg_native_checks_'+secrets.token_hex(3)
env=os.environ.copy();env['MYSQL_PWD']=parts.get('password','')
mysql=[os.environ.get('MYSQL_BIN','mysql'),'--protocol=TCP','-h',parts.get('server','127.0.0.1'),'-u',parts.get('user',parts.get('uid','root')),'-N','-B']
def sql(q):return subprocess.check_output(mysql+['-e',q],env=env,text=True)
password='Native!'+secrets.token_hex(12);newpassword='Native!'+secrets.token_hex(12)
email='native-check@example.test';process=None
try:
    sql('CREATE DATABASE '+database)
    testcs=re.sub(r'(?i)Database=[^;]+','Database='+database,cs)
    appenv=os.environ.copy();appenv.update({'ConnectionStrings__DefaultConnection':testcs,'Bootstrap__Email':email,'Bootstrap__Password':password,'ASPNETCORE_ENVIRONMENT':'Development','ASPNETCORE_URLS':'http://127.0.0.1:5199','Logging__LogLevel__Default':'Warning'})
    dll=root/'HrSystemDhg.Web/bin/Debug/net8.0/HrSystemDhg.Web.dll'
    subprocess.run(['dotnet',str(dll),'--migrate'],env=appenv,cwd=root/'HrSystemDhg.Web',stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,check=True)
    log=open(os.devnull,'w')
    process=subprocess.Popen(['dotnet',str(dll)],env=appenv,cwd=root/'HrSystemDhg.Web',stdout=log,stderr=log)
    token=None
    def req(path,body=None,expected=200,auth=True):
        data=json.dumps(body).encode() if body is not None else None
        headers={'Content-Type':'application/json'}
        if auth and token:headers['Authorization']='Bearer '+token
        request=urllib.request.Request('http://127.0.0.1:5199/api/mobile/'+path,data=data,headers=headers)
        try:
            with urllib.request.urlopen(request,timeout=8) as r:code=r.status;raw=r.read()
        except urllib.error.HTTPError as e:code=e.code;raw=e.read()
        assert code==expected,(path,code,raw[:300])
        return json.loads(raw) if raw else None
    for _ in range(40):
        try:req('v2/auth/me',expected=401,auth=False);break
        except (OSError,AssertionError):time.sleep(.5)
    else:raise Exception('Test server failed to start')
    req('v2/auth/login',{'username':email,'password':'wrong'},expected=401,auth=False)
    token=req('v2/auth/login',{'username':email,'password':password},auth=False)['accessToken']
    assert req('v2/auth/me')['mustChangePassword']
    req('v2/tasks',expected=403)
    old=token;token=req('v2/auth/password',{'current':password,'password':newpassword})['accessToken']
    assert not req('v2/auth/me')['mustChangePassword']
    valid=token;token=old;req('v2/auth/me',expected=401);token=valid
    for path in ['v2/tasks','v2/meetings','v2/notifications','v2/payroll']:
        assert req(path)==[]
    assert req('v2/profile')['employee'] is None
    req('v1/overview');req('v1/attendance')
    req('v2/tasks/9999/status',{'status':'Done'},expected=404)
    req('v2/tasks/9999/status',{'status':'Cancelled'},expected=400)
    req('v2/meetings/9999',expected=404)
    req('v2/payroll/9999',expected=404)
    uid=sql("SELECT Id FROM "+database+".AspNetUsers WHERE Email='"+email+"'").strip()
    sql("INSERT INTO "+database+".WorkTasks (Title,Description,OwnerId,AssigneeId,Status,Priority) VALUES ('Native check','Fixture','"+uid+"','"+uid+"',0,1)")
    task=req('v2/tasks')[0];req('v2/tasks/'+str(task['id'])+'/status',{'status':'Done'})
    assert req('v2/tasks')[0]['status']=='Done'
    assert sql('SELECT COUNT(*) FROM '+database+'.TaskActivities').strip()=='1'
    sql("INSERT INTO "+database+".WorkMeetings (Title,Agenda,OwnerId,JoinUrl,StartsUtc,EndsUtc,Cancelled) VALUES ('Native meeting','Main agenda','"+uid+"','',UTC_TIMESTAMP()-INTERVAL 1 MINUTE,UTC_TIMESTAMP()+INTERVAL 30 MINUTE,0)")
    meeting=req('v2/meetings')[0];mid=str(meeting['id'])
    assert meeting['startsUtc'].endswith('Z')
    sql("INSERT INTO "+database+".MeetingParticipant (MeetingId,UserId) VALUES ("+mid+",'"+uid+"')")
    req('v2/meetings/'+mid+'/agenda',{'title':'Employee agenda','notes':'Discussion'})
    req('v2/meetings/'+mid+'/attend',{});req('v2/meetings/'+mid+'/attend',{})
    detail=req('v2/meetings/'+mid)
    assert detail['attending']==1 and len(detail['agendaItems'])==1
    assert req('v2/meetings/'+mid+'/room')['isOwner']
    sql("INSERT INTO "+database+".AspNetUsers (Id,UserName,NormalizedUserName,Email,NormalizedEmail,PasswordHash,SecurityStamp,ConcurrencyStamp,EmailConfirmed,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount,IsEnabled,MustChangePassword) SELECT 'native-other','other@example.test','OTHER@EXAMPLE.TEST','other@example.test','OTHER@EXAMPLE.TEST',PasswordHash,SecurityStamp,ConcurrencyStamp,0,0,0,1,0,1,0 FROM "+database+".AspNetUsers WHERE Id='"+uid+"'")
    admin=token;token=req('v2/auth/login',{'username':'other@example.test','password':newpassword},auth=False)['accessToken']
    assert req('v2/tasks')==[]
    req('v2/meetings/'+mid,expected=404)
    req('v2/meetings/'+mid+'/room',expected=404)
    req('v2/meetings/'+mid+'/end',{},expected=400)
    req('v2/tasks/'+str(task['id'])+'/status',{'status':'Open'},expected=404)
    sql("INSERT INTO "+database+".UserModuleRestrictions (UserId,Module) VALUES ('native-other','tasks')")
    req('v2/tasks',expected=403)
    token=admin
    req('v2/meetings/'+mid+'/end',{})
    req('v2/meetings/'+mid+'/room',expected=400)
    def clone(name,ident,role=None):
        sql("INSERT INTO "+database+".AspNetUsers (Id,UserName,NormalizedUserName,Email,NormalizedEmail,PasswordHash,SecurityStamp,ConcurrencyStamp,EmailConfirmed,PhoneNumberConfirmed,TwoFactorEnabled,LockoutEnabled,AccessFailedCount,IsEnabled,MustChangePassword) SELECT '"+ident+"','"+name+"','"+name.upper()+"','"+name+"','"+name.upper()+"',PasswordHash,SecurityStamp,ConcurrencyStamp,0,0,0,1,0,1,0 FROM "+database+".AspNetUsers WHERE Id='"+uid+"'")
        if role:sql("INSERT INTO "+database+".AspNetUserRoles (UserId,RoleId) SELECT '"+ident+"',Id FROM "+database+".AspNetRoles WHERE Name='"+role+"'")
        return req('v2/auth/login',{'username':name,'password':newpassword},auth=False)['accessToken']
    administrator=token
    organizer=clone('organizer@example.test','organizer','HR')
    employeeToken=clone('employee@example.test','employee','Employee')
    outsider=clone('outsider@example.test','outsider','Employee')
    token=organizer
    assert req('v2/manage/options?module=tasks')['canAssign']
    taskBody={'id':0,'title':'Managed assignment','description':'Instructions','assigneeIds':['employee'],'dueDate':None,'priority':1,'responsibility':None}
    req('v2/manage/tasks',taskBody)
    managed=next(t for t in req('v2/tasks') if t['title']=='Managed assignment');tid=str(managed['id'])
    assert managed['canManage']
    token=employeeToken
    assert not req('v2/manage/options?module=tasks')['canAssign']
    assert not next(t for t in req('v2/tasks') if t['id']==managed['id'])['canManage']
    req('v2/tasks/'+tid+'/status',{'status':'Done'})
    req('v2/manage/tasks',dict(taskBody,id=managed['id']),expected=404)
    req('v2/manage/tasks/'+tid+'/cancel',{},expected=404)
    req('v2/manage/tasks',dict(taskBody,id=0,assigneeIds=['organizer']),expected=400)
    req('v2/manage/teams',{'title':'Unauthorized','description':'','members':['employee']},expected=403)
    token=outsider
    req('v2/manage/tasks/'+tid,expected=404)
    req('v2/tasks/'+tid+'/status',{'status':'Done'},expected=404)
    token=organizer
    assert req('v2/manage/tasks/'+tid)['activity'][0]['newStatus']=='Done'
    req('v2/manage/tasks',dict(taskBody,id=managed['id'],title='Edited by organizer'))
    token=administrator
    assert req('v2/manage/tasks/'+tid)['canManage']
    req('v2/manage/tasks',dict(taskBody,id=managed['id'],title='Edited by administrator'))
    token=organizer
    team=req('v2/manage/teams',{'title':'Native team','description':'Team brief','members':['employee','organizer']})
    sub={'title':'Write report','description':'Prepare findings','assigneeIds':['employee'],'dueDate':None,'priority':2}
    req('v2/manage/teams/'+str(team['id'])+'/subtasks',sub)
    req('v2/manage/teams/'+str(team['id'])+'/subtasks',dict(sub,assigneeIds=['outsider']),expected=400)
    teams=req('v2/manage/teams');nativeTeam=next(t for t in teams if t['id']==team['id'])
    assert nativeTeam['subtasks'][0]['assignments'][0]['assigneeId']=='employee'
    token=employeeToken
    assert not next(t for t in req('v2/manage/teams') if t['id']==team['id'])['canManage']
    req('v2/manage/teams/'+str(team['id'])+'/subtasks',sub,expected=404)
    from datetime import datetime,timezone,timedelta
    now=datetime.now(timezone.utc)
    meetingBody={'id':0,'title':'Managed meeting','agenda':'Review','starts':(now-timedelta(minutes=1)).isoformat(),'ends':(now+timedelta(minutes=30)).isoformat(),'mode':'BuiltIn','location':None,'joinUrl':None,'guests':['employee']}
    req('v2/manage/meetings',meetingBody,expected=403)
    token=organizer
    meetingResult=req('v2/manage/meetings',meetingBody);meetingId=str(meetingResult['id'])
    req('v2/manage/meetings',dict(meetingBody,id=meetingResult['id'],title='Updated agenda'))
    token=employeeToken
    req('v2/manage/meetings',dict(meetingBody,id=meetingResult['id']),expected=404)
    req('v2/manage/meetings/'+meetingId+'/cancel',{},expected=404)
    req('v2/manage/meetings/'+meetingId+'/minutes',{'minutes':'Not authorized'},expected=404)
    token=outsider
    req('v2/meetings/'+meetingId,expected=404)
    token=organizer
    req('v2/manage/meetings/'+meetingId+'/minutes',{'minutes':'Published decisions'})
    token=employeeToken
    assert req('v2/meetings/'+meetingId)['publishedMinutes']=='Published decisions'
    token=administrator
    assert req('v2/meetings/'+meetingId)['isOwner']
    assert req('v2/meetings/'+meetingId+'/room')['isOwner']
    req('v2/meetings/'+meetingId+'/end',{})
    req('v2/meetings/'+meetingId+'/room',expected=400)
    token=organizer
    future=req('v2/manage/meetings',dict(meetingBody,starts=(now+timedelta(hours=1)).isoformat(),ends=(now+timedelta(hours=2)).isoformat()))
    token=administrator
    req('v2/manage/meetings/'+str(future['id'])+'/cancel',{})
    req('v2/manage/tasks/'+tid+'/cancel',{})
    assert req('v2/manage/tasks/'+tid)['status']=='Cancelled'
    print('PASS mobile administrator and organizer create/edit/assign/cancel, team subtasks, activity tracking, meeting minutes and room controls; participant and outsider escalation rejected')
    token=administrator
    req('v2/auth/logout',{});req('v2/auth/me',expected=401)
    print('PASS native bearer login, mandatory password change, old-token revocation, user-scoped feeds, task update audit, invalid status rejection, missing-record authorization, logout revocation')
finally:
    if process:process.terminate();process.wait(timeout=10)
    sql('DROP DATABASE IF EXISTS '+database)
