using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using SharedLib.Interfaces;
using SQLite;
using Color = Microsoft.Maui.Graphics.Color;
using Encoding = System.Text.Encoding;

namespace LitGe.Lib.Services
{
    public class BookManagement
    {
        private static string SafeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "_";

            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (invalid.Contains(c) || c == '/' || c == '\\' || c == ':' || c == '*'
                    || c == '?' || c == '"' || c == '<' || c == '>' || c == '|')
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        public BookManagement()
        {
            Directory.CreateDirectory(_root);

            Directory.CreateDirectory(_booksFolder);

            if (!File.Exists(_libStateFile))
            {
                File.Create(_libStateFile).Dispose();
            }

            Directory.CreateDirectory(_audiosFolder);

            if (BookStateChanged != null)
                return;
            BookStateChanged += this.BookManagement_BookStateChanged;
        }

        public void ChangeBookState(BookStateEventArgs newState)
        {
            BookStateChanged?.Invoke(this, newState);
        }

        /// <summary>
        ///     აუდიოების ფოლდერში წიგნის სათაურის ქვეშ შენახული მპ3 ბინარები
        ///
        ///     ეს აპის დაბრუნებული პასუხია
        /// "294326[split]294332;294333;294334;294335;294336;294337;294338;294339;294340;294341;294342;294343;294344;294345;294346;294347;294348;294349;294350;294351;294352;"
        /// </summary>
        /// <param name="title">წიგნის სათაური ფოლდერის სახელია</param>
        /// <returns>უკვე ჩამოტვირთულ ბინარებს</returns>
        public string?[] AvailableMp3Content(string title)
        {
            byte[] titleBytes = Encoding.UTF8.GetBytes(title);

            string encodedTitle = Convert.ToBase64String(titleBytes);
            string path = Path.Combine(_audiosFolder, encodedTitle);
            return Directory.Exists(path) ? Directory.GetFiles(path) : Array.Empty<string>();
        }

        /// <summary>
        ///     შეინახავს ჩამოტვირთულ აუდიოს ბინარებს მისივე აიდის მიხედვით
        /// </summary>
        /// <param name="bookTitle"></param>
        /// <param name="content"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<string?> SaveMp3Async(string bookTitle, byte[] content, string? id)
        {
            byte[] titleBytes = Encoding.UTF8.GetBytes(bookTitle);
            string encodedTitle = Convert.ToBase64String(titleBytes);

            string bookFolder = Path.Combine(_audiosFolder, encodedTitle);
            string? mp3Path = Path.Combine(bookFolder, $"{id}.mp3");

            Directory.CreateDirectory(bookFolder);
            await File.WriteAllBytesAsync(mp3Path, content);

            return mp3Path;
        }

        /// <summary>
        ///     წიგნის სათაური + აიდის მიხედვით ეძებს ბინარებს, თუ ვერიპოვა ცარიელი მასივი ბრუნდება
        /// </summary>
        /// <param name="bookTitle"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<byte[]> GetMp3Content(string bookTitle, string id)
        {
            byte[] titleBytes = Encoding.UTF8.GetBytes(bookTitle);
            string encodedTitle = Convert.ToBase64String(titleBytes);

            string mp3Path = Path.Combine(_audiosFolder, encodedTitle, $"{id}.mp3");
            if (!File.Exists(mp3Path))
                return Array.Empty<byte>();

            return await File.ReadAllBytesAsync(mp3Path);
        }

        public BookStateManager BookState()
        {
            lock (_stateFileLock)
            {
                try
                {
                    JsonSerializer jsonSerializer = new JsonSerializer();
                    using StreamReader streamReader = new StreamReader(_libStateFile);
                    using JsonReader jsonReader = new JsonTextReader(streamReader);
                    BookStateManager bookStateManager =
                        jsonSerializer.Deserialize<BookStateManager>(jsonReader)
                        ?? new BookStateManager();
                    return bookStateManager;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading book state: {ex.Message}");
                    return new BookStateManager();
                }
            }
        }

        public BookStateManager BookState(string title)
        {
            BookStateManager bookStateManager = BookState();
            BookState? bs = bookStateManager.Books?.FirstOrDefault(b => b.Title == title);
            if (bs == null)
            {
                bs = new BookState { Title = title };
                ChangeBookState(new BookStateEventArgs(bs));
            }

            bookStateManager.Books = new List<BookState> { bs };
            return bookStateManager;
        }

        public async Task<List<BookItem>> BookItemsAsync()
        {
            _cachedBooks.Clear();
            //if (_cachedBooks.Count == 0)
            //{
            await FetchAndCacheBooksAsync();
            //}
            //List<BookItem> chunkedBooks = _cachedBooks.Skip(_skip).Take(_take).ToList();
            //_skip += _take;

            //if (_skip >= _cachedBooks.Count)
            //{
            //    _skip = 0;
            //}

            //return chunkedBooks;

            return _cachedBooks;
        }

        public List<BookItem> FilterByTitle(string query)
        {
            string[] queryWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return (
                from book in _cachedBooks.Where(x => x is { Title: not null, Author: not null })
                let titleWords = book.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                let authorWords = book.Author.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                let titleMatch = IsMatch(queryWords, titleWords)
                let authorMatch = IsMatch(queryWords, authorWords)
                where titleMatch || authorMatch
                select book
            ).ToList();

            bool IsMatch(string[] words, string[] target)
            {
                int firstIndex = Array.FindIndex(
                    target,
                    t => t.StartsWith(words[0], StringComparison.OrdinalIgnoreCase)
                );
                if (firstIndex == -1)
                {
                    return false;
                }

                return !words
                    .Where(
                        (t, i) =>
                            firstIndex + i >= target.Length
                            || !target[firstIndex + i]
                                .StartsWith(t, StringComparison.OrdinalIgnoreCase)
                    )
                    .Any();
            }
        }

        /// <summary>
        ///     წიგნის წასაკითხვ ნაკადს აბრუნებს, თუ აარ არსებობს წიგნი გადმოიწერს
        /// </summary>
        /// <param name="bookItem"></param>
        /// <returns></returns>
        public async Task<FileStream> BookReadStreamAsync(
            BookItem bookItem,
            KeyManagement.KeyNode keyNode,
            SessionManagement.SessionModel sessionModel
        )
        {
            string userDir = Path.Combine(_booksFolder, keyNode.DeviceId);

            // iOS filesystem is stricter about filename characters; keep it safe everywhere.
            string safeTitle = SafeFileName(bookItem.Title ?? "");
            string lookingForBook = $"{safeTitle}{bookItem.ProductId}.epub";
            // Backward compatibility: older builds used raw title.
            string legacyLookingForBook = $"{bookItem.Title}{bookItem.ProductId}.epub";

            if (Directory.Exists(userDir))
            {
                string[] books = Directory.GetFiles(userDir);
                string? hit = books.FirstOrDefault(book =>
                    book.Contains(lookingForBook) || book.Contains(legacyLookingForBook)
                );
                if (hit != null)
                {
                    // If legacy exists, prefer it to avoid forcing a redownload.
                    string accessPath = hit;
                    Console.WriteLine($"DEBUG_BOOK_READ: Found existing book at {accessPath}");
                    FileStream stream = new FileStream(
                        accessPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite
                    );
                    if (stream.Length > 0)
                    {
                        return stream;
                    }
                    else 
                    {
                        Console.WriteLine("DEBUG_BOOK_READ: Existing file is empty, will re-download.");
                    }
                }
                else 
                {
                    Console.WriteLine($"DEBUG_BOOK_READ: Book '{lookingForBook}' not found locally.");
                }
            }

            Http http = new Http();

            if (DateTime.UtcNow > sessionModel.ExpiryTime)
            {
                SessionManagement sessionManagement = new SessionManagement();
                string newSession = await http.Session(sessionModel.Username, sessionModel.Password);
                sessionManagement.InitSession(newSession, sessionModel.Password);
                sessionModel = await sessionManagement.ReadSessionAsync()
                    ?? throw new Exception("Session cannot be read after refresh");
                Console.WriteLine($"DEBUG_BOOK_READ: Session refreshed successfully.");
            }

            var searchItem = BookByTitle(bookItem.Title ?? "", bookItem.ProductId).Search[0];
            Console.WriteLine($"DEBUG_BOOK_READ: Downloading book. ItemId={searchItem.ItemId}, Session={sessionModel.Session.Substring(0, Math.Min(5, sessionModel.Session.Length))}...");

            Stream bookStream = await http.Downloadbook(
                sessionModel.Session,
                keyNode.DeviceId,
                searchItem.ItemId.ToString()
            );
            // iOS note: network streams may be non-seekable and may not support Length.
            if (bookStream.CanSeek)
            {
                try
                {
                    bookStream.Seek(0, SeekOrigin.Begin);
                }
                catch
                {
                    // Ignore: Some stream implementations lie about CanSeek or throw on Seek.
                }
            }

            Directory.CreateDirectory(userDir);

            string saveBookPath = Path.Combine(userDir, lookingForBook);
            await using (
                Stream fs = new FileStream(saveBookPath, FileMode.Create, FileAccess.Write)
            )
            {
                await bookStream.CopyToAsync(fs);
            }

            return new FileStream(saveBookPath, FileMode.Open, FileAccess.Read);
        }

        public BookItem BookByTitle(string title, string? productId)
        {
            return _cachedBooks.First(x => x.Title == title && x.ProductId == productId);
        }

        public string[] AvailableBooksForOffline(KeyManagement.KeyNode? keyNode = null)
        {
            string[] dirs = Directory.GetDirectories(_booksFolder);
            if (dirs.Length == 0)
            {
                return [];
            }
            string userDir = Path.Combine(_booksFolder, dirs[0]);
            return Directory.Exists(userDir) ? Directory.GetFiles(userDir) : [];
        }

        /// <summary>
        ///     სტატიკური წიგნების სია იქმნება პირველი ჩატვირთვისას
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task FetchAndCacheBooksAsync()
        {
            SessionManagement sessionManagement = new SessionManagement();
            SessionManagement.SessionModel session =
                await sessionManagement.ReadSessionAsync()
                ?? throw new Exception("Session cannot be read");
            Http http = new Http();
            string collection = string.Empty;
            bool retry = true;

            while (retry)
            {
                if (DateTime.UtcNow > session.ExpiryTime)
                {
                    Console.WriteLine("DEBUG_BOOK_MGMT: Session expired locally. Refreshing...");
                    string newSession = await http.Session(session.Username, session.Password);
                    if (!string.IsNullOrEmpty(newSession))
                    {
                        sessionManagement.InitSession(newSession, session.Password, session.Username);
                        session = await sessionManagement.ReadSessionAsync() ?? throw new Exception("Session cannot be read");
                    }
                }

                try 
                {
                    collection = await http.Collection(session.Session);
                    if (!string.IsNullOrWhiteSpace(collection))
                    {
                        break; // Success
                    }
                    else 
                    {
                         Console.WriteLine("DEBUG_BOOK_MGMT: Empty collection response.");
                         retry = false;
                    }
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized && retry)
                {
                    Console.WriteLine("DEBUG_BOOK_MGMT: Server returned 401. Forcing refresh and retry...");
                    // Force refresh by setting expiry to past
                    session.ExpiryTime = DateTime.UtcNow.AddMinutes(-1);
                    // continue loop to refresh
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DEBUG_BOOK_MGMT: API error: {ex.Message}");
                    retry = false;
                }
            }

            Console.WriteLine($"DEBUG_BOOK_MGMT: Collection response received. Length: {collection?.Length ?? 0}");
            
            if (string.IsNullOrWhiteSpace(collection))
            {
                Console.WriteLine("DEBUG_BOOK_MGMT: Received EMPTY collection from server.");
                return;
            }

            string[] lines = collection.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries
            );
            Console.WriteLine($"DEBUG_BOOK_MGMT: Processing {lines.Length} lines from collection.");

            foreach (string line in lines)
            {
                try 
                {
                    string[] parts = line.Split(';');
                    if (parts.Length < 10)
                    {
                        // Some lines might be shorter if certain fields are missing, 
                        // but original code expected at least up to index 9.
                        continue;
                    }

                    BookItem bookItem = new BookItem
                    {
                        ProductId = parts[0],
                        ProductType = int.Parse(parts[1]),
                        Title = parts[2],
                        Author = parts[3],
                        CoverImageUrl = parts[4],
                        Folder = parts[5],
                        Date = parts[6],
                        IsRented = parts[9] == "1",
                    };

                    string[] searchItems = parts[8].Split(",");

                    if (searchItems.Length > 1)
                    {
                        foreach (string s in searchItems)
                        {
                            string[] willTuple = s.Split(":");
                            if (willTuple.Length == 2 && int.TryParse(willTuple[0], out int itemId) && int.TryParse(willTuple[1], out int itemType))
                            {
                                bookItem.Search.Add((itemId, itemType));
                            }
                        }
                    }
                    else
                    {
                        string[] willTuple = parts[8].Split(":");
                        if (willTuple.Length == 2 && int.TryParse(willTuple[0], out int itemId) && int.TryParse(willTuple[1], out int itemType))
                        {
                            bookItem.Search.Add((itemId, itemType));
                        }
                    }

                    _cachedBooks.Add(bookItem);
                }
                catch (Exception lineEx)
                {
                    Console.WriteLine($"DEBUG_BOOK_MGMT_ERROR: Failed to parse book line: {lineEx.Message}. Line: {line}");
                }
            }

            // axali wignebi win wamova
            //_cachedBooks.Reverse();
        }

        /// <summary>
        ///     წიგნებზე მონაცემები ინახება სათაურის მიხედვით განურჩევლად მფლობელისა,
        ///     არის შანსი სხვადასხვა იუზერის შემთხვევაში შეცდომა წარმოიშვას
        /// </summary>
        /// <param name="arg1"></param>
        private void BookManagement_BookStateChanged(object arg1, BookStateEventArgs newState)
        {
            lock (_stateFileLock)
            {
                try
                {
                    BookStateManager bookStateManager;
                    JsonSerializer serializer = new JsonSerializer();

                    using (StreamReader streamReader = new StreamReader(_libStateFile))
                    using (JsonReader jsonReader = new JsonTextReader(streamReader))
                    {
                        bookStateManager =
                            serializer.Deserialize<BookStateManager>(jsonReader)
                            ?? new BookStateManager();
                    }

                    int index = bookStateManager.Books.FindIndex(x =>
                        x.Title == newState.BookState.Title
                    );
                    newState.BookState.LastAccessTime =
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (newState.FontSize != null)
                    {
                        bookStateManager.FontSize = (int)newState.FontSize;
                    }

                    if (newState.Theme != null)
                    {
                        bookStateManager.Theme = (THEMES)newState.Theme;
                    }

                    if (index != -1)
                    {
                        bookStateManager.Books[index] = newState.BookState;
                    }
                    else
                    {
                        bookStateManager.Books.Add(newState.BookState);
                    }

                    using (StreamWriter streamWriter = new StreamWriter(_libStateFile))
                    using (JsonWriter jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        serializer.Serialize(jsonWriter, bookStateManager);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving book state: {ex.Message}");
                }
            }
        }

        private const int _take = 40;
        private static int _skip = 0;
        private static readonly List<BookItem> _cachedBooks = new List<BookItem>();

        private static readonly string _root = Path.Combine(
            FileSystem.Current.AppDataDirectory,
            "Library"
        );

        protected readonly string _booksFolder = Path.Combine(_root, "Books");
        protected readonly string _audiosFolder = Path.Combine(_root, "Audios");
        protected readonly string _libStateFile = Path.Combine(_root, "__lsf.json");

        private static event Action<object, BookStateEventArgs>? BookStateChanged = null;
        private static readonly object _stateFileLock = new object();
    }

    public class BookItem : StateBase, INotifyPropertyChanged
    {
        private string? _coverImageUrl;
        private bool _isDownloaded = false;
        private bool _needDownload = true;
        public int ProductType { get; set; }
        public string? ProductId { get; set; }

        public string? CoverImageUrl
        {
            get => this._coverImageUrl;
            set
            {
                this._coverImageUrl = value;
                NotifyPropertyChanged();
            }
        }

        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Folder { get; set; }
        public string? Date { get; set; }
        public bool IsRented { get; set; }

        public bool IsDownloaded
        {
            get => this._isDownloaded;
            set
            {
                this._isDownloaded = value;
                NotifyPropertyChanged();
            }
        }

        public bool NeedDownload
        {
            get => this._needDownload;
            set
            {
                this._needDownload = value;
                NotifyPropertyChanged();
            }
        }

        public List<(int ItemId, int ItemType)> Search = [];

        public event PropertyChangedEventHandler? PropertyChanged;

        [Ignore, JsonIgnore]
        public FormattedString? FormattedTitle { get; set; } = null;

        [Ignore, JsonIgnore]
        public FormattedString? FormattedAuthor { get; set; } = null;

        [Ignore, JsonIgnore]
        private static Color color => (Color)Application.Current!.Resources["DarkerTextColor"];

        public void UpdateFormattedTitle(string query)
        {
            if (string.IsNullOrEmpty(Title) || string.IsNullOrEmpty(Author))
            {
                return;
            }

            if (string.IsNullOrEmpty(query))
            {
                FormattedTitle = new FormattedString
                {
                    Spans =
                    {
                        new Span { Text = Title, TextColor = color },
                    },
                };
                FormattedAuthor = new FormattedString
                {
                    Spans =
                    {
                        new Span { Text = Author, TextColor = color },
                    },
                };
                return;
            }

            FormattedString formattedString = new FormattedString();

            int indexTitle = Title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
            int indexAuthor = Author.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);

            if (indexTitle >= 0)
            {
                if (indexTitle > 0)
                {
                    formattedString.Spans.Add(
                        new Span { Text = Title.Substring(0, indexTitle), TextColor = color }
                    );
                }

                formattedString.Spans.Add(
                    new Span
                    {
                        Text = Title.Substring(indexTitle, query.Length),
                        TextDecorations = TextDecorations.Underline,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = color,
                    }
                );

                if (indexTitle + query.Length < Title.Length)
                {
                    formattedString.Spans.Add(
                        new Span
                        {
                            Text = Title.Substring(indexTitle + query.Length),
                            TextColor = color,
                        }
                    );
                }

                FormattedTitle = formattedString;
            }
            else
            {
                formattedString.Spans.Add(new Span { Text = Title, TextColor = color });
                FormattedTitle = formattedString;
            }

            formattedString = new FormattedString();

            if (indexAuthor >= 0)
            {
                if (indexAuthor > 0)
                {
                    formattedString.Spans.Add(
                        new Span { Text = Author.Substring(0, indexAuthor), TextColor = color }
                    );
                }

                formattedString.Spans.Add(
                    new Span
                    {
                        Text = Author.Substring(indexAuthor, query.Length),
                        TextDecorations = TextDecorations.Underline,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = color,
                    }
                );

                if (indexAuthor + query.Length < Author.Length)
                {
                    formattedString.Spans.Add(
                        new Span
                        {
                            Text = Author.Substring(indexAuthor + query.Length),
                            TextColor = color,
                        }
                    );
                }

                FormattedAuthor = formattedString;
            }
            else
            {
                formattedString.Spans.Add(new Span { Text = Author, TextColor = color });
                FormattedAuthor = formattedString;
            }
        }

        protected void NotifyPropertyChanged([CallerMemberName] string property = "")
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    public record BookState
    {
        public string Title { get; set; } = "";
        public int LastVisitedPage { get; set; }
        public int TotalPages { get; set; }
        public long LastAccessTime { get; set; }
    }

    public class BookStateManager
    {
        public List<BookState> Books { get; set; } = new List<BookState>();

        // sxva shared atributebi
        public int FontSize { get; set; }

        public THEMES? Theme { get; set; } = null;
    }

    public class BookStateEventArgs : EventArgs
    {
        public BookState BookState { get; }
        public int? FontSize { get; set; }
        public THEMES? Theme { get; set; } = null;

        public BookStateEventArgs(BookState bookState, int? fontsize = null, THEMES? theme = null)
        {
            this.BookState = bookState;

            if (fontsize != null)
            {
                this.FontSize = fontsize;
            }

            if (theme != null)
            {
                Theme = theme;
            }
        }
    }

    public class BookItemComparer : IEqualityComparer<BookItem>
    {
        public bool Equals(BookItem? x, BookItem? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;

            return string.Equals(x.Title?.Trim(), y.Title?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.ProductId?.Trim(), y.ProductId?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode([DisallowNull] BookItem obj)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (obj.Title?.Trim().ToLowerInvariant().GetHashCode() ?? 0);
                hash = hash * 23 + (obj.ProductId?.Trim().ToLowerInvariant().GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}


// TODO chemi logirebis ref
