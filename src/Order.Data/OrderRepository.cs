using Microsoft.EntityFrameworkCore;
using Order.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Order.Data
{
    public class OrderRepository : IOrderRepository
    {
        private const string DefaultOrderStatus = "Created";

        private readonly OrderContext _orderContext;

        public OrderRepository(OrderContext orderContext)
        {
            _orderContext = orderContext;
        }

        /// <summary>
        /// Shared by the "all orders" and "orders by status" queries so both
        /// return an identically shaped summary.
        /// </summary>
        private static readonly Expression<Func<Entities.Order, OrderSummary>> ToOrderSummary =
            x => new OrderSummary
            {
                Id = new Guid(x.Id),
                ResellerId = new Guid(x.ResellerId),
                CustomerId = new Guid(x.CustomerId),
                StatusId = new Guid(x.StatusId),
                StatusName = x.Status.Name,
                ItemCount = x.Items.Count,
                TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                CreatedDate = x.CreatedDate
            };

        public async Task<IEnumerable<OrderSummary>> GetOrdersAsync()
        {
            var orders = await _orderContext.Order
                .Select(ToOrderSummary)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return orders;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string statusName)
        {
            var normalisedStatus = NormaliseStatus(statusName);

            var orders = await _orderContext.Order
                .Where(x => x.Status.Name.ToLower() == normalisedStatus)
                .Select(ToOrderSummary)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            return orders;
        }

        public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId)
        {
            var orderIdBytes = orderId.ToByteArray();

            var order = await _orderContext.Order
                .Where(x => _orderContext.Database.IsInMemory() ? x.Id.SequenceEqual(orderIdBytes) : x.Id == orderIdBytes)
                .Select(x => new OrderDetail
                {
                    Id = new Guid(x.Id),
                    ResellerId = new Guid(x.ResellerId),
                    CustomerId = new Guid(x.CustomerId),
                    StatusId = new Guid(x.StatusId),
                    StatusName = x.Status.Name,
                    CreatedDate = x.CreatedDate,
                    TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                    TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                    Items = x.Items.Select(i => new Model.OrderItem
                    {
                        Id = new Guid(i.Id),
                        OrderId = new Guid(i.OrderId),
                        ServiceId = new Guid(i.ServiceId),
                        ServiceName = i.Service.Name,
                        ProductId = new Guid(i.ProductId),
                        ProductName = i.Product.Name,
                        UnitCost = i.Product.UnitCost,
                        UnitPrice = i.Product.UnitPrice,
                        TotalCost = i.Product.UnitCost * i.Quantity.Value,
                        TotalPrice = i.Product.UnitPrice * i.Quantity.Value,
                        Quantity = i.Quantity.Value
                    })
                }).SingleOrDefaultAsync();

            return order;
        }

        public async Task<OperationResult<OrderDetail>> UpdateOrderStatusAsync(Guid orderId, string statusName)
        {
            var orderIdBytes = orderId.ToByteArray();

            var order = await _orderContext.Order
                .SingleOrDefaultAsync(x => _orderContext.Database.IsInMemory()
                    ? x.Id.SequenceEqual(orderIdBytes)
                    : x.Id == orderIdBytes);

            if (order == null)
            {
                return OperationResult<OrderDetail>.NotFound();
            }

            var status = await FindStatusAsync(statusName);
            if (status == null)
            {
                return OperationResult<OrderDetail>.Invalid(UnknownStatusMessage(statusName));
            }

            order.StatusId = status.Id;
            await _orderContext.SaveChangesAsync();

            return OperationResult<OrderDetail>.Success(await GetOrderByIdAsync(orderId));
        }

        public async Task<OperationResult<OrderDetail>> CreateOrderAsync(CreateOrderRequest request)
        {
            var status = await FindStatusAsync(DefaultOrderStatus);
            if (status == null)
            {
                return OperationResult<OrderDetail>.Invalid(
                    $"The default order status '{DefaultOrderStatus}' is missing from the database.");
            }

            // Products carry the service they belong to, so order items derive
            // their ServiceId from the product rather than trusting the caller.
            var requestedProductIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
            var requestedProductIdBytes = requestedProductIds.Select(x => x.ToByteArray()).ToList();

            var products = await _orderContext.OrderProduct
                .Where(p => requestedProductIdBytes.Contains(p.Id))
                .ToListAsync();

            var productsById = products.ToDictionary(p => new Guid(p.Id));

            var unknownProductIds = requestedProductIds
                .Where(id => !productsById.ContainsKey(id))
                .ToList();

            if (unknownProductIds.Any())
            {
                return OperationResult<OrderDetail>.Invalid(unknownProductIds
                    .Select(id => $"Product '{id}' does not exist.")
                    .ToArray());
            }

            var orderId = Guid.NewGuid();
            var orderIdBytes = orderId.ToByteArray();

            _orderContext.Order.Add(new Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = request.ResellerId.ToByteArray(),
                CustomerId = request.CustomerId.ToByteArray(),
                StatusId = status.Id,
                CreatedDate = DateTime.UtcNow
            });

            foreach (var item in request.Items)
            {
                var product = productsById[item.ProductId];

                _orderContext.OrderItem.Add(new Entities.OrderItem
                {
                    Id = Guid.NewGuid().ToByteArray(),
                    OrderId = orderIdBytes,
                    ProductId = product.Id,
                    ServiceId = product.ServiceId,
                    Quantity = item.Quantity
                });
            }

            await _orderContext.SaveChangesAsync();

            return OperationResult<OrderDetail>.Success(await GetOrderByIdAsync(orderId));
        }

        public async Task<IEnumerable<MonthlyProfit>> GetProfitByMonthAsync(string statusName)
        {
            var normalisedStatus = NormaliseStatus(statusName);

            // Grouping order items (rather than orders) keeps this to a single
            // GROUP BY. Orders without items contribute no profit either way.
            var profitByMonth = await _orderContext.OrderItem
                .Where(i => i.Order.Status.Name.ToLower() == normalisedStatus)
                .GroupBy(i => new { i.Order.CreatedDate.Year, i.Order.CreatedDate.Month })
                .Select(g => new MonthlyProfit
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalCost = g.Sum(i => i.Quantity.Value * i.Product.UnitCost),
                    TotalPrice = g.Sum(i => i.Quantity.Value * i.Product.UnitPrice)
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();

            return profitByMonth;
        }

        private async Task<Entities.OrderStatus> FindStatusAsync(string statusName)
        {
            var normalisedStatus = NormaliseStatus(statusName);

            return await _orderContext.OrderStatus
                .SingleOrDefaultAsync(x => x.Name.ToLower() == normalisedStatus);
        }

        /// <summary>
        /// Status names are compared case-insensitively via ToLower so the
        /// behaviour is the same on MySQL and on the SQLite database used by tests.
        /// </summary>
        private static string NormaliseStatus(string statusName)
        {
            return (statusName ?? string.Empty).Trim().ToLower();
        }

        private static string UnknownStatusMessage(string statusName)
        {
            return $"'{statusName}' is not a known order status.";
        }
    }
}
