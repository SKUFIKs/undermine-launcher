using System.Threading.Tasks;

namespace UnderMine.Core
{
    public interface ISettingsService
    {
        Task LoadDefaultSettingsAsync(string _gamePath);
    }
}
