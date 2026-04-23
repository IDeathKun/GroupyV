using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("facture")]
    public class Facture
    {
        [Key]
        [Column("id_facture")]
        public int IdFacture { get; set; }

        [Column("id_prevente")]
        public int IdPrevente { get; set; }

        [Column("id_client")]
        public int IdClient { get; set; }

        [Column("date_facture")]
        public DateTime? DateFacture { get; set; }

        // C'est la seule info importante : le chemin du fichier
        [Column("pdf_facture")]
        public string? PdfFacture { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}