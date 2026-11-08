using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

[TestFixture]
internal sealed class IndexDocumentOptionsTests {
    [Test]
    public void Validate_WithDefaultValues_DoesNotThrow() {
        IndexDocumentOptions sut = new();

        Assert.DoesNotThrow(sut.Validate);
    }

    [Test]
    public void Validate_WithSeqNoAndPrimaryTerm_DoesNotThrow() {
        IndexDocumentOptions sut = new() {
            IfSeqNo = 7,
            IfPrimaryTerm = 2
        };

        Assert.DoesNotThrow(sut.Validate);
    }

    [Test]
    public void Validate_WithZeroSeqNoAndPrimaryTerm_DoesNotThrow() {
        IndexDocumentOptions sut = new() {
            IfSeqNo = 0,
            IfPrimaryTerm = 0
        };

        Assert.DoesNotThrow(sut.Validate);
    }

    [Test]
    public void Validate_WithRefreshOnly_DoesNotThrow() {
        IndexDocumentOptions sut = new() {
            Refresh = Refresh.WaitFor
        };

        Assert.DoesNotThrow(sut.Validate);
    }

    [Test]
    public void Validate_WithOnlySeqNo_ThrowsInvalidOperationException() {
        IndexDocumentOptions sut = new() {
            IfSeqNo = 7
        };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Test]
    public void Validate_WithOnlyPrimaryTerm_ThrowsInvalidOperationException() {
        IndexDocumentOptions sut = new() {
            IfPrimaryTerm = 2
        };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [TestCase(-1L, 2L)]
    [TestCase(7L, -1L)]
    public void Validate_WithNegativeValue_ThrowsInvalidOperationException(long ifSeqNo, long ifPrimaryTerm) {
        IndexDocumentOptions sut = new() {
            IfSeqNo = ifSeqNo,
            IfPrimaryTerm = ifPrimaryTerm
        };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }
}
