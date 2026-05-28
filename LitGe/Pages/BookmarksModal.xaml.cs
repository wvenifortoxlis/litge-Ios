using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LitGe.Lib;
using LitGe.Lib.ResourceManager;
using LitGe.Lib.Services;
//using LLibrary.Guards;

namespace LitGe.Pages
{
    public partial class BookmarksModal : ContentPage
    {
        private BookmarksModalViewModel _viewmodel;
        private Action<int> selectCallback;

        public BookmarksModal(
            string title,
            List<(string chapter, int page)> sarchevi,
            Action<int> selectCallback,
            BookmarkedPage[] bookmarks,
            int highlightedItem = 0
        )
        {
            InitializeComponent();
            _viewmodel = new BookmarksModalViewModel();
            _viewmodel.Title = title;
            
            try 
            {
                _viewmodel.Sarchevi = new ObservableCollection<Bookmark>(
                    sarchevi.Select(x => new Bookmark { Chapter = x.chapter, Page = x.page }).ToList()
                );
                
                if (highlightedItem >= 0 && highlightedItem < _viewmodel.Sarchevi.Count)
                {
                    _viewmodel.Sarchevi[highlightedItem].TextColor = Color.FromArgb("#9FE870");
                }
            } 
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"BookmarksModal Init Error: {ex.Message}");
                // Fallback to empty if something goes wrong
                _viewmodel.Sarchevi = new ObservableCollection<Bookmark>();
            }

            _viewmodel.BookmarkedPages = bookmarks;

            BindingContext = _viewmodel;
            SarcheviList.ItemsSource = _viewmodel.Sarchevi;
            this.selectCallback = selectCallback;
        }

        private async void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
        {
            if (_viewmodel.Switch)
            {
                return;
            }
            await leftBox.TranslateTo(0, 0, 250, Easing.Linear);
            _viewmodel.Switch = true;
        }

        private async void TapGestureRecognizer_Tapped_1(object sender, TappedEventArgs e)
        {
            if (!_viewmodel.Switch)
            {
                return;
            }
            await leftBox.TranslateTo(this.Width - leftBox.Width, 0, 250, Easing.Linear);
            _viewmodel.Switch = false;
        }

        private async void ImageButton_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.Navigation.PopModalAsync();
        }

        private async void SarcheviList_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem == null)
            {
                return;
            }
            int page = ((Bookmark)e.SelectedItem).Page;

            ((ListView)sender).SelectedItem = null;

            if (selectCallback != null)
            {
                await Navigation.PopModalAsync();
                selectCallback.Invoke(page - 1);
            }
            else
            {
                await Navigation.PopModalAsync();
            }
        }

        private void ImageButton_Clicked_1(object sender, EventArgs e)
        {
            if (sender is not ImageButton garbageBtn)
                return;

            if (garbageBtn.BindingContext is not BookmarkedPage item)
                return;

            /*Guard
                .AgainstNull(*/Provider.GetService<ResourceManager>()/*)*/
                .OnBookmarkDeleted(
                    this,
                    new ResourceManager.BookmarkDeleteEventArgs(_viewmodel.Title, item.Page)
                );

            _viewmodel.BookmarkedPages = _viewmodel
                .BookmarkedPages.Where(x => x.Page != item.Page)
                .ToArray();
        }

        private async void TapGestureRecognizer_Tapped_2(object sender, TappedEventArgs e)
        {
            if (sender is not Label label)
                return;
            if (label.BindingContext is not BookmarkedPage item)
                return;

            await Navigation.PopModalAsync();
            selectCallback?.Invoke(item.Page - 1);
        }
    }

    public class BookmarksModalViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool _switch = true;

        public ObservableCollection<Bookmark> Sarchevi = [];

        private BookmarkedPage[] _bookmarkeds = [];
        public BookmarkedPage[] BookmarkedPages
        {
            get => _bookmarkeds;
            set
            {
                _bookmarkeds = value;
                OnPropertyChanged();
            }
        }

        private string _title = "";

        public string Title
        {
            get => _title;
            set => _title = value;
        }

        public bool Switch
        {
            get => _switch;
            set
            {
                if (_switch != value)
                {
                    _switch = value;
                    OnPropertyChanged();
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    public class BookmarkedPage : INotifyPropertyChanged
    {
        private string _pageText;
        private string _chapterTitle;

        public BookmarkedPage(string chapterTitle, string text, string pageText, int page)
        {
            _chapterTitle = chapterTitle;
            _text = text;
            _pageText = pageText;

            Page = page;
        }

        private string _text;
        public string PageText
        {
            get => _pageText;
            set
            {
                _pageText = value;
                OnPropertyChanged();
            }
        }
        public string ChapterTitle
        {
            get => _chapterTitle;
            set
            {
                _chapterTitle = value;
                OnPropertyChanged();
            }
        }
        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                OnPropertyChanged();
            }
        }
        public int Page { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    public class Bookmark : INotifyPropertyChanged
    {
        private string _chapter = "";
        private Color _textColor = Color.FromArgb("#EDEFEB");

        public string Chapter
        {
            get => _chapter;
            set
            {
                _chapter = value;
                OnPropertyChanged();
            }
        }

        public int Page { get; set; }

        public Color TextColor
        {
            get => _textColor;
            set
            {
                _textColor = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
