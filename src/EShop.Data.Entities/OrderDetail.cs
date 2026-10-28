using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EShop.Data.Entities
{
    [Index("OrderId")]
    [Index("ProductId")]
    public partial class OrderDetail
    {
        [Key]
        public int OrderId { get; set; }

        [Key]
        public int ProductId { get; set; }

        [Precision(19, 4)]
        public decimal UnitPrice { get; set; }

        // A copy of the product name at checkout, like UnitPrice: renaming the product does not alter the order.
        [StringLength(40)]
        public string ProductName { get; set; } = null!;
        public short Quantity { get; set; }
        public float Discount { get; set; }

        [ForeignKey("OrderId")]
        [InverseProperty("OrderDetails")]
        public virtual Order Order { get; set; } = null!;

        [ForeignKey("ProductId")]
        [InverseProperty("OrderDetails")]
        public virtual Product Product { get; set; } = null!;
    }
}
