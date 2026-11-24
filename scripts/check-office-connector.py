"""Runs only inside the disposable database native API fixture."""
import hashlib
import http.client
import json
import os
import secrets
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path

def run(root, database, sql):
    key = secrets.token_hex(32).upper()
    digest = hashlib.sha256(key.encode('ascii')).hexdigest().upper()
    sql("INSERT INTO "+database+".ZkDevices (Name,Endpoint,PushSerial,PushAddress,OfficeConnectorKeyHash) VALUES ('Office bridge fixture','Fixture','BRIDGETEST001','127.0.0.1','"+digest+"')")
    device = int(sql("SELECT Id FROM "+database+".ZkDevices WHERE PushSerial='BRIDGETEST001'").strip())
    base = 'http://127.0.0.1:5199/api/attendance/bridge/'+str(device)
    headers = {'Authorization':'Bearer '+key, 'X-Terminal-Serial':'BRIDGETEST001','X-Terminal-Address':'127.0.0.1'}
    def cloud(path, expected=200, extra=None):
        h = dict(headers);h.update(extra or {})
        try:
            with urllib.request.urlopen(urllib.request.Request(base+path,headers=h),timeout=20) as r:status,body=r.status,r.read()
        except urllib.error.HTTPError as e:status,body=e.code,e.read()
        assert status==expected,(path,status,body[:100])
        return body
    cloud('/heartbeat',401,{'Authorization':'Bearer '+'0'*64})
    cloud('/heartbeat',401,{'X-Terminal-Address':'127.0.0.2'})
    heartbeat=json.loads(cloud('/heartbeat'))
    assert heartbeat['connected'] and heartbeat['terminalLastContactUtc'] is None
    cloud('/cdata?SN=WRONG',404)
    with tempfile.TemporaryDirectory(prefix='dhg-office-bridge-') as directory:
        conf=Path(directory)/'connector.json'
        conf.write_text(json.dumps({'server':'http://127.0.0.1:5199','listen':'127.0.0.1','port':5290,'terminals':[{'id':device,'serial':'BRIDGETEST001','address':'127.0.0.1','key':key}]}));conf.chmod(0o600)
        env=os.environ.copy();env['PYTHONDONTWRITEBYTECODE']='1'
        process=subprocess.Popen(['python3',str(root/'HrSystemDhg.Web/Connectors/attendance_connector.py'),'--config',str(conf),'--allow-loopback-test'],env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        def local(path, data=None, expected=200):
            try:
                with urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:5290'+path,data=data),timeout=25) as r:status,body=r.status,r.read()
            except urllib.error.HTTPError as e:status,body=e.code,e.read()
            assert status==expected,(path,status,body[:100])
            return body
        try:
            for _ in range(40):
                try:local('/health');break
                except OSError:time.sleep(.2)
            else:raise AssertionError('Office connector did not start')
            local('/iclock/cdata?SN=UNKNOWN&options=all',expected=403)
            options=local('/iclock/cdata?SN=BRIDGETEST001&options=all')
            assert b'GET OPTION FROM' in options and b'TimeZone=3\r\n' in options
            assert sql('SELECT LastPushSeenUtc IS NOT NULL FROM '+database+'.ZkDevices WHERE Id='+str(device)).strip()=='1'
            timestamp=(datetime.now(timezone.utc)+timedelta(hours=3,minutes=-5)).strftime('%Y-%m-%d %H:%M:%S')
            payload=('BRIDGE-TEST\t'+timestamp+'\t0\t1\n').encode()
            for _ in range(2):assert b'OK: 1' in local('/iclock/cdata?SN=BRIDGETEST001&table=ATTLOG&Stamp=100',payload)
            assert sql("SELECT COUNT(*) FROM "+database+".ZkPunchs WHERE DeviceId="+str(device)+" AND Pin='BRIDGE-TEST'").strip()=='1'
            diagnostic=json.loads(cloud('/status'))
            assert diagnostic['punchesToday']==1 and diagnostic['totalPunches']==1
            assert diagnostic['lastPunchLocalTime'].replace('T',' ')==timestamp
            expires=(datetime.now(timezone.utc)+timedelta(minutes=3)).strftime('%Y-%m-%d %H:%M:%S')
            sql("INSERT INTO "+database+".ZkDeviceCommands (DeviceId,Serial,Address,Pin,Name,ExpiresUtc) VALUES ("+str(device)+",'BRIDGETEST001','127.0.0.1','999','Fixture','"+expires+"')")
            command=int(sql('SELECT MAX(Id) FROM '+database+'.ZkDeviceCommands WHERE DeviceId='+str(device)).strip())
            assert ('C:'+str(command)+':DATA UPDATE USERINFO').encode() in local('/iclock/getrequest?SN=BRIDGETEST001')
            local('/iclock/devicecmd?SN=BRIDGETEST001',('ID='+str(command)+'&Return=0').encode())
            assert sql('SELECT Result FROM '+database+'.ZkDeviceCommands WHERE Id='+str(command)).strip()=='0'
            sql('UPDATE '+database+'.ZkDevices SET OfficeConnectorKeyHash=NULL WHERE Id='+str(device))
            cloud('/heartbeat',401)
            local('/iclock/cdata?SN=BRIDGETEST001&options=all',expected=503)
        finally:
            process.terminate();process.wait(timeout=10)
    print('PASS office connector authentication, terminal allowlist, cloud forwarding, duplicate punches, command roundtrip, key revocation and no false terminal heartbeat')
