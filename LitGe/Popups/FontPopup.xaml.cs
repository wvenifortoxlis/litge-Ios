using CommunityToolkit.Maui.Views;
using LitGe.Lib;
using LitGe.Pages;
using static LitGe.Pages.ModalReaderViewModel;

namespace LitGe.Popups
{
    public partial class FontPopup : Popup
    {
        private Action increase,
            decrease;
        private Action<THEMES> changeTheme;
        private readonly Action<TextAlignment> changeAlignment;

        public FontPopup(
            Action inc,
            Action dec,
            Action<THEMES> changeTheme,
            Action<TextAlignment> changeAlignment
        )
        {
            InitializeComponent();
            increase = inc;
            decrease = dec;
            this.changeTheme = changeTheme;
            this.changeAlignment = changeAlignment;
            double w = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
            double h = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;

            this.Size = new Size(w, h * 0.3);
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            decrease.Invoke();
        }

        private void Button_Clicked_1(object sender, EventArgs e)
        {
            increase.Invoke();
        }

        private void darkColor_Clicked(object sender, EventArgs e)
        {
            changeTheme?.Invoke(THEMES.Dark);
        }

        private void creamyColor_Clicked(object sender, EventArgs e)
        {
            changeTheme?.Invoke(THEMES.Creamy);
        }

        private void whiteColor_Clicked(object sender, EventArgs e)
        {
            changeTheme?.Invoke(THEMES.Light);
        }

        private void startAlignment_Clicked(object sender, EventArgs e) =>
            changeAlignment.Invoke(TextAlignment.Start);

        private void centerAlignment_Clicked(object sender, EventArgs e) =>
            changeAlignment?.Invoke(TextAlignment.Center);

        private void endAlignment_Clicked(object sender, EventArgs e) =>
            changeAlignment.Invoke(TextAlignment.End);

        private async void ImageButton_Clicked(object sender, EventArgs e)
        {
            await this.CloseAsync();
        }
    }
}
