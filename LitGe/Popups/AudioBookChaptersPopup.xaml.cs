using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using LitGe.Pages;

namespace LitGe.Popups;

public partial class AudioBookChaptersPopup : Popup
{
    private int _activeChapterIndex;
    private readonly Action<string> _downloadAction;
    private readonly Action<string> _deleteAction;
    private readonly Action<string> _changeAudio;
    private readonly Action _downloadAllAction;

    public ObservableCollection<AudioBookChapters> Chapters { get; }

    public ICommand CloseCommand => new Command(Close);
    public ICommand ActionCommand => new Command<AudioBookChapters?>(Action);
    public ICommand DownloadAllCommand => new Command(DownloadAllAction);

    private void DownloadAllAction()
    {
        Close();
        this._downloadAllAction.Invoke();
    }

    private void Action(AudioBookChapters? chapter)
    {
        if (chapter == null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            int newActiveIndex = Chapters.IndexOf(chapter);
            if (newActiveIndex == -1) return;

            if (chapter.NeedDownload)
            {
                _downloadAction.Invoke(chapter.CodeForDownload);

                var prevActive = Chapters.ElementAtOrDefault(_activeChapterIndex);
                if (prevActive != null)
                {
                    prevActive.IsActive = false;
                    prevActive.Color = "#EDEFEB";
                }

                _activeChapterIndex = newActiveIndex;

                chapter.NeedDownload = false;
                chapter.IsActive = true;
                chapter.Color = "#9EE870";
                chapter.Icon = "delete_chapter.png";
            }
            else
            {
                // Action aligned with user expectation: Switch chapter instead of delete
                var prevActive = Chapters.ElementAtOrDefault(_activeChapterIndex);
                if (prevActive != null)
                {
                    prevActive.IsActive = false;
                    prevActive.Color = "#EDEFEB";
                }

                chapter.IsActive = true;
                chapter.Color = "#9EE870";
                _activeChapterIndex = newActiveIndex;

                _changeAudio.Invoke(chapter.CodeForDownload);
            }
        });
    }

    public AudioBookChaptersPopup(
        ObservableCollection<AudioBookChapters> chapters,
        int activeChaperIndex,
        Action<string> downloadAction,
        Action<string> deleteAction,
        Action<string> changeAudio,
        Action downloadAllAction
    )
    {
        InitializeComponent();

        double w = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        double h = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;

        this.Size = new Size(w, h * 0.9);

        this.Chapters = chapters;
        BindingContext = this;

        if (activeChaperIndex >= 0 && activeChaperIndex < Chapters.Count)
        {
            Chapters.ElementAt(activeChaperIndex).IsActive = true;
            Chapters.ElementAt(activeChaperIndex).Color = "#9EE870";
        }

        this._activeChapterIndex = activeChaperIndex;
        this._downloadAction = downloadAction;
        this._deleteAction = deleteAction;
        this._changeAudio = changeAudio;
        this._downloadAllAction = downloadAllAction;
    }

    private void Chapter_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not AudioBookChapters tappedNode)
            return;

        // REMOVED early return: Let users tap the name even if not downloaded.
        // ChangeAudioAsync now handles the download automatically.

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var prevActive = Chapters.ElementAtOrDefault(_activeChapterIndex);
            if (prevActive != null)
            {
                prevActive.IsActive = false;
                prevActive.Color = "#EDEFEB";
            }

            tappedNode.IsActive = true;
            tappedNode.Color = "#9EE870";

            _activeChapterIndex = Chapters.IndexOf(tappedNode);

            this._changeAudio.Invoke(tappedNode.CodeForDownload);
        });
    }
}
