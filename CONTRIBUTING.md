# ME3EXPLORER CONTRIBUTION GUIDELINES
This project is a part of the [ME3Explorer Toolset Community](http://me3explorer.proboards.com). We are a diverse and single-player-focused modding community for the Mass Effect trilogy. Thanks for your interest in contributing to our respository. This document outlines the basic details newcomers need to know prior to contributing to the toolset.  

### Code of Conduct
Regardless of whether you submit an issue or commit code, all contributors are expected to follow and adhere to the [ME3Explorer Code of Conduct](https://github.com/ME3Explorer/ME3Explorer/blob/Beta/CodeofConduct.md). Please follow the instructions in the document to report any violations of this code.

### Maturity, Criticism, and Teamwork
We have an active, established community that pre-dates our presence on GitHub. It's extremely important that coders who choose to do moderate to significant work on the toolset are capable of working with current admins, communicating with the community, and can take feedback and criticism in response to their ideas. Having job experience as a coder is a big plus, as you've likely already learned how to do these things.

### Roadmap for Development
We're currently compiling our internal roadmap for development into a public document suitable for view. It will contain important details, such as:

* the lead developer for each tool
* the tool's WPF conversion status
* whether the tool slotted for future removal or merging with another tool
* features we want added (but don't yet warrant an Issue)

Both new and veteran coders interested in adding new features, functionalities, or new tools, should consult this roadmap when deciding what to work on.

If you have an idea not on the roadmap, feel free to submit an [Issue](https://github.com/ME3Explorer/ME3Explorer/issues). It may be something we've overlooked, or it may be something we've already considered and decided isn't right for the project.

### Getting Started
Before contributing to the project, please note the following:

__Skim the Wiki.__ If you don't know much about ME3Explorer, [the wiki](http://me3explorer.wikia.com/wiki/ME3Explorer_Wiki) is a great place to start. 

__Community Usage and Goals.__ All code contributions should first and foremost reflect the stated goals and needs of our users.

__Conversion to Windows Presentation Foundation (WPF).__ The toolset is currently being re-written in WPF. If a tool is still written in Winforms, minor and moderate contributions to that tool may be made in Winforms. Unnecessary GUI changes should be avoided, however, as the GUI will be completely overhauled with the WPF re-write.

__Property Library.__ Our current Unreal Property library has a large number of bugs. We're close to being finished with compiling a new library. Be aware that any new code submitted that's affected by this library, may break once we deploy the new library.

__Awareness of Toolset Idiosyncracies.__ All code contributors should be aware that the toolset is a bit of an unwieldy beast; this is part of what will be addressed by the WPF re-write. This is not a program that can be coded in a vacuum. This is a tool that is meant to mod game files that need to function within the constraint of the Unreal Engine and the Mass Effect series. This sometimes necessitates various hacks or "improper" ways of doing things. In these cases, "correcting" the "bugged" code can break functionality within multiple tools.

__GPL License.__ The toolset is distributed under a [GPL license](https://github.com/ME3Explorer/ME3Explorer/blob/Beta/LICENSE). Code contributions must be compatible with this license type.

__Reporting Bugs.__ Protocols for reporting bugs can be found on the [ME3Explorer wiki](http://me3explorer.wikia.com/wiki/Reporting_Bugs).

### Contributing Code
Prior to submitting any code, please do the following to help ensure your contribution is accepted:

1. __Fork the Beta branch__ &mdash; New code is pushed into the Beta branch before eventually being merged with the master (stable). All coders should, therefore, fork the Beta branch in preparation for their contribution.
2. __Examine the existing Issues__ &mdash; This is a great way to become familiar with our most current needs.
3. __Skim the roadmap__ &mdash; Take a look at our roadmap for development. This will get you familiar with our more long-term goals.
4. __Determine the contribution type__ &mdash; Determine the size of your contribution by looking at the guidelines below. The expectation associated with each type of contribution is _extremely_ different.
5. __Examine the Style Guide__ &mdash; Consult the Style Guide for the project to ensure your contribution adheres to our guidelines.
6. __Submit a new Issue__ &mdash; If your contribution isn't in an existing issue, or on the roadmap, then submit a new issue.

### Contribution Sizes
Code contributions will vary widely in size, scope, and complexity. The categories below are not set in stone, but meant to provide new coders a guide as what to expect.

#### Minor Code Contributions.
Minor contributions are for the most part, those that fix bugs. No new features or functionalities are added to or removed from the toolset. Coders who choose to work on this type of contribution may do so with little to no community engagement. Minor contributions should not include any GUI changes, unless it is a bug.

#### Moderate Code Contributions.
Moderate contributions add/edit features of existing tools, but do not add/alter functionalities. A recent example of this would be the addition of support for [playing Cinedesign sounds in Soundplorer](https://github.com/ME3Explorer/ME3Explorer/issues/385). This involves no new functionality in the tool, but these sounds were previously unsupported and, therefore, not playable by the tool.

Coders who choose to work on moderate contributions may or may not need to engage with the community. If the feature is on the toolset's Roadmap, engagement can likely be kept to a minimum. If the feature isn't on the roadmap, then the coder should engage with the community to ensure the feature is appropriate for the toolset and the userbase. The level of engagement will depend on the feature. Submitting an issue on Git may be sufficient, but larger features may warrant a discussion in the [development area of the forums](http://me3explorer.proboards.com/board/6/toolset-development). 

Moderate contributions should not alter the tool GUI more than is necessary for the new feature. GUI changes to tools written in WPF should follow the new template and Style Guide.

Coders should be aware that moderate contributions may require a more thorough understanding of the Unreal Engine and the existing toolset codebase.

#### Significant Code Contributions.
Significant contributions include adding/removing/changing functionalities or entire tools. Coders interested in making this level of contribution should do the following:

* Plan on re-writing the tool in WPF, if the tool is still in Winforms. If you don't, your code will go to waste with the re-write.
* Become familiar with Mass Effect file structure&mdash;possibly for all three games&mdash;as some tools affect all three games.
* Become familiar with the relevant portions of the existing toolset code.
* Engage the forum community in what you want to work on. Respectfully repond to feedback and take criticism. Be willing to modify your plans, as we won't use code that doesn't meet the needs of the community. Please introduce yourself here or PM an admin to get the ball rolling.
* Any GUI changes, new GUIs, and icons should follow the Style Guide.

#### Becoming a "Full Time" Toolset Coder.
Coders interested in making a variety of significant contributions to ME3Explorer should become a part of the community. They should develop an active and regular presence on the forum, investigate how the tools are being used, become familiar with the code/Unreal Engine/Mass Effect file structure, know what types of mods are being made, and what features modders want to see added or changed.

Full-time coders who earn a high level of trust, show maturity and leadership, make multiple well-written contributions, and have a clear appreciation for the community and modding Mass Effect, may eventually become an admin.

### Submitting Changes
To submit your code to the respository, please do the following.

* Submit a pull request to the Beta branch.
* Digitally sign the Contributor License Agreement. Coders will only need to sign this upon their first pull, and then any time the CLA changes. 
* The lead developer for the tool(s) your code affects will look at your submission within the next seven days.
* If your code meets the guidelines outlined in this document, our Style Guide, and there are no problems, it will be merged into the Beta.
* If your code does not meet the guidelines outlined in this document, our Style Guide, or has another problem, we'll reject the pull and provide feedback.
* Rejected pull requests that receive no response after a month will be closed for inactivity.

### Additional Resources
* [ME3Explorer Forums](http://me3explorer.proboards.com/)
* [ME3Explorer Wiki](http://me3explorer.wikia.com/wiki/ME3Explorer_Wiki)
* [Style Guide](https://github.com/ME3Explorer/ME3Explorer/blob/Beta/STYLEGUIDE.md)
* [Roadmap for Development] (link forthcoming)
* [Toolset License](https://github.com/ME3Explorer/ME3Explorer/blob/Beta/LICENSE)
* [Code of Conduct](https://github.com/ME3Explorer/ME3Explorer/blob/Beta/CodeofConduct.md)
* [Unreal Wiki](https://docs.unrealengine.com/udk/Three/WebHome.html)
