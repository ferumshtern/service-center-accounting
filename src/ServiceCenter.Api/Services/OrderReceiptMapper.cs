using ServiceCenter.Api.Contracts;
using ServiceCenter.Api.Models;

namespace ServiceCenter.Api.Services;

// Формування електронної квитанції (FR-05), спільне для всіх реалізацій IOrderRepository.
public static class OrderReceiptMapper
{
    public static OrderReceiptDto ToDto(RepairOrder order, Client client, Device device, ServiceEngineer engineer) =>
        new(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.CreatedAt,
            client.FullName,
            client.Phone,
            device.Brand,
            device.Model,
            device.SerialNumber,
            order.IssueDescription,
            order.DiagnosticNotes,
            engineer.FullName,
            order.TotalCost,
            order.Items.Select(i => new RepairItemDto(i.Id, i.Name, i.Quantity, i.UnitPrice, i.Subtotal)).ToArray(),
            order.History.Select(h => new StatusHistoryDto(h.Status.ToString(), h.ChangedAt)).ToArray()
        );
}
