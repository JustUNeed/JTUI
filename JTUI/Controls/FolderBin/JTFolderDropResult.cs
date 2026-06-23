namespace JTUI.Controls.FolderBin
{
    /// <summary>往某个文件夹格子里投放内容的结果。</summary>
    public enum JTFolderDropKind
    {
        MovedFile,      // 移动了一个本地文件进去
        DownloadedUrl,  // 从浏览器拖入的链接已下载进去
        Failed          // 失败
    }

    public sealed class JTFolderDropResult
    {
        public JTFolderDropKind Kind { get; init; }
        public string FolderPath { get; init; } = "";
        public string? Source { get; init; }      // 源文件路径 或 URL
        public string? ResultPath { get; init; }  // 落地后的文件路径(成功时)
        public string? Error { get; init; }       // 失败原因
    }
}
