using System.Net;
using ZaloClone.Tests.Infrastructure;
using static ZaloClone.Tests.Infrastructure.TestHelpers;

namespace ZaloClone.Tests;

[Collection(AppCollection.Name)]
public class AccountTests(ZaloAppFactory factory)
{
    [Fact(DisplayName = "ACC-05 Đăng nhập đúng email/mật khẩu thì vào trang chủ")]
    public async Task Login_ValidCredentials_RedirectsHome()
    {
        var client = factory.CreateNoRedirectClient();

        var response = await PostLoginAsync(client, "user1@zalo.local", Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact(DisplayName = "ACC-07 Sai mật khẩu thì ở lại trang đăng nhập và báo lỗi")]
    public async Task Login_WrongPassword_ShowsError()
    {
        var client = factory.CreateNoRedirectClient();

        var response = await PostLoginAsync(client, "user1@zalo.local", "SaiMatKhau!");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("không chính xác", html);
    }

    [Fact(DisplayName = "ACC-11 ReturnUrl trỏ ra ngoài thì không chuyển hướng ra ngoài")]
    public async Task Login_ExternalReturnUrl_StaysLocal()
    {
        var client = factory.CreateNoRedirectClient();

        var response = await PostLoginAsync(client, "user1@zalo.local", Password, "https://evil.example.com");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Theory(DisplayName = "ACC-13 Chưa đăng nhập thì bị chuyển về trang đăng nhập")]
    [InlineData("/")]
    [InlineData("/Profile")]
    [InlineData("/Admin/Admin")]
    public async Task Anonymous_ProtectedPage_RedirectsToLogin(string url)
    {
        var client = factory.CreateNoRedirectClient();

        var response = await client.GetAsync(url);

        Assert.True(IsRedirectToLogin(response), $"{url} trả về {(int)response.StatusCode} {response.Headers.Location}");
    }

    [Fact(DisplayName = "ACC-14 User thường vào trang Admin thì bị từ chối")]
    public async Task User_AdminPage_AccessDenied()
    {
        var client = await LoginAsync(factory, "user1@zalo.local");

        var response = await client.GetAsync("/Admin/Admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location?.OriginalString);
    }

    [Fact(DisplayName = "ACC-21 QuickLogin không được hoạt động ngoài môi trường Development", Skip = "Bug #2 — bỏ Skip khi đã sửa")]
    public async Task QuickLogin_OutsideDevelopment_IsRejected()
    {
        var client = factory.CreateNoRedirectClient();

        await client.GetAsync($"/Account/QuickLogin?userId={AdminId}");
        var adminPage = await client.GetAsync("/Admin/Admin");

        Assert.NotEqual(HttpStatusCode.OK, adminPage.StatusCode);
    }

    [Fact(DisplayName = "ACC-17 OTP đúng thì đặt lại mật khẩu thành công")]
    public async Task ResetPassword_CorrectOtp_Succeeds()
    {
        const string email = "user4@zalo.local";
        var otp = await RequestOtpAsync(email);

        var response = await SubmitResetAsync(email, otp);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString);
    }

    [Fact(DisplayName = "ACC-19 OTP bị vô hiệu sau 5 lần nhập sai", Skip = "Bug #3 — bỏ Skip khi đã sửa")]
    public async Task ResetPassword_AfterFiveWrongOtps_CorrectOtpIsRejected()
    {
        const string email = "user5@zalo.local";
        var otp = await RequestOtpAsync(email);
        var wrongOtp = otp == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            await SubmitResetAsync(email, wrongOtp);
        }
        var final = await SubmitResetAsync(email, otp);

        // Đặt lại thành công sẽ redirect về /Account/Login; mong đợi là bị từ chối (ở lại trang, 200).
        Assert.NotEqual(HttpStatusCode.Redirect, final.StatusCode);
    }

    private async Task<string> RequestOtpAsync(string email)
    {
        var client = factory.CreateClient();
        var token = await GetAntiForgeryTokenAsync(client, "/Account/ForgotPassword");
        var resetPage = await client.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["EmailOrPhone"] = email,
            ["__RequestVerificationToken"] = token
        }));
        var resetHtml = WebUtility.HtmlDecode(await resetPage.Content.ReadAsStringAsync());
        var otpMatch = System.Text.RegularExpressions.Regex.Match(resetHtml, @"là: (\d{6})");
        Assert.True(otpMatch.Success, "Không đọc được OTP trên trang Đặt lại mật khẩu");
        return otpMatch.Groups[1].Value;
    }

    private async Task<HttpResponseMessage> SubmitResetAsync(string email, string otp)
    {
        var client = factory.CreateNoRedirectClient();
        var token = await GetAntiForgeryTokenAsync(client, $"/Account/ResetPassword?email={email}");
        return await client.PostAsync("/Account/ResetPassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["OtpCode"] = otp,
            ["NewPassword"] = Password,
            ["ConfirmNewPassword"] = Password,
            ["__RequestVerificationToken"] = token
        }));
    }
}
