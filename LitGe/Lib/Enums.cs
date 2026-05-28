namespace LitGe.Lib
{
    [Flags]
    public enum THEMES : byte
    {
        Light = 1 << 0,
        Dark = 1 << 1,
        Creamy = 1 << 2,
    }

    [Flags]
    public enum SCREEN_DISPLAY : byte
    {
        Landscape = 1 << 0,
        Portrait = 1 << 1,
    }
}