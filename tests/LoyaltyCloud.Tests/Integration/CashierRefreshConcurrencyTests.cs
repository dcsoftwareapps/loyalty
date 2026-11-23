using LoyaltyCloud.API.Auth;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Env=LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category","CashierAuth")]
public sealed class CashierRefreshConcurrencyTests
{
    [Fact]
    public async Task Simultaneous_rotation_has_one_atomic_winner_and_does_not_revoke_the_successor()
    {
        var barrier=new Barrier();
        await using var env=await Env.CreateAsync(configure:s=> {
            s.Configure<CashierAuthOptions>(_=>{}); s.AddScoped<CashierRefreshTokenService>();
            s.ConfigureDbContext<AppDbContext>(options=>options.AddInterceptors(barrier));
        });
        var provision=await env.ProvisionAsync("refresh-race","Refresh","owner","Owner123!"); Assert.True(provision.IsSuccess,provision.Error);
        var id=provision.Value.TenantId;
        var issued=await env.InTenantScopeAsync(id,"refresh-race",async services=> {
            var db=services.GetRequiredService<AppDbContext>();
            var result=services.GetRequiredService<CashierRefreshTokenService>().Issue(await db.Tenants.SingleAsync(t=>t.Id==id),await db.TenantAdminUsers.SingleAsync());
            await db.SaveChangesAsync(); return result;
        });
        barrier.Armed=true;
        var first=env.InTenantScopeAsync(id,"refresh-race",s=>s.GetRequiredService<CashierRefreshTokenService>().RotateAsync(issued.Token,CancellationToken.None));
        var second=env.InTenantScopeAsync(id,"refresh-race",s=>s.GetRequiredService<CashierRefreshTokenService>().RotateAsync(issued.Token,CancellationToken.None));
        var results=await Task.WhenAll(first,second); barrier.Armed=false;
        var winner=Assert.Single(results,r=>r.HasValue)!.Value;
        await env.TenantReadAsync(id,"refresh-race",async db=> { Assert.Equal(2,await db.CashierRefreshTokens.CountAsync()); Assert.Equal(1,await db.CashierRefreshTokens.CountAsync(t=>t.RevokedAtUtc==null)); return true; });
        var replay=await env.InTenantScopeAsync(id,"refresh-race",s=>s.GetRequiredService<CashierRefreshTokenService>().RotateAsync(issued.Token,CancellationToken.None)); Assert.Null(replay);
        var legitimate=await env.InTenantScopeAsync(id,"refresh-race",s=>s.GetRequiredService<CashierRefreshTokenService>().RotateAsync(winner.Token,CancellationToken.None)); Assert.NotNull(legitimate);
    }
    private sealed class Barrier : SaveChangesInterceptor
    {
        public bool Armed; private int arrivals; private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if (Armed && data.Context!.ChangeTracker.Entries<CashierRefreshToken>().Any(e=>e.State==EntityState.Modified))
            { if (Interlocked.Increment(ref arrivals)==2) ready.TrySetResult(); await ready.Task.WaitAsync(TimeSpan.FromSeconds(20),ct); }
            return result;
        }
    }
}
