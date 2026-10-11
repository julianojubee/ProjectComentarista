namespace ControleFutebolWeb.Helpers
{
    // Tempo de uma passagem (treinador num clube) em linguagem de gente:
    // "2 anos e 3 meses" em vez de "821 dia(s)".
    public static class DuracaoHelper
    {
        // Meses completos entre as duas datas (o dia do mês conta: 15/01 a 14/02 ainda é 0).
        public static int MesesCompletos(DateTime inicio, DateTime fim)
        {
            if (fim <= inicio) return 0;
            var meses = (fim.Year - inicio.Year) * 12 + fim.Month - inicio.Month;
            if (fim.Day < inicio.Day) meses--;
            return Math.Max(0, meses);
        }

        public static string Formatar(int mesesTotais)
        {
            if (mesesTotais < 1) return "menos de 1 mês";
            int anos = mesesTotais / 12, meses = mesesTotais % 12;
            string Anos() => anos == 1 ? "1 ano" : $"{anos} anos";
            string Meses() => meses == 1 ? "1 mês" : $"{meses} meses";
            if (anos == 0) return Meses();
            if (meses == 0) return Anos();
            return $"{Anos()} e {Meses()}";
        }

        // Passagem em aberto (fim null) conta até "hoje".
        public static string Formatar(DateTime inicio, DateTime? fim, DateTime hoje) =>
            Formatar(MesesCompletos(inicio, fim ?? hoje));
    }
}
