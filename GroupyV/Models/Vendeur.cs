using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("vendeur")]
    public partial class Vendeur
    {
        [Key]
        [Column("id_user")]
        public int IdUser { get; set; }

        [Column("nom_entreprise")]
        public string? NomEntreprise { get; set; }

        [Column("siret")]
        public string? Siret { get; set; }

        [Column("adresse_entreprise")]
        public string? AdresseEntreprise { get; set; }

        [Column("email_pro")]
        public string? EmailPro { get; set; }

        [ForeignKey("IdUser")]
        public virtual Utilisateur Utilisateur { get; set; }
    }
}