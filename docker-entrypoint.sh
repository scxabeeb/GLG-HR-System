#!/bin/sh
set -eu
if [ -n "${PORT:-}" ]; then
  export ASPNETCORE_URLS="http://0.0.0.0:$PORT"
fi
if [ "$(id -u)" = 0 ]; then
  mkdir -p /app/App_Data
  chown -R app:app /app/App_Data
  exec su -s /bin/sh app -c 'exec ./docker-entrypoint.sh'
fi
dotnet HrSystemDhg.Web.dll --migrate
dotnet HrSystemDhg.Web.dll --seed
exec dotnet HrSystemDhg.Web.dll
