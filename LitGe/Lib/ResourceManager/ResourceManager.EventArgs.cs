using CommunityToolkit.Maui.Behaviors;
using LitGe.Lib.Services;
using LitGe.Pages;

namespace LitGe.Lib.ResourceManager
{
    public partial class ResourceManager
    {
        /// <summary>
        ///     როცა პროგრესია შესაცვლელი საწიროა წიგნის სათაური, რომელ თავში იცვლება და ახალი პოზიცია,
        ///     (პოზიციასთან ერთად სხვა ატრიბუტების დამატებაც შეიძლება)
        /// </summary>
        public class AudioResourceChangedEventArgs(
            string book,
            string chapter,
            string chapterCode,
            double newPosition
        ) : EventArgs
        {
            public string Book { get; } = book;
            public string Chapter { get; } = chapter;
            public string ChapterCode { get; } = chapterCode;
            public double NewPosition { get; } = newPosition;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="book"></param>
        /// <param name="chapterCode"></param>
        public class AudioResourceDeletedEventArgs(string book, string chapterCode) : EventArgs
        {
            public string Book { get; } = book;
            public string ChapterCode { get; } = chapterCode;
        }

        /// <summary>
        ///     ჩამოტვირთული წიგნის წაშლისთვის
        /// </summary>
        /// <param name="book"></param>
        public class BookDeleteRequestEventArgs(BookItem book) : EventArgs
        {
            public BookItem Book { get; } = book;
        }

        /// <summary>
        ///    wignis gadmoweis mitxovna
        /// </summary>
        /// <param name="book"></param>
        public class BookDownloadRequestEventArgs(BookItem book) : EventArgs
        {
            public BookItem Book { get; } = book;
        }

        /// <summary>
        ///     axali status bar stili
        /// </summary>
        /// <param name="newBehavior"></param>
        public class StatusBarChangeEventArgs(StatusBarBehavior newBehavior) : EventArgs
        {
            public StatusBarBehavior NewBehavior { get; } = newBehavior;
        }

        /// <summary>
        ///     wignze bookmarkis damatebisas
        /// </summary>
        /// <param name="title"></param>
        /// <param name="page"></param>
        /// <param name="chapterTitle"></param>
        /// <param name="text"></param>
        public class BookmarkAddEventArgs(string title, int page, string chapterTitle, string text)
            : EventArgs
        {
            public string Title { get; } = title;
            public int Page { get; } = page;
            public string ChapterTitle { get; } = chapterTitle;
            public string Text { get; } = text;
        }

        /// <summary>
        ///     wignidan bookmarkis amoshla
        /// </summary>
        /// <param name="title"></param>
        /// <param name="page"></param>
        public class BookmarkDeleteEventArgs(string title, int page) : EventArgs
        {
            public string Title { get; } = title;
            public int Page { get; } = page;
        }

        /// <summary>
        ///     wignis yvela bookmarkis washla
        /// </summary>
        /// <param name="title"></param>
        public class BookmarkDeleteAllEventArgs(string title) : EventArgs
        {
            public string Title { get; } = title;
        }
    }
}
