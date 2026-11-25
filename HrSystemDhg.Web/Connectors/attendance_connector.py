#!/usr/bin/env python3
"""Private-LAN ADMS receiver forwarding only paired terminals to the online ERP over HTTPS."""
import argparse
import hashlib
import sqlite3
import socket
import concurrent.futures
import glob
import subprocess
import datetime
import http.server
import ipaddress
import json
import os
import re
import threading
import urllib.error
import urllib.parse
import urllib.request

MAX_BODY = 256 * 1024

class CloudUnavailable(ConnectionError):
    def __init__(self, message, retryable=False):
        super().__init__(message)
        self.retryable = retryable

class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

def load_config(path, allow_test=False):
    with open(path, encoding='utf-8') as source:
        config = json.load(source)
    origin = urllib.parse.urlsplit(config.get('server', ''))
    if not origin.hostname or origin.username or origin.password or origin.path not in ('', '/') or origin.query or origin.fragment or not (
        origin.scheme == 'https' or allow_test and origin.scheme == 'http' and origin.hostname == '127.0.0.1'):
        raise ValueError('Server must be an HTTPS origin without a path or credentials')
    address = ipaddress.ip_address(config.get('listen', ''))
    if address.version != 4 or not address.is_private or address.is_loopback and not allow_test:
        raise ValueError('Listen address must be the computer\'s private Ethernet IPv4 address')
    if not isinstance(config.get('port'), int) or not 1024 <= config['port'] <= 65535:
        raise ValueError('Choose a listener port between 1024 and 65535')
    terminals = config.get('terminals', [])
    if not 1 <= len(terminals) <= 100:
        raise ValueError('Configure between 1 and 100 terminals')
    serials, addresses, ids = set(), set(), set()
    for terminal in terminals:
        if not isinstance(terminal.get('id'), int) or terminal['id'] <= 0 or not re.fullmatch(r'[A-Za-z0-9_-]{1,64}', terminal.get('serial', '')) or not re.fullmatch(r'[A-Fa-f0-9]{64}', terminal.get('key', '')):
            raise ValueError('Invalid terminal ID, serial or connector key')
        terminal['serial'] = terminal['serial'].upper()
        peer = ipaddress.ip_address(terminal.get('address', ''))
        if peer.version != 4 or not peer.is_private or peer.is_loopback and not allow_test:
            raise ValueError('Terminal address must be a private IPv4 address')
        if terminal['serial'] in serials or terminal['address'] in addresses or terminal['id'] in ids:
            raise ValueError('Each terminal needs a unique ID, serial and IP address')
        serials.add(terminal['serial']); addresses.add(terminal['address']); ids.add(terminal['id'])
    config['server'] = config['server'].rstrip('/')
    config['_queue_path'] = os.path.join(os.path.dirname(os.path.abspath(path)), 'attendance-queue.sqlite3')
    if os.name != 'nt':
        os.chmod(path, 0o600)
    return config

def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

class Connector:
    def __init__(self, config):
        self.config = config
        self.opener = urllib.request.build_opener(NoRedirect())
        self.lock = threading.Lock()
        self.status = {t['id']: {'cloud_connected': False, 'last_cloud_utc': None, 'last_terminal_utc': None, 'last_error': None} for t in config['terminals']}
        self.stop = threading.Event()
        self.queue_lock = threading.Lock()
        self.queue_path = config.get('_queue_path')
        if self.queue_path:
            descriptor = os.open(self.queue_path, os.O_CREAT | os.O_RDWR, 0o600)
            os.close(descriptor)
            os.chmod(self.queue_path, 0o600)
            with sqlite3.connect(self.queue_path) as queue:
                queue.execute('CREATE TABLE IF NOT EXISTS uploads (id INTEGER PRIMARY KEY, device INTEGER, serial TEXT, address TEXT, query TEXT, body BLOB, checksum TEXT UNIQUE, records INTEGER, created TEXT)')

    def enqueue(self, terminal, query, body):
        if not self.queue_path or not body:
            return None
        try:
            fields = urllib.parse.parse_qs(query)
            if fields.get('table') != ['ATTLOG']:
                return None
            lines = body.decode('utf-8').strip().splitlines()
            for line in lines:
                values = line.split('\t')
                if len(values) < 2 or not re.fullmatch(r'[A-Za-z0-9_-]{1,32}', values[0]):
                    return None
                datetime.datetime.strptime(values[1], '%Y-%m-%d %H:%M:%S')
            if not lines:
                return None
            digest = hashlib.sha256((str(terminal['id']) + terminal['serial'] + terminal['address'] + query).encode() + body).hexdigest()
            with self.queue_lock, sqlite3.connect(self.queue_path) as queue:
                if queue.execute('SELECT 1 FROM uploads WHERE checksum=?', (digest,)).fetchone():
                    return len(lines)
                count, size = queue.execute('SELECT COUNT(*), COALESCE(SUM(LENGTH(body)),0) FROM uploads').fetchone()
                if count >= 10000 or size + len(body) > 100 * 1024 * 1024:
                    return None
                queue.execute('INSERT INTO uploads(device,serial,address,query,body,checksum,records,created) VALUES(?,?,?,?,?,?,?,?)',
                    (terminal['id'],terminal['serial'],terminal['address'],query,body,digest,len(lines),utc()))
            return len(lines)
        except (ValueError, UnicodeError, sqlite3.Error, OSError):
            return None

    def queued_counts(self):
        if not self.queue_path:
            return {}
        with self.queue_lock, sqlite3.connect(self.queue_path) as queue:
            return dict(queue.execute('SELECT device,SUM(records) FROM uploads GROUP BY device').fetchall())

    def replay_once(self):
        if not self.queue_path:
            return
        with self.queue_lock, sqlite3.connect(self.queue_path) as queue:
            rows = queue.execute('SELECT id,device,serial,address,query,body,records FROM uploads ORDER BY id LIMIT 100').fetchall()
        for identifier, device, serial, address, query, body, records in rows:
            terminal = next((t for t in self.config['terminals'] if (t['id'],t['serial'],t['address']) == (device,serial,address)), None)
            if terminal is None:
                continue
            try:
                result, _ = self.cloud(terminal, 'cdata', query, body)
                if result.strip() != ('OK: ' + str(records)).encode():
                    break
                with self.queue_lock, sqlite3.connect(self.queue_path) as queue:
                    queue.execute('DELETE FROM uploads WHERE id=?', (identifier,))
            except ConnectionError:
                break

    def replay(self):
        while not self.stop.is_set():
            try:
                self.replay_once()
            except (sqlite3.Error, OSError):
                pass  # Retain records on disk and retry; never acknowledge an unsaved upload.
            self.stop.wait(10)

    def cloud(self, terminal, operation, query='', body=None):
        url = self.config['server'] + '/api/attendance/bridge/' + str(terminal['id']) + '/' + operation
        if query:
            url += '?' + query
        request = urllib.request.Request(url, data=body, headers={
            'Authorization': 'Bearer ' + terminal['key'], 'X-Terminal-Serial': terminal['serial'],
            'X-Terminal-Address': terminal['address'], 'Content-Type': 'application/octet-stream',
        })
        try:
            with self.opener.open(request, timeout=15) as response:
                content = response.read(MAX_BODY + 1)
                if len(content) > MAX_BODY:
                    raise ValueError('Cloud response exceeds protocol limit')
                content_type = response.headers.get('Content-Type', 'text/plain')
            with self.lock:
                state = self.status[terminal['id']]
                state.update(cloud_connected=True, last_cloud_utc=utc(), last_error=None)
                if operation != 'heartbeat':
                    state['last_terminal_utc'] = utc()
            return content, content_type
        except Exception as error:
            message = 'Cloud returned HTTP ' + str(error.code) if isinstance(error, urllib.error.HTTPError) else 'Cloud connection failed (' + type(error).__name__ + ')'
            if isinstance(error, urllib.error.HTTPError) and error.code == 400 and operation == 'cdata' and body:
                fields = body.decode('utf-8', errors='replace').splitlines()[0].split('\t')
                timestamp = fields[1] if len(fields) > 1 and re.fullmatch(r'\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}', fields[1]) else 'unrecognized timestamp format'
                now_eat = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(hours=3)).strftime('%Y-%m-%d %H:%M:%S')
                message += '; record time=' + timestamp + '; current EAT=' + now_eat
            with self.lock:
                self.status[terminal['id']].update(cloud_connected=False, last_error=message)
            raise CloudUnavailable(message, isinstance(error, (urllib.error.URLError, TimeoutError, socket.timeout, OSError)) and not isinstance(error, urllib.error.HTTPError) or isinstance(error, urllib.error.HTTPError) and (error.code in (408,425,429) or error.code >= 500)) from None
    def heartbeat(self):
        while not self.stop.is_set():
            for terminal in self.config['terminals']:
                try:
                    self.cloud(terminal, 'heartbeat')
                except ConnectionError as error:
                    print(utc(), 'device', terminal['id'], str(error), flush=True)
            self.stop.wait(30)

class Handler(http.server.BaseHTTPRequestHandler):
    def setup(self):
        super().setup()
        self.connection.settimeout(20)
    def log_message(self, format, *args):
        pass  # Avoid storing ADMS query parameters or payloads in access logs.
    def reply(self, status, body=b'', content_type='text/plain'):
        self.send_response(status)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(body)
    def do_GET(self):
        self.dispatch()
    def do_POST(self):
        self.dispatch()
    def dispatch(self):
        connector = self.server.connector
        if len(self.path) > 4096:
            return self.reply(414)
        parsed = urllib.parse.urlsplit(self.path)
        if parsed.path == '/health' and self.command == 'GET' and self.client_address[0] in ('127.0.0.1', connector.config['listen']):
            with connector.lock:
                states = {key: dict(value) for key,value in connector.status.items()}
            return self.reply(200, json.dumps({'listener': connector.config['listen'], 'port': connector.config['port'], 'devices': states, 'queued_punches': connector.queued_counts()}).encode(), 'application/json')
        route = parsed.path.lower()
        allowed = {'/iclock/cdata': 'cdata', '/iclock/cdata.aspx': 'cdata', '/iclock/getrequest': 'getrequest', '/iclock/getrequest.aspx': 'getrequest', '/iclock/devicecmd': 'devicecmd', '/iclock/devicecmd.aspx': 'devicecmd'}
        operation = allowed.get(route)
        if operation is None or operation == 'getrequest' and self.command != 'GET' or operation == 'devicecmd' and self.command != 'POST':
            return self.reply(404)
        try:
            fields = urllib.parse.parse_qs(parsed.query, max_num_fields=50)
        except ValueError:
            return self.reply(400)
        serial = fields.get('SN', [])
        if len(serial) != 1:
            return self.reply(403)
        terminal = next((t for t in connector.config['terminals'] if t['address'] == self.client_address[0] and t['serial'] == serial[0].upper()), None)
        if terminal is None:
            return self.reply(403)
        if self.headers.get('Transfer-Encoding') or len(self.headers.get_all('Content-Length', [])) > 1:
            return self.reply(400)
        try:
            length = int(self.headers.get('Content-Length', '0'))
        except ValueError:
            return self.reply(400)
        if length < 0 or length > MAX_BODY:
            return self.reply(413)
        body = self.rfile.read(length) if self.command == 'POST' else None
        if self.command == 'POST' and len(body) != length:
            return self.reply(400)
        try:
            content, content_type = connector.cloud(terminal, operation, parsed.query, body)
        except ConnectionError as error:
            print(utc(), 'device', terminal['id'], operation, str(error), flush=True)
            if isinstance(error, CloudUnavailable) and error.retryable and operation == 'cdata' and self.command == 'POST':
                count = connector.enqueue(terminal, parsed.query, body)
                if count is not None:
                    return self.reply(200, ('OK: ' + str(count)).encode())
            return self.reply(503, b'Cloud unavailable; retry later')
        self.reply(200, content, content_type)
        if self.command == 'POST':
            print(utc(), 'device', terminal['id'], operation, 'forwarded successfully', flush=True)

def discovery_network(value):
    network = ipaddress.ip_network(value, strict=False)
    if network.version != 4 or network.prefixlen < 24 or not any(network.subnet_of(ipaddress.ip_network(block)) for block in ('10.0.0.0/8','172.16.0.0/12','192.168.0.0/16','169.254.0.0/16')):
        raise ValueError('Enter a private IPv4 office subnet of /24 or smaller, for example 192.168.1.0/24')
    return network

def scan_network(value):
    network = discovery_network(value)
    def probe(address):
        try:
            with socket.create_connection((str(address),4370),timeout=0.5):
                return str(address)
        except OSError:
            return None
    print('Checking', network, 'for reachable TCP/4370 candidates. No settings or attendance records are changed.')
    with concurrent.futures.ThreadPoolExecutor(max_workers=24) as pool:
        found = [address for address in pool.map(probe,network.hosts()) if address]
    for address in found:
        print(address, 'TCP/4370 reachable; verify ZKTeco model and serial on the terminal before pairing.')
    if not found:
        print('No TCP/4370 candidates found. This does not prove devices are offline: UDP-only devices or blocked ports may not appear. Read the IP from COMM / Ethernet on the terminal.')
    print('Discovery does not pair devices. Register each verified IP and serial in Admin / Attendance devices, assign its branch, then configure ADMS.')
    return found

def computer_addresses():
    rows=[]
    if os.name=='nt':
        # Read interface names and actual subnet prefixes locally; no internet request.
        command="[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; Get-NetIPAddress -AddressFamily IPv4 -AddressState Preferred | Select-Object IPAddress,InterfaceAlias,PrefixLength | ConvertTo-Json -Compress"
        try:
            output=subprocess.check_output(['powershell.exe','-NoProfile','-NonInteractive','-Command',command],text=True,encoding='utf-8',timeout=10,stderr=subprocess.DEVNULL)
            records=json.loads(output.lstrip('\ufeff')) if output.strip() else []
            if isinstance(records,dict):records=[records]
            for item in records:
                rows.append({'address':item['IPAddress'],'interface':item['InterfaceAlias'],'prefix':int(item['PrefixLength'])})
        except (OSError,ValueError,KeyError,TypeError,subprocess.SubprocessError):
            pass
    if not rows:
        try:
            rows=[{'address':address,'interface':'Local adapter','prefix':None} for address in socket.gethostbyname_ex(socket.gethostname())[2]]
        except OSError:
            pass
    found={}
    for row in rows:
        try:
            discovery_network(row['address']+'/32')
            if row['prefix'] is not None and not 0<=row['prefix']<=32:continue
            found.setdefault(row['address'],row)
        except ValueError:
            continue
    return sorted(found.values(),key=lambda row:tuple(int(part) for part in row['address'].split('.')))

def display_computer_addresses(rows,terminals=()):
    print('Current computer private IPv4 addresses (detected locally):')
    for index,row in enumerate(rows,1):
        network=ipaddress.ip_network(row['address']+'/'+str(row['prefix']),strict=False) if row['prefix'] is not None else None
        matches=network is not None and any(ipaddress.ip_address(t['address']) in network for t in terminals)
        suffix=' - same subnet as a configured terminal' if matches else ''
        print(str(index)+'.',row['interface']+':',row['address']+('/'+str(row['prefix']) if row['prefix'] is not None else ''),suffix)
    if not rows:print('No private IPv4 address detected. Check the Ethernet adapter and cable, or configure its IPv4 address first.')
    print('Use the Ethernet adapter connected to the terminal. Wi-Fi may provide internet separately.')

def choose_computer_address(terminals):
    while True:
        rows=computer_addresses();display_computer_addresses(rows,terminals)
        choice=input('Select adapter number, R to scan again, or M to enter IP manually: ').strip()
        if choice.lower()=='r':continue
        if choice.lower()=='m':return input('Office computer Ethernet IPv4 address: ').strip()
        if choice.isdigit() and 1<=int(choice)<=len(rows):return rows[int(choice)-1]['address']
        print('Choose a listed adapter number, R or M.')

def setup_windows(path):
    with open(path,encoding='utf-8') as source:
        config=json.load(source)
    address=choose_computer_address(config.get('terminals',[]))
    ip=ipaddress.ip_address(address)
    if ip.version!=4 or not ip.is_private or ip.is_loopback:
        raise ValueError('Choose the computer private Ethernet IPv4 address')
    with socket.socket() as listener:
        listener.bind((address,0)) # Verify the chosen address belongs to this computer.
    config['listen']=address
    temporary=path+'.tmp'
    with open(temporary,'w',encoding='utf-8') as target:
        json.dump(config,target,indent=2)
    os.chmod(temporary,0o600);os.replace(temporary,path)
    print('On each configured terminal: ADMS server address =',address,', port =',config['port'],', HTTPS OFF, domain mode OFF.')
    print('Allow this connector on the trusted private office network if Windows Firewall asks. Keep this window open and the computer awake.')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config')
    parser.add_argument('--computer-ip', action='store_true', help='Show this computer private IPv4 addresses and adapter names without changing settings')
    parser.add_argument('--scan', nargs='?', const='prompt', help='Find TCP/4370 candidates on a private office subnet; does not pair or modify terminals')
    parser.add_argument('--setup', action='store_true', help='Choose this office computer IP before starting; select a downloaded configuration if omitted')
    parser.add_argument('--check', action='store_true', help='Check cloud authentication without reporting a terminal connection')
    parser.add_argument('--allow-loopback-test', action='store_true', help=argparse.SUPPRESS)
    args = parser.parse_args()
    if args.computer_ip:
        display_computer_addresses(computer_addresses())
        return
    if args.scan is not None:
        try:
            scan_network(input('Office network, e.g. 192.168.1.0/24: ').strip() if args.scan=='prompt' else args.scan)
        except ValueError as error:
            parser.error(str(error))
        return
    if args.setup and not args.config:
        choices=sorted(glob.glob(os.path.join(os.path.dirname(os.path.abspath(__file__)),'attendance-connector-*.json')))
        if not choices:
            parser.error('Download the device connector configuration from Admin / Attendance devices and save it in this folder.')
        if len(choices)>1:
            for index,path in enumerate(choices,1):print(index,os.path.basename(path))
            try:
                selected=int(input('Choose this office configuration number: '))
                if not 1<=selected<=len(choices):raise ValueError('Invalid selection')
                args.config=choices[selected-1]
            except (ValueError,IndexError):parser.error('Choose a listed configuration number')
        else:args.config=choices[0]
    if not args.config:
        parser.error('--config is required to run the connector; use --scan for network discovery')
    try:
        if args.setup:setup_windows(args.config)
        config = load_config(args.config, args.allow_loopback_test)
    except (ValueError, OSError) as error:
        parser.error(str(error))
    connector = Connector(config)
    if args.check:
        for terminal in config['terminals']:
            try:
                result, _ = connector.cloud(terminal, 'heartbeat')
                print('Cloud connection OK:', result.decode(), flush=True)
            except ConnectionError as error:
                parser.exit(1, str(error) + '\n')
        return
    server = http.server.ThreadingHTTPServer((config['listen'], config['port']), Handler)
    server.daemon_threads = True
    server.connector = connector
    threading.Thread(target=connector.heartbeat, daemon=True).start()
    threading.Thread(target=connector.replay, daemon=True).start()
    print(utc(), 'Attendance connector listening on', config['listen'] + ':' + str(config['port']), flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        connector.stop.set(); server.server_close()
if __name__ == '__main__':
    main()
