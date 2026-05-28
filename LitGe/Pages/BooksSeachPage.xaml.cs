using System.ComponentModel;
using System.Net.Quic;
using System.Runtime.CompilerServices;
using System.Timers;
using System.Windows.Input;
using LitGe.Lib.Services;
using SharedLib;
using Timer = System.Timers.Timer;

namespace LitGe.Pages
{
    public partial class BooksSeachPage : ContentPage
    {
        private readonly BooksSearchViewModel _viewModel;
        private bool _isOpeningBook = false;

        private System.Timers.Timer? _searchTimer;

        public BooksSeachPage()
        {
            InitializeComponent();
            _viewModel = new BooksSearchViewModel();
            BindingContext = _viewModel;
        }

        private void SearchBar_SearchButtonPressed(object sender, EventArgs e)
        {
            //if (SearchBar.Text != "")
            //    _viewModel.FilterList(SearchBar.Text);
        }

        private async void Back_Clicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void ListView_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is not BookItem book || _isOpeningBook) return;
            _isOpeningBook = true;
            
            try
            {
                string currentUser = Preferences.Default.Get("CurrentUser", "");
                StateMachine<BookItem> stateMachine = new StateMachine<BookItem>(
                    Path.Combine(FileSystem.Current.AppDataDirectory, $"{currentUser}.db")
                );
                
                book.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Proactive deduplication: check if already in DB before inserting
                var existingBooks = await stateMachine.RetrieveAsync();
                var found = existingBooks.FirstOrDefault(b => 
                    (string.IsNullOrEmpty(b.ProductId) && b.Title == book.Title) || 
                    (!string.IsNullOrEmpty(b.ProductId) && b.ProductId == book.ProductId));

                if (found != null)
                {
                    // Update existing record with new timestamp/metadata
                    await stateMachine.UpdateAsync(book);
                }
                else
                {
                    await stateMachine.InsertAsync(book);
                }

                await Navigation.PushModalAsync(new ModalReader(book), true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening book from search: {ex.Message}");
            }
            finally
            {
                _isOpeningBook = false;
            }
        }

        private void Entry_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (e.NewTextValue.Length >= 3)
            {
                _searchTimer = new Timer();
                _searchTimer.Interval = 300;
                _searchTimer.AutoReset = false;
                _searchTimer.Elapsed += (s, a) =>
                {
                    _viewModel.FilterList(e.NewTextValue.Trim());
                };
                _searchTimer.Enabled = true;
            }
        }
    }

    public class BooksSearchViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void NotifyPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public ICommand PerformSearch =>
            new Command<string>(
                (string query) =>
                {
                    if (query != "")
                    {
                        FilterList(query);
                    }
                }
            );

        public List<BookItem> Books
        {
            get => _books;
            set
            {
                _books.Clear();
                _books = value;
                NotifyPropertyChanged();
            }
        }

        public void FilterList(string query)
        {
            List<BookItem> books = bm.FilterByTitle(query);
            foreach (BookItem book in books)
            {
                book.UpdateFormattedTitle(query);
            }
            Books = books;
        }

        private List<BookItem> _books = [];
        private readonly BookManagement bm = new BookManagement();
    }
}
