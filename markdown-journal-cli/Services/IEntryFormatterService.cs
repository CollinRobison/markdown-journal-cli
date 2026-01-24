using System;

namespace markdown_journal_cli.Services;

/// <summary>
/// Provides an abstraction layer for file entry formatting operations, enabling testability and cross-platform compatibility.
/// </summary>
public interface IEntryFormatterService
{
    /// <summary>
    /// Replaces spaces from input with space seperators.
    /// </summary>
    /// <param name="input">the string to add space seperators.</param>
    /// <returns>formatted string with spaces seperators instead of spaces.</returns>
    string AddSpaceSeperators(string input);

    /// <summary>
    /// Replaces space seperators from input with spaces.
    /// </summary>
    /// <param name="input">the string to remove spaced seperators.</param>
    /// <returns>formatted string with spaces instead of space seperators.</returns>
    string RemoveSpaceSeperators(string input);

    /// <summary>
    /// Parses subheading string and returns an array of subheadings without space seperators.
    /// </summary>
    /// <param name="subheadings">The string of subheadings to parse. must be in seperator format.</param>
    /// <returns>Array of subheadings without seperators</returns>
    string[] SeperateSubheadingString(string subheadings);

    /// <summary>
    /// Combines nested headings together with heading and space seperators.
    /// </summary>
    /// <param name="headings">The string array of headings to combine.</param>
    /// <returns>String of combined headings with heading and space seperators.</returns>
    string AddHeadingSeperators(string[] heading);
}
