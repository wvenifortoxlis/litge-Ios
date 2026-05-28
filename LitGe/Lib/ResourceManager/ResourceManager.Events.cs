using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LitGe.Lib.ResourceManager
{
    public partial class ResourceManager
    {
        /// <summary>
        ///     გამოიძახება როცა რომელიმე ფაილის პროგრესი იცვლება
        /// </summary>
        public event EventHandler<AudioResourceChangedEventArgs> AudioResourceChangedEvent;

        /// <summary>
        ///     romelime chapteris washlis dros unda gaisrolos es eventi
        /// </summary>
        public event EventHandler<AudioResourceDeletedEventArgs> AudioResourceDeletedEvent;

        /// <summary>
        ///     mtavari gverdidan, katalogidan, xdeba wignis washla
        /// </summary>
        public event EventHandler<BookDeleteRequestEventArgs> BookDeleteRequestEvent;

        /// <summary>
        ///    mtavari gverdidan, katalogidan, xdeba wignis gadmowera
        /// </summary>
        public event EventHandler<BookDownloadRequestEventArgs> BookDownloadRequestEvent;

        /// <summary>
        ///     device status baris cvlilebisas
        /// </summary>
        public event EventHandler<StatusBarChangeEventArgs>? StatusBarChangeRequestEvent;

        /// <summary>
        ///     axali bookmarkis damatebibsas
        /// </summary>
        public event EventHandler<BookmarkAddEventArgs> BookBookmarkAddEvent;

        /// <summary>
        ///     bookmarkis washla
        /// </summary>
        public event EventHandler<BookmarkDeleteEventArgs> BookBookmarkDeleteEvent;

        /// <summary>
        ///     fontis zomis cvlilebisas gverdebis rekalkulacia xdeba ratomac dzveli
        ///     bookmarkebis gverdebi aravaliduria.
        ///
        ///     TODO gverdi sad gadavida imis gamotvla shemdegshi
        /// </summary>
        public event EventHandler<BookmarkDeleteAllEventArgs> BookBookmarkDeleteAllEvent;
    }
}
