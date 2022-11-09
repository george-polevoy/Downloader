namespace Downloader.Core
{
	using System.Net;

	public class FilePart
	{
		private FileDownloadState state;

		public FileDownload FileDownload { get; set; }

		public bool IsFinished
		{
			get
			{
				return this.State >= FileDownloadState.Finished;
			}
		}

		public long StartIndex { get; set; }

		public long EndIndex { get; set; }

		public HttpWebRequest WebRequest { get; set; }

		public long DownloadedTotal { get; set; }

		public long Size
		{
			get
			{
				return this.EndIndex - this.StartIndex + 1;
			}
		}

		public FileDownloadState State
		{
			get
			{
				return this.state;
			}

			set
			{
				this.state = value;
				this.UpdateState();
			}
		}

		/// <summary>
		/// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.String"/> that represents the current <see cref="T:System.Object"/>.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		public override string ToString()
		{
			return string.Format("FileDownload: {0}, StartIndex: {1}, EndIndex: {2}, DownloadedTotal: {3}, State: {4}", this.FileDownload, this.StartIndex, this.EndIndex, this.DownloadedTotal, this.State);
		}

		/// <summary>
		/// Thread safe write method
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="positionInChunk"></param>
		/// <param name="length"></param>
		public void WriteBlock(byte[] buffer, long positionInChunk, int length)
		{
			this.FileDownload.WriteBlock(buffer, this.StartIndex + positionInChunk, length);
		}

		private void UpdateState()
		{
			if (this.FileDownload != null)
			{
				this.FileDownload.UpdateState();
			}
		}
	}
}