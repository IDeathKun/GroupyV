using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("signaler")] // Attention, le nom de la table est 'signaler' dans votre SQL
    public partial class Signalement
    {
        [Key]
        [Column("id_signal")]
        public int IdSignal { get; set; }

        [Column("id_user")]
        public int IdUser { get; set; }

        [Column("id_produit")]
        public int IdProduit { get; set; }

        [Column("date_signal")]
        public DateTime? DateSignal { get; set; }

        [ForeignKey("IdUser")]
        public virtual Utilisateur Utilisateur { get; set; }

        [ForeignKey("IdProduit")]
        public virtual Produit Produit { get; set; }
    }
}