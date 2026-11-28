# Connect a ZKTeco terminal

This application supports the attendance-log subset of ZKTeco ADMS / Attendance PUSH. The [manufacturer’s FAQ](https://www.zkteco.com/en/faq.html) explains that ADMS depends on the model and firmware. The terminal must have **COMM → ADMS** or **Cloud Server Setting** with a server address and port. If the menu is missing, get the model number from the rear label and confirm the supported SDK with the vendor before changing firmware or buying anything. This app does not include a TCP/4370 pull driver.

## Railway / online ERP: office connector

When the ERP runs on Railway, use **Admin → Attendance devices → Edit / configure → Office connector**. Download the script and device configuration and run the connector on the office Mac. The connector listens on the private Ethernet address and forwards requests to the authenticated cloud receiver over HTTPS. It also carries profile/enrollment commands back to the terminal. The ADMS routes remain private-LAN-only unless a device is explicitly enabled for the HTTPS/public-IP direct-router mode described below.

For the direct Mac cable connection, the terminal is `192.168.50.2/24`, the Mac Ethernet interface is `192.168.50.1/24`, and the terminal ADMS server is `192.168.50.1`, port `5190`, HTTPS OFF, domain mode OFF. Keep the Mac awake and online. Start the downloaded script with `python3 attendance_connector.py --config attendance-connector-ID.json`; the JSON's `listen` field must be the Mac's actual Ethernet address. `--check` tests the cloud key without reporting the terminal online. The local `/health` endpoint and the web device page distinguish connector heartbeat from actual terminal contact.

### Automatic connection on this Mac

Run the repository's installer once with the downloaded configuration:

```sh
python3 HrSystemDhg.Web/Connectors/install_macos_connector.py --config /path/to/attendance-connector-ID.json
```

It installs a per-user Garowe City LaunchAgent, starts the connector immediately and at each login, and restarts it if it exits. Private configuration, logs and the retained offline queue live in `~/Library/Application Support/GaroweCity/AttendanceConnector`; existing queued records are copied without overwriting an existing runtime queue. No MySQL credentials are needed. Do not run a second manual connector on the same port.

If the configured Ethernet address is unavailable at login, the receiver retries every five seconds without switching to a public or wildcard interface. Reconnect the cable with the same static Mac/terminal addresses; the terminal's next ADMS contact updates its real online status. A cable or connector heartbeat alone does not mark the terminal online. Keep the Mac awake, logged in and connected to the internet for immediate cloud sync. The connector does not change network settings or rewrite the terminal's server address. To remove automatic startup, run `launchctl bootout gui/$(id -u)/so.garowecity.attendance-connector` and remove its plist from `~/Library/LaunchAgents`; preserve the private queue until it is uploaded.

Configuration downloads rotate a device-specific key. Revocation and pairing changes invalidate it. Keep the JSON private; no MySQL password is required on the office Mac. The connector accepts only the paired terminal IP and serial, uses verified HTTPS to the cloud, and returns an error when forwarding fails instead of acknowledging unsaved attendance. The terminal must retry unacknowledged requests; make a real punch to verify this firmware's behavior.

## Setup on the office network

1. Connect the HR host and terminal to the same trusted office network. Give the terminal a stable private IPv4 address (for example, use a DHCP reservation). On the terminal, note that address and the serial number from System Information or the back label.
2. Sign in as Admin and open **Administration → Attendance devices**. Add the terminal if needed, then enter its serial and private IP under **Save ADMS pairing**.
3. The usual local app is listening only at `127.0.0.1:5189`. To accept the terminal, add a dedicated listener on the HR host’s **actual office interface**. From the project directory, substituting your server address:

   ```sh
   ASPNETCORE_ENVIRONMENT=Development AttendancePush__ListenerPort=5190 dotnet run --project HrSystemDhg.Web --no-launch-profile --urls 'http://127.0.0.1:5189;http://192.168.1.20:5190'
   ```

   The dedicated port serves only device endpoints; staff pages stay on the original listener. This example is for local setup only. For ongoing office use, deploy the app with production settings, a firewall that permits terminal traffic only from the configured terminal IP, and HTTPS for staff access. Legacy ADMS traffic is plaintext and IP/serial pairing is not cryptographic authentication; do not port-forward these routes or expose them to the internet. Connect the terminal directly on the private LAN; proxy forwarding is not used as proof of the terminal's source address.
4. On the terminal, set its Cloud Server address to the **HR host’s office IP** (not the terminal IP or `127.0.0.1`), the server port to **5190**, and select ADMS / Attendance PUSH if the menu offers a protocol choice. Use the correct date and local office time. On models with domain mode, use the format specified by the vendor.
5. Make a test punch. **Attendance devices** shows last contact and last successful upload. Under **People & attendance → Attendance & imports**, check the employee and date. If the punch is in the **Unmapped PIN queue**, map the device user ID to the employee. A valid work assignment and working-hours rule are needed to produce a daily summary.

The app accepts tab-delimited attendance rows and only stores the user ID and local punch timestamp. It does not accept enrollment data, fingerprint templates, photos, or access-control logs. Repeated uploads are deduplicated; unmapped punches are preserved. A batch acknowledgement is sent only after the database commits. Compatibility must still be verified with a test punch from the physical terminal; this is not a full implementation of the vendor SDK.

## Multiple branches and locations

Register each physical terminal separately in Administration → Attendance devices. Save its branch and office location, then pair its unique serial number and stable source address. Each terminal retains its own contact status, upload status, error, command queue and source device ID on every punch. The department label organizes devices. Employee assignments and effective work rules determine attendance calculations.

Use the same unique employee PIN on every terminal where that employee punches. All terminals send to the central HR device listener. Offices on separate networks need private routed connectivity, such as a site-to-site VPN, to that listener. Configure distinct routed source addresses for the terminals and pair the addresses observed by the server. Test each office with a real punch and verify its source machine in the attendance register.

The ADMS initialization response advertises `TimeZone=3` (East Africa Time) while HTTP Date headers remain standard GMT/UTC. After an update, restart the terminal or reconnect ADMS so it requests initialization options again. This changes future clock synchronization; previously saved punch timestamps are not shifted.

Future-dated records more than 15 minutes ahead of EAT are saved in a separate **HR → Attendance → Clock review** queue. The batch is acknowledged only after both valid punches and clock exceptions are committed, allowing the terminal to continue uploading. Exceptions never enter attendance or payroll automatically, even when their recorded date eventually arrives. HR/Admin can enter a confirmed actual EAT timestamp with a reason or ignore the invalid punch; the original timestamp and reviewer remain in history. Repeated terminal uploads are deduplicated against both raw punches and the review history.

## Offline attendance
The terminal retains scans while disconnected from the office computer; reconnect it and allow ADMS to send its stored records. When the Mac receives valid ATTLOG uploads but the internet is temporarily unavailable, the connector commits them to `attendance-queue.sqlite3` beside its configuration before acknowledging them. Keep the Mac connector running: queued uploads retry every 10 seconds after connectivity returns, preserving original East Africa timestamps. Duplicate uploads are deduplicated by the server. Commands and biometric enrollment data are never queued. Local `/health` includes queued punch counts. Do not delete the queue during upgrades.

Employees start attendance requirements the day after their registration date in East Africa Time, or their later hire date. Registration-day punches remain visible, but that day creates no absence or attendance deduction. Basic salary remains unchanged; approved unpaid leave remains a separate payroll deduction.

## Windows computers in different offices
Open Admin → Attendance devices and download **Windows scan & setup tools**. Extract the ZIP on each office's Windows computer. Install Python 3 if needed, then run **Scan office network.cmd** on that office's private subnet. It checks TCP/4370 reachability only; candidates require model/serial verification, and UDP-only or firewalled devices may not appear. It does not register or modify devices.

Register each terminal under the correct branch with its verified serial and current office IP. **Download Windows setup for this device** produces a ZIP with that device's configuration and rotates its previous connector key. Extract it, run **Start connector.cmd**, and enter the Windows computer's private Ethernet IP. Set that terminal's ADMS server to this computer's IP and port 5190. Allow the connector through Windows Firewall on the trusted private network and keep it running. Each separate office needs its own connector/computer; the Railway website cannot scan private networks across the internet. Existing Mac-specific 192.168.50.x settings are examples only, not mandatory addresses for the new offices.

## Direct router connection (no office computer)
This mode is off by default. Save the device's private IP/serial pairing, then open its **Router → internet → system** settings. Enter the office's stable public WAN IPv4 address and enable direct HTTPS mode. Connect the terminal to the router; configure gateway and DNS. In ADMS set domain mode ON, server `erp.garowecity.pl.so`, port 443, HTTPS ON, proxy OFF. No inbound router port forwarding is needed. The terminal must support the server's TLS/certificate; otherwise use the Windows connector. Test the real hardware before relying on it.

Direct uploads require HTTPS, the exact paired serial and the saved office public IPv4 address. An office with a changing public IP must update the allowlist whenever it changes; shared carrier NAT is a weaker office boundary, so use the authenticated connector when stable office egress cannot be assured. Re-pairing disables direct access until explicitly approved again. Direct mode does not add bearer authentication to firmware and does not scan remote networks.

Railway deployments resolve the public source using edge-replaced `X-Real-IP` and HTTPS metadata, only when running inside Railway behind a private peer. Other deployments ignore forwarding headers and require a direct HTTPS connection. See [Railway request headers](https://docs.railway.com/networking/public-networking/specs-and-limits). ZKTeco's [FAQ](https://www.zkteco.com/en/faq.html) notes that device/server ADMS HTTP/HTTPS settings must agree.

### Detect the current Windows computer IP
The Windows package includes **Show computer IP.cmd**, which lists current private IPv4 addresses with adapter names and subnet prefixes. **Start connector.cmd** performs the same local detection and lets you select an adapter by number; choose Ethernet connected to the terminal. `R` rescans after plugging in a cable and `M` permits manual entry. Detection does not change Windows network settings. A missing Ethernet address must be configured in Windows first. The web server cannot detect a remote Windows computer's private adapter addresses; run this tool on that computer.

**Button-based setup:** open `Open Windows setup.cmd` from the Windows ZIP. The setup window provides **Scan computer IP**, **Use selected IP**, and **Save & start connector** buttons. Select the Ethernet adapter and a downloaded device configuration. The GUI needs Python Tcl/Tk; the existing command-line tools remain available.
