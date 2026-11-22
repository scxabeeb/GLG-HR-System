using System.Text;
using System.Text.RegularExpressions;
using HrSystemDhg.Web.Models;

namespace HrSystemDhg.Web.Services;

public static class ZkEmployeeCommands
{
    public static bool ValidPin(string? pin) => pin != null && Regex.IsMatch(pin, @"\A[1-9][0-9]{0,8}\z");

    public static ZkDeviceCommand Create(ZkDevice device, Employee employee, int? fingerSlot = null)
    {
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
            Pin = employee.ZkPin!, Name = name.ToString(), FingerSlot = fingerSlot,
            ExpiresUtc = fingerSlot.HasValue ? DateTime.UtcNow.AddMinutes(3) : DateTime.UtcNow.AddDays(1)
        };
    }
}
