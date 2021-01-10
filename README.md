# MPV-SMTC

Integrate MPV with Windows' System Media Transport Controls 
(the control buttons near the volume indicator thingy)

## Installing the binaries

* Download a release archive from the [Releases page](https://github.com/x0wllaar/MPV-SMTC/releases)
* Inside your MPV scripts directory, create another directory (for example "SMTC")
* Unpack the archive into this folder (so that the main.lua file is inside the folder, for example "SMTC/main.lua")
* Use MPV as usual, you should start seeing messages from both the lua script and the executable when you run MPV from the command line 

## Contributing

### Prerequisites

* Visual Studio Community 2019
* .NET Desktop development workflow installed
* Universal Windows Platform development workflow installed
* .NET 5.0 Runtime installed
* Windows 10 2004+

### Building

After cloning the repo, you can build MPV-SMTC using the F6 key in Visual Studio

The binaries for the releases are generated with the Publish tool from VS using the StaticReleaseBinary
profile.

The release binaries are enormous in size because they are static standalone binaries and include
the entire .NET 5 runtime in them.


## Built With

* [MPV](https://mpv.io/) - The media player I use and love
* [Windows Runtime](https://developer.microsoft.com/en-us/windows/) - The actual API used for SMTC
* [Json.NET](https://www.newtonsoft.com/json) - Used for JSON Parsing
* [Serilog](https://github.com/serilog/serilog) - Used for logging
* [Command Line Parser](https://github.com/commandlineparser/commandline) - Used for parsing arguments

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, 
see the [tags on this repository](https://github.com/x0wllaar/MPV-SMTC/tags). 

## Authors

* **Gregory Khvatsky** - *Initial work* - [x0wllaar](https://github.com/x0wllaar/)

See also the list of [contributors](https://github.com/x0wllaar/MPV-SMTC/contributors) who participated in this project.

## License

This project is licensed under the GNU GPL 3 License - see the [LICENSE.md](LICENSE.md) file for details

## Acknowledgments

* [haggen](https://gist.github.com/haggen/) for the Lua [random string generator](https://gist.github.com/haggen/2fd643ea9a261fea2094)
