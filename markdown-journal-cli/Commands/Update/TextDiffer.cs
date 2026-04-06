using System;
using System.Collections.Generic;

namespace markdown_journal_cli.Commands.Update;

internal enum DiffLineType
{
    Unchanged,
    Added,
    Removed,
}

internal readonly record struct DiffLine(DiffLineType Type, string Content);

/// <summary>
/// Produces a line-level diff between two text strings using the
/// Longest Common Subsequence (LCS) algorithm.
///
/// Phase 1 — BuildLcsTable: fills an (n+1)×(m+1) DP grid where each cell
///   holds the length of the LCS for the first i lines of <c>current</c>
///   and the first j lines of <c>preview</c>.
///
/// Phase 2 — BacktrackToDiff: walks the grid from the bottom-right corner
///   back to [0,0], classifying each line as Unchanged, Added, or Removed.
///   The resulting list is built in reverse order, so callers must reverse it.
/// </summary>
internal static class TextDiffer
{
    internal static IReadOnlyList<DiffLine> ComputeDiff(
        string currentContent,
        string previewContent
    )
    {
        var currentLines = currentContent.Split('\n');
        var previewLines = previewContent.Split('\n');

        var lcsTable = BuildLcsTable(currentLines, previewLines);
        var diff = BacktrackToDiff(currentLines, previewLines, lcsTable);

        diff.Reverse();
        return diff;
    }

    /// <summary>
    /// Builds the LCS dynamic-programming table.
    /// lcsTable[i, j] = length of the LCS of currentLines[0..i-1] and previewLines[0..j-1].
    /// </summary>
    private static int[,] BuildLcsTable(string[] currentLines, string[] previewLines)
    {
        var currentCount = currentLines.Length;
        var previewCount = previewLines.Length;
        var table = new int[currentCount + 1, previewCount + 1];

        for (var i = 1; i <= currentCount; i++)
        {
            for (var j = 1; j <= previewCount; j++)
            {
                table[i, j] =
                    currentLines[i - 1] == previewLines[j - 1]
                        ? table[i - 1, j - 1] + 1 // lines match — extend the LCS
                        : Math.Max(table[i - 1, j], table[i, j - 1]); // take the longer branch
            }
        }

        return table;
    }

    /// <summary>
    /// Walks the LCS table from bottom-right to top-left, emitting a
    /// <see cref="DiffLine"/> for each line. The result is in reverse order.
    /// </summary>
    private static List<DiffLine> BacktrackToDiff(
        string[] currentLines,
        string[] previewLines,
        int[,] lcsTable
    )
    {
        var diff = new List<DiffLine>();
        var currentIdx = currentLines.Length;
        var previewIdx = previewLines.Length;

        while (currentIdx > 0 || previewIdx > 0)
        {
            var bothRemain = currentIdx > 0 && previewIdx > 0;
            var linesMatch =
                bothRemain && currentLines[currentIdx - 1] == previewLines[previewIdx - 1];

            if (linesMatch)
            {
                diff.Add(new DiffLine(DiffLineType.Unchanged, currentLines[currentIdx - 1]));
                currentIdx--;
                previewIdx--;
            }
            else if (
                previewIdx > 0
                && (
                    currentIdx == 0
                    || lcsTable[currentIdx, previewIdx - 1] >= lcsTable[currentIdx - 1, previewIdx]
                )
            )
            {
                // Preview has a line that current doesn't — it was added
                diff.Add(new DiffLine(DiffLineType.Added, previewLines[previewIdx - 1]));
                previewIdx--;
            }
            else
            {
                // Current has a line that preview doesn't — it was removed
                diff.Add(new DiffLine(DiffLineType.Removed, currentLines[currentIdx - 1]));
                currentIdx--;
            }
        }

        return diff;
    }
}
