using Order.Data;
using Order.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Order.Service
{
    public class OrderService : IOrderService
    {
        /// <summary>
        /// Profit is only reported for orders that reached this status.
        /// </summary>
        private const string CompletedStatus = "Completed";

        private readonly IOrderRepository _orderRepository;

        public OrderService(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersAsync()
        {
            var orders = await _orderRepository.GetOrdersAsync();
            return orders;
        }

        public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId);
            return order;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string statusName)
        {
            var orders = await _orderRepository.GetOrdersByStatusAsync(statusName);
            return orders;
        }

        public async Task<OperationResult<OrderDetail>> UpdateOrderStatusAsync(Guid orderId, string statusName)
        {
            var result = await _orderRepository.UpdateOrderStatusAsync(orderId, statusName);
            return result;
        }

        public async Task<OperationResult<OrderDetail>> CreateOrderAsync(CreateOrderRequest request)
        {
            if (request == null)
            {
                return OperationResult<OrderDetail>.Invalid("An order must be supplied.");
            }

            var result = await _orderRepository.CreateOrderAsync(request);
            return result;
        }

        public async Task<IEnumerable<MonthlyProfit>> GetCompletedOrderProfitByMonthAsync()
        {
            var profit = await _orderRepository.GetProfitByMonthAsync(CompletedStatus);
            return profit;
        }
    }
}
