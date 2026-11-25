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

from unittest.mock import patch,MagicMock
assert str(m.discovery_network('192.168.50.9/24'))=='192.168.50.0/24'
for invalid in ('8.8.8.0/24','192.168.0.0/16','127.0.0.0/24','240.0.0.0/24','::1/128'):
    try:m.discovery_network(invalid)
    except ValueError:pass
    else:raise AssertionError('Invalid discovery scope accepted: '+invalid)
def fake_connection(target,timeout):
    if target==('192.168.50.1',4370):return MagicMock()
    raise OSError('closed')
with patch.object(m.socket,'create_connection',side_effect=fake_connection):
    assert m.scan_network('192.168.50.0/30')==['192.168.50.1']
print('Discovery checks passed: bounded private subnets, candidate-only results and no device writes.')

# Windows adapter detection is exercised with realistic PowerShell results on any host.
import json
adapters=json.dumps([
 {'IPAddress':'192.168.50.1','InterfaceAlias':'Ethernet','PrefixLength':24},
 {'IPAddress':'192.168.1.40','InterfaceAlias':'Wi-Fi','PrefixLength':24},
 {'IPAddress':'127.0.0.1','InterfaceAlias':'Loopback','PrefixLength':8},
 {'IPAddress':'8.8.8.8','InterfaceAlias':'Invalid public adapter','PrefixLength':32},
 {'IPAddress':'192.168.50.1','InterfaceAlias':'Duplicate','PrefixLength':24}])
with patch.object(m.os,'name','nt'),patch.object(m.subprocess,'check_output',return_value=adapters) as powershell:
    addresses=m.computer_addresses()
    assert len(addresses)==2 and addresses[1]=={'address':'192.168.50.1','interface':'Ethernet','prefix':24}
    assert 'Get-NetIPAddress' in powershell.call_args.args[0][-1]
with patch.object(m,'computer_addresses',return_value=addresses) as detect,patch('builtins.input',side_effect=['r','2']):
    assert m.choose_computer_address([{'address':'192.168.50.2'}])=='192.168.50.1'
    assert detect.call_count==2
with patch.object(m,'computer_addresses',return_value=[]),patch('builtins.input',side_effect=['m','192.168.50.1']):
    assert m.choose_computer_address([])=='192.168.50.1'
with patch.object(m.os,'name','nt'),patch.object(m.subprocess,'check_output',side_effect=OSError()),patch.object(m.socket,'gethostbyname_ex',return_value=('test',[],['192.168.5.10','127.0.0.1'])):
    assert m.computer_addresses()==[{'address':'192.168.5.10','interface':'Local adapter','prefix':None}]
print('Computer IP checks passed: Windows adapter names/prefixes, private-address filtering, deduplication, numbered selection, rescanning and manual fallback.')

with tempfile.TemporaryDirectory() as folder:
    path=pathlib.Path(folder)/'attendance-connector-1.json'
    original={'server':'https://example.invalid','listen':'192.168.50.1','port':5190,'terminals':[{'id':1,'serial':'TEST','address':'192.168.50.2','key':'0'*64}]}
    path.write_text(json.dumps(original))
    with patch.object(m.socket,'socket'):
        changed=m.save_listen_address(str(path),'192.168.50.10')
    assert changed['listen']=='192.168.50.10' and json.loads(path.read_text())['terminals']==original['terminals']
    before=path.read_bytes()
    try:m.save_listen_address(str(path),'8.8.8.8')
    except ValueError:pass
    else:raise AssertionError('Public listen address accepted')
    assert path.read_bytes()==before and not list(path.parent.glob('.attendance-*.json'))

# Exercise the actual setup-window callbacks without requiring a desktop display.
import types,sys,time
fake_tk=types.ModuleType('tkinter');fake_tk.ttk=MagicMock();fake_tk.messagebox=MagicMock()
class Variable:
    def __init__(self,value=''):self.value=value
    def set(self,value):self.value=value
    def get(self):return self.value
fake_tk.StringVar=Variable
ui_spec=importlib.util.spec_from_file_location('windows_setup',pathlib.Path(__file__).resolve().parents[1]/'HrSystemDhg.Web/Connectors/Windows/windows_setup.py')
ui=importlib.util.module_from_spec(ui_spec)
with patch.dict(sys.modules,{'tkinter':fake_tk,'attendance_connector':m}):
    ui_spec.loader.exec_module(ui)
    window=ui.SetupWindow(MagicMock())
    with patch.object(m,'computer_addresses',return_value=addresses):
        window.scan()
        for _ in range(100):
            if not window.results.empty():break
            time.sleep(.01)
        window.poll_results()
    assert window.rows==addresses and not window.scanning
    window.table.selection.return_value=['1'];window.use_selected();assert window.address.get()=='192.168.50.1'
    buttons={call.kwargs.get('text'):call.kwargs.get('command') for call in fake_tk.ttk.Button.call_args_list}
    assert buttons['Scan computer IP']==window.scan and buttons['Use selected IP']==window.use_selected and buttons['Save & start connector']==window.start
with tempfile.TemporaryDirectory() as folder:
    window.folder=pathlib.Path(folder);config_path=window.folder/'attendance-connector-1.json';config_path.write_text('{}')
    window.config.set(config_path.name)
    with patch.object(m,'save_listen_address',return_value={'port':5190}) as save,patch.object(ui.socket,'create_connection',side_effect=OSError()),patch.object(ui.subprocess,'Popen') as launch:
        window.start()
        save.assert_called_once_with(str(config_path),'192.168.50.1')
        command=launch.call_args.args[0]
        assert command[-2:]==['--config',str(config_path)] and command[2]==str(window.folder/'attendance_connector.py')
print('Windows setup checks passed: clickable scan/select/start callbacks, async results, selected address and atomic private configuration updates.')
