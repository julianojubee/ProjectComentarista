using ControleFutebolWeb.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ControleFutebolWeb.Data
{
    public class FutebolContext : IdentityDbContext<ApplicationUser>
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
        public DbSet<EscalacaoSeta> SetasEscalacao { get; set; }
        public DbSet<Gol> Gols { get; set; }
        public DbSet<Cartao> Cartoes { get; set; }
        public DbSet<Competicao> Competicoes { get; set; }
        public DbSet<TimeEscalacaoPadrao> TimeEscalacaoPadrao { get; set; }
        public DbSet<Notadetalhe> NotaDetalhes { get; set; }
        public DbSet<Treinador> Treinadores { get; set; }
        public DbSet<TreinadorHistorico> TreinadoresHistorico { get; set; }
        public DbSet<Assistencia> Assistencias { get; set; }
        public DbSet<TransfermarktSincronizacaoLog> TransfermarktSincronizacaoLogs { get; set; }
        public DbSet<Substituicao> Substituicoes { get; set; }
        public DbSet<PenaltiPerdido> PenaltisPerdidos { get; set; }
        public DbSet<PenaltiDisputa> PenaltisDisputa { get; set; }
        public DbSet<EstatisticaJogador> EstatisticasJogador { get; set; }
        public DbSet<CriterioNota> CriteriosNota { get; set; }
        public DbSet<AnotacaoTime> AnotacoesTime { get; set; }
        public DbSet<JogoAnalisadoUsuario> JogosAnalisadosUsuario { get; set; }
        public DbSet<ObservacaoJogoUsuario> ObservacoesJogoUsuario { get; set; }
        public DbSet<ObservacaoJogoTag> ObservacoesJogoTag { get; set; }
        public DbSet<CompeticaoTopTierUsuario> CompeticoesTopTierUsuario { get; set; }
        public DbSet<CronometroPartida> CronometrosPartida { get; set; }
        public DbSet<FaseTatica> FasesTaticas { get; set; }
        public DbSet<CuriosidadeTime> CuriosidadesTime { get; set; }
        public DbSet<SelecaoCopaUsuario> SelecoesCopaUsuario { get; set; }
        public DbSet<Transferencia> Transferencias { get; set; }

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
                if (entry.State != EntityState.Added && entry.State != EntityState.Modified) continue;

                foreach (var property in entry.Properties)
                {
                    var columnType = property.Metadata.GetColumnType();
                    bool isWithTz = columnType != null &&
                        columnType.Contains("with time zone", StringComparison.OrdinalIgnoreCase);

                    if (!isWithTz) continue; // pula timestamp without time zone

                    if (property.Metadata.ClrType == typeof(DateTime))
                    {
                        var valor = (DateTime)property.CurrentValue!;
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


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.UseSerialColumns();

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    // Só aplica converter UTC em colunas "timestamp with time zone"
                    var columnType = property.GetColumnType();
                    bool isTimestampWithTz = columnType != null &&
                        columnType.Contains("with time zone", StringComparison.OrdinalIgnoreCase);

                    if (property.ClrType == typeof(DateTime) && isTimestampWithTz)
                    {
                        property.SetValueConverter(new ValueConverter<DateTime, DateTime>(
                            v => DateTime.SpecifyKind(v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(), DateTimeKind.Utc),
                            v => DateTime.SpecifyKind(v, DateTimeKind.Utc)
                        ));
                    }
                    else if (property.ClrType == typeof(DateTime?) && isTimestampWithTz)
                    {
                        property.SetValueConverter(new ValueConverter<DateTime?, DateTime?>(
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime(), DateTimeKind.Utc) : v,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v
                        ));
                    }
                }
            }

            // Gols
            modelBuilder.Entity<Gol>(entity =>
            {
                entity.ToTable("gols");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.JogoId).HasColumnName("jogoid");
                entity.Property(e => e.JogadorId).HasColumnName("jogadorid");
                entity.Property(e => e.Minuto).HasColumnName("minuto");
                entity.Property(e => e.Contra).HasColumnName("contra");

                entity.HasOne(e => e.Jogo)
                    .WithMany()
                    .HasForeignKey(e => e.JogoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Jogador)
                    .WithMany()
                    .HasForeignKey(e => e.JogadorId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Assistencias
            modelBuilder.Entity<Assistencia>(entity =>
            {
                entity.ToTable("assistencias");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.JogoId).HasColumnName("jogoid");
                entity.Property(e => e.JogadorId).HasColumnName("jogadorid");
                entity.Property(e => e.Minuto).HasColumnName("minuto");

                entity.HasOne(e => e.Jogo)
                    .WithMany()
                    .HasForeignKey(e => e.JogoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Jogador)
                    .WithMany()
                    .HasForeignKey(e => e.JogadorId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Cartoes
            modelBuilder.Entity<Cartao>(entity =>
            {
                entity.ToTable("cartoes");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.JogoId).HasColumnName("jogoid");
                entity.Property(e => e.JogadorId).HasColumnName("jogadorid");
                entity.Property(e => e.Minuto).HasColumnName("minuto");
                entity.Property(e => e.Tipo).HasColumnName("tipo");

                entity.HasOne(e => e.Jogo)
                    .WithMany()
                    .HasForeignKey(e => e.JogoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Jogador)
                    .WithMany()
                    .HasForeignKey(e => e.JogadorId)
                    .OnDelete(DeleteBehavior.Cascade);
            });



            // Substituicoes
            modelBuilder.Entity<Substituicao>(entity =>
            {
                entity.ToTable("substituicoes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.JogoId).HasColumnName("jogoid");
                entity.Property(e => e.JogadorEntrouId).HasColumnName("jogadorentroud");
                entity.Property(e => e.JogadorSaiuId).HasColumnName("jogadorsaiuid");
                entity.Property(e => e.Minuto).HasColumnName("minuto");
                entity.Property(e => e.IsTimeCasa).HasColumnName("istimecasa");

                entity.HasOne(e => e.Jogo)
                    .WithMany()
                    .HasForeignKey(e => e.JogoId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.JogadorEntrou)
                    .WithMany()
                    .HasForeignKey(e => e.JogadorEntrouId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.JogadorSaiu)
                    .WithMany()
                    .HasForeignKey(e => e.JogadorSaiuId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

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
                .HasColumnType("timestamp without time zone")
                .IsRequired(false);

            modelBuilder.Entity<Jogador>()
                .HasOne(j => j.Selecao)
                .WithMany()
                .HasForeignKey(j => j.SelecaoId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Treinador>()
                .Property(t => t.DataNascimento)
                .HasConversion(
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value.ToUniversalTime(), DateTimeKind.Utc) : (DateTime?)null,
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : (DateTime?)null
                )
                .IsRequired(false);

            modelBuilder.Entity<Nota>()
                .HasOne(n => n.Usuario)
                .WithMany()
                .HasForeignKey(n => n.UsuarioId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // 🔹 Índices para acelerar filtros/joins frequentes em relatórios e listagens.
            // (As FKs JogoId/JogadorId/CompeticaoId já são indexadas automaticamente pelo EF.)
            modelBuilder.Entity<Jogo>().HasIndex(j => j.Temporada);
            // Data é filtrada por intervalo em "Jogos de Hoje" e usada em ORDER BY
            // em várias listagens — sem índice vira seq scan com a tabela grande.
            modelBuilder.Entity<Jogo>().HasIndex(j => j.Data);
            modelBuilder.Entity<Jogador>().HasIndex(j => j.Posicao);
            modelBuilder.Entity<Nota>().HasIndex(n => new { n.UsuarioId, n.JogoId, n.JogadorId });
            modelBuilder.Entity<Escalacao>().HasIndex(e => new { e.JogoId, e.UsuarioId });
            modelBuilder.Entity<ObservacaoJogoTag>().HasIndex(o => new { o.JogadorId, o.UsuarioId });

            // Transferências: apagar time/jogo não apaga o histórico (FK vira null);
            // apagar o jogador remove as transferências dele junto.
            modelBuilder.Entity<Transferencia>(entity =>
            {
                entity.HasOne(t => t.Jogador).WithMany().HasForeignKey(t => t.JogadorId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(t => t.TimeOrigem).WithMany().HasForeignKey(t => t.TimeOrigemId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(t => t.TimeDestino).WithMany().HasForeignKey(t => t.TimeDestinoId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(t => t.Jogo).WithMany().HasForeignKey(t => t.JogoId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(t => t.Usuario).WithMany().HasForeignKey(t => t.UsuarioId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(t => t.Data);
            });

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
