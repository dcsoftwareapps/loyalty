using System.Text.Json;

namespace LoyaltyCloud.Common.Services;

public sealed record CashierStampReward(Guid Id, string Name, string? Description, int RequiredStamps, DateTime EarnedAt);
public sealed record CashierStampState(string SerialNumber, string CustomerName, Guid CycleId, int CycleSequence,
    int CurrentStamps, int MaxStamps, bool CycleCompleted, IReadOnlyList<CashierStampReward> AvailableRewards,
    bool CanReverseLastEarn = false);
public sealed record CashierStampResult(CashierStampState State, IReadOnlyList<CashierStampReward> UnlockedRewards);
public sealed record StampCall<T>(T? Value, string? Error = null, bool Uncertain = false) where T : class;
public sealed record PendingStampOperation(string Operation, string SerialNumber, string IdempotencyKey, Guid? RewardId);
public sealed record StampScanResult(string? Value, string? Error = null);

public interface IStampCashierApi
{
    Task<StampCall<CashierStampState>> ReadAsync(string serial);
    Task<StampCall<CashierStampResult>> ExecuteAsync(PendingStampOperation operation);
}

public interface IStampPendingStore
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task RemoveAsync(string key);
}

/// <summary>One unresolved operation per authenticated tenant/operator/environment.</summary>
public sealed class StampCashierFlow(IStampCashierApi api, IStampPendingStore store, string storageKey)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private int gate;
    public bool Busy => Volatile.Read(ref gate) != 0;
    public PendingStampOperation? Pending { get; private set; }
    public CashierStampState? State { get; private set; }
    public IReadOnlyList<CashierStampReward> Unlocked { get; private set; } = [];
    public string? Message { get; private set; }
    public string? Error { get; private set; }
    public bool ScanConsumed { get; private set; }

    public async Task RestoreAsync()
    {
        var raw = await store.GetAsync(storageKey);
        if (raw is not null)
        {
            Pending = JsonSerializer.Deserialize<PendingStampOperation>(raw, JsonOptions)
                ?? throw new InvalidOperationException("No se pudo recuperar la operación pendiente.");
            ScanConsumed = true;
            Error = "Hay una operación pendiente. Reintenta para confirmarla.";
        }
    }

    public async Task LookupAsync(string serial)
    {
        if (Pending is not null || Interlocked.CompareExchange(ref gate, 1, 0) != 0) return;
        try
        {
            ClearMessages();
            ScanConsumed = false;
            var result = await api.ReadAsync(serial);
            State = result.Value;
            Error = result.Error;
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    public async Task AcceptScanAsync(string? serial)
    {
        if (ScanConsumed || Pending is not null || Interlocked.CompareExchange(ref gate, 1, 0) != 0) return;
        ScanConsumed = true;
        try
        {
            ClearMessages();
            if (string.IsNullOrWhiteSpace(serial)) { Error = "El QR no contiene un ID de cliente válido."; return; }
            var read = await api.ReadAsync(serial);
            State = read.Value;
            if (State is null) { Error = read.Error; return; }
            await BeginCoreAsync("Earn", State.SerialNumber, null);
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    public async Task BeginAsync(string operation, Guid? rewardId = null)
    {
        if (State is null || Pending is not null || (operation == "Earn" && ScanConsumed)
            || Interlocked.CompareExchange(ref gate, 1, 0) != 0) return;
        try
        {
            if (operation == "Earn") ScanConsumed = true;
            await BeginCoreAsync(operation, State.SerialNumber, rewardId);
        }
        finally { Volatile.Write(ref gate, 0); }
    }

    private async Task BeginCoreAsync(string operation, string serial, Guid? rewardId)
    {
        Pending = new(operation, serial, Guid.NewGuid().ToString("N"), rewardId);
        // Persist before sending: even a lost HTTP response must retain the same key.
        await ExecuteCoreAsync();
    }

    public async Task RetryAsync()
    {
        if (Pending is null || Interlocked.CompareExchange(ref gate, 1, 0) != 0) return;
        try { await ExecuteCoreAsync(); }
        finally { Volatile.Write(ref gate, 0); }
    }

    private async Task ExecuteCoreAsync()
    {
        ClearMessages();
        var operation = Pending!;
        try { await store.SetAsync(storageKey, JsonSerializer.Serialize(operation, JsonOptions)); }
        catch (Exception)
        {
            Error = "No se pudo guardar la solicitud en este dispositivo. No se envió la operación; reintenta.";
            return;
        }
        var result = await api.ExecuteAsync(operation);
        if (result.Uncertain)
        {
            Error = operation.Operation == "Earn" ? "No pudimos confirmar el sello." : "No pudimos confirmar la operación.";
            return;
        }
        if (result.Value is not null)
        {
            State = result.Value.State;
            Unlocked = result.Value.UnlockedRewards;
            Message = operation.Operation switch
            {
                "Earn" when State.CycleCompleted => "¡Tarjeta completada!",
                "Earn" when Unlocked.Count > 0 => "¡Recompensa desbloqueada!",
                "Earn" => "Sello agregado",
                "Redeem" => "Recompensa canjeada",
                _ => "Último sello revertido"
            };
        }
        else Error = result.Error;
        try { await store.RemoveAsync(storageKey); }
        catch (Exception)
        {
            Error = "La respuesta se recibió, pero no se pudo cerrar la solicitud local. Reintenta para resolverla.";
            return;
        }
        Pending = null;
    }

    public void PrepareNextScan()
    {
        if (Busy || Pending is not null) return;
        State = null;
        ScanConsumed = false;
        ClearMessages();
    }

    private void ClearMessages() { Message = Error = null; Unlocked = []; }
}
