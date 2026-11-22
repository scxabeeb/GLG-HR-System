using System.Text.Json;
using HrSystemDhg.Web.Models;
using HrSystemDhg.Web.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

public class OperationsNotificationsTests
{
    [Theory]
    [InlineData("+252 61 234 5678", "+252612345678")]
    [InlineData("00252 (61) 234-5678", "+252612345678")]
    [InlineData("0612345678", null)]
    [InlineData("+252612345678 ext 1", null)]
    public void OnlyExplicitInternationalNumbersCanBeUsed(string input, string? expected) =>
        Assert.Equal(expected, OperationsNotices.NormalizePhone(input));

    [Fact]
    public async Task UsesApprovedTemplateWithoutPrivateTaskOrMeetingDetails()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsApp:Enabled"] = "true", ["WhatsApp:GraphVersion"] = "v99.0",
            ["WhatsApp:PhoneNumberId"] = "12345", ["WhatsApp:AccessToken"] = "test-token",
            ["WhatsApp:TaskTemplate"] = "task_alert", ["WhatsApp:MeetingTemplate"] = "meeting_alert"
        }).Build();
        var settings = Assert.IsType<WhatsAppSettings>(WhatsAppSettings.From(config));
        using var request = settings.CreateMessage("+252612345678", OperationsNoticeKind.MeetingInvitation);
        Assert.Equal("https://graph.facebook.com/v99.0/12345/messages", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        Assert.Equal("252612345678", body.RootElement.GetProperty("to").GetString());
        var template = body.RootElement.GetProperty("template");
        Assert.Equal("meeting_alert", template.GetProperty("name").GetString());
        Assert.False(template.TryGetProperty("components", out _));
        config["WhatsApp:Enabled"] = "false";
        Assert.Null(WhatsAppSettings.From(config));
    }
}
