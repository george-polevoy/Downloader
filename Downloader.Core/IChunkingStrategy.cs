namespace Downloader.Core
{
	using System.Net;

	public interface IChunkingStrategy
	{
		FilePart ReserveChunk(FileDownload fileDownload);

		HttpWebRequest CreateRequest(FilePart filePart);
	}
}