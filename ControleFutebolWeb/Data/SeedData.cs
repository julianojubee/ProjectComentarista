using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

public static class SeedData
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using (var context = new FutebolContext(
            serviceProvider.GetRequiredService<DbContextOptions<FutebolContext>>()))
        {
            if (context.Formacoes.Any())
            {
                return; // já populado
            }

            // Formação 4-3-3
            var formacao433 = new Formacao
            {
                Nome = "4-3-3",
                Posicoes = new List<PosicaoFormacao>
                {
                    new PosicaoFormacao { NomePosicao = "Goleiro", PosicaoX = 45, PosicaoY = 85, Ordem = 1 },
                    new PosicaoFormacao { NomePosicao = "Lateral Esquerdo", PosicaoX = 20, PosicaoY = 70, Ordem = 2 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 1", PosicaoX = 35, PosicaoY = 70, Ordem = 3 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 2", PosicaoX = 55, PosicaoY = 70, Ordem = 4 },
                    new PosicaoFormacao { NomePosicao = "Lateral Direito", PosicaoX = 70, PosicaoY = 70, Ordem = 5 },
                    new PosicaoFormacao { NomePosicao = "Meia 1", PosicaoX = 30, PosicaoY = 50, Ordem = 6 },
                    new PosicaoFormacao { NomePosicao = "Meia 2", PosicaoX = 45, PosicaoY = 50, Ordem = 7 },
                    new PosicaoFormacao { NomePosicao = "Meia 3", PosicaoX = 60, PosicaoY = 50, Ordem = 8 },
                    new PosicaoFormacao { NomePosicao = "Atacante 1", PosicaoX = 25, PosicaoY = 25, Ordem = 9 },
                    new PosicaoFormacao { NomePosicao = "Atacante 2", PosicaoX = 45, PosicaoY = 25, Ordem = 10 },
                    new PosicaoFormacao { NomePosicao = "Atacante 3", PosicaoX = 65, PosicaoY = 25, Ordem = 11 }
                }
            };

            // Formação 4-4-2
            var formacao442 = new Formacao
            {
                Nome = "4-4-2",
                Posicoes = new List<PosicaoFormacao>
                {
                    new PosicaoFormacao { NomePosicao = "Goleiro", PosicaoX = 45, PosicaoY = 85, Ordem = 1 },
                    new PosicaoFormacao { NomePosicao = "Lateral Esquerdo", PosicaoX = 20, PosicaoY = 70, Ordem = 2 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 1", PosicaoX = 35, PosicaoY = 70, Ordem = 3 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 2", PosicaoX = 55, PosicaoY = 70, Ordem = 4 },
                    new PosicaoFormacao { NomePosicao = "Lateral Direito", PosicaoX = 70, PosicaoY = 70, Ordem = 5 },
                    new PosicaoFormacao { NomePosicao = "Meia 1", PosicaoX = 25, PosicaoY = 50, Ordem = 6 },
                    new PosicaoFormacao { NomePosicao = "Meia 2", PosicaoX = 40, PosicaoY = 50, Ordem = 7 },
                    new PosicaoFormacao { NomePosicao = "Meia 3", PosicaoX = 55, PosicaoY = 50, Ordem = 8 },
                    new PosicaoFormacao { NomePosicao = "Meia 4", PosicaoX = 70, PosicaoY = 50, Ordem = 9 },
                    new PosicaoFormacao { NomePosicao = "Atacante 1", PosicaoX = 35, PosicaoY = 25, Ordem = 10 },
                    new PosicaoFormacao { NomePosicao = "Atacante 2", PosicaoX = 55, PosicaoY = 25, Ordem = 11 }
                }
            };

            // Formação 3-5-2
            var formacao352 = new Formacao
            {
                Nome = "3-5-2",
                Posicoes = new List<PosicaoFormacao>
                {
                    new PosicaoFormacao { NomePosicao = "Goleiro", PosicaoX = 45, PosicaoY = 85, Ordem = 1 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 1", PosicaoX = 30, PosicaoY = 70, Ordem = 2 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 2", PosicaoX = 45, PosicaoY = 70, Ordem = 3 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 3", PosicaoX = 60, PosicaoY = 70, Ordem = 4 },
                    new PosicaoFormacao { NomePosicao = "Meia 1", PosicaoX = 20, PosicaoY = 50, Ordem = 5 },
                    new PosicaoFormacao { NomePosicao = "Meia 2", PosicaoX = 35, PosicaoY = 50, Ordem = 6 },
                    new PosicaoFormacao { NomePosicao = "Meia 3", PosicaoX = 50, PosicaoY = 50, Ordem = 7 },
                    new PosicaoFormacao { NomePosicao = "Meia 4", PosicaoX = 65, PosicaoY = 50, Ordem = 8 },
                    new PosicaoFormacao { NomePosicao = "Meia 5", PosicaoX = 45, PosicaoY = 35, Ordem = 9 },
                    new PosicaoFormacao { NomePosicao = "Atacante 1", PosicaoX = 35, PosicaoY = 20, Ordem = 10 },
                    new PosicaoFormacao { NomePosicao = "Atacante 2", PosicaoX = 55, PosicaoY = 20, Ordem = 11 }
                }
            };

            context.Formacoes.AddRange(formacao433, formacao442, formacao352);
            context.SaveChanges();
        }
    }
}