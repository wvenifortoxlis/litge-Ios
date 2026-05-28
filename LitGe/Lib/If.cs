namespace LitGe.Lib
{
    public class If : ContentView
    {
        public static readonly BindableProperty ConditionProperty =
            BindableProperty.Create(nameof(Condition), typeof(bool), typeof(If), true, propertyChanged: OnBindablePropertyChanged);

        public bool Condition
        {
            get => (bool)GetValue(ConditionProperty);
            set => SetValue(ConditionProperty, value);
        }

        public static readonly BindableProperty TrueProperty =
            BindableProperty.Create(nameof(True), typeof(View), typeof(If), null, propertyChanged: OnBindablePropertyChanged);

        public View True
        {
            get => (View)GetValue(TrueProperty);
            set => SetValue(TrueProperty, value);
        }

        public static readonly BindableProperty FalseProperty =
            BindableProperty.Create(nameof(False), typeof(View), typeof(If), null, propertyChanged: OnBindablePropertyChanged);

        public View False
        {
            get => (View)GetValue(FalseProperty);
            set => SetValue(FalseProperty, value);
        }

        private static void OnBindablePropertyChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((If)bindable).Update();
        }

        private void Update()
        {
            Content = Condition ? True : False;
        }
    }
}