namespace Downloader.Ui
{
	using System.Collections.Generic;

	public interface IDownloaderView
	{
		void DisplayDownloads(IEnumerable<FileDownloadListItem> items);

		void Log(string message);
	}
}