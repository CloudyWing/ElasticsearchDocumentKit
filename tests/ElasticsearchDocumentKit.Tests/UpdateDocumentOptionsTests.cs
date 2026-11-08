using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

[TestFixture]
internal sealed class UpdateDocumentOptionsTests {
    [Test]
    public void Validate_WithDefaultValues_DoesNotThrow() {
        UpdateDocumentOptions sut = new();

        Assert.DoesNotThrow(sut.Validate);
    }

    [Test]
    public void Validate_WithSeqNoAndPrimaryTerm_DoesNotThrow() {
        UpdateDocumentOptions sut = new() {
            IfSeqNo = 7,
            IfPrimaryTerm = 2
        };

        Assert.DoesNotThrow(sut.Validate);
    }

    [Test]
    public void Validate_WithRetryOnConflictAndRefresh_DoesNotThrow() {
        UpdateDocumentOptions sut = new() {
            RetryOnConflict = 3,
            Refresh = Refresh.True
        };

        Assert.DoesNotThrow(sut.Validate);
    }

    [Test]
    public void Validate_WithZeroRetryOnConflictAndSeqNo_DoesNotThrow() {
        UpdateDocumentOptions sut = new() {
            RetryOnConflict = 0,
            IfSeqNo = 7,
            IfPrimaryTerm = 2
        };

        Assert.DoesNotThrow(sut.Validate);
    }

    [Test]
    public void Validate_WithOnlySeqNo_ThrowsInvalidOperationException() {
        UpdateDocumentOptions sut = new() {
            IfSeqNo = 7
        };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Test]
    public void Validate_WithOnlyPrimaryTerm_ThrowsInvalidOperationException() {
        UpdateDocumentOptions sut = new() {
            IfPrimaryTerm = 2
        };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [TestCase(-1L, 2L)]
    [TestCase(7L, -1L)]
    public void Validate_WithNegativeValue_ThrowsInvalidOperationException(long ifSeqNo, long ifPrimaryTerm) {
        UpdateDocumentOptions sut = new() {
            IfSeqNo = ifSeqNo,
            IfPrimaryTerm = ifPrimaryTerm
        };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Test]
    public void Validate_WithNegativeRetryOnConflict_ThrowsInvalidOperationException() {
        UpdateDocumentOptions sut = new() {
            RetryOnConflict = -1
        };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }

    [Test]
    public void Validate_WithRetryOnConflictAndSeqNo_ThrowsInvalidOperationException() {
        UpdateDocumentOptions sut = new() {
            RetryOnConflict = 1,
            IfSeqNo = 7,
            IfPrimaryTerm = 2
        };

        Assert.Throws<InvalidOperationException>(sut.Validate);
    }
}
