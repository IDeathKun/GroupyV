using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("participation")]
    public partial class Participation
    {
        [Key]
        [Column("id_particiption")]
        public int IdParticipation { get; set; }

        [Column("id_client")]
        public int IdClient { get; set; }

        [Column("id_prevente")]
        public int IdPrevente { get; set; }

        [Column("id_facture")]
        public int? IdFacture { get; set; }

        [Column("created_at")]
        public DateTime? DateParticipation { get; set; }

        [Column("updated_at")]
        public DateTime? DateModification { get; set; }

        // --- RELATIONS ---
        [ForeignKey("IdClient")]
        public virtual Client Client { get; set; }

        [ForeignKey("IdPrevente")]
        public virtual Prevente Prevente { get; set; }

        [ForeignKey("IdFacture")]
        public virtual Facture Facture { get; set; }
    }
}