using ServiceCenter.Api.Contracts;

namespace ServiceCenter.Api.Services;

public interface IWarrantyService
{
    (bool Success, string? ErrorCode, string? ErrorDetail, OrderReceiptDto? Order) CreateClaim(int originalOrderId, WarrantyClaimRequest request);
}
