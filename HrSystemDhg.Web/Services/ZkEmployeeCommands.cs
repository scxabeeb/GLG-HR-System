using System.Text;
using System.Text.RegularExpressions;
using HrSystemDhg.Web.Models;

namespace HrSystemDhg.Web.Services;

public static class ZkEmployeeCommands
{
    public static string Label(ZkDeviceCommand command) => command.BiometricType == 9 ? "Visible-light face enrollment" : command.BiometricType == 2 ? "Face enrollment" : command.FingerSlot.HasValue ? "Fingerprint enrollment" : "Employee sync";

    public static string Payload(ZkDeviceCommand command) => command.BiometricType switch
    {
        2 or 9 => $"ENROLL_BIO TYPE={command.BiometricType}\tPIN={command.Pin}\tCardNo=\tRETRY=3\tOVERWRITE=0",
        null when command.FingerSlot is int slot => $"ENROLL_FP PIN={command.Pin}\tFID={slot}\tRETRY=3\tOVERWRITE=0",
        null => $"DATA UPDATE USERINFO PIN={command.Pin}\tName={command.Name}\tPri=0",
        _ => throw new InvalidOperationException("Unsupported biometric enrollment type.")
    };

    public static bool ValidPin(string? pin) => pin != null && Regex.IsMatch(pin, @"\A[1-9][0-9]{0,8}\z");

    public static ZkDeviceCommand Create(ZkDevice device, Employee employee, int? fingerSlot = null, int? biometricType = null)
    {
        if (biometricType is not (null or 2 or 9)) throw new ArgumentOutOfRangeException(nameof(biometricType));
        var name = new StringBuilder();
        var bytes = 0;
        if (fingerSlot == null)
        {
            foreach (var rune in employee.FullName.Trim().EnumerateRunes())
            {
                if (Rune.IsControl(rune)) continue;
                if (bytes + rune.Utf8SequenceLength > 24) break;
                name.Append(rune.ToString());
                bytes += rune.Utf8SequenceLength;
            }
        }

        return new()
        {
            DeviceId = device.Id, Serial = device.PushSerial!, Address = device.PushAddress!,
            Pin = employee.ZkPin!, Name = name.ToString(), FingerSlot = fingerSlot, BiometricType = biometricType,
            ExpiresUtc = fingerSlot.HasValue || biometricType.HasValue ? DateTime.UtcNow.AddMinutes(3) : DateTime.UtcNow.AddDays(1)
        };
    }
}
