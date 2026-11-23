using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Application.SelfServiceSignup;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class SelfServiceSignupTests
{
    private static readonly DateTime FixedNow =
        new(2026, 1, 31, 10, 15, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public async Task Happy_path_creates_complete_trial_without_billing_artifacts()
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);
        var command = ValidCommand("happy-path", "attempt-happy", " Owner@Example.COM ");

        var result = await env.SignupAsync(command);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("happy-path", result.Value.TenantSlug);
        Assert.Equal(FixedNow.AddMonths(1), result.Value.TrialEndsAtUtc);

        var state = await env.ReadAsync(async db => new
        {
            Tenant = await db.Tenants.SingleAsync(t => t.Id == result.Value.TenantId),
            Branding = await db.TenantBrandings.SingleAsync(t => t.TenantId == result.Value.TenantId),
            Admin = await db.TenantAdminUsers.IgnoreQueryFilters()
                .SingleAsync(user => user.Id == result.Value.AdminUserId),
            Subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == result.Value.TenantId),
            Configs = await db.ProgramConfigs.IgnoreQueryFilters()
                .CountAsync(config => config.TenantId == result.Value.TenantId),
            Levels = await db.TenantLoyaltyLevels.IgnoreQueryFilters()
                .CountAsync(level => level.TenantId == result.Value.TenantId),
            Attempts = await db.SelfServiceSignupAttempts.CountAsync(a => a.AttemptId == "attempt-happy"),
            BillingOrders = await db.BillingOrders.IgnoreQueryFilters()
                .CountAsync(order => order.TenantId == result.Value.TenantId),
            Payments = await db.PaymentTransactions.IgnoreQueryFilters()
                .CountAsync(payment => payment.TenantId == result.Value.TenantId),
            BillingProfile = await db.TenantBillingProfiles.IgnoreQueryFilters()
                .SingleOrDefaultAsync(profile => profile.TenantId == result.Value.TenantId)
        });

        Assert.Equal("Happy Path", state.Tenant.DisplayName);
        Assert.NotNull(state.Branding);
        Assert.Equal("Owner@Example.COM", state.Admin.Email);
        Assert.Equal("OWNER@EXAMPLE.COM", state.Admin.NormalizedEmail);
        Assert.Equal(TenantUserRole.Admin, state.Admin.Role);
        Assert.True(state.Admin.IsActive);
        Assert.NotEqual(command.Password, state.Admin.PasswordHash);
        Assert.DoesNotContain(command.Password, state.Admin.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(TenantSubscriptionStatus.Trial, state.Subscription.Status);
        Assert.Equal(FixedNow, state.Subscription.CurrentPeriodStart);
        Assert.Equal(FixedNow.AddMonths(1), state.Subscription.CurrentPeriodEnd);
        Assert.Equal(TenantProvisioningDefaults.ProgramConfigRows.Count, state.Configs);
        Assert.Equal(TenantProvisioningDefaults.LoyaltyLevels.Count, state.Levels);
        Assert.Equal(1, state.Attempts);
        Assert.Equal(0, state.BillingOrders);
        Assert.Equal(0, state.Payments);
        Assert.Null(state.BillingProfile);
    }

    [Theory]
    [Trait("Category", "SelfServiceSignup")]
    [InlineData("bad-email", "Password123!", "Business", "valid-slug", "America/Tijuana", "attempt-1")]
    [InlineData("owner@example.com", "short", "Business", "valid-slug", "America/Tijuana", "attempt-2")]
    [InlineData("owner@example.com", "Password123!", " ", "valid-slug", "America/Tijuana", "attempt-3")]
    [InlineData("owner@example.com", "Password123!", "Business", "Bad Slug", "America/Tijuana", "attempt-4")]
    [InlineData("owner@example.com", "Password123!", "Business", "valid-slug", "Not/AZone", "attempt-5")]
    public async Task Invalid_public_input_is_rejected(
        string email,
        string password,
        string businessName,
        string slug,
        string timeZone,
        string attemptId)
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);

        var result = await env.SignupAsync(new SelfServiceSignupCommand(
            email, password, businessName, slug, timeZone, attemptId));

        Assert.True(result.IsFailure);
        Assert.Equal(0, await env.ReadAsync(db => db.SelfServiceSignupAttempts.CountAsync()));
    }

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public async Task Same_email_is_allowed_for_different_tenants()
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);

        var first = await env.SignupAsync(ValidCommand("email-a", "attempt-email-a", "owner@example.com"));
        var second = await env.SignupAsync(ValidCommand("email-b", "attempt-email-b", " OWNER@EXAMPLE.COM "));

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(2, await env.ReadAsync(db => db.TenantAdminUsers.IgnoreQueryFilters()
            .CountAsync(user => user.NormalizedEmail == "OWNER@EXAMPLE.COM")));
    }

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public async Task Completed_same_attempt_and_request_replays_original_result()
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);
        var command = ValidCommand("replay", "attempt-replay");

        var first = await env.SignupAsync(command);
        var second = await env.SignupAsync(command with
        {
            Email = " OWNER@EXAMPLE.COM ",
            BusinessDisplayName = " Replay ",
            Slug = " replay "
        });

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(1, await env.ReadAsync(db => db.Tenants.CountAsync(t => t.Slug == "replay")));
        Assert.Equal(1, await env.ReadAsync(db => db.TenantAdminUsers.IgnoreQueryFilters()
            .CountAsync(user => user.TenantId == first.Value.TenantId)));
        Assert.Equal(1, await env.ReadAsync(db => db.TenantSubscriptions
            .CountAsync(subscription => subscription.TenantId == first.Value.TenantId)));
    }

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public async Task Same_attempt_with_different_request_is_rejected()
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);
        var first = await env.SignupAsync(ValidCommand("original", "attempt-conflict"));
        var conflicting = await env.SignupAsync(ValidCommand("different", "attempt-conflict"));

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(conflicting.IsFailure);
        Assert.Contains("solicitud diferente", conflicting.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await env.ReadAsync(db => db.SelfServiceSignupAttempts
            .CountAsync(attempt => attempt.AttemptId == "attempt-conflict")));
    }

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public async Task Completed_attempt_discards_password_verifier_and_replays_from_non_sensitive_fingerprint()
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);
        var command = ValidCommand("password-conflict", "attempt-password-conflict");
        var first = await env.SignupAsync(command);
        var conflicting = await env.SignupAsync(command with { Password = "Different123!" });

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(conflicting.IsSuccess, conflicting.Error);
        Assert.Equal(first.Value, conflicting.Value);
        var verificationHash = await env.ReadAsync(db => db.SelfServiceSignupAttempts
            .Where(attempt => attempt.AttemptId == "attempt-password-conflict")
            .Select(attempt => attempt.PasswordVerificationHash)
            .SingleAsync());
        Assert.Null(verificationHash);
    }

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public async Task Parallel_same_attempt_creates_exactly_one_tenant()
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);
        var command = ValidCommand("parallel-key", "attempt-parallel-key");

        var results = await Task.WhenAll(env.SignupAsync(command), env.SignupAsync(command));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Error));
        Assert.Equal(results[0].Value, results[1].Value);
        Assert.Equal(1, await env.ReadAsync(db => db.Tenants.CountAsync(t => t.Slug == "parallel-key")));
        Assert.Equal(1, await env.ReadAsync(db => db.SelfServiceSignupAttempts
            .CountAsync(attempt => attempt.AttemptId == "attempt-parallel-key")));
    }

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public async Task Duplicate_slug_returns_controlled_failure_and_keeps_one_tenant()
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);
        var first = await env.SignupAsync(ValidCommand("duplicate-signup", "attempt-duplicate-a"));
        var second = await env.SignupAsync(ValidCommand("duplicate-signup", "attempt-duplicate-b"));

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsFailure);
        Assert.Equal("El identificador del negocio ya está en uso.", second.Error);
        Assert.Equal(1, await env.ReadAsync(db => db.Tenants.CountAsync(t => t.Slug == "duplicate-signup")));
        Assert.Equal(1, await env.ReadAsync(db => db.SelfServiceSignupAttempts.CountAsync()));
    }

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public async Task Parallel_duplicate_slug_creates_only_one_tenant()
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);

        var results = await Task.WhenAll(
            env.SignupAsync(ValidCommand("parallel-slug", "attempt-parallel-a")),
            env.SignupAsync(ValidCommand("parallel-slug", "attempt-parallel-b")));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.Equal(1, results.Count(result => result.IsFailure));
        Assert.Equal(1, await env.ReadAsync(db => db.Tenants.CountAsync(t => t.Slug == "parallel-slug")));
    }

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public async Task Completion_failure_rolls_back_everything_and_retry_can_succeed()
    {
        await using var env = await SignupTestEnvironment.CreateAsync(FixedNow);
        await env.ExecuteSqlAsync(
            """
            CREATE TRIGGER TR_SelfServiceSignupAttempts_TestFailure
            ON SelfServiceSignupAttempts
            AFTER UPDATE
            AS
            BEGIN
                THROW 51000, 'Controlled signup completion failure.', 1;
            END
            """);

        var command = ValidCommand("rollback-signup", "attempt-rollback");
        await Assert.ThrowsAnyAsync<Exception>(() => env.SignupAsync(command));

        Assert.Equal(0, await env.ReadAsync(db => db.Tenants.CountAsync(t => t.Slug == "rollback-signup")));
        Assert.Equal(0, await env.ReadAsync(db => db.TenantAdminUsers.IgnoreQueryFilters()
            .CountAsync(user => user.Email == "owner@example.com")));
        Assert.Equal(0, await env.ReadAsync(db => db.TenantSubscriptions.CountAsync()));
        Assert.Equal(0, await env.ReadAsync(db => db.SelfServiceSignupAttempts
            .CountAsync(attempt => attempt.AttemptId == "attempt-rollback")));

        await env.ExecuteSqlAsync("DROP TRIGGER TR_SelfServiceSignupAttempts_TestFailure");
        var retry = await env.SignupAsync(command);

        Assert.True(retry.IsSuccess, retry.Error);
        Assert.Equal(1, await env.ReadAsync(db => db.Tenants.CountAsync(t => t.Slug == "rollback-signup")));
    }

    [Fact]
    [Trait("Category", "SelfServiceSignup")]
    public void Public_contract_exposes_only_allowed_input()
    {
        var actual = typeof(SelfServiceSignupCommand).GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();
        var allowed = new[]
        {
            "BusinessDisplayName", "Email", "Password", "SignupAttemptId", "Slug", "TimeZoneId"
        }.OrderBy(name => name).ToArray();

        Assert.Equal(allowed, actual);
    }

    private static SelfServiceSignupCommand ValidCommand(
        string slug,
        string attemptId,
        string email = "owner@example.com") =>
        new(email, "Password123!", ToDisplayName(slug), slug, "America/Tijuana", attemptId);

    private static string ToDisplayName(string slug) =>
        string.Join(' ', slug.Split('-').Select(part =>
            char.ToUpperInvariant(part[0]) + part[1..]));

    private sealed class SignupTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private SignupTestEnvironment(ServiceProvider services) => _services = services;

        public static async Task<SignupTestEnvironment> CreateAsync(DateTime nowUtc)
        {
            var dbName = "LoyaltyCloud_Signup_" + Guid.NewGuid().ToString("N");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;",
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                    ["Apple:PassTypeIdentifier"] = "pass.com.kbeautymx.loyalty",
                    ["Apple:TeamIdentifier"] = "TESTTEAM01",
                    ["Apple:WebServiceURL"] = "https://test.local",
                    ["Apple:OrganizationName"] = "LoyaltyCloud Test",
                    ["Wallet:UseRealPassSigning"] = "false",
                    ["Wallet:UseRealApns"] = "false",
                    ["Provisioning:TrialDays"] = "14"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(nowUtc));

            var provider = services.BuildServiceProvider(validateScopes: true);
            var environment = new SignupTestEnvironment(provider);
            await environment.InitializeAsync();
            return environment;
        }

        public async Task<Result<SelfServiceSignupResult>> SignupAsync(SelfServiceSignupCommand command)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
        }

        public async Task<T> ReadAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async Task ExecuteSqlAsync(string sql)
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync(sql);
        }

        private async Task InitializeAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            }
            finally
            {
                await _services.DisposeAsync();
            }
        }
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
        public DateTime Today => UtcNow.Date;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoyaltyCloud.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
