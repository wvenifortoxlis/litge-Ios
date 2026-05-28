using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using LitGe.Lib.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LitGe.Pages
{
    public partial class ProfilePage : ContentPage
    {
        private readonly ProfilePageViewModel _viewModel;

        public ProfilePage()
        {
            InitializeComponent();
            _viewModel = new ProfilePageViewModel();
            BindingContext = _viewModel;
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            _viewModel.Session = await new SessionManagement().ReadSessionAsync();
        }
    }

    public class ProfilePageViewModel : INotifyPropertyChanged
    {
        private SessionManagement.SessionModel? _session;

        public SessionManagement.SessionModel? Session
        {
            get => _session;
            set
            {
                _session = value;
                NotifyPropertyChanged();
            }
        }

        public Command LogoutCommand => new Command(async () =>
        {
            new SessionManagement().DeleteSession();
            Preferences.Default.Set("logged", false);
            await Shell.Current.GoToAsync($"//{nameof(MainPage)}");
        });

        public Command ThemeCommand => new Command(async () =>
        {
            IToast toast = Toast.Make("დაელოდეთ განახლებას", ToastDuration.Short);
            await toast.Show();
        });

        public Command PersonalInformationCommand => new Command(async () =>
        {
            IToast toast = Toast.Make("დაელოდეთ განახლებას", ToastDuration.Short);
            await toast.Show();
        });

        public Command ChangePasswordCommand => new Command(async () =>
        {
            IToast toast = Toast.Make("დაელოდეთ განახლებას", ToastDuration.Short);
            await toast.Show();
        });

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}