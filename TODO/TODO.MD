
- [x] Finish JournalConfiguration Implementations and add tests
- [x] create initial journalrc (run control) file in new command
- [x] Update the journalrc to not have the index cache 
  - [x] and instead implement a hash system with its own .mdjournal file for all files similar to how git tracks files but simple where it only remembers last change before update. 
- [ ] update readme
- [ ] add the add command 
  - https://spectreconsole.net/cli/composing 
  - [x] decide how to update journalrc/table of contents when its added
- [ ] create a command to add a journal rc for an existing journal without one.

- [x] implement this into the new structure: 
  - // lets make a file be composed like this heading_1-heading_2-this_is_the_title
  - // so if a title or a heading has a space the space is defined as an underscore _
  - // the seperators to tell where a file is located logically is defined as a dash - 
  - //with the title of the file entry always being listed last
  - // ex: heading 1 -> heading 2 -> this is the title = heading_1-heading_2-this_is_the_title


- look into using the hash tracking to eventually make a command that regenerates a file or brings back the last version of it.
- Also using hash tracking if noticed a file was deleted but actually was just manually renamed allows user to reassign it to the new file name.
