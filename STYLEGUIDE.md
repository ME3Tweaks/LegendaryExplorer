# ME3EXPLORER STYLE GUIDE

Each coder comes into a project with a different experience set. For that reason, we've laid out some basic expectations for coding style. This document also explains how to handle a variety of issues you may encounter when contributing to the repo.

### Code Contribution Guide
Remember to start by reviewing the [ME3Explorer Code Contribution Guide](https://github.com/ME3Explorer/ME3Explorer/blob/Beta/CodeContributionGuidelines.md). It covers basic details about getting started with the repo, types of code contributions, etc.

### Branches
* **Beta Branch**. Always use/fork/commit via this branch. This is especially true if you are working on a new tool or overhauling an existing tool.
* **Master branch**. The Beta branch is periodically merged into the master. This occurs prior to pushing a new stable release.
* **Feature branch**. These branches are rare and normally used only when implementing a new, large feature. For example, when we overhauled the main window to WPF.

### Commits
* Commits should be small and often.
* Commit messages should be short and concise, but adequately descriptive.
* Commits should be _functional_. No commit should break anything or cause the solution to fail building.
* **Do not store binaries.** Git stores the code, not you. Binaries get built along with it; they don't need to be in the repository. Some notable exceptions: 
    * Dependencies that aren't on nuget
    * Images/icons/templates etc.
* Always indicate which bug is being fixed when committing with `#<git bug number>` (e.g. "Closes #153 - Added null check."). This not only closes the bug automatically, but indicates in the history and on the Issues page what was actually done to fix the problem.

### Code
The stylistic conventions below are to assist in readability and understanding. There's no need to obfuscate code or make it "smaller" for the most part.
* **English Language.** All code/comments should be in English. Sorry, but most of the programming world uses English.
* **Readable Names.** No C/c++ rubbish of dropping vowels and calling a variable "pxCntr" instead of "pixelCounter". A name, whether it be class/method/variable, should describe its existence without requiring comments.
* **Self-Explanatory.** Well-written code shouldn't require comments to describe functionality.
* **Cases.**
    * Classes should use camelCase with an upper case start.
    * Methods should use camelCase with an upper case start.
    * Public variables/properties should use camelCase with an upper case start.
    * Private/internal/protected variables/properties should use camelCase with a lower case start.
* **Comments.** Comments should describe _why_ the code is the way it is, not _what_ the code does.
    * Public methods/properties/classes should have VS/C# `/// <Summary>` comments to describe what they do and why.
    * All bug fixes should be indicated in the code with a comment. For example, adding a null check should have a comment near it similar to: `// <User>: Null check added for #154 <link to #154>`.

### Dependencies
Our main goal with dependencies is to minimize them. This keeps the toolset slim and efficient.
* **Research.** Always thoroughly research your options before adding a new dependency.
* **Reuse**. Don't add overlapping dependencies; reuse existing ones whenever possible.
* **Size**. Don't use excessively large dependencies (i.e, add an enormous library to use one function). Instead, pull that function, _only_, and reference the library.
* **NuGent**. Use NuGet whenever possible, as it's almost always the best option. Dependencies can be pulled down as required with NuGet.

When choosing a new dependency, it's also important to be aware of:
* **Licensing**. Any third-party code used must be legally usable by the toolset and compatible with the GPL License.
* **Reference**. All third-party code added to the toolset must be cited properly in the toolset's "About" dialog, as well as in our internal spreadsheet on Google Docs. 

### UI Code
Using the conventions below improve the compartmentalization and testability of the program.
* **WPF.** As explained in the [Code Contribution Guidelines](https://github.com/ME3Explorer/ME3Explorer/blob/Beta/CodeContributionGuidelines.md), the toolset is being rewritten in WPF. WinForms is has no sensible DPI management,  poor graphics management, poor window management and performance, and lacks the flexibility built into WPF. If you are overhauling an existing tool or writing a new one, it needs to be written in WPF. Don't ask us if you can stick to WinForms. The answer is no.
* **Use Commands.** Commands keep the operation in with the class that uses them (e.g., a ListBoxItem with a "Copy" Button). The Copy doesn't belong to the listbox _per se_, but to the item. Instead of a Click handler, use a Command.
* **Separate View and Code.** Avoid putting UI code directly into operations. For example, progress and status updates should be done through a binding property instead of directly calling up `ProgressBar.Value`. `ProgressBar.Value` should be bound to `ViewModel.Progress` (or something) and that Progress variable is updated in code.

### UI Design + Icons + Graphics
[Giftfish](https://github.com/giftfish) is our resident GUI and graphic designer. She will design and mock up new GUIs, assign color palettes, create icons, and design miscellaneous graphics upon request. We encourage you to use her.

If you prefer to do these things yourself, our internal spreadsheet on Google Docs lays out what you need to know to create a GUI that conforms to our chosen standards and is consistent with the rest of the toolset. Overhauls of existing GUIs and new GUIs should be posted in the [toolset development section of the forum](http://me3explorer.freeforums.org/me3explorer-toolset-development-f43.html) for community feedback. Expect any GUI to go through a series of changes.

A few key details:

* **GUI Templates.** Part of the WPF rewrite includes implementing two new GUI templates that will establish new and consistent standards across tools. These templates will be uploaded to the repo soon.
* **Tool Icons.** Tool icons are designed by Giftfish and all conform to the same appearance standards. If you need a new icon, submit an Issue to Git or drop her a PM on the forum. 
* **Standard GUI Icons.** As part of the rewrite, we have a new icon set that should be used for all tools. If you need an icon that isn't in the set, let Giftfish know. She'll track one down or make one from scratch.
