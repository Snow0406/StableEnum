using System;

namespace StableEnum
{
    /// <summary>
    /// Marks an Animation Event method whose enum parameter should be migrated together with the [StableEnum] enum.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class StableEnumEventAttribute : Attribute
    {
    }
}
