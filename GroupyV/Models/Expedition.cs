using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("expeditions")]
    public class Expedition
    {
        [Key]
        [Column("id_expeditions")]
        public int IdExpedition { get; set; }

        [Column("id_prevente")]
        public int IdPrevente { get; set; }

        [Column("numero_tracking")]
        public string? NumeroTracking { get; set; }

        [Column("transporteur")]
        public string? Transporteur { get; set; }

        [Column("statut")]
        public string Statut { get; set; }

        [Column("date_preparation")]
        public DateTime? DatePreparation { get; set; }  // Ajouté

        [Column("date_expedition")]
        public DateTime? DateExpedition { get; set; }

        [Column("date_livraison_prevue")]
        public DateTime? DateLivraisonPrevue { get; set; } // Ajouté

        [Column("date_livraison_reelle")]
        public DateTime? DateLivraisonReelle { get; set; } // Ajouté

        [Column("poids")]
        public decimal? Poids { get; set; }

        [Column("dimensions")]
        public string? Dimensions { get; set; }

        [Column("date_creation")]
        public DateTime? DateCreation { get; set; }
    }
}