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

        // ProductId without a foreign key (#75): the order keeps its own copy of name and price, so the product
        // can be deleted, and the catalog gets its own database (#78).
    }
}
