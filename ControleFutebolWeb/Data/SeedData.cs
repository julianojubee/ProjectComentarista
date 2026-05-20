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

            // Formação 4-2-3-1
            var formacao4231 = new Formacao
            {
                Nome = "4-2-3-1",
                Posicoes = new List<PosicaoFormacao>
                {
                    new PosicaoFormacao { NomePosicao = "Goleiro", PosicaoX = 45, PosicaoY = 85, Ordem = 1 },
                    new PosicaoFormacao { NomePosicao = "Lateral Esquerdo", PosicaoX = 20, PosicaoY = 70, Ordem = 2 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 1", PosicaoX = 35, PosicaoY = 70, Ordem = 3 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 2", PosicaoX = 55, PosicaoY = 70, Ordem = 4 },
                    new PosicaoFormacao { NomePosicao = "Lateral Direito", PosicaoX = 70, PosicaoY = 70, Ordem = 5 },
                    new PosicaoFormacao { NomePosicao = "Volante 1", PosicaoX = 35, PosicaoY = 55, Ordem = 6 },
                    new PosicaoFormacao { NomePosicao = "Volante 2", PosicaoX = 55, PosicaoY = 55, Ordem = 7 },
                    new PosicaoFormacao { NomePosicao = "Meia Ofensivo Esquerdo", PosicaoX = 25, PosicaoY = 40, Ordem = 8 },
                    new PosicaoFormacao { NomePosicao = "Meia Central", PosicaoX = 45, PosicaoY = 40, Ordem = 9 },
                    new PosicaoFormacao { NomePosicao = "Meia Ofensivo Direito", PosicaoX = 65, PosicaoY = 40, Ordem = 10 },
                    new PosicaoFormacao { NomePosicao = "Atacante", PosicaoX = 45, PosicaoY = 20, Ordem = 11 }
                }
            };

            // Formação 4-5-1
            var formacao451 = new Formacao
            {
                Nome = "4-5-1",
                Posicoes = new List<PosicaoFormacao>
                {
                    new PosicaoFormacao { NomePosicao = "Goleiro", PosicaoX = 45, PosicaoY = 85, Ordem = 1 },
                    new PosicaoFormacao { NomePosicao = "Lateral Esquerdo", PosicaoX = 20, PosicaoY = 70, Ordem = 2 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 1", PosicaoX = 35, PosicaoY = 70, Ordem = 3 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 2", PosicaoX = 55, PosicaoY = 70, Ordem = 4 },
                    new PosicaoFormacao { NomePosicao = "Lateral Direito", PosicaoX = 70, PosicaoY = 70, Ordem = 5 },
                    new PosicaoFormacao { NomePosicao = "Meia 1", PosicaoX = 25, PosicaoY = 55, Ordem = 6 },
                    new PosicaoFormacao { NomePosicao = "Meia 2", PosicaoX = 35, PosicaoY = 50, Ordem = 7 },
                    new PosicaoFormacao { NomePosicao = "Meia 3", PosicaoX = 55, PosicaoY = 50, Ordem = 8 },
                    new PosicaoFormacao { NomePosicao = "Meia 4", PosicaoX = 65, PosicaoY = 55, Ordem = 9 },
                    new PosicaoFormacao { NomePosicao = "Meia 5", PosicaoX = 45, PosicaoY = 40, Ordem = 10 },
                    new PosicaoFormacao { NomePosicao = "Atacante", PosicaoX = 45, PosicaoY = 20, Ordem = 11 }
                }
            };

            // Formação 3-4-1-2
            var formacao3412 = new Formacao
            {
                Nome = "3-4-1-2",
                Posicoes = new List<PosicaoFormacao>
                {
                    new PosicaoFormacao { NomePosicao = "Goleiro", PosicaoX = 45, PosicaoY = 85, Ordem = 1 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 1", PosicaoX = 30, PosicaoY = 70, Ordem = 2 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 2", PosicaoX = 45, PosicaoY = 70, Ordem = 3 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 3", PosicaoX = 60, PosicaoY = 70, Ordem = 4 },
                    new PosicaoFormacao { NomePosicao = "Meia 1", PosicaoX = 25, PosicaoY = 55, Ordem = 5 },
                    new PosicaoFormacao { NomePosicao = "Meia 2", PosicaoX = 65, PosicaoY = 55, Ordem = 6 },
                    new PosicaoFormacao { NomePosicao = "Meia 3", PosicaoX = 40, PosicaoY = 50, Ordem = 7 },
                    new PosicaoFormacao { NomePosicao = "Meia 4", PosicaoX = 55, PosicaoY = 50, Ordem = 8 },
                    new PosicaoFormacao { NomePosicao = "Meia Ofensivo", PosicaoX = 45, PosicaoY = 35, Ordem = 9 },
                    new PosicaoFormacao { NomePosicao = "Atacante 1", PosicaoX = 35, PosicaoY = 20, Ordem = 10 },
                    new PosicaoFormacao { NomePosicao = "Atacante 2", PosicaoX = 55, PosicaoY = 20, Ordem = 11 }
                }
            };

            // Formação 4-4-1-1
            var formacao4411 = new Formacao
            {
                Nome = "4-4-1-1",
                Posicoes = new List<PosicaoFormacao>
                {
                    new PosicaoFormacao { NomePosicao = "Goleiro", PosicaoX = 45, PosicaoY = 85, Ordem = 1 },
                    new PosicaoFormacao { NomePosicao = "Lateral Esquerdo", PosicaoX = 20, PosicaoY = 70, Ordem = 2 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 1", PosicaoX = 35, PosicaoY = 70, Ordem = 3 },
                    new PosicaoFormacao { NomePosicao = "Zagueiro 2", PosicaoX = 55, PosicaoY = 70, Ordem = 4 },
                    new PosicaoFormacao { NomePosicao = "Lateral Direito", PosicaoX = 70, PosicaoY = 70, Ordem = 5 },
                    new PosicaoFormacao { NomePosicao = "Meia 1", PosicaoX = 25, PosicaoY = 55, Ordem = 6 },
                    new PosicaoFormacao { NomePosicao = "Meia 2", PosicaoX = 40, PosicaoY = 55, Ordem = 7 },
                    new PosicaoFormacao { NomePosicao = "Meia 3", PosicaoX = 55, PosicaoY = 55, Ordem = 8 },
                    new PosicaoFormacao { NomePosicao = "Meia 4", PosicaoX = 70, PosicaoY = 55, Ordem = 9 },
                    new PosicaoFormacao { NomePosicao = "Meia Ofensivo", PosicaoX = 45, PosicaoY = 40, Ordem = 10 },
                    new PosicaoFormacao { NomePosicao = "Atacante", PosicaoX = 45, PosicaoY = 20, Ordem = 11 }
                }
            };

            context.Formacoes.AddRange(formacao4231, formacao451, formacao3412, formacao4411);
            context.SaveChanges();
            }
    }
}