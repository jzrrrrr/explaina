using System.Windows.Documents;
using MdXaml;

namespace explaina.Services;

public class MarkdownService
{
    private readonly Markdown _markdownParser;

    public MarkdownService() {
        _markdownParser = new Markdown();
    }

    public FlowDocument ConvertMarkdownToFlowDocument(string markdown) {
        if (string.IsNullOrEmpty(markdown)) {
            return new FlowDocument();
        }
        return _markdownParser.Transform(markdown);
    }
}
