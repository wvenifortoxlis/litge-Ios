using LitGe.Lib.Services;

namespace LitGe.Pages
{
    public partial class Authenticator : ContentPage
    {
        private readonly SessionManagement _session;

        public Authenticator(SessionManagement session)
        {
            InitializeComponent();
            this._session = session;
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            try
            {
                SessionManagement sessionManagement = new();
                SessionManagement.SessionModel? session = await sessionManagement.ReadSessionAsync();

                //List<KeyManagement.KeyNode> keys = await new KeyManagement().KeysAsync();

                // tu saertod ar moidzebna sessia an logout gaaketa

                bool logged = Preferences.Default.Get("logged", false);

                if (!logged || session == null /*|| (session != null) || keys.Count == 0*/)
                {
                    await Navigation.PushAsync(new LoginPage(), true);
                }
                else if (Application.Current != null) // gamoiyeneba ukanaskneli sesia, ExpiryDate shemowmdeba api call gashvebamde
                {
                    //Application.Current.MainPage = new HomePage();

                    await Shell.Current.GoToAsync($"//{nameof(BooksPage)}", true);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert(Title, ex.Message, "Ok");
                //await Shell.Current.GoToAsync($"//{nameof(MainPage)}", true);
                await Navigation.PushAsync(new LoginPage(), true);
            }
        }

        //protected override async void OnNavigatedFrom(NavigatedFromEventArgs args)
        //{
        //    base.OnNavigatedFrom(args);
        //    await Shell.Current.GoToAsync($"//{nameof(MainPage)}", true);
        //}
    }
}