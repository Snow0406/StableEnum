using System;

namespace StableEnum.Runtime
{
    /// <summary>
    /// enum 순서 변경으로 인해 인스펙터에 미리 설정해둔 값이 바뀌는 문제 방지 <br/>
    ///
    /// Prevents inspector-serialized values from breaking when an enum is reordered, extended, or pruned. <br/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false)]
    public sealed class StableEnumAttribute : Attribute
    {
    }
}