using System.Net;
using System.Net.Http.Json;
using LoyaltyCloud.API.Auth;
using LoyaltyCloud.API.Controllers;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category","CashierAuth")]
public sealed class CashierRefreshAuthTests
{
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Remember_me_issues_hash_only_refresh_with_one_hour_access(bool remember)
    {
        await using var env=await Env.CreateAsync(); var result=await env.Login(remember);
        Assert.InRange(result.ExpiresInSeconds,3599,3601);
        Assert.Equal(remember,result.RefreshToken is not null);
        await env.Read(async db=> {
            var rows=await db.CashierRefreshTokens.ToListAsync(); Assert.Equal(remember ? 1 : 0,rows.Count);
            if (remember) { var row=Assert.Single(rows); Assert.Equal(CashierRefreshTokenService.Hash(result.RefreshToken!),row.TokenHash);
                Assert.NotEqual(result.RefreshToken,row.TokenHash); Assert.Equal(env.Id,row.TenantId); Assert.Equal(env.UserId,row.TenantAdminUserId);
                Assert.Equal(env.Clock.UtcNow.AddDays(90),row.ExpiresAtUtc); }
        });
    }
    [Fact]
    public async Task Rotation_slides_ninety_days_and_rejects_replay_and_logout()
    {
        await using var env=await Env.CreateAsync(); var login=await env.Login(true); env.Clock.UtcNow=env.Clock.UtcNow.AddDays(20);
        using var response=await env.Refresh(login.RefreshToken!); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var rotated=(await response.Content.ReadFromJsonAsync<CashierAuthController.CashierLoginResponse>())!;
        Assert.NotEqual(login.RefreshToken,rotated.RefreshToken); Assert.NotEqual(login.AccessToken,rotated.AccessToken);
        Assert.Equal(new DateTimeOffset(env.Clock.UtcNow.AddDays(90)),rotated.RefreshTokenExpiresAtUtc);
        using var replay=await env.Refresh(login.RefreshToken!); Assert.Equal(HttpStatusCode.Unauthorized,replay.StatusCode);
        using var replayAgain=await env.Refresh(login.RefreshToken!); Assert.Equal(HttpStatusCode.Unauthorized,replayAgain.StatusCode);
        using var logout=await env.Client.PostAsJsonAsync("/api/auth/cashier/logout",new { refreshToken=rotated.RefreshToken }); Assert.Equal(HttpStatusCode.NoContent,logout.StatusCode);
        using var rejected=await env.Refresh(rotated.RefreshToken!); Assert.Equal(HttpStatusCode.Unauthorized,rejected.StatusCode);
    }
    [Fact]
    public async Task Rotation_in_same_second_still_issues_distinct_access_token()
    {
        await using var env=await Env.CreateAsync(); var login=await env.Login(true);
        using var response=await env.Refresh(login.RefreshToken!);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var rotated=(await response.Content.ReadFromJsonAsync<CashierAuthController.CashierLoginResponse>())!;
        Assert.NotEqual(login.AccessToken,rotated.AccessToken);
        Assert.Equal(login.ExpiresAtUtc,rotated.ExpiresAtUtc);
    }
    [Theory]
    [InlineData("expired")] [InlineData("revoked")] [InlineData("user")] [InlineData("tenant")] [InlineData("subscription")]
    public async Task Refresh_validates_expiry_revocation_and_live_account_state(string change)
    {
        await using var env=await Env.CreateAsync(); var login=await env.Login(true);
        if (change=="expired") env.Clock.UtcNow=env.Clock.UtcNow.AddDays(91);
        else await env.Read(async db=> {
            switch(change) {
                case "revoked": (await db.CashierRefreshTokens.SingleAsync()).Revoke(env.Clock.UtcNow); break;
                case "user": (await db.TenantAdminUsers.SingleAsync()).Deactivate(); break;
                case "tenant": (await db.Tenants.SingleAsync(t=>t.Id==env.Id)).Deactivate(env.Clock.UtcNow); break;
                case "subscription": (await db.TenantSubscriptions.SingleAsync(t=>t.TenantId==env.Id)).SuspendAdministratively(); break;
            }
            await db.SaveChangesAsync();
        });
        using var response=await env.Refresh(login.RefreshToken!); Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
    }
    [Fact]
    public async Task Refresh_builds_current_role_and_user_wide_revocation_is_available()
    {
        await using var env=await Env.CreateAsync(); var first=await env.Login(true); var second=await env.Login(true);
        await env.Read(async db=> { (await db.TenantAdminUsers.SingleAsync()).ChangeRole(TenantUserRole.Admin); await db.SaveChangesAsync(); });
        using var response=await env.Refresh(first.RefreshToken!); var body=(await response.Content.ReadFromJsonAsync<CashierAuthController.CashierLoginResponse>())!;
        Assert.Equal("Admin",body.Role);
        using(var scope=env.Factory.Services.CreateScope()) {
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(env.Id,"refresh-test");
            await scope.ServiceProvider.GetRequiredService<CashierRefreshTokenService>().RevokeAllForUserAsync(env.UserId);
        }
        using var a=await env.Refresh(body.RefreshToken!); using var b=await env.Refresh(second.RefreshToken!);
        Assert.Equal(HttpStatusCode.Unauthorized,a.StatusCode); Assert.Equal(HttpStatusCode.Unauthorized,b.StatusCode);
    }
    private sealed class Clock : IDateTimeProvider { public DateTime UtcNow {get;set;}=DateTime.SpecifyKind(new DateTime(2026,10,3,12,0,0),DateTimeKind.Utc); public DateTime Today=>UtcNow.Date; }
    private sealed class Env : IAsyncDisposable
    {
        private readonly CustomWebApplicationFactory original=new();
        public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Factory=null!;
        public HttpClient Client=null!; public readonly Clock Clock=new(); public Guid Id=Guid.NewGuid(); public Guid UserId=Guid.NewGuid();
        public static async Task<Env> CreateAsync() {
            var e=new Env(); e.Factory=e.original.WithWebHostBuilder(b=>b.ConfigureTestServices(s=>s.AddSingleton<IDateTimeProvider>(e.Clock))); e.Client=e.Factory.CreateClient();
            await e.Read(async db=> {
                await db.Database.EnsureCreatedAsync(); db.Add(new Tenant(e.Id,"refresh-test","Refresh","America/Tijuana",e.Clock.UtcNow));
                db.Add(new TenantSubscription(e.Id,TenantSubscriptionStatus.Active,"test",paidThroughUtc:e.Clock.UtcNow.AddDays(365)));
                using var scope=e.Factory.Services.CreateScope(); var hash=scope.ServiceProvider.GetRequiredService<IPasswordHashingService>().HashPassword("Refresh123!");
                db.Add(new TenantAdminUser(e.UserId,e.Id,"operator",hash,e.Clock.UtcNow,role:TenantUserRole.Cashier)); await db.SaveChangesAsync();
            }); return e;
        }
        public async Task Read(Func<AppDbContext,Task> read) { using var scope=Factory.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(Id,"refresh-test"); await read(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
        public async Task<CashierAuthController.CashierLoginResponse> Login(bool remember) { using var r=await Client.PostAsJsonAsync("/api/auth/cashier/login",new {tenantSlug="refresh-test",username="operator",password="Refresh123!",rememberMe=remember}); Assert.Equal(HttpStatusCode.OK,r.StatusCode); return (await r.Content.ReadFromJsonAsync<CashierAuthController.CashierLoginResponse>())!; }
        public Task<HttpResponseMessage> Refresh(string token)=>Client.PostAsJsonAsync("/api/auth/cashier/refresh",new { refreshToken=token });
        public async ValueTask DisposeAsync() { Client.Dispose(); await Factory.DisposeAsync(); await original.DisposeAsync(); }
    }
}
