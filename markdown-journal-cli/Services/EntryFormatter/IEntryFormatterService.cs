using System;

namespace markdown_journal_cli.Services;

/// <summary>
/// Provides an abstraction layer for file entry formatting operations, enabling testability and cross-platform compatibility.
/// </summary>
public interface IEntryFormatterService
{
    /// <summary>
    /// Replaces spaces from input with space Separators.
    /// </summary>
    /// <param name="input">the string to add space Separators.</param>
    /// <returns>formatted string with spaces Separators instead of spaces.</returns>
    string AddSpaceSeparators(string input);

    /// <summary>
    /// Replaces space Separators from input with spaces.
    /// </summary>
    /// <param name="input">the string to remove spaced Separators.</param>
    /// <returns>formatted string with spaces instead of space Separators.</returns>
    string RemoveSpaceSeparators(string input);

    /// <summary>
    /// Parses subheading string and returns an array of subheadings without space Separators.
    /// </summary>
    /// <param name="subheadings">The string of subheadings to parse. must be in Separator format.</param>
    /// <returns>Array of subheadings without Separators</returns>
    string[] SeperateSubheadingString(string subheadings);

    /// <summary>
    /// Combines nested headings together with heading and space Separators.
    /// </summary>
    /// <param name="headings">The string array of headings to combine.</param>
    /// <returns>String of combined headings with heading and space Separators.</returns>
    string AddHeadingSeparators(string[] heading);

    /// <summary>
    /// Returns an array of parsed headings and subheading
    /// </summary>
    /// /// <param name="subheadings">The heading string.</param>
    /// <param name="subheadings">The string of subheadings to parse. must be in Separator format.</param>
    /// <returns>Array of parsed headings and subheading</returns>
    public string[] BuildHeadingArray(string? heading, string? subheading);
}
