namespace Downloader.Ui
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Threading;

	using Downloader.Core;

	public class DownloaderMainFormController : IDownloaderMainFormController
	{
		private const double InfoRequestTimeoutSeconds = 60;

		public DownloaderMainFormController(IDownloadScheduler downloadScheduler)
		{
			this.DownloadScheduler = downloadScheduler;
		}

		public IDownloadScheduler DownloadScheduler { get; set; }

		private IDownloaderView View { get; set; }

		private bool Dirty { get; set; }

		public void StartDownload(string url)
		{
			var basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

			basePath = Path.Combine(basePath, "TestDownloads");

			if (!Directory.Exists(basePath))
			{
				Directory.CreateDirectory(basePath);
			}

			Uri uri;
			if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
			{
				this.View.Log(string.Format("Can't create url from string: {0}", url));
				return;
			}

			var fileName = Path.GetFileName(uri.LocalPath);
			var extension = Path.GetExtension(fileName);
			var urlExt = !string.IsNullOrWhiteSpace(extension) ? extension.Substring(1, extension.Length - 1) : string.Empty;

			var infoRequest = WebRequest.Create(url);

			this.View.Log(string.Format("Starting file info request: {0}", url));

			var infoRequestAsyncResult = infoRequest.BeginGetResponse(
				a =>
				{
					try
					{
						var r = infoRequest.EndGetResponse(a);

						r.Close();

						View.Log(string.Format("Info request returned: Content Type: {0}, Content Length: {1}, Accept-ranges: {2}", r.ContentType, r.ContentLength, r.Headers["Accept-ranges"]));

						var mime = r.ContentType.Split(new[] { ' ', ';' }, StringSplitOptions.RemoveEmptyEntries).First();

						var extensionsByMime =
							new StaticMimeMappingFromApache()
							.GetExtensionsByMime(mime)
							.ToArray();

						var ext = !string.IsNullOrWhiteSpace(urlExt) ? urlExt : extensionsByMime.FirstOrDefault() ?? string.Empty;

						if (string.IsNullOrWhiteSpace(fileName))
						{
							fileName = "unnamed";
						}

						var path = Path.ChangeExtension(Path.Combine(basePath, fileName), ext);

						while (File.Exists(path))
						{
							path = Path.ChangeExtension(Path.Combine(basePath, Guid.NewGuid() + fileName), ext);
						}

						DownloadScheduler.AddDownload(url, Path.Combine(basePath, path));
					}
					catch (WebException ioException)
					{
						View.Log(ioException.Message);
						return;
					}

					this.UpdateDisplay();
				},
				infoRequest);

			// It it takes too long, abort the request
			ThreadPool.RegisterWaitForSingleObject(
				infoRequestAsyncResult.AsyncWaitHandle,
				(state, timout) =>
				{
					if (timout)
					{
						((WebRequest)state).Abort();
						View.Log(string.Format("Request for {0} is aborted on timeout", url));
					}
				},
				infoRequest,
				TimeSpan.FromSeconds(InfoRequestTimeoutSeconds),
				true);
		}

		public void Load()
		{
			this.DownloadScheduler.Change += this.DownloadSchedulerChange;
			this.View.DisplayDownloads(this.GetCurrentDownloads());

			this.DownloadScheduler.Start();
		}

		public void SetView(IDownloaderView downloaderMainForm)
		{
			this.View = downloaderMainForm;
		}

		public void Close()
		{
			this.DownloadScheduler.Change -= this.DownloadSchedulerChange;
		}

		public void Tick()
		{
			if (!this.Dirty)
			{
				return;
			}

			this.UpdateDisplay();
		}

		public void Stop()
		{
			this.DownloadScheduler.Stop();
		}

		protected void DownloadSchedulerChange()
		{
			this.Dirty = true;
		}

		private void UpdateDisplay()
		{
			this.Dirty = false;
			this.View.DisplayDownloads(this.GetCurrentDownloads());
		}

		private IEnumerable<FileDownloadListItem> GetCurrentDownloads()
		{
			return this.DownloadScheduler.GetAll().Select(
				i => new FileDownloadListItem
					{
						Path = i.Path,
						Url = i.Url,
						AddedDate = i.AddedDate,
						State = i.State,
						DownloadedTotal = i.DownloadedTotal,
						Size = i.Size,
						LastActivity = i.LastActivity
					})
					.ToList();
		}
	}
}
