#!/bin/sh
set -eu
dotnet HrSystemDhg.Web.dll --migrate
dotnet HrSystemDhg.Web.dll --seed
exec dotnet HrSystemDhg.Web.dll
