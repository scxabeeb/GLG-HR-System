#!/usr/bin/env python3
"""Durable attendance queue regression checks; no real terminals or credentials."""
import importlib.util
import pathlib
import tempfile
import threading
import urllib.request
import urllib.error
spec=importlib.util.spec_from_file_location('connector', pathlib.Path(__file__).resolve().parents[1]/'HrSystemDhg.Web/Connectors/attendance_connector.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
with tempfile.TemporaryDirectory() as folder:
    terminal={'id':1,'serial':'TEST','address':'192.168.50.2','key':'0'*64}
    config={'server':'https://example.invalid','terminals':[terminal],'_queue_path':folder+'/queue.sqlite3'}
    c=m.Connector(config)
    query='SN=TEST&table=ATTLOG'
    body=b'1\t2026-11-24 06:35:00\t0\t1\n'
    assert c.enqueue(terminal,query,body)==1
    assert c.enqueue(terminal,query,body)==1
    assert c.queued_counts()=={1:1}
    assert c.enqueue(terminal,'table=USERINFO',body) is None
    assert c.enqueue(terminal,query,b'bad') is None
    assert pathlib.Path(config['_queue_path']).stat().st_mode & 0o777 == 0o600
    c=m.Connector(config) # Restart must retain every unsent punch.
    def offline(*args): raise m.CloudUnavailable('offline',True)
    c.cloud=offline;c.replay_once();assert c.queued_counts()=={1:1}
    c.cloud=lambda *args:(b'Unexpected response','text/plain')
    c.replay_once();assert c.queued_counts()=={1:1}
    received=[]
    def online(t,operation,q,b):
        received.append((q,b));return b'OK: 1','text/plain'
    c.cloud=online;c.replay_once();c.replay_once()
    assert received==[(query,body)] and c.queued_counts()=={}
    # Rotated/mismatching device identity cannot receive another terminal's queue.
    c.enqueue(terminal,query,body)
    config['terminals']=[dict(terminal,serial='OTHER')]
    c=m.Connector(config);c.cloud=online;c.replay_once()
    assert c.queued_counts()=={1:1} and len(received)==1
print('Offline queue checks passed: durable restart, deduplication, original timestamps, retry, acknowledgement and device isolation.')

with tempfile.TemporaryDirectory() as folder:
    terminal={'id':1,'serial':'TEST','address':'127.0.0.1','key':'0'*64}
    c=m.Connector({'server':'https://example.invalid','terminals':[terminal],'listen':'127.0.0.1','port':5190,'_queue_path':folder+'/queue.sqlite3'})
    server=m.http.server.ThreadingHTTPServer(('127.0.0.1',0),m.Handler);server.connector=c
    thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
    def send(payload=body,table='ATTLOG'):
        request=urllib.request.Request('http://127.0.0.1:'+str(server.server_port)+'/iclock/cdata?SN=TEST&table='+table,data=payload)
        try:
            with urllib.request.urlopen(request) as response:return response.status,response.read()
        except urllib.error.HTTPError as error:return error.code,error.read()
    try:
        c.cloud=offline
        assert send()==(200,b'OK: 1') and c.queued_counts()=={1:1}
        assert send(b'malformed')[0]==503 and send(table='USERINFO')[0]==503
        def denied(*args):raise m.CloudUnavailable('HTTP 401',False)
        c.cloud=denied
        assert send(body.replace(b'06:35',b'06:36'))[0]==503 and c.queued_counts()=={1:1}
    finally:server.shutdown();server.server_close();thread.join()
print('HTTP offline checks passed: ACK follows durable save; malformed, biometric and authentication failures are not acknowledged.')
