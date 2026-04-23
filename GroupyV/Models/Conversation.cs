using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("conversation")]
    public class Conversation
    {
        [Key]
        [Column("id_conversation")]
        public int IdConversation { get; set; }

        [Column("id_client")]
        public int IdClient { get; set; }

        [Column("id_vendeur")]
        public int IdVendeur { get; set; }

        [Column("id_prevente")]
        public int IdPrevente { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
