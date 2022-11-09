namespace Downloader.Core.Tests
{
	using NUnit.Framework;
	using System.Linq;

	public class StaticMimeMappingFromApacheTests
	{
		[Test]
		public void CanDeriveMultipleExtensionsFromKnownMimeType()
		{
			var actual = new StaticMimeMappingFromApache().GetExtensionsByMime("application/vnd.ms-excel");
			Assert.AreEqual(6, actual.Count());
		}

		[Test]
		public void CanDeriveExtensionFromKnownMimeType()
		{
			var actual = new StaticMimeMappingFromApache().GetExtensionsByMime("application/vnd.lotus-1-2-3");
			CollectionAssert.AreEquivalent(new[] { "123" }, actual.ToArray());
		}

		[Test]
		public void YieldsEmptyIEnmerableOnUnknownMimeType()
		{
			var actual = new StaticMimeMappingFromApache().GetExtensionsByMime("foo");

			Assert.IsNotNull(actual);
			Assert.IsFalse(actual.Any());
		}
	}
}