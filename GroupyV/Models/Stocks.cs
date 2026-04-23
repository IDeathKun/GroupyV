using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("stocks")]
    public partial class Stock
    {
        [Key]
        [Column("id_stock")]
        public int IdStock { get; set; }

        [Column("id_produit")]
        public int IdProduit { get; set; }

        [Column("stock_physique")]
        public int StockPhysique { get; set; }

        [Column("stock_reserve")]
        public int StockReserve { get; set; }

        [Column("seuil_alerte")]
        public int SeuilAlerte { get; set; }

        [Column("prix_achat")]
        public decimal? PrixAchat { get; set; }

        [Column("emplacement")]
        public string? Emplacement { get; set; }

        [ForeignKey("IdProduit")]
        public virtual Produit Produit { get; set; }
    }
}