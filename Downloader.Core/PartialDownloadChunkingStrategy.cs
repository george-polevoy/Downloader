namespace Downloader.Core
{
	using System.Net;

	public class PartialDownloadChunkingStrategy : IChunkingStrategy
	{
		public FilePart ReserveChunk(FileDownload fileDownload)
		{
			return fileDownload.Chunk(1024 * 1024 * 1);
		}

		public HttpWebRequest CreateRequest(FilePart filePart)
		{
			var request = (HttpWebRequest)WebRequest.Create(filePart.FileDownload.Url);
			request.AddRange(filePart.StartIndex, filePart.EndIndex);
			return request;
		}
	}
}