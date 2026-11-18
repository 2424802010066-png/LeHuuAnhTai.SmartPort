using Microsoft.Maui.Controls;

namespace SmartPort.Mobile;

/// <summary>Tien ich nho: dat cot cho phan tu trong Grid khi dung C# thuan</summary>
public static class TienIch
{
    public static T Cot<T>(this T v, int cot) where T : BindableObject
    {
        Grid.SetColumn(v, cot);
        return v;
    }
}
