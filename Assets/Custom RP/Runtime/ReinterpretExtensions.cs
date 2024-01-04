using System.Runtime.InteropServices;

// 即使renderingMask现在已经能影响光照了,效果仍然显示不正确.
// Light.renderingLayerMask属性把他们的bitmask属性暴露为int,
// 当在lightSetup方法中转化为float时会产生乱码.
// 没有办法直接发送一个整数列给GPU,所以我们需要把int解释为float而不是直接转换
// 但是C++++中并没有类似asuint的方法直接可以转换,并不能像hlsl那样直接转化,
// 因为C++++是强类型语言.
// 我们可以通过使用联合结构unionStruct来给数据重命名
public static class ReinterpretExtensions
{
    public static float ReinterpretAsFloat(this int value)
    {
        IntFloat converter = default;
        converter.intValue = value;

        return converter.floatValue;
    }

    // 为了完成转换我们不得不把两个类型的范围重叠,故他们应该共享相同的数据
    // 因为他们都是四比特宽所以这个操作是可行的.
    // 我们通过把这个结构标记为structLayout(Layout Kind.Explict),不进行结构对齐
    [StructLayout(LayoutKind.Explicit)]
    struct IntFloat
    {
        // 添加FieldOffset来传递我们应该把该数据放在哪
        // 把他们都设定为0,这样就会叠加.这些属性来自System.Runtime.InteropServices
        [FieldOffset(0)]
        public int intValue;

        [FieldOffset(0)]
        public float floatValue;
    }
}