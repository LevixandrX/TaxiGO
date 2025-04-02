using System;
using System.Collections.Generic;

namespace TaxiGO.Models;

public partial class PromoCode
{
    public int PromoCodeId { get; set; }

    public string Code { get; set; } = null!;

    public int DiscountPercent { get; set; }

    public DateTime ExpiryDate { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
