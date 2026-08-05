using Order.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.Service
{
    public interface IOrderService
    {
        Task<IEnumerable<OrderSummary>> GetOrdersAsync();

        Task<OrderDetail> GetOrderByIdAsync(Guid orderId);

        Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string statusName);

        Task<OperationResult<OrderDetail>> UpdateOrderStatusAsync(Guid orderId, string statusName);

        Task<OperationResult<OrderDetail>> CreateOrderAsync(CreateOrderRequest request);

        Task<IEnumerable<MonthlyProfit>> GetCompletedOrderProfitByMonthAsync();
    }
}
