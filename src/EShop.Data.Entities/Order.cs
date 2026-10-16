using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EShop.Data.Entities
{
    [Index("CustomerId")]
    [Index("EmployeeId")]
    [Index("OrderDate")]
    [Index("ShipPostalCode")]
    [Index("ShippedDate")]
    [Index("ShipVia")]
    public partial class Order
    {
        public Order()
        {
            OrderDetails = new HashSet<OrderDetail>();
        }

        [Key]
        public int OrderId { get; set; }

        [StringLength(5)]
        public string? CustomerId { get; set; }
        public int? EmployeeId { get; set; }

        [Column(TypeName = "timestamp without time zone")]
        public DateTime? OrderDate { get; set; }

        [Column(TypeName = "timestamp without time zone")]
        public DateTime? RequiredDate { get; set; }

        [Column(TypeName = "timestamp without time zone")]
        public DateTime? ShippedDate { get; set; }
        public int? ShipVia { get; set; }

        [Precision(19, 4)]
        public decimal? Freight { get; set; }

        [StringLength(40)]
        public string? ShipName { get; set; }

        [StringLength(60)]
        public string? ShipAddress { get; set; }

        [StringLength(15)]
        public string? ShipCity { get; set; }

        [StringLength(15)]
        public string? ShipRegion { get; set; }

        [StringLength(10)]
        public string? ShipPostalCode { get; set; }

        [StringLength(15)]
        public string? ShipCountry { get; set; }

        [ForeignKey("CustomerId")]
        [InverseProperty("Orders")]
        public virtual Customer? Customer { get; set; }

        [ForeignKey("EmployeeId")]
        [InverseProperty("Orders")]
        public virtual Employee? Employee { get; set; }

        [ForeignKey("ShipVia")]
        [InverseProperty("Orders")]
        public virtual Shipper? ShipViaNavigation { get; set; }

        [InverseProperty("Order")]
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
