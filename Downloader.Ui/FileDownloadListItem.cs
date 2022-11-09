namespace Downloader.Ui
{
	using System;
	using System.ComponentModel;

	using Downloader.Core;

	public class FileDownloadListItem
	{
		public string Path { get; set; }

		public string Url { get; set; }

		public DateTime AddedDate { get; set; }

		public FileDownloadState State { get; set; }

		public long Size { get; set; }

		public long DownloadedTotal { get; set; }

		public double DownloadedPercentage
		{
			get
			{
				return 100.0 * DownloadedTotal / Size;
			}
		}

		 [Browsable(false)]
		public DateTime LastActivity { get; set; }

		[DisplayName("Download speed (bytes per second)")]
		public double BytesPerSecond
		{
			get
			{
				return this.DownloadedTotal / (LastActivity - AddedDate).TotalSeconds;
			}
		}
	}
}