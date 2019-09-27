#### FolderCompare

Parameters:

````
  -v, --verbose      Set output to verbose messages.
  -j, --json         Required. Json file to store to or read the hashes from
  -d, --directory    Required. Directory to check
  -g, --generate     (Default: false) Run in generator mode
```` 

Features:
* All operations run in parallel (traversing a directory, hashing, comparing files).
* Hashes files using the [xxHash](http://cyan4973.github.io/xxHash/) algorithm.
* Files are checked against their size first and if that matches, their contents are hashed and hashes compared (highly unlikely that a file named the same with the same size and the same hash will actually refer to a different file)

Future:
* Exception checking.
* Give me a suggestion.

---
#### GenerateDatabaseFile & CheckAgainstDatabaseFile
FolderCompare split into two separate applications.

---
##### GenerateDatabaseFile
Generate a json file that contains a list of all files in a folder (recursively), including their size, last write date and (optionally) a hash generated using xxHash.

Future:
* Create rules for what should included/excluded from the directory traversal
* Create rules for what should included/excluded from the hashing (options: hash none, hash all, use rule)
* Both rules should be included in the generated file, so that they are applied in check step as well.

---

##### CheckAgainstDatabaseFile
Using a pre-generated database file, repeat the traversal process, checking against the file instead of generating a new database.

Future:
* Allow to switch between list view and tree view of the output (currently no clue how that will work)
  * Idea is to create a tree structure based on the path

---

Nuget Packages used:
* [Costura.Fody](https://www.nuget.org/packages/Costura.Fody/)
* [Orc.Controls](https://www.nuget.org/packages/Orc.Controls/)
* [Standart.Hash.xxHash](https://www.nuget.org/packages/Standart.Hash.xxHash/)
* [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/)

Credits:
* [Scott Chamberlain](https://stackoverflow.com/users/80274/scott-chamberlain) for this [answer](https://stackoverflow.com/a/46516878). 
