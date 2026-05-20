using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
        public DbSet<Treinador> Treinadores { get; set; }
        public DbSet<TreinadorHistorico> TreinadoresHistorico { get; set; }
        public DbSet<Assistencia> Assistencias { get; set; }
        public DbSet<TransfermarktSincronizacaoLog> TransfermarktSincronizacaoLogs { get; set; }

        public override int SaveChanges()
        {
            AjustarDatasParaUtc();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AjustarDatasParaUtc();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void AjustarDatasParaUtc()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                // Verifica apenas entidades que estão sendo adicionadas ou modificadas
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    foreach (var property in entry.Properties)
                    {
                        if (property.Metadata.ClrType == typeof(DateTime))
                        {
                            var valor = (DateTime)property.CurrentValue;
                            if (valor.Kind != DateTimeKind.Utc)
                                property.CurrentValue = valor.ToUniversalTime();
                        }
                        else if (property.Metadata.ClrType == typeof(DateTime?))
                        {
                            var valor = (DateTime?)property.CurrentValue;
                            if (valor.HasValue && valor.Value.Kind != DateTimeKind.Utc)
                                property.CurrentValue = valor.Value.ToUniversalTime();
                        }
                    }
                }
            }
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.UseSerialColumns();

            // 🔹 Converter global para DateTime e DateTime? em UTC
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(new ValueConverter<DateTime, DateTime>(
                            v => DateTime.SpecifyKind(v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(), DateTimeKind.Utc),
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                        ));
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        property.SetValueConverter(new ValueConverter<DateTime?, DateTime?>(
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime(), DateTimeKind.Utc) : v,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v
                        ));
                    }
                }
            }

            // 🔹 Exemplo específico para Jogo.Data
            modelBuilder.Entity<Jogo>()
                .Property(j => j.Data)
                .HasColumnType("timestamp with time zone")
                .HasConversion(
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value.ToUniversalTime(), DateTimeKind.Utc) : (DateTime?)null,
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null
                );

            // 🔹 Exemplo específico para TransfermarktSincronizacaoLog.Data
            modelBuilder.Entity<TransfermarktSincronizacaoLog>()
                .Property(l => l.Data)
                .HasColumnType("timestamp with time zone")
                .HasConversion(
                    v => DateTime.SpecifyKind(v.ToUniversalTime(), DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                );

            // 🔹 Tipos de coluna específicos
            modelBuilder.Entity<Jogador>()
                .Property(j => j.DataNascimento)
                .HasColumnType("timestamp without time zone");

            modelBuilder.Entity<Treinador>()
                .Property(t => t.DataNascimento)
                .HasConversion(
                    v => DateTime.SpecifyKind(v.ToUniversalTime(), DateTimeKind.Utc),
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                );

            // 🔹 Converte nomes de tabelas e colunas para minúsculas
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                entity.SetTableName(entity.GetTableName()?.ToLower());
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(property.GetColumnName()?.ToLower());
                }
            }

            // 🔹 Seed inicial de nacionalidades
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
        }

    }
}
