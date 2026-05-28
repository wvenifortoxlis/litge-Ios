using System.Collections.ObjectModel;

namespace LitGe
{
    public class LibraryItem
    {
        private int _id;
        private string _title;
        private string _author;
        private string _publisher;
        private string _language;
        private short _version;

        //private string[] _categories;
        private string _categories;
        private DateTime _expireDate;
        private DateTime _insertDate;
        private DateTime _updateDate;
        private DateTime _buyout;
        private int _volume;
        private int _size;
        private string _description;
        private byte[] _coverImage;

        private readonly ObservableCollection<Chapter> _chapters;
        private bool _isDownloaded;
        private byte _downloadProgress;
        private bool _isDownloading;
        private DateTime _openDate;
        private int _lastChapterId;
        private int _lastPageNumber;
        private int _readProgress;
        private int _currentProgress;

        /// <summary>
        /// Gets or sets object id.
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
        /// Gets or sets the title.
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
        /// Gets or sets the author.
        /// </summary>
        public string Author
        {
            get { return _author; }
            set
            {
                _author = value;
                //RaisePropertyChanged(() => Author);
            }
        }

        /// <summary>
        /// Gets or sets the publisher.
        /// </summary>
        public string Publisher
        {
            get { return _publisher; }
            set
            {
                _publisher = value;
                //RaisePropertyChanged(() => Publisher);
            }
        }

        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        /*
        public string Language
        {
            get { return _language; }
            set
            {
                _language = value;
                RaisePropertyChanged(() => Language);
            }
        }
        */
        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        public short Version
        {
            get { return _version; }
            set
            {
                _version = value;
                //RaisePropertyChanged(() => Version);
            }
        }

        /// <summary>
        /// Gets or sets product categories.
        /// </summary>
        //public string[] Categories
        /*
        public string Categories
        {
            get { return _categories; }
            set
            {
                _categories = value;
                RaisePropertyChanged(() => Categories);
            }
        }
        */

        /// <summary>
        /// Gets or sets the expire date.
        /// </summary>
        public DateTime ExpireDate
        {
            get { return _expireDate; }
            set
            {
                _expireDate = value;
                //RaisePropertyChanged(() => ExpireDate);
            }
        }

        /// <summary>
        /// Gets or sets the insert date.
        /// </summary>
        public DateTime InsertDate
        {
            get { return _insertDate; }
            set { _insertDate = value; }
        }

        /// <summary>
        /// Gets or sets the update date.
        /// </summary>
        public DateTime UpdateDate
        {
            get { return _updateDate; }
            set { _updateDate = value; }
        }

        /// <summary>
        /// Gets or sets the buyout.
        /// </summary>
        public DateTime Buyout
        {
            get { return _buyout; }
            set { _buyout = value; }
        }

        /// <summary>
        /// Gets or sets the open date.
        /// </summary>
        public DateTime OpenDate
        {
            get { return _openDate; }
            set { _openDate = value; }
        }

        /// <summary>
        /// Gets or sets the volume.
        /// </summary>
        public int Volume
        {
            get { return _volume; }
            set
            {
                _volume = value;
                //RaisePropertyChanged(() => Volume);
            }
        }

        /// <summary>
        /// Gets or sets product size.
        /// </summary>
        public int Size
        {
            get { return _size; }
            set
            {
                _size = value;
                //RaisePropertyChanged(() => Size);
            }
        }

        /// <summary>
        /// Gets or sets last page number.
        /// </summary>
        public int LastPageNumber
        {
            get { return _lastPageNumber; }
            set { _lastPageNumber = value; }
        }

        /// <summary>
        /// Gets or sets the last chapter id.
        /// </summary>
        public int LastChapterId
        {
            get { return _lastChapterId; }
            set { _lastChapterId = value; }
        }

        public int ReadProgress
        {
            get { return _readProgress; }
            set
            {
                _readProgress = value;
                //RaisePropertyChanged(() => ReadProgress);
            }
        }

        public int CurrentProgress
        {
            get { return _currentProgress; }
            set
            {
                _currentProgress = value;
                //RaisePropertyChanged(() => CurrentProgress);
            }
        }

        /// <summary>
        /// Gets or sets product description.
        /// </summary>
        /*
        public string Description
        {
            get { return _description; }
            set
            {
                _description = value;
                RaisePropertyChanged(() => Description);
            }
        }
        */

        /// <summary>
        /// Gets or sets cover image.
        /// </summary>
        public byte[] CoverImage
        {
            get { return _coverImage; }
            set
            {
                _coverImage = value;
                //RaisePropertyChanged(() => CoverImage);
            }
        }

        /// <summary>
        /// Gets library item chapters.
        /// </summary>
        public ObservableCollection<Chapter> Chapters
        {
            get { return _chapters; }
        }

        public LibraryItem()
        {
            _chapters = new ObservableCollection<Chapter>();
        }
    }
}
