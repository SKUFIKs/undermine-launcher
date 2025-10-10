using System.Collections.ObjectModel;
using System.ComponentModel;

namespace UnderMine.Fonts;

public class MenuWindowViewModel : INotifyPropertyChanged
{
    private Server? _selectedServer;

    public Server? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (_selectedServer != value)
            {
                _selectedServer = value;
                OnPropertyChanged(nameof(SelectedServer));
            }
        }
    }

    public ObservableCollection<Server> Servers { get; } =
        new ObservableCollection<Server>
        {
            new Server { Name= "TechnoMagic", ID = 1 }
        };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
public class Server
{
    public string Name { get; set; }
    public int ID { get; set; }
}