#!/usr/bin/env python3
"""Private-LAN ADMS receiver forwarding only paired terminals to the online ERP over HTTPS."""
import argparse
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
            raise ConnectionError(message) from None
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
                return self.reply(200, json.dumps({'listener': connector.config['listen'], 'port': connector.config['port'], 'devices': connector.status}).encode(), 'application/json')
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
            return self.reply(503, b'Cloud unavailable; retry later')
        self.reply(200, content, content_type)
        if self.command == 'POST':
            print(utc(), 'device', terminal['id'], operation, 'forwarded successfully', flush=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', required=True)
    parser.add_argument('--check', action='store_true', help='Check cloud authentication without reporting a terminal connection')
    parser.add_argument('--allow-loopback-test', action='store_true', help=argparse.SUPPRESS)
    args = parser.parse_args()
    try:
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
    print(utc(), 'Attendance connector listening on', config['listen'] + ':' + str(config['port']), flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        connector.stop.set(); server.server_close()
if __name__ == '__main__':
    main()
