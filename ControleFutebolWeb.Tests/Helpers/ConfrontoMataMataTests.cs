using ControleFutebolWeb.Controllers;
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Tests.Helpers;

public class ConfrontoMataMataTests
{
    private static readonly Time A = new() { Id = 1, Nome = "A", Cidade = "" };
    private static readonly Time B = new() { Id = 2, Nome = "B", Cidade = "" };

    private static Jogo J(Time casa, Time fora, int? gc, int? gf, int? pc = null, int? pf = null) => new()
    {
        TimeCasa = casa, TimeCasaId = casa.Id, TimeVisitante = fora, TimeVisitanteId = fora.Id,
        PlacarCasa = gc, PlacarVisitante = gf, PenaltisCasa = pc, PenaltisVisitante = pf
    };

    [Fact]
    public void JogoUnico_DecidePeloPlacar()
    {
        var c = new ConfrontoMataMata { TimeA = A, TimeB = B, JogoIda = J(A, B, 0, 2) };
        Assert.Same(B, c.Vencedor(jogoUnico: true));
    }

    [Fact]
    public void JogoUnico_EmpateVaiParaOsPenaltis()
    {
        var c = new ConfrontoMataMata { TimeA = A, TimeB = B, JogoIda = J(A, B, 1, 1, 5, 4) };
        Assert.Same(A, c.Vencedor(jogoUnico: true));
    }

    [Fact]
    public void JogoUnico_EmpateSemPenaltis_SemVencedor()
    {
        var c = new ConfrontoMataMata { TimeA = A, TimeB = B, JogoIda = J(A, B, 1, 1) };
        Assert.Null(c.Vencedor(jogoUnico: true));
    }

    [Fact]
    public void IdaEVolta_PeloAgregado()
    {
        var c = new ConfrontoMataMata { TimeA = A, TimeB = B, JogoIda = J(A, B, 2, 0), JogoVolta = J(B, A, 1, 0) };
        Assert.Same(A, c.Vencedor(jogoUnico: false));
    }

    [Fact]
    public void IdaEVolta_AgregadoEmpatado_PenaltisDaVolta()
    {
        // Volta: B manda e perde nos pênaltis 3 x 4
        var c = new ConfrontoMataMata { TimeA = A, TimeB = B, JogoIda = J(A, B, 1, 0), JogoVolta = J(B, A, 1, 0, 3, 4) };
        Assert.Same(A, c.Vencedor(jogoUnico: false));
    }

    [Fact]
    public void IdaEVolta_VoltaNaoJogada_SemVencedor()
    {
        var c = new ConfrontoMataMata { TimeA = A, TimeB = B, JogoIda = J(A, B, 3, 0), JogoVolta = J(B, A, null, null) };
        Assert.Null(c.Vencedor(jogoUnico: false));
    }
}
