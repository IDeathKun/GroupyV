using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("message")]
    public class ChatMessage
    {
        [Key]
        [Column("id_message")]
        public int IdMessage { get; set; }

        [Column("id_conversation")]
        public int IdConversation { get; set; }

        [Column("id_expediteur")]
        public int IdExpediteur { get; set; }

        [Column("contenu")]
        public string? Contenu { get; set; }

        [Column("lu")]
        public bool Lu { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        // --- RELATIONS ---
        [ForeignKey("IdExpediteur")]
        public virtual Utilisateur? Expediteur { get; set; }
    }
}
