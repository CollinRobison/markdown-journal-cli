using System;
//TODO implement interface and add tests
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
    /// parses subheading string and returns an array of subheadings without space seperators.
    /// </summary>
    /// <param name="subheadings">the string of subheadings to parse. must be in seperator format.</param>
    /// <returns>array of subheadings without seperators</returns>
    string[] SeperateSubheadingString(string subheadings);

    /// <summary>
    /// combines sections together with section and space seperators.
    /// </summary>
    /// <param name="sections">the string array of sections to combine.</param>
    /// <returns>string of combined sections with section and space seperators.</returns>
    string AddSectionSeperators(string[] sections);
}
