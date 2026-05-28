using LitGe.Lib.Services;
using LitGe.Pages;
//using LLibrary.Guards;
//using LLibrary.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Text;
using Debug = System.Diagnostics.Debug;

namespace LitGe.Lib.ResourceManager
{
    /// <summary>
    ///     აუდიო წიგნების პროგრესს მიხედავს ეს კლასი. ამაში მოქცევა ორგანიზებული სტრუქტურას შექმნიდა თავიდანვე
    /// </summary>
    public partial class ResourceManager : BookManagement
    {
        public ResourceManager(/*IDebugAdapter debugAdapter, ITelegramAdapter telegramAdapter*/)
        {
            //_debugAdapter = debugAdapter;
            //_telegramAdapter = telegramAdapter;

            _progressFileName = "_progress.json";
            _bookmarksFile = "_bookmarks.json";

            _workQueue = [];
            _audioResources = [];
            _bookmarks = [];
            
            // TaskCompletionSource to track background hydration status
            _initializationTcs = new TaskCompletionSource<bool>();

            AudioResourceChangedEvent += ResourceManager_AudioResourceChanged;
            AudioResourceDeletedEvent += ResourceManager_AudioResourceDeletedEvent;
            BookDeleteRequestEvent += ResourceManager_BookDeleteRequestEvent;
            BookDownloadRequestEvent += ResourceManager_BookDownloadRequestEvent;
            BookBookmarkAddEvent += ResourceManager_BookBookmarkAddEvent;
            BookBookmarkDeleteEvent += ResourceManager_BookBookmarkDeleteEvent;
            BookBookmarkDeleteAllEvent += ResourceManager_BookBookmarkDeleteAllEvent;
        }

        /// <summary>
        ///     რესურს მენეჯერის გააქტიურება ცალკე ნაკადზე
        /// </summary>
        public void Start() =>
            new Thread(() =>
            {
                _cts = new CancellationTokenSource();
                Execute(_cts.Token);
            }).Start();

        /// <summary>
        ///     რესურს მენეჯერის შეჩერება
        /// </summary>
        public void Stop() => _cts?.Cancel();

        // Property to allow UI to wait for hydration
        public Task InitializationTask => _initializationTcs.Task;
        private readonly TaskCompletionSource<bool> _initializationTcs;

        /// <summary>
        ///     თუ არსებობს პროგრესის ჩანაწერი ამ წინგნისთვის მაშინ მოძებნის იმ ნოუდს რომელშიც 'გაგრძელება' მონიშნულია
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public (string audioFile, string chapterCode, double position)? ContinueFrom(string title)
        {
            if (
                !_audioResources.TryGetValue(
                    title,
                    out LinkedList<(
                        string chapter,
                        string chapterCode,
                        double lastPosition,
                        bool continueFromHere
                    )>? link
                )
            )
                return null;
            LinkedListNode<(
                string chapter,
                string chapterCode,
                double lastPosition,
                bool continueFromHere
            )>? headNode = link.First;

            while (headNode != null)
            {
                if (headNode.Value.continueFromHere)
                {
                    return new ValueTuple<string, string, double>(
                        headNode.Value.chapter,
                        headNode.Value.chapterCode,
                        headNode.Value.lastPosition
                    );
                }

                headNode = headNode.Next;
            }

            return null;
        }

        /// <summary>
        ///     კონკრეტული თავის მიხედვით პროგრესის და მისამართის პოვნა
        /// </summary>
        /// <param name="title"></param>
        /// <param name="chapterCode"></param>
        /// <returns></returns>
        public (string file, double position)? ChapterProgress(string title, string chapterCode)
        {
            if (
                _audioResources.TryGetValue(
                    title,
                    out LinkedList<(
                        string chapter,
                        string chapterCode,
                        double lastPosition,
                        bool continueFromHere
                    )>? link
                )
            )
            {
                LinkedListNode<(
                    string chapter,
                    string chapterCode,
                    double lastPosition,
                    bool continueFromHere
                )>? headNode = link.First;

                while (headNode != null)
                {
                    if (headNode.Value.chapterCode == chapterCode)
                    {
                        return new ValueTuple<string, double>(
                            headNode.Value.chapter,
                            headNode.Value.lastPosition
                        );
                    }

                    headNode = headNode.Next;
                }
            }

            return null;
        }

        /// <summary>
        ///     useris foldershi ra wignebicaa im failebs daabrunebs
        /// </summary>
        /// <returns>srul misamartebs</returns>
        public async Task<string[]> AvailableBooks()
        {
            SessionManagement sessionManagement = new SessionManagement();
            KeyManagement keyManagement = new KeyManagement();

            //_session = Guard.AgainstNull(await sessionManagement.ReadSessionAsync());

            //_keyNode = Guard.AgainstNull(
            //    (await keyManagement.KeysAsync()).FirstOrDefault(kn =>
            //        (kn.Username == _session.Email || kn.Username == _session.Username)
            //        && kn.Password == _session.Password
            //    )
            //);

            _session = await sessionManagement.ReadSessionAsync();
            if (_session == null) return [];

            _keyNode = 
                (await keyManagement.KeysAsync()).FirstOrDefault(kn =>
                    (kn.Username == _session.Email || kn.Username == _session.Username)
                    && kn.Password == _session.Password
                )
            ;

            if (_keyNode == null)
            {
                Console.WriteLine("DEBUG_BOOKS: _keyNode is null — user not found in key store.");
                return [];
            }

            string userDir = Path.Combine(_booksFolder, _keyNode.DeviceId);
            return Directory.Exists(userDir) ? Directory.GetFiles(userDir) : [];
        }

        public BookmarkedPage[] GetBookmarks(string title) =>
            _bookmarks.TryGetValue(title, out BookmarkedPage[]? bookmarks) ? bookmarks : [];

        public void OnAudioResourceChanged(object sender, AudioResourceChangedEventArgs e) =>
            EnqueuTask(() => AudioResourceChangedEvent.Invoke(sender, e));

        public void OnAudioResourceDeleted(object sender, AudioResourceDeletedEventArgs e) =>
            EnqueuTask(() => AudioResourceDeletedEvent.Invoke(sender, e));

        public void OnBookDeleteRequested(object sender, BookDeleteRequestEventArgs e) =>
            EnqueuTask(() => BookDeleteRequestEvent.Invoke(sender, e));

        public void OnBookDownloadRequested(object sender, BookDownloadRequestEventArgs e) =>
            EnqueuTask(() => BookDownloadRequestEvent?.Invoke(sender, e));

        public void OnStatusBarChangeRequested(object sender, StatusBarChangeEventArgs e) =>
            StatusBarChangeRequestEvent?.Invoke(sender, e);

        public void OnBookmarkAdded(object sender, BookmarkAddEventArgs e) =>
            EnqueuTask(() => BookBookmarkAddEvent.Invoke(sender, e));

        public void OnBookmarkDeleted(object sender, BookmarkDeleteEventArgs e) =>
            EnqueuTask(() => BookBookmarkDeleteEvent.Invoke(sender, e));

        public void OnBookmarkDeleteAll(object sender, BookmarkDeleteAllEventArgs e) =>
            EnqueuTask(() => BookBookmarkDeleteAllEvent.Invoke(sender, e));

        /// <summary>
        ///     დავალებები რომელიც ციკლის განმავლობაში შეიძლება შესრულდეს
        /// </summary>
        /// <param name="task"></param>
        private void EnqueuTask(Action task) => _workQueue.Enqueue(task);

        /// <summary>
        ///     ციკლი რომელიც ყოველ 2 წამში სრულდება
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private void Execute(CancellationToken cancellationToken)
        {
            try
            {
                string? currentProgress = ReadProgressFile();
                if (string.IsNullOrEmpty(currentProgress))
                {
                    LoadAudioFiles();
                }
                else
                {
                    _audioResources =
                        JsonConvert.DeserializeObject<
                            Dictionary<
                                string,
                                LinkedList<(
                                    string chapter,
                                    string chapterCode,
                                    double lastPosition,
                                    bool continueFromHere
                                )>
                            >
                        >(currentProgress)
                        ?? new Dictionary<
                            string,
                            LinkedList<(
                                string chapter,
                                string chapterCode,
                                double lastPosition,
                                bool continueFromHere
                            )>
                        >();
                }

                LoadBookmarks();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex}");
            }
            finally
            {
                // Signal that hydration effort is complete (success or fail)
                _initializationTcs.TrySetResult(true);
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    //_telegramAdapter.OnLogEvent(
                    //    new AbstractAdapter.LoggerEventArgs("Lit.ge", "Executing")
                    //);


                    ProcessQueue();

                    SaveProgressFile();

                    if (_bookmarksChanged)
                    {
                        _bookmarksChanged = false;
                        SaveBookmarks();
                    }

                    if (_workQueue.IsEmpty)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex}");
            }
        }

        /// <summary>
        ///     დამატებითი დავალებების შესრულება
        /// </summary>
        private void ProcessQueue()
        {
            while (_workQueue.TryDequeue(out Action? work))
            {
                try
                {
                    work.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }

        /// <summary>
        ///     დისკიდან პროგრეს ფაილის წაკითხვა
        /// </summary>
        /// <returns></returns>
        private string? ReadProgressFile()
        {
            lock (_resourceFileLock)
            {
                try
                {
                    string path = Path.Combine(_audiosFolder, _progressFileName);
                    return File.Exists(path)
                        ? Encoding.UTF8.GetString(File.ReadAllBytes(path))
                        : null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading progress file: {ex.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        ///     სტრუქტურის ინიციალიზება აუდიო ფაილში არსებული დირექტორიების საფუძველზე და არა უშუალოდ პროგრეს ფაილის
        /// </summary>
        private void LoadAudioFiles()
        {
            string[] audioBookFolders = Directory.GetDirectories(_audiosFolder);
            foreach (string folder in audioBookFolders)
            {
                Span<byte> buffer = new();
                string target = folder[(folder.LastIndexOf('/') + 1)..];
                if (!Convert.TryFromBase64String(target, buffer, out int bytesWritten))
                {
                    Directory.Delete(folder, true);
                    continue;
                }

                string decodedFolder = Encoding.UTF8.GetString(buffer);
                _audioResources[decodedFolder] =
                    new LinkedList<(
                        string chapter,
                        string chapterCode,
                        double lastPosition,
                        bool continueFromHere
                    )>();
                string[] audioFiles = Directory.GetFiles(folder);

                foreach (string file in audioFiles)
                {
                    _audioResources[decodedFolder]
                        .AddLast((file, file.Substring(file.LastIndexOf('/') + 1), 0f, false));
                }
            }
        }


        /// <summary>
        ///     Force saves the progress to disk, bypassing the 10-second timer.
        /// </summary>
        public void ForceSaveProgress()
        {
            lock (_resourceFileLock)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(_audioResources);
                    string path = Path.Combine(_audiosFolder, _progressFileName);
                    File.WriteAllBytes(path, Encoding.UTF8.GetBytes(json));
                    _lastSyncTime = DateTime.Now;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error saving progress file: {e}");
                }
            }
        }

        private void SaveProgressFile()
        {
            if (DateTime.Now - _lastSyncTime < TimeSpan.FromSeconds(10))
                return;

            ForceSaveProgress();
        }

        /// <summary>
        ///     wignshi shlis konkretul chapteris progress
        /// </summary>
        /// <param name="bookTitle"></param>
        /// <param name="chapterCode"></param>
        private void DeleteNode(string bookTitle, string chapterCode)
        {
            if (_audioResources.TryGetValue(bookTitle, out var linkedList))
            {
                LinkedListNode<(
                    string chapter,
                    string chapterCode,
                    double lastPosition,
                    bool continueFromHere
                )>? headNode = linkedList.First;
                while (headNode != null)
                {
                    if (headNode.Value.chapterCode == chapterCode)
                    {
                        linkedList.Remove(headNode);
                        break;
                    }

                    headNode = headNode.Next;
                }
            }
        }

        /// <summary>
        ///     პროგრესის დამახსოვრება
        ///     თუ არ არსებობს წიგნზე ჩანაწერი ახალი იქმნება, იგივე კონკრეტული თავის შემთხვევაშიც
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResourceManager_AudioResourceChanged(
            object? sender,
            AudioResourceChangedEventArgs e
        )
        {
            // თუ არსებობს ამ სათაურზე პროგრესი
            if (
                _audioResources.TryGetValue(
                    e.Book,
                    out LinkedList<(
                        string chapter,
                        string chapterCode,
                        double lastPosition,
                        bool continueFromHere
                    )>? progress
                )
            )
            {
                //ძველ ნოუდზ რომლიდანაც გრძელდებოდა პროგრესი უნდა გაითიშოს
                LinkedListNode<(
                    string chapter,
                    string chapterCode,
                    double lastPosition,
                    bool continueFromHere
                )>? headNode = progress.First;

                while (headNode != null)
                {
                    if (headNode.Value.continueFromHere)
                    {
                        //headNode.Value = (headNode.Value.chapter,headNode.Value.lastPosition,false);
                        headNode.ValueRef.continueFromHere = false;
                        break;
                    }

                    headNode = headNode.Next;
                }

                // არსებობს ჩაპტერი რომელზეც უნდა ჩაიწეროს პროგრესი
                headNode = progress.First;
                bool chapterExists = false;
                while (headNode != null)
                {
                    if (headNode.Value.chapterCode == e.ChapterCode)
                    {
                        headNode.Value = (
                            headNode.Value.chapter,
                            headNode.Value.chapterCode,
                            e.NewPosition,
                            true
                        );
                        chapterExists = true;
                        break;
                    }

                    headNode = headNode.Next;
                }

                // თუ არ არსებობს დაემატოს ბოლოში
                if (!chapterExists)
                {
                    progress.AddLast((e.Chapter, e.ChapterCode, e.NewPosition, true));
                }
            }
            // თუ არა დაემატოს ახალი სათაური და ინიციალიზდეს პირველი ჩაპტერი
            else
            {
                _audioResources[e.Book] =
                    new LinkedList<(
                        string chapter,
                        string chapterCode,
                        double lastPosition,
                        bool continueFromHere
                    )>();
                _audioResources[e.Book].AddLast((e.Chapter, e.ChapterCode, e.NewPosition, true));
            }
        }

        /// <summary>
        ///     cachidan washalos es chapteri da mere diskzec dasinqrondeba
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ResourceManager_AudioResourceDeletedEvent(
            object? sender,
            AudioResourceDeletedEventArgs e
        )
        {
            (string file, double position)? progress = ChapterProgress(e.Book, e.ChapterCode);

            if (progress == null)
                return;

            File.Delete(progress.Value.file);
            DeleteNode(e.Book, e.ChapterCode);
        }

        /// <summary>
        ///     shenaxuli zipi ishleba, shenaxuli progresic unda waishalos
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ResourceManager_BookDeleteRequestEvent(
            object? sender,
            BookDeleteRequestEventArgs e
        )
        {
            SessionManagement sessionManagement = new SessionManagement();
            KeyManagement keyManagement = new KeyManagement();

            //_session = Guard.AgainstNull(sessionManagement.ReadSessionAsync().Result);

            //_keyNode = Guard.AgainstNull(
            //    keyManagement
            //        .KeysAsync()
            //        .Result.FirstOrDefault(kn =>
            //            kn.Username == _session.Username && kn.Password == _session.Password
            //        )
            //);
            _session = sessionManagement.ReadSessionAsync().Result;

            _keyNode = 
                keyManagement
                    .KeysAsync()
                    .Result.FirstOrDefault(kn =>
                        kn.Username == _session.Username && kn.Password == _session.Password);

            string userDir = Path.Combine(_booksFolder, _keyNode.DeviceId);

            string lookingForBook = $"{e.Book.Title}.epub";

            if (Directory.Exists(userDir))
            {
                string[] books = Directory.GetFiles(userDir);
                if (books.Any(book => book.Contains(lookingForBook)))
                {
                    string completePath = Path.Join(userDir, lookingForBook);
                    File.Delete(completePath);
                }
            }

            // TODO progresis washla
        }

        /// <summary>
        ///     gadmoiwers wigns tu ara arsebobs
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ResourceManager_BookDownloadRequestEvent(
            object? sender,
            BookDownloadRequestEventArgs e
        )
        {
            try
            {
                if (_keyNode is null || _session is null)
                    return;

                string userDir = Path.Combine(_booksFolder, _keyNode.DeviceId);

                string lookingForBook = $"{e.Book.Title}{e.Book.ProductId}.epub";

                Http http = new();

                Stream bookStream = await http.Downloadbook(
                    _session.Session,
                    _keyNode.DeviceId,
                    BookByTitle(e.Book.Title ?? "", e.Book.ProductId).Search[0].ItemId.ToString()
                );
                // iOS note: network streams may be non-seekable.
                if (bookStream.CanSeek)
                {
                    try
                    {
                        bookStream.Seek(0, SeekOrigin.Begin);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                Directory.CreateDirectory(userDir);

                string saveBookPath = Path.Combine(userDir, lookingForBook);
                await using Stream fs = new FileStream(
                    saveBookPath,
                    FileMode.Create,
                    FileAccess.Write
                );
                await bookStream.CopyToAsync(fs);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ex}");
            }
        }

        private void LoadBookmarks()
        {
            lock (_resourceFileLock)
            {
                try
                {
                    string path = Path.Combine(_booksFolder, _bookmarksFile);
                    if (!File.Exists(path))
                        return;

                    string text = Encoding.UTF8.GetString(File.ReadAllBytes(path));

                    _bookmarks =
                        JsonConvert.DeserializeObject<Dictionary<string, BookmarkedPage[]>>(text)
                        ?? [];
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading bookmarks: {ex.Message}");
                }
            }
        }

        private void SaveBookmarks()
        {
            lock (_resourceFileLock)
            {
                try
                {
                    string path = Path.Combine(_booksFolder, _bookmarksFile);
                    string text = JsonConvert.SerializeObject(_bookmarks);
                    File.WriteAllBytes(path, Encoding.UTF8.GetBytes(text));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving bookmarks: {ex.Message}");
                }
            }
        }

        private void ResourceManager_BookBookmarkDeleteEvent(
            object? sender,
            BookmarkDeleteEventArgs e
        )
        {
            _bookmarksChanged = true;
            lock (_lockObject)
            {
                if (_bookmarks.TryGetValue(e.Title, out _))
                {
                    _bookmarks[e.Title] = _bookmarks[e.Title]
                        .Where(x => x.Page != e.Page)
                        .ToArray();
                }
            }
        }

        // TODO modalshi gverdebis mixedvit shesacvlelia icon, da aq ar amatebs kide mgoni
        private void ResourceManager_BookBookmarkAddEvent(object? sender, BookmarkAddEventArgs e)
        {
            _bookmarksChanged = true;
            lock (_lockObject)
            {
                if (!_bookmarks.TryGetValue(e.Title, out BookmarkedPage[]? value))
                {
                    value = [];
                }

                _bookmarks[e.Title] =
                [
                    .. value,
                    new BookmarkedPage(
                        chapterTitle: e.ChapterTitle,
                        text: e.Text,
                        pageText: $"გვერდი {e.Page}",
                        page: e.Page
                    ),
                ];
            }
        }

        private void ResourceManager_BookBookmarkDeleteAllEvent(
            object? sender,
            BookmarkDeleteAllEventArgs e
        )
        {
            lock (_lockObject)
            {
                //_bookmarks[e.Title] = [];

                // yvelaze unda waishalos, fontis zoma yvela wignistvis izrdeba
                _bookmarks = [];
            }
        }

        private readonly object _lockObject = new();

        //private readonly IDebugAdapter _debugAdapter;
        //private readonly ITelegramAdapter _telegramAdapter;
        private readonly ConcurrentQueue<Action> _workQueue;
        private readonly string _progressFileName;
        private readonly string _bookmarksFile;
        private static readonly object _resourceFileLock = new object();

        private DateTime _lastSyncTime = DateTime.Now;
        private bool _bookmarksChanged;

        private Dictionary<
            string,
            LinkedList<(
                string chapter,
                string chapterCode,
                double lastPosition,
                bool continueFromHere
            )>
        > _audioResources;

        private Dictionary<string, BookmarkedPage[]> _bookmarks;
        private CancellationTokenSource? _cts;
        private SessionManagement.SessionModel? _session;
        private KeyManagement.KeyNode? _keyNode;
    }
}
