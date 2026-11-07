using DiGi.GIS.YOLO.UI.Classes;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the exclusion-box editor's text rule (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#39): one EPSG:2180 box per line, four numbers separated by semicolon, tab or space, blank and <c>#</c> lines skipped, and a malformed line refusing the whole text with its line number.
        /// <para>The rows of the T30 area list paste in tab-separated as they are. The fact runs its assertions under pl-PL too, where a comma is the decimal mark: the editor reads invariant culture only, so <c>637739,56</c> is refused rather than read as a different box. Formatting with <see cref="Convert.ToSystem_String(IEnumerable{YOLOExclusionBoundingBox}?)"/> and parsing back returns the same coordinates exactly.</para>
        /// </summary>
        [Fact]
        public void TryParseYOLOExclusionBoundingBoxes()
        {
            CultureInfo cultureInfo = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (string culture in new[] { "en-US", "pl-PL" })
                {
                    Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);

                    // Empty text: no boxes, accepted.
                    Assert.True(Query.TryParseYOLOExclusionBoundingBoxes(null, out List<YOLOExclusionBoundingBox>? yOLOExclusionBoundingBoxes, out string? reason));
                    Assert.Empty(yOLOExclusionBoundingBoxes!);
                    Assert.Null(reason);
                    Assert.True(Query.TryParseYOLOExclusionBoundingBoxes("  \r\n ", out yOLOExclusionBoundingBoxes, out _));
                    Assert.Empty(yOLOExclusionBoundingBoxes!);

                    // P01 pasted tab-separated, P02 with semicolons, a comment and a blank line between.
                    string text = "637739.56\t486870.79\t638739.56\t487870.79\r\n# P02 Piaseczno\r\n\r\n637814.37; 469460.41; 638814.37; 470460.41";
                    Assert.True(Query.TryParseYOLOExclusionBoundingBoxes(text, out yOLOExclusionBoundingBoxes, out reason));
                    Assert.Null(reason);
                    Assert.Equal(2, yOLOExclusionBoundingBoxes!.Count);
                    Assert.Equal(637739.56, yOLOExclusionBoundingBoxes[0].MinX);
                    Assert.Equal(487870.79, yOLOExclusionBoundingBoxes[0].MaxY);
                    Assert.Equal(469460.41, yOLOExclusionBoundingBoxes[1].MinY);
                    Assert.Equal(638814.37, yOLOExclusionBoundingBoxes[1].MaxX);

                    // Round trip through the formatter: identical coordinates.
                    string text_Formatted = Convert.ToSystem_String(yOLOExclusionBoundingBoxes);
                    Assert.True(Query.TryParseYOLOExclusionBoundingBoxes(text_Formatted, out List<YOLOExclusionBoundingBox>? yOLOExclusionBoundingBoxes_RoundTrip, out _));
                    Assert.Equal(2, yOLOExclusionBoundingBoxes_RoundTrip!.Count);
                    for (int i = 0; i < 2; i++)
                    {
                        Assert.Equal(yOLOExclusionBoundingBoxes[i].MinX, yOLOExclusionBoundingBoxes_RoundTrip[i].MinX);
                        Assert.Equal(yOLOExclusionBoundingBoxes[i].MinY, yOLOExclusionBoundingBoxes_RoundTrip[i].MinY);
                        Assert.Equal(yOLOExclusionBoundingBoxes[i].MaxX, yOLOExclusionBoundingBoxes_RoundTrip[i].MaxX);
                        Assert.Equal(yOLOExclusionBoundingBoxes[i].MaxY, yOLOExclusionBoundingBoxes_RoundTrip[i].MaxY);
                    }

                    Assert.Equal(string.Empty, Convert.ToSystem_String(null));

                    // Refusals name the line: three numbers, a word, a comma decimal mark, a non-finite value.
                    Assert.False(Query.TryParseYOLOExclusionBoundingBoxes("1 2 3 4\n1 2 3", out yOLOExclusionBoundingBoxes, out reason));
                    Assert.Null(yOLOExclusionBoundingBoxes);
                    Assert.Contains("line 2", reason);

                    Assert.False(Query.TryParseYOLOExclusionBoundingBoxes("1 2 three 4", out _, out reason));
                    Assert.Contains("line 1", reason);

                    Assert.False(Query.TryParseYOLOExclusionBoundingBoxes("637739,56 486870,79 638739,56 487870,79", out _, out _));
                    Assert.False(Query.TryParseYOLOExclusionBoundingBoxes("1 2 NaN 4", out _, out _));
                    Assert.False(Query.TryParseYOLOExclusionBoundingBoxes("1 2 3 4 5", out _, out _));
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = cultureInfo;
            }
        }
    }
}
