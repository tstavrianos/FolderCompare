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
* GUI?
* Give me a suggestion.

Credits:
* [Scott Chamberlain](https://stackoverflow.com/users/80274/scott-chamberlain) for this [answer](https://stackoverflow.com/a/46516878). 
* [uranium62](https://github.com/uranium62) for his xxHash [implementation](https://github.com/uranium62/xxHash#).