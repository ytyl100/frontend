using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// 1. 添加 Razor Pages 服务
builder.Services.AddRazorPages();

// 2. 配置 OIDC 认证
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie() // 本地凭证存储在安全 Cookie 中
.AddGoogle(googleOptions =>
{
    googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
    googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    
    // 【核心黑魔法】：当 Google 验证通过后，注入我们自定义的角色
    googleOptions.Events.OnTokenValidated = context =>
    {
        // 从 Google 返回的 Claim 中找到用户的邮箱
        var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;

        if (!string.IsNullOrEmpty(email))
        {
            // 从 appsettings.json 中读取这个邮箱对应的角色 (实际开发中这里可以改为查你的 MySQL/PostgreSQL 数据库)
            var roleMappings = builder.Configuration.GetSection("RoleMappings").Get<Dictionary<string, string>>();
            
            if (roleMappings != null && roleMappings.TryGetValue(email, out var roleName))
            {
                // 创建一个标准的 .NET 角色 Claim
                var roleClaim = new Claim(ClaimTypes.Role, roleName);
                
                // 把角色章盖到用户的身份证（Identity）上
                var appIdentity = context.Principal?.Identity as ClaimsIdentity;
                appIdentity?.AddClaim(roleClaim);
            }
        }
        
        return Task.CompletedTask;
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// 确保 Cloud Run 的 HTTPS 转发正常（解决容器内 HTTP 导致的回调错误）
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 必须严格遵守此顺序
app.UseAuthentication(); 
app.UseAuthorization();

app.MapRazorPages();

app.Run();