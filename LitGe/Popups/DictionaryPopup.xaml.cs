using CommunityToolkit.Maui.Views;

namespace LitGe.Popups
{
    public partial class DictionaryPopup : Popup
    {
        public DictionaryPopup(List<string> values, int fontsize = 18)
        {
            InitializeComponent();
            foreach (
                Label label in values.Select(val => new Label
                {
                    FormattedText = FormatString(val, fontsize),
                    Margin = new Thickness(15, 10),
                })
            )
            {
                stack.Add(label);
            }
        }

        private FormattedString FormatString(string text, int fontsize)
        {
            FormattedString formattedString = new FormattedString();
            int dashIndex = text.IndexOf('-');

            if (dashIndex != -1)
            {
                string beforeDash = text.Substring(0, dashIndex);
                string afterDash = text.Substring(dashIndex);

                formattedString.Spans.Add(
                    new Span
                    {
                        Text = beforeDash,
                        FontAttributes = FontAttributes.Bold,
                        FontSize = fontsize,
                        TextColor = (Color)Application.Current!.Resources["DarkerTextColor"],
                    }
                );
                formattedString.Spans.Add(
                    new Span
                    {
                        Text = afterDash,
                        FontSize = fontsize,
                        TextColor = (Color)Application.Current.Resources["DarkerTextColor"],
                    }
                );
            }
            else
            {
                formattedString.Spans.Add(
                    new Span
                    {
                        Text = text,
                        FontSize = fontsize,
                        TextColor = (Color)Application.Current!.Resources["DarkerTextColor"],
                    }
                );
            }

            return formattedString;
        }
    }
}
