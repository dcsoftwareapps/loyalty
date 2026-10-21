extern alias AdminApp;

using System.Net;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using AdminApp::LoyaltyCloud.Admin.Auth;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "CustomerDetailProgram")]
public sealed class CustomerDetailProgramTests
{
    [Theory]
    [InlineData(LoyaltyProgramType.Points, false)]
    [InlineData(LoyaltyProgramType.Points, true)]
    [InlineData(LoyaltyProgramType.Stamps, false)]
    [InlineData(LoyaltyProgramType.Stamps, true)]
    public async Task Direct_detail_by_id_or_serial_preserves_only_relevant_program_information(LoyaltyProgramType program, bool bySerial)
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory();
        await factory.EnsureDatabaseCreatedAsync();
        var (cookie, customerId) = await Seed(factory, program);
        using var errors = new ErrorLog(); factory.Services.GetRequiredService<ILoggerFactory>().AddProvider(errors);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var response = await client.GetAsync(bySerial ? "/customers/CD-DETAIL" : $"/customers/{customerId}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, string.Join("\n", errors.Exceptions));
        Assert.Equal($"/customers/{customerId}", response.RequestMessage!.RequestUri!.AbsolutePath);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        foreach (var text in new[] { "Customer Detail Test", "6465550101", "Información general", "Guardar cumpleaños", "CD-DETAIL", "Eliminar cliente" })
            Assert.Contains(text, html);
        foreach (var text in new[] { "Sumar puntos", "Estadísticas rápidas", "Puntos actuales", "Nivel actual", "Fecha de nivel", "Auditoría de saldo", "Próxima expiración", "Rolling Progress", "Historial de puntos", "Lotes de puntos", "Consumo FIFO", "Historial de canjes", "Bronce" })
        {
            if (program == LoyaltyProgramType.Points) Assert.Contains(text, html);
            else Assert.DoesNotContain(text, html);
        }
    }

    [Theory]
    [InlineData(LoyaltyProgramType.Points, false)]
    [InlineData(LoyaltyProgramType.Points, true)]
    [InlineData(LoyaltyProgramType.Stamps, false)]
    [InlineData(LoyaltyProgramType.Stamps, true)]
    public async Task Foreign_customer_is_not_exposed_by_direct_id_or_serial(LoyaltyProgramType program, bool bySerial)
    {
        await using var factory = new AdminRoutingTests.AdminWebApplicationFactory();
        await factory.EnsureDatabaseCreatedAsync();
        var (cookie, _) = await Seed(factory, program);
        Guid foreignCustomer;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var tenant = Guid.NewGuid(); foreignCustomer = Guid.NewGuid();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenant, "foreign-detail");
            db.Add(new Tenant(tenant, "foreign-detail", "Foreign", "America/Tijuana", DateTime.UtcNow, programType: program));
            db.Add(new Customer(foreignCustomer, tenant, "Foreign Private Name", "foreign@local.test", Customer.BirthdayNotCaptured, DateTime.UtcNow, "6465550199"));
            db.Add(new LoyaltyCard(Guid.NewGuid(), tenant, foreignCustomer, "CD-FOREIGN", DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        using var response = await client.GetAsync(bySerial ? "/customers/CD-FOREIGN" : $"/customers/{foreignCustomer}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("No se", html); Assert.DoesNotContain("Foreign Private Name", html); Assert.DoesNotContain("6465550199", html);
        Assert.DoesNotContain("Guardar cumpleaños", html);
    }

    private sealed class ErrorLog : ILoggerProvider, ILogger
    {
        public readonly System.Collections.Concurrent.ConcurrentBag<string> Exceptions = new();
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (exception is not null) Exceptions.Add(exception.ToString()); }
        public void Dispose() { }
    }

    private static async Task<(string Cookie, Guid CustomerId)> Seed(AdminRoutingTests.AdminWebApplicationFactory factory, LoyaltyProgramType program)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = Guid.NewGuid(); var customerId = Guid.NewGuid(); var now = DateTime.UtcNow;
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenant, "detail-test");
        db.Add(new Tenant(tenant, "detail-test", "Detail Test", "America/Tijuana", now, programType: program));
        db.Add(new TenantBranding(tenant, "#111111", "#999999"));
        db.Add(new TenantSubscription(tenant, TenantSubscriptionStatus.Active, "test", paidThroughUtc: now.AddDays(30)));
        var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
        db.Add(new TenantAdminUser(Guid.NewGuid(), tenant, "owner", hashing.HashPassword("Detail123!"), now));
        db.Add(new Customer(customerId, tenant, "Customer Detail Test", "detail@local.test", new DateTime(1990, 1, 15), now, "6465550101"));
        db.Add(new LoyaltyCard(Guid.NewGuid(), tenant, customerId, "CD-DETAIL", now));
        if (program == LoyaltyProgramType.Points)
        {
            db.Add(new TenantLoyaltyLevel(Guid.NewGuid(), tenant, "Bronce", 0, 1, now));
            db.Add(new TenantLoyaltyLevel(Guid.NewGuid(), tenant, "Plata", 100, 2, now));
            db.Add(new TenantLoyaltyLevel(Guid.NewGuid(), tenant, "Oro", 200, 3, now));
        }
        await db.SaveChangesAsync();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider }; context.Request.Scheme = "https";
        Assert.Equal(AdminLoginResult.Success, await scope.ServiceProvider.GetRequiredService<AdminAuthService>().TrySignInAsync(context, "detail-test", "owner", "Detail123!"));
        var cookie = context.Response.Headers.SetCookie.First(x => x!.StartsWith("loyaltycloud.admin.auth=", StringComparison.Ordinal))!.Split(';')[0];
        return (cookie, customerId);
    }
}
