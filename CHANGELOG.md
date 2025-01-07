### v1.2.15
- When parsing quote nodes, duplicate leading newlines now get collapsed into one, preventing erroneous line breaks when rendering
- Improved performance when parsing quote & code block nodes

### v1.2.14
- The NuGet package now correctly references the changelog by URL

### v1.2.13
- The NuGet package now contains this changelog

### v1.2.12
- Uppercase ASCII letters are now allowed in fn descriptor and arg keys

### v1.2.11
- Uppercase ASCII letters are now allowed in fn arg values

### v1.2.10
- Edge cases regarding mentions followed by special characters have been fixed

### 1.2.9
- Quote block trailing newline handling has been improved

### 1.2.8
- Quote blocks now implicitly contain an extra newline

### 1.2.7
- Single letter local mentions are now parsed correctly

### 1.2.6
- A bug causing link & url nodes to not preserve urlencode sequences has been fixed

### 1.2.5
- Nested empty tags now get parsed correctly
- The `AutoResizeArray<T>` struct is now `internal`

### 1.2.4
- Line endings are no longer canonicalized to `\n`
- The `Iceshrimp.MfmSharp` package is now published on NuGet
- The package license was updated to MIT