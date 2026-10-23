# Local MySQL setup

Configured on 22 October 2026.

- Server: `127.0.0.1:3306`
- Database: `hr_system_dhg`
- Database account: `root`
- Server version verified: MySQL 9.4.0
- Applied migrations: `InitialCreate`, `CompleteHrWorkflows`
- Schema: 36 tables; four Identity roles initialized.
- Application administrator: `admin@garowe.local`
- Private initial application login: `HrSystemDhg.Web/App_Data/local-admin-login.txt`

The MySQL password is stored only in `HrSystemDhg.Web/App_Data/local-settings.json`, with owner-only file permissions. Both private files are ignored by Git and excluded from the source archive. The application administrator must change its separate temporary password on first sign-in.

Run with `dotnet run --project HrSystemDhg.Web --launch-profile http`. In Workbench, open the local server connection and refresh Schemas to see `hr_system_dhg`. The earlier synthetic demo database was left separate.
