namespace Downloader.Ui
{
	public interface IDownloaderMainFormController
	{
		void StartDownload(string url);

		void Load();

		void SetView(IDownloaderView downloaderMainForm);

		void Close();

		void Tick();

		void Stop();
	}
}