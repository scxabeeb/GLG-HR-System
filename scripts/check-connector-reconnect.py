"""No hardware or credentials required: verify receiver startup while Ethernet is absent."""
import errno
import importlib.util
from pathlib import Path
import threading
import unittest

path=Path(__file__).resolve().parents[1]/'HrSystemDhg.Web/Connectors/attendance_connector.py'
spec=importlib.util.spec_from_file_location('attendance_connector',path)
connector=importlib.util.module_from_spec(spec);spec.loader.exec_module(connector)

class StubConnector:
    config={'listen':'192.168.50.1','port':5190}
    def __init__(self):self.stop=threading.Event()
    def heartbeat(self):pass
    def replay(self):pass

class ReconnectChecks(unittest.TestCase):
    def test_waits_for_ethernet_then_serves_configured_address(self):
        state=StubConnector();attempts=[]
        class Server:
            closed=False
            def serve_forever(self):self.served=True
            def server_close(self):self.closed=True
        server=Server()
        def bind(address,handler):
            attempts.append(address)
            if len(attempts)<3:raise OSError(errno.EADDRNOTAVAIL,'Ethernet unavailable')
            return server
        connector.run_receiver(state,bind,0)
        self.assertEqual(attempts,[('192.168.50.1',5190)]*3)
        self.assertTrue(server.served and server.closed and state.stop.is_set())
    def test_does_not_hide_port_conflicts_or_bind_to_all_interfaces(self):
        state=StubConnector()
        def bind(address,handler):raise OSError(errno.EADDRINUSE,'Port in use')
        with self.assertRaises(OSError):connector.run_receiver(state,bind,0)
        self.assertTrue(state.stop.is_set())
    def test_can_stop_while_waiting_for_a_cable(self):
        state=StubConnector()
        def bind(address,handler):
            state.stop.set();raise OSError(errno.EADDRNOTAVAIL,'No cable')
        connector.run_receiver(state,bind,0)
        self.assertTrue(state.stop.is_set())

if __name__=='__main__':unittest.main()
