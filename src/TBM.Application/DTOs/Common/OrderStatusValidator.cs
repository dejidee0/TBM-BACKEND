using TBM.Core.Enums;

namespace TBM.Application.DTOs.Common;

public static class OrderStatusValidator
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidTransitions = new()
    {
        { OrderStatus.Pending, new[] { OrderStatus.Processing, OrderStatus.Cancelled } },
        { OrderStatus.Processing, new[] { OrderStatus.Shipped, OrderStatus.Cancelled } },
        { OrderStatus.Shipped, new[] { OrderStatus.Completed } },
        { OrderStatus.Completed, new[] { OrderStatus.Refunded } },
        { OrderStatus.Cancelled, Array.Empty<OrderStatus>() },
        { OrderStatus.Refunded, Array.Empty<OrderStatus>() }
    };

    public static bool CanTransition(OrderStatus current, OrderStatus next)
    {
        return ValidTransitions.ContainsKey(current) &&
               ValidTransitions[current].Contains(next);
    }
}
