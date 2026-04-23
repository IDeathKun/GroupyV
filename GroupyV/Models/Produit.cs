using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("produit")]
    public class Produit
    {
        [Key]
        [Column("id_produit")]
        public int IdProduit { get; set; }

        [Column("nom_produit")]
        public string? NomProduit { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("prix")]
        public decimal? Prix { get; set; }

        [Column("image")]
        public string? Image { get; set; }

        // La clé étrangère (le chiffre)
        [Column("id_categorie")]
        public int? IdCategorie { get; set; }

        [Column("id_vendeur")]
        public int? IdVendeur { get; set; }

        // --- RELATIONS (Ce qui manquait) ---

        [ForeignKey("IdCategorie")]
        public virtual Categorie Categorie { get; set; } // <--- C'est ici que l'erreur se corrige

        [ForeignKey("IdVendeur")]
        public virtual Vendeur Vendeur { get; set; }
    }
}