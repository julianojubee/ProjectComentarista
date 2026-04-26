using System.Threading.Tasks;

namespace ControleFutebolWeb.Services
{
    public interface ITeamImportService
    {
        Task<int> ImportSerieATeamsAsync(CancellationToken cancellationToken = default);
    }
}   