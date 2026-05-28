using LitGe.Lib.Services;
using Newtonsoft.Json;

namespace LitGe.Pages
{
    public partial class NoInternetRealm : ContentPage
    {
        private bool _isOpeningBook = false;

        public NoInternetRealm()
        {
            InitializeComponent();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_isOpeningBook) return;

            string bookString = Preferences.Default.Get(nameof(BookItem), "");
            BookItem? book = JsonConvert.DeserializeObject<BookItem>(bookString);

            if (book == null)
            {
                noInternet.IsVisible = true;
            }
            else
            {
                _isOpeningBook = true;
                noInternet.IsVisible = false;
                ModalReader modal = new ModalReader(book, true, true);

                await Shell.Current.Navigation.PushModalAsync(modal, true);
                _isOpeningBook = false;
            }
        }
    }
}