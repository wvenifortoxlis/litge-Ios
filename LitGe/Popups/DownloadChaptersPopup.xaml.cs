using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;

namespace LitGe.Popups
{
    public partial class DownloadChaptersPopup : Popup, INotifyPropertyChanged
    {
        private readonly Action _downloadAll;
        private string _sizeText;

        public ICommand DownloadAllCallbackCommand =>
            new Command(() =>
            {
                Close();
                this._downloadAll?.Invoke();
            });
        public ICommand CloseCommand => new Command(Close);

        public string SizeText
        {
            get => _sizeText;
            set
            {
                if (this._sizeText != value)
                {
                    this._sizeText = value;
                    OnPropertyChanged();
                }
            }
        }

        public DownloadChaptersPopup(Action downloadAll, float size)
        {
            InitializeComponent();

            this._downloadAll = downloadAll;
            this._sizeText = $"საჭირო მეხსიერება {size} მბ";

            double w = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
            double h = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;

            this.Size = new Size(w, h * 0.5);

            BindingContext = this;
        }
    }
}
