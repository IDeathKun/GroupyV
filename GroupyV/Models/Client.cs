using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GroupyV.Models
{
    [Table("client")]
    public partial class Client
    {
        [Key]
        [Column("id_user")]
        public int IdUser { get; set; }

        // Lien vers les infos perso (Nom, Email, etc.)
        [ForeignKey("IdUser")]
        public virtual Utilisateur Utilisateur { get; set; }

        // Relations
        public virtual ICollection<Participation> Participations { get; set; }

        // --- ASTUCE POUR CORRIGER L'ERREUR 'Client ne contient pas Nom' ---
        // Ces propriétés vont chercher l'info dans Utilisateur automatiquement.
        [NotMapped] public string Nom => Utilisateur?.Nom;
        [NotMapped] public string Prenom => Utilisateur?.Prenom;
        [NotMapped] public string Adresse => Utilisateur?.Adresse;
        [NotMapped] public string Email => Utilisateur?.Email;
        [NotMapped] public string Phone => Utilisateur?.Phone;
    }
}