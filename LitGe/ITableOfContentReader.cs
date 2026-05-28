using System.Collections.ObjectModel;

namespace LitGe
{
    public interface ITableOfContentReader
    {
        /// <summary>
        /// Parses epub table of content.
        /// </summary>
        /// <param name="stream">The stream containing TOC buffer.</param>
        /// <returns>A list of parsed TOCs.</returns>
        IEnumerable<Chapter> Parse(Stream stream);
    }

    public class Chapter
    {
        private int _id;
        private string _content;
        private string _title;
        private int _playOrder;
        private LibraryItem _libraryItem;
        private Chapter _parent;
        private string _source;
        private readonly ObservableCollection<Chapter> _chapters;
        private bool _isSelected;

        /// <summary>
        /// Gets or sets associated library item.
        /// </summary>
        public LibraryItem LibraryItem
        {
            get { return _libraryItem; }
            set
            {
                _libraryItem = value;
                //RaisePropertyChanged(() => LibraryItem);
            }
        }

        /// <summary>
        /// Gets or sets the parent chapter.
        /// </summary>
        public Chapter Parent
        {
            get { return _parent; }
            set
            {
                _parent = value;
                //RaisePropertyChanged(() => Parent);
            }
        }

        /// <summary>
        /// Gets sub chapters.
        /// </summary>
        public ObservableCollection<Chapter> Chapters
        {
            get { return _chapters; }
        }

        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public int Id
        {
            get { return _id; }
            set
            {
                _id = value;
                //RaisePropertyChanged(() => Id);
            }
        }

        /// <summary>
        /// Gets or sets chapter content.
        /// </summary>
        public string Content
        {
            get { return _content; }
            set
            {
                _content = value;
                //RaisePropertyChanged(() => Content);
            }
        }

        /// <summary>
        /// Gets or sets chapter title.
        /// </summary>
        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                //RaisePropertyChanged(() => Title);
            }
        }

        /// <summary>
        /// Gets or sets chapter's source file name.
        /// </summary>
        public string Source
        {
            get { return _source; }
            set
            {
                _source = value;
                //RaisePropertyChanged(() => Source);
            }
        }

        /// <summary>
        /// Gets or sets chapter's play order.
        /// </summary>
        public int PlayOrder
        {
            get { return _playOrder; }
            set
            {
                _playOrder = value;
                //RaisePropertyChanged(() => PlayOrder);
            }
        }

        /// <summary>
        /// Helper property.
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                //RaisePropertyChanged(() => IsSelected);
            }
        }

        /// <summary>
        /// Gets or sets the last page.
        /// </summary>
        public int LastPage { get; set; }

        /// <summary>
        /// Gets or sets the pages count.
        /// </summary>
        public int Count { get; set; }

        public Chapter()
        {
            _chapters = new ObservableCollection<Chapter>();
        }

        public int Level { get; set; }
    }
}
