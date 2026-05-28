using LitGe.Lib.Services;
using Newtonsoft.Json;

namespace LitGe.Pages
{
    public partial class ReaderPage : ContentPage
    {
        private bool _isOpeningBook = false;

        public ReaderPage()
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
                emptyLabel.IsVisible = true;
            }
            else
            {
                _isOpeningBook = true;
                emptyLabel.IsVisible = false;
                ModalReader modal = new ModalReader(book, true);

                await Shell.Current.Navigation.PushModalAsync(modal, true);
                _isOpeningBook = false;
            }
        }
    }
}
