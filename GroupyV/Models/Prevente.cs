using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace GroupyV.Models
{
    [Table("prevente")]
    public class Prevente
    {
        [Key]
        [Column("id_prevente")]
        public int IdPrevente { get; set; }

        [Column("id_produit")]
        public int IdProduit { get; set; }

        [Column("statut")]
        [MaxLength(255)]
        public string? Statut { get; set; }

        [Column("prix_prevente")]
        public decimal PrixPrevente { get; set; }

        [Column("created_at")]
        public DateTime DateCreation { get; set; } // Correspond à ton SQL

        [Column("date_limite")]
        public DateTime? DateLimite { get; set; }

        [Column("nombre_min")]
        public int NombreMin { get; set; }

        [Column("id_vendeur")]
        public int? IdVendeur { get; set; }

        // --- RELATIONS ---
        [ForeignKey("IdProduit")]
        public virtual Produit Produit { get; set; }

        [ForeignKey("IdVendeur")]
        public virtual Vendeur Vendeur { get; set; }

        // Lien vers la table participation
        public virtual ICollection<Participation> Participations { get; set; }

        // --- PROPRIÉTÉS CALCULÉES (Non mappées en base) ---

        // La quantité vendue est le nombre de participations
        [NotMapped]
        public int QuantiteVendue => Participations?.Count ?? 0;

        // Calcul du CA total pour cette prévente
        [NotMapped]
        public decimal TotalGenere => QuantiteVendue * PrixPrevente;

        // Pour afficher la progression (ex: 5 / 10 participants)
        [NotMapped]
        public string Progression => $"{QuantiteVendue} / {NombreMin}";
    }
}