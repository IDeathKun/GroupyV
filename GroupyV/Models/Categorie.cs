using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("categorie")]
    public class Categorie
    {
        [Key]
        [Column("id_categorie")]
        public int IdCategorie { get; set; }

        [Column("lib")]
        public string? Lib { get; set; } // Le nom de la catégorie dans votre BDD

        // Ajoutez d'autres colonnes si nécessaire (id_gestionnaire, created_at...)
    }
}