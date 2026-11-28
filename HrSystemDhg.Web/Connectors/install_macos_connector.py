#!/usr/bin/env python3
"""Install the current user's attendance login agent; preserves private offline records."""
import argparse
import os
from pathlib import Path
import plistlib
import shutil
import subprocess
import sys

LABEL = 'so.garowecity.attendance-connector'

def install(config_path):
    if sys.platform != 'darwin':
        raise ValueError('This installer runs on macOS only.')
    source = Path(config_path).expanduser().resolve(strict=True)
    import attendance_connector
    attendance_connector.load_config(str(source))
    runtime = Path.home() / 'Library/Application Support/GaroweCity/AttendanceConnector'
    runtime.mkdir(parents=True, exist_ok=True, mode=0o700)
    runtime.chmod(0o700)
    config = runtime / 'config.json'
    queue = source.parent / 'attendance-queue.sqlite3'
    destination_queue = runtime / queue.name
    if source != config:
        # Never overwrite an existing runtime queue during an update.
        if queue.exists() and not destination_queue.exists():
            import sqlite3
            with sqlite3.connect(queue) as old, sqlite3.connect(destination_queue) as new:
                old.backup(new)
            destination_queue.chmod(0o600)
        shutil.copy2(source, config)
    config.chmod(0o600)
    script = runtime / 'attendance_connector.py'
    origin = Path(__file__).resolve().with_name('attendance_connector.py')
    if origin != script:
        shutil.copy2(origin, script)
    script.chmod(0o600)
    agent = Path.home() / 'Library/LaunchAgents' / (LABEL + '.plist')
    agent.parent.mkdir(parents=True, exist_ok=True)
    data = {'Label': LABEL, 'ProgramArguments': [sys.executable, '-u', str(script), '--config', str(config)],
            'WorkingDirectory': str(runtime), 'RunAtLoad': True, 'KeepAlive': True,
            'ThrottleInterval': 15, 'ProcessType': 'Background',
            'StandardOutPath': str(runtime / 'connector.log'),
            'StandardErrorPath': str(runtime / 'connector-error.log')}
    domain = 'gui/' + str(os.getuid())
    # Reload only our own agent. Other login items are untouched.
    subprocess.run(['launchctl', 'bootout', domain + '/' + LABEL], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    with agent.open('wb') as target:
        plistlib.dump(data, target)
    agent.chmod(0o600)
    subprocess.run(['launchctl', 'bootstrap', domain, str(agent)], check=True)
    print('Automatic attendance connector installed for this Mac account.')
    print('Starts at login, restarts after a crash, and waits for the configured Ethernet IP.')
    print('Terminal status becomes online only after actual device contact reaches the system.')
    print('Logs:', runtime)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', required=True, help='Existing downloaded connector JSON; contents are never displayed')
    args = parser.parse_args()
    try:
        install(args.config)
    except (ValueError, OSError, subprocess.CalledProcessError):
        parser.exit(1, 'Could not install the login agent. Check configuration, macOS login session and folder permissions.\n')
