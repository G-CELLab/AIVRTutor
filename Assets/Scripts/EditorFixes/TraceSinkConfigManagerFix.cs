#if !UNITY_EDITOR
namespace Unity.AI.Tracing
{
    internal static class TraceSinkConfigManager
    {
        public static int MaxFileSizeMB => 10;
        public static int TrimFileSizeMB => 12;
    }
}
#endif
