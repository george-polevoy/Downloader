namespace Downloader.Core
{
	using System.Net;

	public class NoneChunkingStrategy : IChunkingStrategy
	{
		public FilePart ReserveChunk(FileDownload fileDownload)
		{
			return fileDownload.Chunk(fileDownload.Size);
		}

		public HttpWebRequest CreateRequest(FilePart filePart)
		{
			return (HttpWebRequest)WebRequest.Create(filePart.FileDownload.Url);
		}
	}
}