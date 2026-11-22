#!/bin/sh
set -eu
if [ -n "${PORT:-}" ]; then
  export ASPNETCORE_URLS="http://0.0.0.0:$PORT"
fi
dotnet HrSystemDhg.Web.dll --migrate
dotnet HrSystemDhg.Web.dll --seed
exec dotnet HrSystemDhg.Web.dll
