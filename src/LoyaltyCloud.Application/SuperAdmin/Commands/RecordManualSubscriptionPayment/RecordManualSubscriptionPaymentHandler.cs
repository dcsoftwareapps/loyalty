using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.RecordManualSubscriptionPayment;

internal sealed class RecordManualSubscriptionPaymentHandler
    : IRequestHandler<RecordManualSubscriptionPaymentCommand, Result<RecordManualSubscriptionPaymentResult>>
{
    private readonly ISuperAdminTenantManagementService _management;

    public RecordManualSubscriptionPaymentHandler(ISuperAdminTenantManagementService management)
    {
        _management = management;
    }

    public async Task<Result<RecordManualSubscriptionPaymentResult>> Handle(
        RecordManualSubscriptionPaymentCommand request,
        CancellationToken cancellationToken) =>
        await _management.RecordPaymentAsync(request.TenantId, request.Months, cancellationToken);
}
