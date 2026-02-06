
- [x] Finish JournalConfiguration Implementations and add tests
- [x] create initial journalrc (run control) file in new command
- [x] Update the journalrc to not have the index cache 
  - [x] and instead implement a hash system with its own .mdjournal file for all files similar to how git tracks files but simple where it only remembers last change before update. 
- [ ] update readme
- [ ] add the add command 
  - https://spectreconsole.net/cli/composing 
  - [x] decide how to update journalrc/table of contents when its added
- [x] create a command to add a journal rc for an existing journal without one.

- [x] implement this into the new structure: 
  - // lets make a file be composed like this heading_1-heading_2-this_is_the_title
  - // so if a title or a heading has a space the space is defined as an underscore _
  - // the seperators to tell where a file is located logically is defined as a dash - 
  - //with the title of the file entry always being listed last
  - // ex: heading 1 -> heading 2 -> this is the title = heading_1-heading_2-this_is_the_title

- [ ] TOC update make sure when regenerating table of content or journalrc file it respects which file is toc. have a way of detecting.
- [ ] have a way for updating the toc file in journalrc add it to update and maybe new toc commands.

- [ ] create flow chart from written notes on the flow between toc - journalrc - tracking file

