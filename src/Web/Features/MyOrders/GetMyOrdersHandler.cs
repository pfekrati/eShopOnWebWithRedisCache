using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.eShopWeb.Web.ViewModels;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace Microsoft.eShopWeb.Web.Features.MyOrders;

public class GetMyOrdersHandler : IRequestHandler<GetMyOrders, IEnumerable<OrderViewModel>>
{
    private readonly IReadRepository<Order> _orderRepository;
    private readonly IDistributedCache _cache;

    public GetMyOrdersHandler(IReadRepository<Order> orderRepository, IDistributedCache cache)
    {
        _orderRepository = orderRepository;
        _cache = cache;
    }

    public async Task<IEnumerable<OrderViewModel>> Handle(GetMyOrders request,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"eShopOnWeb:MyOrders:userName:{request.UserName}";

        IEnumerable<OrderViewModel> result;

        var encodedCachedItem = await _cache.GetAsync(cacheKey);
        if(encodedCachedItem != null)
        {
            result = JsonConvert.DeserializeObject<IEnumerable<OrderViewModel>>(Encoding.UTF8.GetString(encodedCachedItem));
        }
        else
        {
            var specification = new CustomerOrdersWithItemsSpecification(request.UserName);
            var orders = await _orderRepository.ListAsync(specification, cancellationToken);

            result = orders.Select(o => new OrderViewModel
            {
                OrderDate = o.OrderDate,
                OrderItems = o.OrderItems?.Select(oi => new OrderItemViewModel()
                {
                    PictureUrl = oi.ItemOrdered.PictureUri,
                    ProductId = oi.ItemOrdered.CatalogItemId,
                    ProductName = oi.ItemOrdered.ProductName,
                    UnitPrice = oi.UnitPrice,
                    Units = oi.Units
                }).ToList(),
                OrderNumber = o.Id,
                ShippingAddress = o.ShipToAddress,
                Total = o.Total()
            });

            byte[] encodedOrdersList = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
            var options = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(30));
            await _cache.SetAsync(cacheKey, encodedOrdersList, options);
        }

        return result;

    }
}
