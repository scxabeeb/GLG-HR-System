#!/usr/bin/env python3
"""Exercise command generation without connecting to a database or terminal."""
from pathlib import Path
import subprocess
import tempfile
from xml.sax.saxutils import escape

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix="garowe-face-") as folder:
    target = Path(folder)
    project = escape(str(root / "HrSystemDhg.Web/HrSystemDhg.Web.csproj"))
    (target / "check.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><ProjectReference Include="{project}" /></ItemGroup></Project>''')
    (target / "Program.cs").write_text('using HrSystemDhg.Web.Models;\nusing HrSystemDhg.Web.Services;\nvar device = new ZkDevice { Id=1, PushSerial="TEST", PushAddress="192.168.100.200" };\nvar employee = new Employee { FullName="Test Employee", ZkPin="42" };\nforeach (var type in new[]{2,9}) {\n var command=ZkEmployeeCommands.Create(device, employee, biometricType:type);\n var payload=ZkEmployeeCommands.Payload(command);\n if (!payload.StartsWith($"ENROLL_BIO TYPE={type}\\tPIN=42\\t") || !payload.EndsWith("OVERWRITE=0") || command.FingerSlot != null || command.ExpiresUtc > DateTime.UtcNow.AddMinutes(4)) throw new Exception("Face command failed");\n}\nif (!ZkEmployeeCommands.Payload(ZkEmployeeCommands.Create(device,employee,3)).StartsWith("ENROLL_FP PIN=42\\tFID=3")) throw new Exception("Fingerprint regression");\nif (!ZkEmployeeCommands.Payload(ZkEmployeeCommands.Create(device,employee)).Contains("Name=Test Employee")) throw new Exception("Sync regression");\ntry { ZkEmployeeCommands.Create(device,employee,biometricType:8); throw new Exception("Invalid type accepted"); } catch (ArgumentOutOfRangeException) {}\nConsole.WriteLine("Face types, expiry, non-overwrite, fingerprint, profile sync and invalid type checks passed.");\n')
    subprocess.run(["dotnet", "run", "--project", str(target / "check.csproj"), "-p:NuGetAudit=false"], check=True, cwd=root)
