using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Campeonatos;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Data
{
    // Mapeamento do módulo de campeonatos próprios, fora do OnModelCreating
    // principal para o módulo ficar num lugar só. Chamado ANTES do laço que põe
    // tabelas e colunas em minúsculas — por isso os check constraints já usam os
    // nomes em minúsculas.
    //
    // Deleções: o usuário leva tudo dele junto. Partida e evento de súmula também
    // caem em cascata com o participante. NO ACTION/RESTRICT ali quebram a exclusão
    // do campeonato: o Postgres confere a chave do evento (dois níveis abaixo,
    // campeonato → partida → evento) antes de a cascata chegar nele. Remover
    // participante de campeonato já em andamento é barrado pela tela, não pelo banco.
    public static class CampeonatosModelConfig
    {
        public static void Configurar(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.Modulos)
                .HasDefaultValue(ModuloSistema.Analise);

            modelBuilder.Entity<TimeProprio>(entity =>
            {
                entity.Property(t => t.Nome).HasMaxLength(80);
                entity.Property(t => t.Sigla).HasMaxLength(5);
                entity.HasIndex(t => t.UsuarioId);

                entity.HasOne(t => t.Usuario).WithMany()
                    .HasForeignKey(t => t.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<JogadorProprio>(entity =>
            {
                entity.Property(j => j.Nome).HasMaxLength(80);
                entity.Property(j => j.Apelido).HasMaxLength(40);
                entity.HasIndex(j => j.UsuarioId);

                entity.HasOne(j => j.Usuario).WithMany()
                    .HasForeignKey(j => j.UsuarioId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(j => j.Nacionalidade).WithMany()
                    .HasForeignKey(j => j.NacionalidadeId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ElencoItem>(entity =>
            {
                entity.ToTable("itenselenco", t => t.HasCheckConstraint(
                    "ck_itenselenco_um_jogador",
                    "num_nonnulls(jogadorid, jogadorproprioid) = 1"));

                entity.HasIndex(e => e.TimeProprioId);
                // O mesmo jogador não entra duas vezes no mesmo time.
                entity.HasIndex(e => new { e.TimeProprioId, e.JogadorId }).IsUnique()
                    .HasFilter("jogadorid IS NOT NULL");
                entity.HasIndex(e => new { e.TimeProprioId, e.JogadorProprioId }).IsUnique()
                    .HasFilter("jogadorproprioid IS NOT NULL");

                entity.HasOne(e => e.TimeProprio).WithMany(t => t.Elenco)
                    .HasForeignKey(e => e.TimeProprioId).OnDelete(DeleteBehavior.Cascade);
                // Cascade nos dois cadastros: SetNull furaria o check constraint, e
                // um item de elenco sem jogador não significa nada.
                entity.HasOne(e => e.Jogador).WithMany()
                    .HasForeignKey(e => e.JogadorId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.JogadorProprio).WithMany()
                    .HasForeignKey(e => e.JogadorProprioId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Campeonato>(entity =>
            {
                entity.Property(c => c.Nome).HasMaxLength(100);
                entity.Property(c => c.Tipo).HasMaxLength(20);
                entity.Property(c => c.Modalidade).HasConversion<string>().HasMaxLength(20);
                entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
                entity.Property(c => c.TokenPublico).HasMaxLength(64);

                entity.HasIndex(c => c.UsuarioId);
                entity.HasIndex(c => c.TokenPublico).IsUnique()
                    .HasFilter("tokenpublico IS NOT NULL");

                entity.HasOne(c => c.Usuario).WithMany()
                    .HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CampeonatoFase>(entity =>
            {
                entity.Property(f => f.Tipo).HasMaxLength(20);
                entity.HasIndex(f => new { f.CampeonatoId, f.Ordem });

                entity.HasOne(f => f.Campeonato).WithMany(c => c.Fases)
                    .HasForeignKey(f => f.CampeonatoId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CampeonatoParticipante>(entity =>
            {
                entity.ToTable("campeonatoparticipantes", t => t.HasCheckConstraint(
                    "ck_campeonatoparticipantes_no_maximo_um_time",
                    "num_nonnulls(timeproprioid, timeid) <= 1"));

                entity.HasIndex(p => p.CampeonatoId);

                entity.HasOne(p => p.Campeonato).WithMany(c => c.Participantes)
                    .HasForeignKey(p => p.CampeonatoId).OnDelete(DeleteBehavior.Cascade);
                // Os snapshots seguram a exibição se o time sumir.
                entity.HasOne(p => p.TimeProprio).WithMany()
                    .HasForeignKey(p => p.TimeProprioId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(p => p.Time).WithMany()
                    .HasForeignKey(p => p.TimeId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<PartidaCampeonato>(entity =>
            {
                entity.ToTable("partidascampeonato");
                entity.HasIndex(p => new { p.CampeonatoId, p.Rodada });

                entity.HasOne(p => p.Campeonato).WithMany(c => c.Partidas)
                    .HasForeignKey(p => p.CampeonatoId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(p => p.Fase).WithMany()
                    .HasForeignKey(p => p.FaseId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(p => p.ParticipanteCasa).WithMany()
                    .HasForeignKey(p => p.ParticipanteCasaId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(p => p.ParticipanteVisitante).WithMany()
                    .HasForeignKey(p => p.ParticipanteVisitanteId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(p => p.TimeCasaUsado).WithMany()
                    .HasForeignKey(p => p.TimeCasaUsadoId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(p => p.TimeVisitanteUsado).WithMany()
                    .HasForeignKey(p => p.TimeVisitanteUsadoId).OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<EventoPartidaCampeonato>(entity =>
            {
                entity.ToTable("eventospartidacampeonato", t => t.HasCheckConstraint(
                    "ck_eventospartidacampeonato_no_maximo_um_jogador",
                    "num_nonnulls(jogadorid, jogadorproprioid) <= 1"));

                entity.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.NomeSnapshot).HasMaxLength(80);
                entity.HasIndex(e => e.PartidaId);
                // Artilharia histórica de um jogador em todos os campeonatos do usuário.
                entity.HasIndex(e => e.JogadorId);
                entity.HasIndex(e => e.JogadorProprioId);

                entity.HasOne(e => e.Partida).WithMany(p => p.Eventos)
                    .HasForeignKey(e => e.PartidaId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Participante).WithMany()
                    .HasForeignKey(e => e.ParticipanteId).OnDelete(DeleteBehavior.Cascade);
                // O gol continua na súmula (pelo NomeSnapshot) se o cadastro sumir.
                entity.HasOne(e => e.Jogador).WithMany()
                    .HasForeignKey(e => e.JogadorId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.JogadorProprio).WithMany()
                    .HasForeignKey(e => e.JogadorProprioId).OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(e => e.Gol).WithMany()
                    .HasForeignKey(e => e.GolId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
