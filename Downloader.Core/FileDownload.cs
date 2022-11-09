namespace Downloader.Core
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// State enumeration is ordered by severity. When accumulating parts, maximum state is choosen
	/// </summary>
	public enum FileDownloadState
	{
		Added = 0,
		Started,
		Finished,
		Failed,
	}

	public class FileDownload : IDisposable
	{
		/// <summary>
		/// Used for file access locking accross threads
		/// </summary>
		private readonly object fileWriterLock = new object();

		private IList<FilePart> parts = new List<FilePart>();

		private FileDownloadState ownState;

		public IEnumerable<FilePart> Parts
		{
			get
			{
				lock (this.parts)
				{
					return new List<FilePart>(this.parts);
				}
			}
		}

		public string Url { get; set; }

		public string Path { get; set; }

		public DateTime AddedDate { get; set; }

		public FileDownloadState OwnState
		{
			get
			{
				return this.ownState;
			}

			set
			{
				this.ownState = value;

				this.UpdateState();
			}
		}

		public FileDownloadState State
		{
			get
			{
				lock (this.parts)
				{
					if (this.parts.Count == 0)
					{
						return this.OwnState;
					}

					var stateByParts = this.CalculateStateByParts();

					return new[] { stateByParts, this.ownState }.Max();
				}
			}
		}

		public long Size { get; set; }

		public long DownloadedTotal
		{
			get
			{
				lock (this.parts)
				{
					var sum = this.parts.Where(i => i.State == FileDownloadState.Finished).Sum(i => i.Size);
					return sum;
				}
			}
		}

		public DateTime LastActivity { get; set; }

		public bool DownloadInParts { get; set; }

		public FileStream DestinationStream { get; set; }

		/// <summary>
		/// Returns next unprocessed chunk of the file.
		/// </summary>
		/// <param name="size">Size of the chunk to return. The actual returned chunk can be smaller then specified size, but not larger</param>
		/// <returns>Created chunk in started state</returns>
		public FilePart Chunk(long size)
		{
			Contract.Requires(this.Size != 0);
			lock (this.parts)
			{
				if (this.parts.Count == 0)
				{
					var part1 = new FilePart { State = FileDownloadState.Added, FileDownload = this, StartIndex = 0, EndIndex = this.Size - 1 };
					this.parts.Add(part1);
					part1.FileDownload = this;
				}

				var part = this.parts.FirstOrDefault(i => i.State == FileDownloadState.Added);

				if (part == null)
				{
					return null;
				}

				if (part.Size <= size)
				{
					part.State = FileDownloadState.Started;
					return part;
				}

				var slisedPart = new FilePart
					{
						State = FileDownloadState.Started,
						FileDownload = this,
						StartIndex = part.StartIndex,
						EndIndex = part.StartIndex + size - 1
					};
				this.parts.Add(slisedPart);
				part.StartIndex = slisedPart.EndIndex + 1;
				this.Normalize();

				return slisedPart;
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
			return string.Format("OwnState: {0}, Size: {1}, DownloadInParts: {2}", this.OwnState, this.Size, this.DownloadInParts);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Thread safe write method
		/// </summary>
		/// <param name="asyncBuffer"></param>
		/// <param name="seekPosition"></param>
		/// <param name="length"></param>
		public void WriteBlock(byte[] asyncBuffer, long seekPosition, int length)
		{
			lock (this.fileWriterLock)
			{
				var file = this.DestinationStream;
				{
					file.Seek(seekPosition, SeekOrigin.Begin);
					file.Write(asyncBuffer, 0, length);
				}
			}
		}

		public void InitializeStorage()
		{
			if (this.DestinationStream == null)
			{
				this.DestinationStream = File.OpenWrite(this.Path);
			}
		}

		public void UpdateState()
		{
			var fileDownloadState = this.State;

			if (fileDownloadState >= FileDownloadState.Finished)
			{
				lock (this.fileWriterLock)
				{
					if (this.DestinationStream != null)
					{
						this.DestinationStream.Dispose();
						this.DestinationStream = null;

						if (fileDownloadState == FileDownloadState.Failed)
						{
							File.Delete(this.Path);
						}
					}
				}
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (this.DestinationStream != null)
				{
					this.DestinationStream.Dispose();
					this.DestinationStream = null;
				}
			}
		}

		private FileDownloadState CalculateStateByParts()
		{
			var isStarted = false;
			var finishedCount = 0;

			foreach (var filePart in this.parts.Select(p => p.State))
			{
				switch (filePart)
				{
					case FileDownloadState.Failed:
						return FileDownloadState.Failed;
					case FileDownloadState.Started:
						isStarted = true;
						break;
					case FileDownloadState.Finished:
						finishedCount++;
						break;
				}
			}

			if (isStarted)
			{
				return FileDownloadState.Started;
			}

			if (finishedCount == this.parts.Count)
			{
				return FileDownloadState.Finished;
			}

			return FileDownloadState.Added;
		}

		/// <summary>
		/// After calling this method there will be no adjacent chunks with finished state.
		/// </summary>
		private void Normalize()
		{
			this.parts = this.parts.OrderBy(p => p.StartIndex).Aggregate(
				new List<FilePart>(),
				(list, next) =>
				{
					var last = list.LastOrDefault();

					// Find two adjacent parts of the same inactive state, and join them to one part, or just add the next part
					if (last != null && last.State != FileDownloadState.Started && last.State == next.State)
					{
						list[list.Count - 1] = new FilePart { State = last.State, StartIndex = last.StartIndex, EndIndex = next.EndIndex, FileDownload = last.FileDownload };
					}
					else
					{
						list.Add(next);
					}

					return list;
				});
		}
	}
}
