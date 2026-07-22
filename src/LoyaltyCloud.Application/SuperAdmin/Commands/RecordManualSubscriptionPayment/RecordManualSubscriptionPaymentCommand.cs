using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.RecordManualSubscriptionPayment;

public sealed record RecordManualSubscriptionPaymentCommand(Guid TenantId, int Months)
    : IRequest<Result<RecordManualSubscriptionPaymentResult>>;

public sealed record RecordManualSubscriptionPaymentResult(
    Guid TenantId,
    string TenantSlug,
    int Months,
    DateTime PaidThroughUtc);
