using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Data
{
    public static class DbInitializer
    {
        public static void Initialize(FutebolContext context)
        {

            if (!context.Nacionalidades.Any())
            {
                var nacionalidades = new Nacionalidade[]
                {
                    new Nacionalidade { Nome = "Brasil" },
                    new Nacionalidade { Nome = "Argentina" },
                    new Nacionalidade { Nome = "Uruguai" },
                    new Nacionalidade { Nome = "Chile" },
                    new Nacionalidade { Nome = "Paraguai" },
                    new Nacionalidade { Nome = "Bolívia" },
                    new Nacionalidade { Nome = "Peru" },
                    new Nacionalidade { Nome = "Equador" },
                    new Nacionalidade { Nome = "Colômbia" },
                    new Nacionalidade { Nome = "Venezuela" },
                    new Nacionalidade { Nome = "Portugal" },
                    new Nacionalidade { Nome = "Espanha" },
                    new Nacionalidade { Nome = "Itália" },
                    new Nacionalidade { Nome = "França" },
                    new Nacionalidade { Nome = "Alemanha" },
                    new Nacionalidade { Nome = "Inglaterra" },
                    new Nacionalidade { Nome = "Holanda" },
                    new Nacionalidade { Nome = "Bélgica" },
                    new Nacionalidade { Nome = "Suíça" },
                    new Nacionalidade { Nome = "Croácia" }
                };
                context.Nacionalidades.AddRange(nacionalidades);
                context.SaveChanges();
            }

            // Formações
            if (!context.Formacoes.Any())
            {
                var formacao433 = new Formacao { Nome = "4-3-3" };
                var formacao442 = new Formacao { Nome = "4-4-2" };
                var formacao352 = new Formacao { Nome = "3-5-2" };

                context.Formacoes.AddRange(formacao433, formacao442, formacao352);
                context.SaveChanges();

                var posicoes = new PosicaoFormacao[]
                {
                    // Goleiro
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Goleiro", PosicaoX = 50, PosicaoY = 5 },

                    // Defesa (4 jogadores)
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Defesa", PosicaoX = 25, PosicaoY = 20 },
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Defesa", PosicaoX = 75, PosicaoY = 20 },
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Defesa", PosicaoX = 40, PosicaoY = 25 },
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Defesa", PosicaoX = 60, PosicaoY = 25 },

                    // Meio‑campo (3 jogadores)
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Meio", PosicaoX = 30, PosicaoY = 45 },
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Meio", PosicaoX = 70, PosicaoY = 45 },
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Meio", PosicaoX = 50, PosicaoY = 50 },

                    // Ataque (3 jogadores)
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Ataque", PosicaoX = 30, PosicaoY = 75 },
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Ataque", PosicaoX = 70, PosicaoY = 75 },
                    new PosicaoFormacao { FormacaoId = formacao433.Id, NomePosicao = "Ataque", PosicaoX = 50, PosicaoY = 85 },
                };

                context.PosicoesFormacao.AddRange(posicoes);
                context.SaveChanges();
            }
        }
    }
}