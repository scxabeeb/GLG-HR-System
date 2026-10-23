using System.Globalization;
namespace HrSystemDhg.Web.Services;
public static class RegistrationChoices {
 public static readonly string[] MaritalStatuses = ["Single", "Married", "Divorced", "Widowed", "Separated", "Prefer not to say"];
 public static readonly string[] Nationalities = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Select(c => new RegionInfo(c.Name).EnglishName).Append("Somalia").Distinct().OrderBy(c => c).ToArray();
 public static bool ValidOptionalChoice(string? value, IEnumerable<string> choices) => string.IsNullOrWhiteSpace(value) || choices.Contains(value);
}
