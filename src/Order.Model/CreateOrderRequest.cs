using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Order.Model
{
    public class CreateOrderRequest
    {
        [NotEmptyGuid]
        public Guid ResellerId { get; set; }

        [NotEmptyGuid]
        public Guid CustomerId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "An order must contain at least one item.")]
        public IList<CreateOrderItemRequest> Items { get; set; }
    }
}
