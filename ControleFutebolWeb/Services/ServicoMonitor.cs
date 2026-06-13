namespace ControleFutebolWeb.Services
{
    public enum EstadoServico { Parado, Rodando, Aguardando }

    public class InfoServico
    {
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
        public EstadoServico Estado { get; set; } = EstadoServico.Parado;
        public DateTime? IniciadoEm { get; set; }
        public DateTime? UltimoCicloEm { get; set; }
        public DateTime? ProximoCicloEm { get; set; }
        public string UltimaAtividade { get; set; } = "";
        public int CiclosCompletos { get; set; }
        public int JogadoresAtualizados { get; set; }
        public int Falhas { get; set; }
    }

    // Singleton que rastreia estado de todos os BackgroundServices
    public class ServicoMonitor
    {
        private readonly Dictionary<string, InfoServico> _servicos = new();
        private readonly object _lock = new();

        public void Registrar(string chave, string nome, string descricao)
        {
            lock (_lock)
            {
                if (!_servicos.ContainsKey(chave))
                    _servicos[chave] = new InfoServico { Nome = nome, Descricao = descricao };
            }
        }

        public void Atualizar(string chave, Action<InfoServico> update)
        {
            lock (_lock)
            {
                if (_servicos.TryGetValue(chave, out var info))
                    update(info);
            }
        }

        public IReadOnlyList<InfoServico> ObterTodos()
        {
            lock (_lock)
                return _servicos.Values.ToList();
        }

        public InfoServico? Obter(string chave)
        {
            lock (_lock)
                return _servicos.TryGetValue(chave, out var v) ? v : null;
        }
    }
}
