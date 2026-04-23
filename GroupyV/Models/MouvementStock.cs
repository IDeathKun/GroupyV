using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("mouvements_stock")]
    public class MouvementStock
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("id_produit")]
        public int IdProduit { get; set; }

        [Column("id_vendeur")]
        public int IdVendeur { get; set; }

        [Column("type_mouvement")]
        [Required]
        [MaxLength(20)]
        public string TypeMouvement { get; set; }

        [Column("quantite")]
        public int Quantite { get; set; }

        [Column("stock_avant")]
        public int StockAvant { get; set; }

        [Column("stock_apres")]
        public int StockApres { get; set; }

        [Column("motif")]
        [Required]
        [MaxLength(100)]
        public string Motif { get; set; }

        [Column("motif_detaille")]
        public string? MotifDetaille { get; set; }

        [Column("date_mouvement")]
        public DateTime DateMouvement { get; set; }

        // Relations de navigation
        [ForeignKey("IdProduit")]
        public virtual Produit Produit { get; set; }

        [ForeignKey("IdVendeur")]
        public virtual Vendeur Vendeur { get; set; }
    }
}