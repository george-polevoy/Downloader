namespace Downloader.Core.Tests
{
	using NUnit.Framework;
	using System.Linq;

	public class FileDownloadTests
	{
		[Test]
		public void FileDownloadChunkReturned()
		{
			var fileDownload = new FileDownload { Size = 100 };

			var expected = fileDownload.Chunk(100);

			var actual = fileDownload.Parts.Single();

			Assert.AreSame(expected, actual);
		}

		[Test]
		public void FileDownloadCanReserveWholeRangeAsOneChunk()
		{
			var fileDownload = new FileDownload { Size = 100 };

			fileDownload.Chunk(100);

			var actual = fileDownload.Parts.Select(i => new { i.StartIndex, i.EndIndex, i.State, i.Size }).ToArray();

			CollectionAssert.AreEquivalent(
				new[]
					{
						new { StartIndex = (long)0, EndIndex = (long)99, State = FileDownloadState.Started, Size = (long)100 },
					},
				actual);
		}

		[Test]
		public void FileDownloadCanDivideFileToChunks()
		{
			var fileDownload = new FileDownload { Size = 100 };

			fileDownload.Chunk(50);

			var actual = fileDownload.Parts.Select(i => new { i.StartIndex, i.EndIndex, i.State, i.Size }).ToArray();

			CollectionAssert.AreEquivalent(
				new[]
					{
						new { StartIndex = (long)0, EndIndex = (long)49, State = FileDownloadState.Started, Size = (long)50 },
						new { StartIndex = (long)50, EndIndex = (long)99, State = FileDownloadState.Added, Size = (long)50 },
					},
				actual);

			Assert.AreEqual(2, fileDownload.Parts.Count());
		}
	}
}
