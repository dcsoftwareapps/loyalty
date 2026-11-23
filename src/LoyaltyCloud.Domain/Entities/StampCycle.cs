using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class StampCycle : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string LoyaltyCardSerialNumber { get; private set; } = string.Empty;
    public Guid StampProgramVersionId { get; private set; }
    public int Sequence { get; private set; }
    public int CurrentStamps { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private StampCycle() { }

    public void Earn(StampProgramVersion program, DateTime nowUtc)
    {
        if (program.Id != StampProgramVersionId || program.TenantId != TenantId)
            throw new ArgumentException("Invalid cycle rules.");
        if (CompletedAt.HasValue || CurrentStamps >= program.MaxStamps)
            throw new InvalidOperationException("El ciclo ya esta completo.");
        CurrentStamps++;
        if (CurrentStamps == program.MaxStamps) CompletedAt = nowUtc;
    }

    public void ReverseEarn()
    {
        if (CurrentStamps <= 0) throw new InvalidOperationException("No hay sellos para revertir.");
        CurrentStamps--;
        CompletedAt = null;
    }

    public StampCycle(Guid id, LoyaltyCard card, StampProgramVersion program, DateTime nowUtc, int sequence = 1) : base(id)
    {
        if (card.TenantId != program.TenantId) throw new ArgumentException("Cross-tenant cycle.");
        if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
        TenantId = card.TenantId; LoyaltyCardSerialNumber = card.SerialNumber; StampProgramVersionId = program.Id;
        Sequence = sequence; CurrentStamps = sequence == 1 ? program.WelcomeStamps : 0; StartedAt = nowUtc;
    }
}
