using System.ComponentModel.DataAnnotations;

namespace Order.Model
{
    public class UpdateOrderStatusRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "A status name is required.")]
        public string Status { get; set; }
    }
}
