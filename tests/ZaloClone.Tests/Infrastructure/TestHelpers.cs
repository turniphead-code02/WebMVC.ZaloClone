using System.Text.RegularExpressions;

namespace ZaloClone.Tests.Infrastructure;

public static partial class TestHelpers
{
    public static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid User1Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid User2Id = Guid.Parse("00000000-0000-0000-0000-000000000003");
    public static readonly Guid User3Id = Guid.Parse("00000000-0000-0000-0000-000000000004");
    public const string Password = "Test@123";

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiForgeryRegex();

    public static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = AntiForgeryRegex().Match(html);
        Assert.True(match.Success, $"Không tìm thấy antiforgery token ở {url}");
        return match.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email, string password, string? returnUrl = null)
    {
        var token = await GetAntiForgeryTokenAsync(client, "/Account/Login");
        var form = new Dictionary<string, string>
        {
            ["EmailOrPhone"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl ?? string.Empty,
            ["__RequestVerificationToken"] = token
        };
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form));
    }

    /// <summary>Đăng nhập bằng form và trả về client đã có cookie (không tự theo redirect).</summary>
    public static async Task<HttpClient> LoginAsync(ZaloAppFactory factory, string email)
    {
        var client = factory.CreateNoRedirectClient();
        var response = await PostLoginAsync(client, email, Password);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    public static bool IsRedirectToLogin(HttpResponseMessage response) =>
        response.StatusCode == System.Net.HttpStatusCode.Redirect &&
        response.Headers.Location?.OriginalString.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase) == true;
}
