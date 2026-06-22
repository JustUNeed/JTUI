namespace JTUI.Services
{
    /// <summary>单次导入某一来源的结果。</summary>
    public class JTImportResult
    {
        public bool Success { get; init; }
        /// <summary>成功时:落地后的文件路径;失败时为 null。</summary>
        public string? FilePath { get; init; }
        /// <summary>失败原因分类。</summary>
        public JTImportFailReason Reason { get; init; }
        /// <summary>原始来源描述(文件名 / URL / "剪贴板"),用于提示。</summary>
        public string Source { get; init; } = string.Empty;
        /// <summary>附加错误信息(异常消息等),可空。</summary>
        public string? Message { get; init; }

        public static JTImportResult Ok(string path, string source) =>
            new() { Success = true, FilePath = path, Source = source, Reason = JTImportFailReason.None };

        public static JTImportResult Fail(JTImportFailReason reason, string source, string? msg = null) =>
            new() { Success = false, Reason = reason, Source = source, Message = msg };
    }

    public enum JTImportFailReason
    {
        None = 0,
        UnsupportedFormat,   // 格式不正确 / 不是图片
        DownloadFailed,      // 浏览器图下载失败
        CopyFailed,          // 本地文件复制失败
        DecodeFailed,        // 解码 / 保存失败
        NoImageInData,       // 拖放/剪贴板里根本没有图片来源
        Unknown
    }
}
