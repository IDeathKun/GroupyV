using GroupyV.Models;
using GroupyV.Services;
using Microsoft.EntityFrameworkCore;

namespace GroupyV.Data
{
    public class GroupyContext : DbContext
    {
        public DbSet<Utilisateur> Utilisateurs { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<Produit> Produits { get; set; }
        public DbSet<Prevente> Preventes { get; set; }
        public DbSet<Participation> Participations { get; set; }
        public DbSet<Expedition> Expeditions { get; set; }
        public DbSet<Facture> Factures { get; set; }

        // --- LES AJOUTS ---
        public DbSet<Vendeur> Vendeurs { get; set; }
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<Signalement> Signalements { get; set; }
        public DbSet<MouvementStock> MouvementsStock { get; set; }

        // --- MESSAGERIE ---
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = AppSettings.ConnectionString;
                optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapping Participation (Clé simple selon votre SQL 'id_particiption')
            modelBuilder.Entity<Participation>()
                .HasKey(p => p.IdParticipation);

            // Mapping Signalement (Table 'signaler')
            modelBuilder.Entity<Signalement>()
                .ToTable("signaler");

            // Mapping Stock (Table 'stocks')
            modelBuilder.Entity<Stock>()
                .ToTable("stocks");

            // Messagerie : désactiver les cascades pour éviter les conflits de clés multiples
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Expediteur)
                .WithMany()
                .HasForeignKey(m => m.IdExpediteur)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}