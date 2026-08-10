using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

internal sealed class GoogleWalletService : IGoogleWalletService
{
    private readonly GoogleWalletOptions _options;
    private readonly IMemberWalletDataService _memberWalletData;
    private readonly IMemberDigitalWalletRepository _wallets;
    private readonly IUnitOfWork _uow;
    private readonly IGoogleWalletClient _client;
    private readonly IGoogleWalletCredentialsProvider _credentialsProvider;
    private readonly GoogleWalletIdGenerator _idGenerator;
    private readonly GoogleWalletObjectMapper _mapper;
    private readonly GoogleWalletJwtFactory _jwtFactory;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<GoogleWalletService> _logger;

    public GoogleWalletService(
        IOptions<GoogleWalletOptions> options,
        IMemberWalletDataService memberWalletData,
        IMemberDigitalWalletRepository wallets,
        IUnitOfWork uow,
        IGoogleWalletClient client,
        IGoogleWalletCredentialsProvider credentialsProvider,
        GoogleWalletIdGenerator idGenerator,
        GoogleWalletObjectMapper mapper,
        GoogleWalletJwtFactory jwtFactory,
        IDateTimeProvider dt,
        ILogger<GoogleWalletService> logger)
    {
        _options = options.Value;
        _memberWalletData = memberWalletData;
        _wallets = wallets;
        _uow = uow;
        _client = client;
        _credentialsProvider = credentialsProvider;
        _idGenerator = idGenerator;
        _mapper = mapper;
        _jwtFactory = jwtFactory;
        _dt = dt;
        _logger = logger;
    }

    public async Task<Result<GoogleWalletSaveLinkResponse>> GetOrCreateSaveLinkAsync(
        string serialNumber,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Result.Fail<GoogleWalletSaveLinkResponse>("Google Wallet esta deshabilitado.");

        var dataResult = await _memberWalletData.GetBySerialNumberAsync(serialNumber, ct);
        if (dataResult.IsFailure)
            return Result.Fail<GoogleWalletSaveLinkResponse>(dataResult.Errors);

        var data = dataResult.Value;
        if (!data.IsActive)
            return Result.Fail<GoogleWalletSaveLinkResponse>("La clienta o tarjeta esta inactiva.");

        try
        {
            var sync = await SynchronizeCoreAsync(data, createIfMissing: true, ct);
            var credentials = await _credentialsProvider.GetAsync(ct);
            var saveUrl = _jwtFactory.CreateSaveUrl(credentials, sync.ObjectData, _dt.UtcNow);

            sync.Wallet.RecordSaveLinkCreated(_dt.UtcNow);
            _wallets.Update(sync.Wallet);
            await _uow.SaveChangesAsync(ct);

            return Result.Ok(new GoogleWalletSaveLinkResponse(
                SaveUrl: saveUrl,
                ObjectId: sync.Wallet.ExternalObjectId,
                ClassId: sync.Wallet.ExternalClassId,
                LastSynchronizedAt: sync.Wallet.LastSynchronizedAt));
        }
        catch (Exception ex)
        {
            var googleApiDetails = TryExtractGoogleApiExceptionDetails(ex);
            if (googleApiDetails is not null)
            {
                _logger.LogError(
                    ex,
                    "Google Wallet save link generation failed for serial {Serial}. Exception={Exception}. GoogleApiStatus={GoogleApiStatus}. GoogleApiErrorCode={GoogleApiErrorCode}. GoogleApiErrorMessage={GoogleApiErrorMessage}. GoogleApiErrors={GoogleApiErrors}. GoogleApiResponseBody={GoogleApiResponseBody}",
                    serialNumber,
                    ex.ToString(),
                    googleApiDetails.Status,
                    googleApiDetails.ErrorCode,
                    googleApiDetails.ErrorMessage,
                    googleApiDetails.Errors,
                    googleApiDetails.ResponseBody);
            }
            else
            {
                _logger.LogError(
                    ex,
                    "Google Wallet save link generation failed for serial {Serial}. Exception={Exception}",
                    serialNumber,
                    ex.ToString());
            }

            return Result.Fail<GoogleWalletSaveLinkResponse>(
                "No se pudo generar el enlace de Google Wallet. Revise configuracion, credenciales y logs.");
        }
    }

    public async Task SynchronizeBySerialNumberIfExistsAsync(string serialNumber, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return;

        var dataResult = await _memberWalletData.GetBySerialNumberAsync(serialNumber, ct);
        if (dataResult.IsFailure)
        {
            _logger.LogWarning("Google Wallet sync skipped for serial {Serial}: {Error}", serialNumber, dataResult.Error);
            return;
        }

        try
        {
            await SynchronizeCoreAsync(dataResult.Value, createIfMissing: false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Wallet sync failed for serial {Serial}", serialNumber);
        }
    }

    private async Task<GoogleWalletSyncState> SynchronizeCoreAsync(
        MemberWalletData data,
        bool createIfMissing,
        CancellationToken ct)
    {
        var now = _dt.UtcNow;
        var classId = _idGenerator.BuildClassId(_options);
        var objectId = _idGenerator.BuildObjectId(_options, data.TenantId, data.SerialNumber);

        var wallet = await _wallets.GetByLoyaltyCardAndProviderAsync(
            data.LoyaltyCardId,
            DigitalWalletProvider.Google,
            ct);

        if (wallet is null)
        {
            if (!createIfMissing)
                return GoogleWalletSyncState.NotLinked(classId, objectId, data);

            wallet = new MemberDigitalWallet(
                Guid.NewGuid(),
                data.TenantId,
                data.CustomerId,
                data.LoyaltyCardId,
                DigitalWalletProvider.Google,
                classId,
                objectId,
                now);
            await _wallets.AddAsync(wallet, ct);
        }
        else if (!string.Equals(wallet.ExternalClassId, classId, StringComparison.Ordinal) ||
                 !string.Equals(wallet.ExternalObjectId, objectId, StringComparison.Ordinal))
        {
            wallet.UpdateExternalIds(classId, objectId, now);
            _wallets.Update(wallet);
        }

        var classData = _mapper.ToClassData(classId, _options);
        var objectData = _mapper.ToObjectData(objectId, classId, data);

        try
        {
            await _client.EnsureLoyaltyClassAsync(classData, ct);
            await _client.CreateOrUpdateObjectAsync(objectData, ct);
            wallet.MarkSynchronized(now);
        }
        catch (Exception ex)
        {
            wallet.MarkSynchronizationFailed(SafeError(ex), now);
            _wallets.Update(wallet);
            await _uow.SaveChangesAsync(ct);
            throw;
        }

        _wallets.Update(wallet);
        await _uow.SaveChangesAsync(ct);
        return new GoogleWalletSyncState(wallet, objectData);
    }

    private static string SafeError(Exception ex)
    {
        var message = ex.Message;
        return message.Length <= 500 ? message : message[..500];
    }

    private static GoogleApiExceptionDetails? TryExtractGoogleApiExceptionDetails(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.Equals(current.GetType().FullName, "Google.GoogleApiException", StringComparison.Ordinal))
                continue;

            var error = current.GetType().GetProperty("Error")?.GetValue(current);
            var status = current.GetType().GetProperty("HttpStatusCode")?.GetValue(current)?.ToString()
                ?? current.GetType().GetProperty("StatusCode")?.GetValue(current)?.ToString();
            var responseBody = current.GetType().GetProperty("ResponseBody")?.GetValue(current)?.ToString();
            var errorCode = error?.GetType().GetProperty("Code")?.GetValue(error)?.ToString();
            var errorMessage = error?.GetType().GetProperty("Message")?.GetValue(error)?.ToString();
            var errors = error?.GetType().GetProperty("Errors")?.GetValue(error);

            return new GoogleApiExceptionDetails(
                status,
                errorCode,
                errorMessage,
                errors is null ? null : JsonSerializer.Serialize(errors),
                responseBody);
        }

        return null;
    }

    private sealed record GoogleApiExceptionDetails(
        string? Status,
        string? ErrorCode,
        string? ErrorMessage,
        string? Errors,
        string? ResponseBody);

    private sealed record GoogleWalletSyncState(MemberDigitalWallet Wallet, GoogleWalletObjectData ObjectData)
    {
        public static GoogleWalletSyncState NotLinked(
            string classId,
            string objectId,
            MemberWalletData data)
        {
            var wallet = new MemberDigitalWallet(
                Guid.NewGuid(),
                data.TenantId,
                data.CustomerId,
                data.LoyaltyCardId,
                DigitalWalletProvider.Google,
                classId,
                objectId,
                data.LastActivityAt);
            return new GoogleWalletSyncState(
                wallet,
                new GoogleWalletObjectData(
                    objectId,
                    classId,
                    data.FullName,
                    data.SerialNumber,
                    data.CurrentPoints,
                    data.Level,
                    data.BarcodeValue,
                    data.IsActive,
                    data.LastActivityAt));
        }
    }
}

