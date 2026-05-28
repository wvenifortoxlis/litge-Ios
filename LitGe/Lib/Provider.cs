namespace LitGe.Lib
{
    public static class Provider
    {
        public static T? GetService<T>() => Services != null ? Services.GetService<T>() : default;

        private static IServiceProvider? Services =>
            Application.Current?.Handler?.MauiContext?.Services;
    }
}
