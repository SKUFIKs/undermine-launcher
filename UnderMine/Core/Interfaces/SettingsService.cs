using System.Threading.Tasks;

namespace UnderMine.Core.Interfaces
{
    public interface ISettingsService
    {
        Task LoadDefaultSettingsAsync(string serverFolderName);
    }
}
