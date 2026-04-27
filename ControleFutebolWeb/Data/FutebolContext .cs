using Microsoft.EntityFrameworkCore;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Data
{
    public class FutebolContext : DbContext
    {
        public FutebolContext(DbContextOptions<FutebolContext> options) : base(options) { }

        public DbSet<Time> Times { get; set; }
        public DbSet<Jogador> Jogadores { get; set; }
        public DbSet<Jogo> Jogos { get; set; }
        public DbSet<Nota> Notas { get; set; }
        public DbSet<Nacionalidade> Nacionalidades { get; set; }
        public DbSet<Formacao> Formacoes { get; set; }
        public DbSet<PosicaoFormacao> PosicoesFormacao { get; set; }
        public DbSet<Escalacao> Escalacoes { get; set; }
        public DbSet<Gol> Gols { get; set; }
        public DbSet<Cartao> Cartoes { get; set; }
        public DbSet<Competicao> Competicoes { get; set; }
        public DbSet<TimeEscalacaoPadrao> TimeEscalacaoPadrao { get; set; }
        public DbSet<Notadetalhe> NotaDetalhes { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.UseSerialColumns();

            // Seed inicial de nacionalidades
            modelBuilder.Entity<Nacionalidade>().HasData(
                new Nacionalidade { Id = 1, Nome = "Brasil" },
                new Nacionalidade { Id = 2, Nome = "Argentina" },
                new Nacionalidade { Id = 3, Nome = "França" },
                new Nacionalidade { Id = 4, Nome = "Alemanha" },
                new Nacionalidade { Id = 5, Nome = "Itália" },
                new Nacionalidade { Id = 6, Nome = "Espanha" },
                new Nacionalidade { Id = 7, Nome = "Portugal" },
                new Nacionalidade { Id = 8, Nome = "Uruguai" },
                new Nacionalidade { Id = 9, Nome = "Chile" },
                new Nacionalidade { Id = 10, Nome = "Paraguai" },
                new Nacionalidade { Id = 11, Nome = "Bolívia" },
                new Nacionalidade { Id = 12, Nome = "Peru" },
                new Nacionalidade { Id = 13, Nome = "Equador" },
                new Nacionalidade { Id = 14, Nome = "Colômbia" },
                new Nacionalidade { Id = 15, Nome = "Venezuela" },
                new Nacionalidade { Id = 16, Nome = "Guiana" },
                new Nacionalidade { Id = 17, Nome = "Suriname" }
            );

            // Tipos de coluna
            modelBuilder.Entity<Jogador>()
                .Property(j => j.DataNascimento)
                .HasColumnType("timestamp without time zone");

            modelBuilder.Entity<Jogo>()
                .Property(j => j.Data)
                .HasColumnType("timestamp with time zone");

            // Relação Jogador -> Time
            modelBuilder.Entity<Jogador>()
                .HasOne(j => j.Time)
                .WithMany(t => t.Jogadores)
                .HasForeignKey(j => j.TimeId);

            // Relação Time -> TimeEscalacaoPadrao
            modelBuilder.Entity<Time>()
                .HasMany(t => t.TimeEscalacaoPadrao)
                .WithOne(e => e.Time)
                .HasForeignKey(e => e.TimeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relação Time -> FormacaoPadrao
            modelBuilder.Entity<Time>()
                .HasOne(t => t.FormacaoPadrao)
                .WithMany()
                .HasForeignKey(t => t.FormacaoPadraoId);

            // Converte nomes de tabelas e colunas para minúsculas
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(entity.GetTableName()?.ToLower());

                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(property.GetColumnName()?.ToLower());
                }
            }
        }
    }
}